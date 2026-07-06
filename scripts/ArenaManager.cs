using System;
using System.Collections.Generic;
using Godot;
using AshenPantheon.Core;

/// <summary>Proceduralny run (multiplayer-ready): plan z seedu wspólny dla wszystkich peerów (przeszkody
/// deterministyczne lokalnie), logika pokoi/spawnów/win-lose TYLKO u hosta, klienci dostają status RPC.</summary>
public partial class ArenaManager : Node
{
    private enum State { Starting, Fighting, RoomCleared, MapCleared, Won, Lost }
    private State _state = State.Starting;

    private List<RoomPlan> _plan;
    private int _room;
    private float _timer = 1.2f;
    private readonly HashSet<int> _deadPlayers = new();

    private ZoneDefinition _zone;
    private readonly List<Node> _obstacles = new();

    // wyzwanie endgame ("" = zwykły run): mnożniki + ilvl dropu + tytuł
    private string _challenge = "";
    private string _challengeTitle = "";
    private string _nextMap = ""; // run Q: kolejna mapa po M1/M2
    private float _chalHp = 1f, _chalDmg = 1f, _chalXp = 1f;
    private int _chalIlvl;

    /// <summary>Auto-quest runu Q oddaje się sam po M3 (nie ma NPC; nagrody + wpis do czatu).</summary>
    private static void CompleteQuestAuto()
    {
        var q = QuestCatalog.Find(EndgameCatalog.QQuest);
        if (q == null || !GameState.Quests.ReadyToTurnIn(q)) return;
        GameState.Quests.TurnIn(q);
        GameState.Progress.GainXp(q.RewardXp);
        GameState.Wallet.Gold += q.RewardGold;
        PlayerController.Local?.Refresh();
        Net.SendChatLocal($"Quest completed: {q.Name}  (+{q.RewardXp} XP, +{q.RewardGold} gold)");
    }

    public string TopStatus { get; private set; } = "";
    public string CenterMessage { get; private set; } = "";
    private string _lastTop = "", _lastCenter = "";

    public override void _Ready()
    {
        AddToGroup("arena");
        DataLoader.LoadAll();
        // strefa runu z TravelZoneId (finałowe dungeony), fallback: ashen_wastes
        string zoneId = Bestiary.Zones.ContainsKey(Net.TravelZoneId) ? Net.TravelZoneId : "ashen_wastes";
        _zone = Bestiary.Zone(zoneId);

        _challenge = Net.TravelChallengeId;
        if (EndgameCatalog.TryParseQ(_challenge, out int q))
        {
            var s = EndgameCatalog.QScale(q);
            (_chalHp, _chalDmg, _chalXp, _chalIlvl) = (s.Hp, s.Dmg, s.Xp, s.ItemLevel);
            int mapIdx = EndgameCatalog.QMapIndex(zoneId);
            _challengeTitle = $"THE FINAL PROVING  Q{q} — MAP {mapIdx}/{EndgameCatalog.QMaps.Count}   ";
        }
        else if (EndgameCatalog.TryParseGroup(_challenge, out var dun, out var diff))
        {
            (_chalHp, _chalDmg, _chalXp, _chalIlvl) = (diff.HpMult, diff.DmgMult, diff.XpMult, diff.ItemLevel);
            _challengeTitle = $"{dun.Name.ToUpperInvariant()}  [{diff.Name.ToUpperInvariant()}]   ";
        }

        int seed = Net.RunSeed != 0 ? Net.RunSeed : (int)(GD.Randi() % int.MaxValue);
        _plan = RunGenerator.Generate(seed, GameState.Progress.Level, _zone);
    }

    /// <summary>Klient: status od hosta.</summary>
    public void SetStatusRemote(string top, string center)
    {
        TopStatus = top;
        CenterMessage = center;
    }

    /// <summary>Klient: host rozpoczął pokój — postaw lokalnie deterministyczne przeszkody.</summary>
    public void OnRoomStartedRemote(int index)
    {
        if (Net.IsServer) return;
        _room = index;
        SpawnObstacles(_plan[index]);
    }

    /// <summary>Host: zgłoszenie śmierci gracza (lokalnej lub z RPC).</summary>
    public void PlayerDied(int peer)
    {
        if (!Net.IsServer || _state is State.Won or State.Lost) return;
        _deadPlayers.Add(peer);
        if (_deadPlayers.Count >= Net.PlayerCount())
        {
            _state = State.Lost;
            SetStatus("", "PARTY DEFEATED\n[R on host] return to town");
            GameState.Save();
        }
    }

    private void SetStatus(string top, string center)
    {
        TopStatus = top;
        CenterMessage = center;
        if (Net.Online && (top != _lastTop || center != _lastCenter))
        {
            _lastTop = top;
            _lastCenter = center;
            Net.BroadcastArenaStatus(top, center);
        }
    }

    public override void _Process(double delta)
    {
        if (!Net.IsServer) return;

        float dt = (float)delta;
        switch (_state)
        {
            case State.Starting:
                SetStatus("Get ready...", "");
                _timer -= dt;
                if (_timer <= 0f) StartRoom(0);
                break;

            case State.Fighting:
                int alive = GetTree().GetNodesInGroup("enemies").Count;
                var room = _plan[_room];
                SetStatus($"{_challengeTitle}Room {_room + 1}/{_plan.Count}{(room.Boss ? " [BOSS]" : "")}   enemies: {alive}   players: {Net.PlayerCount() - _deadPlayers.Count}", "");
                if (alive == 0)
                {
                    if (_room + 1 >= _plan.Count)
                    {
                        Net.BroadcastQuestClear(_zone.Id); // cel questowy Clear u wszystkich graczy (zalicza auto-questa Q)

                        // run Q = 3 mapy: M1/M2 (mini-bossy) → auto-przejście, M3 = finał
                        string nextMap = _challenge.StartsWith("q:") ? EndgameCatalog.NextQMap(_zone.Id) : null;
                        if (nextMap != null)
                        {
                            _state = State.MapCleared;
                            _timer = 3f;
                            _nextMap = nextMap;
                            SetStatus("", $"MAP {EndgameCatalog.QMapIndex(_zone.Id)}/{EndgameCatalog.QMaps.Count} CLEARED!\nEntering the next map...");
                            break;
                        }

                        if (_challenge.Length > 0)
                        {
                            Net.BroadcastEndgameClear(_challenge); // odblokowanie u całej drużyny
                            CompleteQuestAuto(); // auto-quest runu Q oddaje się sam (bez NPC)
                        }
                        SetStatus("", "RUN COMPLETE!\n[R on host] return to town");
                        GameState.Save();
                    }
                    else
                    {
                        _state = State.RoomCleared;
                        _timer = 1.6f;
                        // polegli wstają po oczyszczeniu pokoju (50% HP) — jak w Hero Siege
                        if (_deadPlayers.Count > 0)
                        {
                            _deadPlayers.Clear();
                            Net.ReviveAll(0.5f);
                        }
                    }
                }
                break;

            case State.RoomCleared:
                SetStatus("Room cleared — next one incoming...", "");
                _timer -= dt;
                if (_timer <= 0f) StartRoom(_room + 1);
                break;

            case State.MapCleared:
                _timer -= dt;
                if (_timer <= 0f)
                {
                    int seed = (int)(GD.Randi() % int.MaxValue);
                    if (seed == 0) seed = 1;
                    Net.TravelAll("res://scenes/Arena.tscn", seed, _nextMap, _challenge);
                }
                break;

            case State.Won:
            case State.Lost:
                if (Input.IsPhysicalKeyPressed(Key.R))
                    Net.TravelAll("res://scenes/Main.tscn", 0);
                break;
        }
    }

    private void StartRoom(int index)
    {
        _room = index;
        _state = State.Fighting;
        var room = _plan[index];

        SpawnObstacles(room);
        Net.StartRoomAll(index);

        // skalowanie trudności od liczby graczy (co-op)
        int n = Net.PlayerCount();
        float coopHp = 1f + 0.6f * (n - 1);
        float coopDmg = 1f + 0.25f * (n - 1);

        foreach (var monsterId in room.Spawns)
        {
            var m = Monster.Create(monsterId);
            ApplyPlan(m, room, coopHp, coopDmg);
            m.Position = RandomEdgePosition();
            GetParent().AddChild(m);
        }

        if (room.Boss)
        {
            var b = Monster.Create(room.BossId);
            ApplyPlan(b, room, coopHp, coopDmg);
            b.Position = AnchorPos() + new Vector2(0f, -360f);
            GetParent().AddChild(b);
        }
    }

    private void ApplyPlan(EnemyBase e, RoomPlan room, float coopHp, float coopDmg)
    {
        e.HpMult = room.HpMult * coopHp * _chalHp;
        e.DmgMult = room.DmgMult * coopDmg * _chalDmg;
        e.XpMult = room.XpMult * _chalXp;
        e.LootIlvlOverride = _chalIlvl; // 0 = zwykły run (poziom potwora)
    }

    private void SpawnObstacles(RoomPlan room)
    {
        foreach (var o in _obstacles) if (IsInstanceValid(o)) o.QueueFree();
        _obstacles.Clear();

        var rng = new Random(room.ObstacleSeed);
        for (int i = 0; i < room.ObstacleCount; i++)
        {
            float ang = (float)(rng.NextDouble() * Math.Tau);
            float dist = 160f + (float)rng.NextDouble() * 260f;
            var pos = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist; // wokół (0,0) — deterministycznie na każdym peerze
            float w = 40f + rng.Next(60), h = 40f + rng.Next(60);

            // warstwa 4 = teren/przeszkody: blokuje i gracza (maska 4), i wrogów (maska 4)
            var body = new StaticBody2D { Position = pos, CollisionLayer = 4, CollisionMask = 0 };
            var shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(w, h) } };
            body.AddChild(shape);
            var rect = new ColorRect
            {
                Size = new Vector2(w, h), Position = new Vector2(-w / 2f, -h / 2f),
                Color = new Color(0.20f, 0.17f, 0.28f)
            };
            body.AddChild(rect);
            GetParent().AddChild(body);
            _obstacles.Add(body);
        }
    }

    private Vector2 AnchorPos() =>
        PlayerController.Local != null && IsInstanceValid(PlayerController.Local)
            ? PlayerController.Local.GlobalPosition : Vector2.Zero;

    private Vector2 RandomEdgePosition()
    {
        float ang = GD.Randf() * Mathf.Tau;
        float dist = (float)GD.RandRange(380.0, 520.0);
        return AnchorPos() + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
    }
}

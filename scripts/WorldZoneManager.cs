using System.Collections.Generic;
using System.Linq;
using Godot;
using AshenPantheon.Core;

/// <summary>Trwała strefa mapy świata: mob packi ze stałym respawnem co X s (DsoCraft-style),
/// wyjścia do huba/innych stref. Packi żyją u hosta (replikacja jak zwykle).</summary>
public partial class WorldZoneManager : Node
{
    private WorldZoneDefinition _zone;

    private sealed class PackState
    {
        public MobPackDefinition Def;
        public readonly List<EnemyBase> Alive = new();
        public float RespawnTimer;
        public bool Spawned;
    }

    private readonly List<PackState> _packs = new();

    // run Q (tryb world): mnożniki wyzwania na packach + ilvl dropu (0 = zwykła strefa)
    private float _qHp = 1f, _qDmg = 1f, _qXp = 1f;
    private int _qIlvl;

    /// <summary>Loteria znaczników (Q9): id "prawdziwych" wylosowane przy budowie mapy.</summary>
    public static readonly HashSet<string> LotteryWinners = new();

    // WARIANTY MAPY: lustrzane odbicie całej strefy w X/Y z seeda (co-op spójne) — 4 orientacje
    // z jednego layoutu; wejścia lądują góra/dół/lewo/prawo. Wszystkie pozycje przez Tf().
    private bool _flipX, _flipY;
    private Vector2 Tf(float x, float y) => new(_flipX ? -x : x, _flipY ? -y : y);

    public string TopStatus { get; private set; } = "";
    public string CenterMessage => "";
    public Vector2 SpawnPoint => _zone == null ? Vector2.Zero : Tf(_zone.SpawnX, _zone.SpawnY);

    /// <summary>Gracz pojawia się przy wyjściu prowadzącym do strefy, z której przyszedł
    /// (odsunięty od portalu w głąb mapy, żeby nie teleportować z powrotem). Fallback: domyślny spawn.</summary>
    private void PlaceLocalAtEntry()
    {
        string from = Net.TravelFromZoneId;
        if (_zone == null || string.IsNullOrEmpty(from)) return;
        foreach (var exit in _zone.Exits)
        {
            if (exit.Target != from) continue;
            var portalPos = Tf(exit.X, exit.Y);
            var inward = (SpawnPoint - portalPos).Normalized();
            if (inward == Vector2.Zero) inward = Vector2.Right;
            var entry = portalPos + inward * 170f + new Vector2(0, 26f * (Net.MyId & 3));
            if (PlayerController.Local is { } p && IsInstanceValid(p)) p.GlobalPosition = entry;
            return;
        }
    }

    public override void _Ready()
    {
        AddToGroup("arena"); // Hud czyta TopStatus z tej grupy
        DataLoader.LoadAll();

        string zoneId = string.IsNullOrEmpty(Net.TravelZoneId) ? "swerdfield" : Net.TravelZoneId;
        _zone = WorldMaps.Zone(zoneId);
        GameState.DiscoverZone(zoneId); // waystone fast-travel odblokowany
        TopStatus = $"{_zone.Name}  (levels {_zone.LevelMin}-{_zone.LevelMax})";

        // wariant mapy: lustro X/Y z seeda runu (co-op: identyczne u wszystkich)
        int seed = Net.RunSeed != 0 ? Net.RunSeed : zoneId.GetHashCode();
        _flipX = (seed & 1) != 0;
        _flipY = (seed & 2) != 0;

        // run Q w trybie world: skala trudności (Inf/Hell/Blood) + ilvl dropu z wyzwania
        if (EndgameCatalog.TryParseQ(Net.TravelChallengeId, out int qLvl, out var qd) && qd != null)
        {
            (_qHp, _qDmg, _qXp, _qIlvl) = (qd.HpMult, qd.DmgMult, qd.XpMult, qd.ItemLevel);
            int mapIdx = EndgameCatalog.QMapIndex(zoneId);
            int mapCount = EndgameCatalog.RunOfMap(zoneId)?.Maps.Count ?? 3;
            TopStatus = $"THE PROVING  Q{qLvl} [{qd.Name}] — MAP {mapIdx}/{mapCount}   {_zone.Name}";
        }

        // wyjścia — widoczne u wszystkich (deterministyczne z definicji)
        foreach (var exit in _zone.Exits)
        {
            var portal = GD.Load<PackedScene>("res://scenes/Portal.tscn").Instantiate<Portal>();
            portal.TargetScene = exit.Scene.Length > 0 ? exit.Scene
                : exit.Target == "hub" ? "res://scenes/Main.tscn" : "res://scenes/WorldZone.tscn";
            portal.TargetZone = exit.Target == "hub" ? "" : exit.Target;
            portal.Position = Tf(exit.X, exit.Y);
            var label = portal.GetNodeOrNull<Label>("Label");
            if (label != null) label.Text = string.IsNullOrEmpty(exit.Label) ? exit.Target : exit.Label;
            GetParent().CallDeferred(Node.MethodName.AddChild, portal);
        }

        // wejście od strony poprzedniej strefy (deferred: gracz musi już istnieć po _Ready wszystkich node'ów)
        CallDeferred(nameof(PlaceLocalAtEntry));

        // znaczniki: reach/interact = lokalne per gracz; escort/defend = obiekty host-authoritative (na wszystkich)
        foreach (var marker in _zone.Markers)
        {
            Node2D node = marker.Type switch
            {
                "escort" => new EscortNpc
                {
                    MarkerId = marker.Id, LabelText = marker.Label,
                    DestPos = Tf(marker.DestX, marker.DestY),
                    MaxHp = marker.EscortHp, MoveSpeed = marker.EscortSpeed,
                },
                "defend" => new DefendZone
                {
                    MarkerId = marker.Id, LabelText = marker.Label,
                    Waves = marker.Waves, WaveMonsters = marker.WaveMonsters, WaveInterval = marker.WaveInterval,
                },
                "survive" => new SurviveZone
                {
                    MarkerId = marker.Id, LabelText = marker.Label,
                    SurviveSeconds = marker.SurviveSeconds, WaveMonsters = marker.WaveMonsters, WaveInterval = marker.WaveInterval,
                },
                "hazard" => new HazardZone
                {
                    LabelText = marker.Label,
                    Radius = marker.Radius > 0f ? marker.Radius : 220f,
                    RequiresObjective = marker.RequiresObjective,
                },
                "take" => new QuestMarkerNode { MarkerId = marker.Id, LabelText = marker.Label, Interact = true, CarryTake = marker.CarryId },
                "deposit" => new QuestMarkerNode { MarkerId = marker.Id, LabelText = marker.Label, Interact = true, CarryPut = marker.CarryId },
                "lottery" => new QuestMarkerNode { MarkerId = marker.Id, LabelText = marker.Label, Interact = true, Lottery = true },
                _ => new QuestMarkerNode { MarkerId = marker.Id, LabelText = marker.Label, Interact = marker.Type == "interact" },
            };
            node.Position = Tf(marker.X, marker.Y);
            GetParent().CallDeferred(Node.MethodName.AddChild, node);
        }

        // loteria (Q9): przy wejściu losujemy, które znaczniki są "prawdziwe" — reszta to atrapy
        var lotteryIds = new List<string>();
        int lotteryPick = 0;
        foreach (var marker in _zone.Markers)
            if (marker.Type == "lottery")
            {
                lotteryIds.Add(marker.Id);
                if (marker.LotteryPick > 0) lotteryPick = marker.LotteryPick;
            }
        if (lotteryIds.Count > 0)
        {
            LotteryWinners.Clear();
            var rng = new System.Random();
            while (LotteryWinners.Count < Mathf.Min(lotteryPick, lotteryIds.Count))
                LotteryWinners.Add(lotteryIds[rng.Next(lotteryIds.Count)]);
        }

        // waystone przy wejściu do strefy (fast-travel)
        var waystone = new Waystone { Position = SpawnPoint + Tf(-120f, -120f) };
        GetParent().CallDeferred(Node.MethodName.AddChild, waystone);

        // STRUKTURA MAPY: komnaty (spawn + packi + wyjścia) połączone korytarzami zamiast płaskiego prostokąta.
        // Wszystkie punkty PRZETRANSFORMOWANE (flip X/Y) — layout spójny z wariantem mapy.
        var layout = new ZoneLayout
        {
            Seed = seed,
            Spawn = SpawnPoint,
            RoomCenters = BuildRoomCenters(),
            Portals = _zone.Exits.Select(e => Tf(e.X, e.Y)).Append(SpawnPoint).ToList(),
        };
        GetParent().CallDeferred(Node.MethodName.AddChild, layout);

        if (!Net.IsServer) return;
        foreach (var packDef in _zone.Packs)
            _packs.Add(new PackState { Def = packDef, RespawnTimer = 0f });
    }

    /// <summary>Węzły komnat: spawn + każdy pack + każde wyjście + markery (wszystko przez Tf → wariant mapy).</summary>
    private List<Vector2> BuildRoomCenters()
    {
        var centers = new List<Vector2> { SpawnPoint };
        centers.AddRange(_zone.Packs.Select(p => Tf(p.X, p.Y)));
        centers.AddRange(_zone.Exits.Select(e => Tf(e.X, e.Y)));
        centers.AddRange(_zone.Markers.Select(m => Tf(m.X, m.Y)));
        return centers;
    }

    public void SetStatusRemote(string top, string center) => TopStatus = top;
    public void OnRoomStartedRemote(int index) { }
    public void PlayerDied(int peer) { } // na mapie świata brak wipe'u — gracz wstaje przy wejściu/wyjściu

    public override void _Process(double delta)
    {
        if (!Net.IsServer || _zone == null) return;
        float dt = (float)delta;

        foreach (var pack in _packs)
        {
            if (pack.Spawned)
            {
                pack.Alive.RemoveAll(e => !IsInstanceValid(e));
                if (pack.Alive.Count == 0)
                {
                    pack.Spawned = false;
                    pack.RespawnTimer = pack.Def.RespawnSeconds; // stały respawn co X s
                }
            }
            else
            {
                pack.RespawnTimer -= dt;
                if (pack.RespawnTimer <= 0f) SpawnPack(pack);
            }
        }
    }

    /// <summary>Znacznik questowy: Reach = wejście zalicza cel; Interact = wejście + E.
    /// WIDOCZNY tylko, gdy jego cel jest AKTYWNY i nieukończony (bez mylących popupów).</summary>
    private partial class QuestMarkerNode : Area2D
    {
        public string MarkerId = "";
        public string LabelText = "";
        public bool Interact;
        public string CarryTake = ""; // Q10: interakcja daje token (tylko gdy rąk nie zajmuje inny)
        public string CarryPut = "";  // Q10: interakcja zużywa token (wymaga niesienia)
        public bool Lottery;          // Q9: liczy się tylko wylosowany znacznik (reszta = atrapy)
        private bool _used; // interact z wildcard-celem: każdy marker liczy się RAZ (np. 5 różnych grzybów)

        private bool _inside;
        private Label _label;
        private float _visCheck;

        public override void _Ready()
        {
            AddToGroup("minimap_objective");
            CollisionLayer = 0;
            CollisionMask = 1;
            AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 60f } });
            _label = new Label
            {
                Text = LabelHint(),
                Position = new Vector2(-70, -50),
                Modulate = new Color(1f, 0.9f, 0.5f),
            };
            AddChild(_label);
            UpdateVisibility();
            BodyEntered += b =>
            {
                if (b is not PlayerController p || !p.IsMultiplayerAuthority()) return;
                _inside = true;
                if (Interact) return;
                if (GameState.Quests.OnReach(MarkerId)) Credit("objective reached!"); // popup TYLKO przy zaliczeniu
            };
            BodyExited += b => { if (b is PlayerController p && p.IsMultiplayerAuthority()) _inside = false; };
        }

        public override void _Process(double delta)
        {
            _visCheck -= (float)delta;
            if (_visCheck > 0f) return;
            _visCheck = 0.5f;
            UpdateVisibility();
        }

        /// <summary>Cel z tym targetem jest w aktywnym quescie i nieukończony?</summary>
        private bool ObjectiveActive()
        {
            foreach (var questId in GameState.Quests.Active.Keys)
            {
                var q = QuestCatalog.Find(questId);
                if (q == null) continue;
                foreach (var o in q.Objectives)
                    if (QuestLog.TargetMatches(o.Target, MarkerId) && !GameState.Quests.ObjectiveDone(q, o))
                        return true;
            }
            return false;
        }

        private void UpdateVisibility()
        {
            bool active = !_used && ObjectiveActive();
            Visible = active;
            SetDeferred(Area2D.PropertyName.Monitoring, active);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!Interact || !_inside || !Visible) return;
            if (@event is InputEventKey k && k.Pressed && !k.Echo && Keybinds.Matches(k, "interact"))
            {
                GetViewport().SetInputAsHandled();

                // loteria (Q9): atrapa — informacja i zużycie znacznika, bez postępu
                if (Lottery && !LotteryWinners.Contains(MarkerId))
                {
                    _used = true;
                    UpdateVisibility();
                    FloatingText.Spawn(GetParent(), GlobalPosition, "a decoy — nothing here", new Color(0.7f, 0.7f, 0.75f), 15);
                    return;
                }
                // niesienie (Q10): najpierw odłóż, dopiero potem weź kolejny (1 → 1)
                if (CarryTake.Length > 0 && GameState.CarryTokens.Contains(CarryTake))
                {
                    FloatingText.Spawn(GetParent(), GlobalPosition, "deposit the fragment you carry first!", new Color(1f, 0.7f, 0.4f), 15);
                    return;
                }
                if (CarryPut.Length > 0 && !GameState.CarryTokens.Contains(CarryPut))
                {
                    FloatingText.Spawn(GetParent(), GlobalPosition, "you need a fragment to deposit!", new Color(1f, 0.7f, 0.4f), 15);
                    return;
                }

                if (GameState.Quests.OnInteract(MarkerId))
                {
                    if (CarryTake.Length > 0) GameState.CarryTokens.Add(CarryTake);
                    if (CarryPut.Length > 0) GameState.CarryTokens.Remove(CarryPut);
                    Credit("used!");
                    _used = true; // ten obiekt zużyty — cel wymaga N RÓŻNYCH markerów
                    UpdateVisibility();
                    QRunFlow.CheckAutoComplete(); // quest Q może kończyć się interakcją
                }
            }
        }

        private void Credit(string text)
        {
            GameState.Save();
            FloatingText.Spawn(GetParent(), GlobalPosition, text, new Color(1f, 0.9f, 0.5f), 16);
        }

        private string LabelHint() => Interact ? $"◈ {LabelText} [E]" : $"◈ {LabelText}";

        public override void _Draw() =>
            DrawArc(Vector2.Zero, 60f, 0, Mathf.Tau, 40, new Color(1f, 0.9f, 0.5f, 0.5f), 2f);
    }

    private void SpawnPack(PackState pack)
    {
        pack.Spawned = true;
        var center = Tf(pack.Def.X, pack.Def.Y);
        int i = 0;
        foreach (var monsterId in pack.Def.Monsters)
        {
            var m = Monster.Create(monsterId);
            m.AggroRange = pack.Def.AggroRange;
            m.HomePos = center;
            if (_qIlvl > 0) // run Q: skala trudności + drop na ilvl stopnia
            {
                m.HpMult *= _qHp;
                m.DmgMult *= _qDmg;
                m.XpMult *= _qXp;
                m.LootIlvlOverride = _qIlvl;
            }
            m.Position = center + Vector2.Right.Rotated(Mathf.Tau * i / Mathf.Max(1, pack.Def.Monsters.Count)) * pack.Def.Spread;
            i++;
            pack.Alive.Add(m);
            GetParent().AddChild(m);
        }
    }
}

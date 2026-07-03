using System.Collections.Generic;
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

    public string TopStatus { get; private set; } = "";
    public string CenterMessage => "";
    public Vector2 SpawnPoint => _zone == null ? Vector2.Zero : new Vector2(_zone.SpawnX, _zone.SpawnY);

    public override void _Ready()
    {
        AddToGroup("arena"); // Hud czyta TopStatus z tej grupy
        DataLoader.LoadAll();

        string zoneId = string.IsNullOrEmpty(Net.TravelZoneId) ? "swerdfield" : Net.TravelZoneId;
        _zone = WorldMaps.Zone(zoneId);
        GameState.DiscoverZone(zoneId); // waystone fast-travel odblokowany
        TopStatus = $"{_zone.Name}  (levels {_zone.LevelMin}-{_zone.LevelMax})";

        // wyjścia — widoczne u wszystkich (deterministyczne z definicji)
        foreach (var exit in _zone.Exits)
        {
            var portal = GD.Load<PackedScene>("res://scenes/Portal.tscn").Instantiate<Portal>();
            portal.TargetScene = exit.Scene.Length > 0 ? exit.Scene
                : exit.Target == "hub" ? "res://scenes/Main.tscn" : "res://scenes/WorldZone.tscn";
            portal.TargetZone = exit.Target == "hub" ? "" : exit.Target;
            portal.Position = new Vector2(exit.X, exit.Y);
            var label = portal.GetNodeOrNull<Label>("Label");
            if (label != null) label.Text = string.IsNullOrEmpty(exit.Label) ? exit.Target : exit.Label;
            GetParent().CallDeferred(Node.MethodName.AddChild, portal);
        }

        // znaczniki: reach/interact = lokalne per gracz; escort/defend = obiekty host-authoritative (na wszystkich)
        foreach (var marker in _zone.Markers)
        {
            Node2D node = marker.Type switch
            {
                "escort" => new EscortNpc
                {
                    MarkerId = marker.Id, LabelText = marker.Label,
                    DestPos = new Vector2(marker.DestX, marker.DestY),
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
                _ => new QuestMarkerNode { MarkerId = marker.Id, LabelText = marker.Label, Interact = marker.Type == "interact" },
            };
            node.Position = new Vector2(marker.X, marker.Y);
            GetParent().CallDeferred(Node.MethodName.AddChild, node);
        }

        // waystone przy wejściu do strefy (fast-travel)
        var waystone = new Waystone { Position = new Vector2(_zone.SpawnX - 120f, _zone.SpawnY - 120f) };
        GetParent().CallDeferred(Node.MethodName.AddChild, waystone);

        if (!Net.IsServer) return;
        foreach (var packDef in _zone.Packs)
            _packs.Add(new PackState { Def = packDef, RespawnTimer = 0f });
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

    /// <summary>Znacznik questowy: Reach = wejście zalicza cel; Interact = wejście + E (dźwignia/ołtarz).</summary>
    private partial class QuestMarkerNode : Area2D
    {
        public string MarkerId = "";
        public string LabelText = "";
        public bool Interact;

        private bool _inside;
        private Label _label;

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
            BodyEntered += b =>
            {
                if (b is not PlayerController p || !p.IsMultiplayerAuthority()) return;
                _inside = true;
                if (Interact) { _label.Text = $"◈ {LabelText} [E]"; return; }
                Credit("objective reached!");
            };
            BodyExited += b => { if (b is PlayerController p && p.IsMultiplayerAuthority()) { _inside = false; if (_label != null) _label.Text = LabelHint(); } };
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!Interact || !_inside) return;
            if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.E)
            {
                if (GameState.Quests.OnInteract(MarkerId)) { Credit("used!"); }
                GetViewport().SetInputAsHandled();
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
        var center = new Vector2(pack.Def.X, pack.Def.Y);
        int i = 0;
        foreach (var monsterId in pack.Def.Monsters)
        {
            var m = Monster.Create(monsterId);
            m.AggroRange = pack.Def.AggroRange;
            m.HomePos = center;
            m.Position = center + Vector2.Right.Rotated(Mathf.Tau * i / Mathf.Max(1, pack.Def.Monsters.Count)) * pack.Def.Spread;
            i++;
            pack.Alive.Add(m);
            GetParent().AddChild(m);
        }
    }
}

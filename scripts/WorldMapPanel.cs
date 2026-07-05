using System.Linq;
using Godot;
using AshenPantheon.Core;

/// <summary>Mapa świata (M lub waystone): lista stref wg poziomu, teleport do ODKRYTYCH instancji.
/// W co-op podróżą steruje host (party travel).</summary>
public partial class WorldMapPanel : CanvasLayer
{
    private Panel _root;
    private VBoxContainer _list;

    public override void _Ready()
    {
        Layer = 9;
        AddToGroup("worldmap");
        _root = new Panel
        {
            AnchorLeft = 0f, AnchorTop = 0f, AnchorRight = 1f, AnchorBottom = 1f,
            OffsetLeft = 40, OffsetTop = 36, OffsetRight = -40, OffsetBottom = -170,
            Visible = false,
        };
        UiPanels.Solidify(_root);
        AddChild(_root);

        var vb = new VBoxContainer { AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = 18, OffsetTop = 14, OffsetRight = -18, OffsetBottom = -14 };
        vb.AddThemeConstantOverride("separation", 8);
        _root.AddChild(vb);
        vb.AddChild(new Label { Text = "WORLD MAP    [M] close    fast-travel between discovered zones" });

        var scroll = UiKit.VScroll();
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);
        vb.AddChild(scroll);
    }

    public static void Toggle(SceneTree tree)
    {
        if (tree.GetFirstNodeInGroup("worldmap") is WorldMapPanel panel)
            panel.SetOpen(!panel._root.Visible);
    }

    private void SetOpen(bool open)
    {
        _root.Visible = open;
        if (open) Rebuild();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && (Keybinds.Matches(k, "map") || k.PhysicalKeycode == Key.Escape) && _root.Visible)
        {
            _root.Visible = false;
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventKey k2 && k2.Pressed && !k2.Echo && Keybinds.Matches(k2, "map") && !_root.Visible)
        {
            SetOpen(true);
            GetViewport().SetInputAsHandled();
        }
    }

    private void Rebuild()
    {
        foreach (Node c in _list.GetChildren()) c.QueueFree();

        bool canTravel = !Net.Online || Net.IsServer;
        string curZone = Net.TravelZoneId;

        if (!canTravel)
            _list.AddChild(new Label { Text = "Only the host can travel the party.", Modulate = new Color(0.9f, 0.7f, 0.4f) });

        // Town (zawsze dostępne); ❓ = masz quest do oddania (NPC są w mieście)
        bool turnInReady = GameState.Quests.Active.Keys
            .Select(QuestCatalog.Find)
            .Any(q => q != null && GameState.Quests.ReadyToTurnIn(q));
        AddRow("⌂ Town  (hub)" + (turnInReady ? "   ❓ quest turn-in" : ""),
            curScene: GetTree().CurrentScene?.Name == "Hub",
            enabled: canTravel, () => Travel("res://scenes/Main.tscn", ""));

        foreach (var z in WorldMaps.Ordered())
        {
            bool discovered = GameState.DiscoveredZones.Contains(z.Id);
            bool isCurrent = z.Id == curZone && GetTree().CurrentScene?.Name == "WorldZone";
            string label = $"{z.Name}   (levels {z.LevelMin}-{z.LevelMax})" +
                           (ZoneHasActiveQuest(z.Id) ? "   ❗ quest" : "") +
                           (discovered ? "" : "   — undiscovered");
            AddRow(label, isCurrent, enabled: canTravel && discovered, () => Travel("res://scenes/WorldZone.tscn", z.Id));
        }
    }

    /// <summary>Czy w strefie toczy się aktywny (nieukończony) quest — ❗ na liście mapy.</summary>
    private static bool ZoneHasActiveQuest(string zoneId) =>
        GameState.Quests.Active.Keys
            .Select(QuestCatalog.Find)
            .Any(q => q != null && q.Zone == zoneId && !GameState.Quests.ReadyToTurnIn(q));

    private void AddRow(string label, bool curScene, bool enabled, System.Action onTravel)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var name = new Label { Text = (curScene ? "▶ " : "   ") + label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        if (curScene) name.Modulate = new Color(0.6f, 0.9f, 1f);
        else if (!enabled) name.Modulate = new Color(0.5f, 0.5f, 0.55f);
        row.AddChild(name);

        if (!curScene)
        {
            var btn = new Button { Text = "Travel", Disabled = !enabled, CustomMinimumSize = new Vector2(90, 0) };
            btn.Pressed += () => onTravel();
            row.AddChild(btn);
        }
        _list.AddChild(row);
    }

    private void Travel(string scene, string zoneId)
    {
        _root.Visible = false;
        int seed = (int)(GD.Randi() % int.MaxValue);
        if (seed == 0) seed = 1;
        Net.TravelAll(scene, seed, zoneId);
    }
}

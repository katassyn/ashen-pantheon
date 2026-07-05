using System.Text.Json;
using Godot;
using AshenPantheon.Core;

/// <summary>Inspekcja innego gracza (PPM na nicku → Inspect): poziom, klasa i ekwipunek.
/// Dane przychodzą RPC od TAMTEGO klienta — nic nie zgadujemy lokalnie.</summary>
public partial class InspectPanel : CanvasLayer
{
    private long _peer;
    private Panel _root;
    private VBoxContainer _list;
    private Label _header;

    public static void Open(SceneTree tree, long peer)
    {
        tree.Root.GetNodeOrNull<InspectPanel>("InspectPanel")?.QueueFree(); // jedno okno naraz
        var p = new InspectPanel { Name = "InspectPanel", Layer = 8, _peer = peer };
        tree.Root.AddChild(p);
        Net.RequestInspect(peer);
    }

    public override void _Ready()
    {
        Net.InspectReceived += OnInspect; // nazwany handler — odpinany w _ExitTree (pattern)
        _root = UiKit.Window(this, $"INSPECT — {Net.NameOf(_peer)}    [E/Esc] close");
        var vb = _root.GetNode<VBoxContainer>("VB");
        _header = new Label { Text = "Waiting for reply…" };
        vb.AddChild(_header);

        var scroll = UiKit.VScroll();
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);
        vb.AddChild(scroll);
    }

    public override void _ExitTree() => Net.InspectReceived -= OnInspect;

    private void OnInspect(long from, string json)
    {
        if (from != _peer) return;

        InspectPayload data;
        try { data = JsonSerializer.Deserialize<InspectPayload>(json); }
        catch { _header.Text = "Malformed reply."; return; }
        if (data == null) return;

        string cls = GameData.Classes.TryGetValue(data.ClassId ?? "", out var c) ? c.Name : data.ClassId;
        _header.Text = $"{data.Name}   —   Level {data.Level} {cls}";

        foreach (Node n in _list.GetChildren()) n.QueueFree();
        if (data.Items.Count == 0)
        {
            _list.AddChild(new Label { Text = "  (no equipment)" });
            return;
        }
        foreach (var (slot, dto) in data.Items)
        {
            var item = ItemMapper.FromDto(dto);
            var row = new Label
            {
                Text = $"{slot,-14}  {item.Name}  [{item.Rarity}]",
                TooltipText = CharacterPanel.Describe(item), // pełne affixy w tooltipie
                MouseFilter = Control.MouseFilterEnum.Stop,   // tooltip wymaga odbioru myszy
            };
            row.Modulate = ItemPickup.RarityColor(item.Rarity);
            _list.AddChild(row);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode is Key.E or Key.Escape)
        {
            QueueFree();
            GetViewport().SetInputAsHandled();
        }
    }
}

/// <summary>Kontrakt odpowiedzi inspekcji (JSON przez RPC).</summary>
public class InspectPayload
{
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public string ClassId { get; set; } = "";
    public System.Collections.Generic.Dictionary<string, ItemDto> Items { get; set; } = new();
}

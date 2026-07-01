using System.Collections.Generic;
using Godot;
using AshenPantheon.Core;

/// <summary>Panel postaci/EQ. Otwierany klawiszem I wszędzie. Klik w item = załóż, klik w slot = zdejmij.</summary>
public partial class CharacterPanel : CanvasLayer
{
    private static readonly EquipmentSlot[] SlotOrder =
    {
        EquipmentSlot.Helmet, EquipmentSlot.Shoulders, EquipmentSlot.BodyArmour, EquipmentSlot.Gloves,
        EquipmentSlot.Boots, EquipmentSlot.Belt, EquipmentSlot.Amulet, EquipmentSlot.Ring1,
        EquipmentSlot.Ring2, EquipmentSlot.Weapon, EquipmentSlot.OffHand
    };

    private Panel _root;
    private VBoxContainer _slots;
    private VBoxContainer _bag;
    private Label _stats;
    private PlayerController _player;
    private Button[] _slotButtons;

    public override void _Ready()
    {
        _root = GetNode<Panel>("%Root");
        _slots = GetNode<VBoxContainer>("%Slots");
        _bag = GetNode<VBoxContainer>("%Bag");
        _stats = GetNode<Label>("%Stats");

        _slotButtons = new Button[SlotOrder.Length];
        for (int i = 0; i < SlotOrder.Length; i++)
        {
            var slot = SlotOrder[i];
            var b = new Button { Text = slot.ToString() };
            b.Pressed += () => OnSlotPressed(slot);
            _slots.AddChild(b);
            _slotButtons[i] = b;
        }

        _root.Visible = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.I)
        {
            _root.Visible = !_root.Visible;
            if (_root.Visible) Refresh();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Refresh()
    {
        _player ??= GetTree().GetFirstNodeInGroup("player") as PlayerController;

        for (int i = 0; i < SlotOrder.Length; i++)
        {
            var item = GameState.Equipment.Get(SlotOrder[i]);
            _slotButtons[i].Text = item == null ? $"{SlotOrder[i]}: —" : $"{SlotOrder[i]}: {Describe(item)}";
        }

        foreach (Node c in _bag.GetChildren()) c.QueueFree();
        foreach (var item in GameState.Inventory.Items)
        {
            var captured = item;
            var b = new Button { Text = Describe(captured) };
            b.Pressed += () => OnBagPressed(captured);
            _bag.AddChild(b);
        }

        _stats.Text = "Statystyki → klawisz C";
    }

    private static string Describe(Item item)
    {
        var parts = new List<string>();
        foreach (var a in item.Affixes) parts.Add($"{a.Stat} {a.Value:0.##}");
        return parts.Count > 0 ? $"{item.Name} ({string.Join(", ", parts)})" : item.Name;
    }

    private void OnSlotPressed(EquipmentSlot slot)
    {
        var item = GameState.Equipment.Unequip(slot);
        if (item != null) GameState.Inventory.Add(item);
        _player?.Refresh();
        Refresh();
    }

    private void OnBagPressed(Item item)
    {
        var removed = GameState.Equipment.EquipAuto(item);
        if (removed == null) return; // brak pasującego slotu
        GameState.Inventory.Remove(item);
        foreach (var r in removed) GameState.Inventory.Add(r);
        _player?.Refresh();
        Refresh();
    }
}

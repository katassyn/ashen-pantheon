using System.Collections.Generic;
using System.Text;
using Godot;
using AshenPantheon.Core;

/// <summary>Panel ekwipunku (I): 11 slotów + plecak-siatka (tetris). Drag&drop item↔slot i w obrębie siatki;
/// PPM na itemie w siatce = szybkie założenie. Tooltips z affixami.</summary>
public partial class CharacterPanel : CanvasLayer
{
    private static readonly EquipmentSlot[] SlotOrder =
    {
        EquipmentSlot.Helmet, EquipmentSlot.Shoulders, EquipmentSlot.BodyArmour, EquipmentSlot.Gloves,
        EquipmentSlot.Boots, EquipmentSlot.Belt, EquipmentSlot.Amulet, EquipmentSlot.Ring1,
        EquipmentSlot.Ring2, EquipmentSlot.Weapon, EquipmentSlot.OffHand
    };

    public const int Cell = 40;

    private Panel _root;
    private VBoxContainer _slots;
    private Control _gridHost;
    private PlayerController _player;

    public override void _Ready()
    {
        _root = GetNode<Panel>("%Root");
        _slots = GetNode<VBoxContainer>("%Slots");
        _gridHost = GetNode<Control>("%GridHost");
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

    public static string Describe(Item item)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{item.Name}  [{item.Rarity}]");
        foreach (var a in item.Affixes) sb.AppendLine($"  {a.Stat} +{a.Value:0.##}");
        if (item.Effect != UniqueEffect.None) sb.AppendLine($"  ✦ {item.Effect}");
        sb.Append($"  wartość: {Vendor.SellPrice(item)} złota");
        return sb.ToString();
    }

    public void Refresh()
    {
        _player ??= GetTree().GetFirstNodeInGroup("player") as PlayerController;

        // ── sloty EQ ──
        foreach (Node c in _slots.GetChildren()) c.QueueFree();
        foreach (var slot in SlotOrder)
        {
            var item = GameState.Equipment.Get(slot);
            var b = new EquipSlotButton
            {
                Slot = slot, Panel = this,
                Text = item == null ? $"{slot}: —" : $"{slot}: {item.Name}",
                TooltipText = item == null ? "Upuść tu pasujący item" : Describe(item),
            };
            if (item != null) b.Modulate = ItemPickup.RarityColor(item.Rarity);
            _slots.AddChild(b);
        }

        // ── siatka plecaka ──
        foreach (Node c in _gridHost.GetChildren()) c.QueueFree();
        var bag = GameState.Bag;
        _gridHost.CustomMinimumSize = new Vector2(bag.Width * Cell, bag.Height * Cell);

        var bg = new GridDropArea { Panel = this, Size = new Vector2(bag.Width * Cell, bag.Height * Cell) };
        _gridHost.AddChild(bg);

        foreach (var placed in bag.Placed)
        {
            var (w, h) = placed.Item.Size;
            var btn = new BagItemButton
            {
                Item = placed.Item, Panel = this,
                Position = new Vector2(placed.X * Cell, placed.Y * Cell),
                Size = new Vector2(w * Cell - 2, h * Cell - 2),
                Text = ShortName(placed.Item),
                TooltipText = Describe(placed.Item) + "\nPPM = załóż · przeciągnij na slot/siatkę",
                ClipText = true,
            };
            btn.Modulate = ItemPickup.RarityColor(placed.Item.Rarity);
            _gridHost.AddChild(btn);
        }

        _player?.Refresh();
    }

    private static string ShortName(Item item)
    {
        string letter = item.Kind switch
        {
            ItemKind.Helmet => "HEŁM", ItemKind.Shoulders => "NAR", ItemKind.BodyArmour => "ZBR",
            ItemKind.Gloves => "RĘK", ItemKind.Boots => "BUT", ItemKind.Belt => "PAS",
            ItemKind.Amulet => "AMU", ItemKind.Ring => "PIER", ItemKind.OneHandWeapon => "1H",
            ItemKind.TwoHandWeapon => "2H", ItemKind.OffHand => "OFF", _ => "?"
        };
        return item.Rarity >= Rarity.Legendary ? $"★{letter}" : letter;
    }

    // ── operacje ──

    public void EquipFromBag(Item item)
    {
        var removed = GameState.Equipment.EquipAuto(item);
        if (removed == null) return;
        GameState.Bag.Remove(item);
        foreach (var r in removed) GameState.Bag.TryAutoPlace(r);
        GameState.Save();
        Refresh();
    }

    public void EquipToSlot(Item item, EquipmentSlot slot)
    {
        if (!GameState.Equipment.CanEquip(item, slot)) return;
        GameState.Bag.Remove(item);
        var removed = GameState.Equipment.Equip(item, slot);
        foreach (var r in removed) GameState.Bag.TryAutoPlace(r);
        GameState.Save();
        Refresh();
    }

    public void UnequipToBag(EquipmentSlot slot)
    {
        var item = GameState.Equipment.Unequip(slot);
        if (item == null) return;
        if (!GameState.Bag.TryAutoPlace(item))
            GameState.Equipment.Equip(item, slot); // plecak pełny → wraca
        GameState.Save();
        Refresh();
    }

    public void MoveInBag(Item item, int x, int y)
    {
        if (GameState.Bag.CanPlaceAt(item, x, y, ignore: item))
        {
            GameState.Bag.PlaceAt(item, x, y);
            GameState.Save();
        }
        Refresh();
    }
}

/// <summary>Item w siatce: źródło drag&drop, PPM = szybkie założenie.</summary>
public partial class BagItemButton : Button
{
    public Item Item;
    public CharacterPanel Panel;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
        {
            Panel.EquipFromBag(Item);
            AcceptEvent();
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var preview = new Label { Text = Item.Name, Modulate = ItemPickup.RarityColor(Item.Rarity) };
        SetDragPreview(preview);
        return new GodotObjectRef { Item = Item };
    }
}

/// <summary>Tło siatki: przyjmuje dropy itemów (precyzyjne ułożenie w komórkach).</summary>
public partial class GridDropArea : ColorRect
{
    public CharacterPanel Panel;

    public GridDropArea() => Color = new Color(0.05f, 0.045f, 0.08f, 0.9f);

    public override void _Draw()
    {
        var bag = GameState.Bag;
        var line = new Color(0.3f, 0.27f, 0.4f, 0.5f);
        for (int x = 0; x <= bag.Width; x++)
            DrawLine(new Vector2(x * CharacterPanel.Cell, 0), new Vector2(x * CharacterPanel.Cell, bag.Height * CharacterPanel.Cell), line);
        for (int y = 0; y <= bag.Height; y++)
            DrawLine(new Vector2(0, y * CharacterPanel.Cell), new Vector2(bag.Width * CharacterPanel.Cell, y * CharacterPanel.Cell), line);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        data.As<GodotObject>() is GodotObjectRef;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (data.As<GodotObject>() is not GodotObjectRef dragRef) return;
        int x = (int)(atPosition.X / CharacterPanel.Cell);
        int y = (int)(atPosition.Y / CharacterPanel.Cell);
        Panel.MoveInBag(dragRef.Item, x, y);
    }
}

/// <summary>Slot EQ: przyjmuje drop itemu (jeśli pasuje), klik = zdejmij.</summary>
public partial class EquipSlotButton : Button
{
    public EquipmentSlot Slot;
    public CharacterPanel Panel;

    public override void _Pressed() => Panel.UnequipToBag(Slot);

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        data.As<GodotObject>() is GodotObjectRef dragRef && GameState.Equipment.CanEquip(dragRef.Item, Slot);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (data.As<GodotObject>() is GodotObjectRef dragRef)
            Panel.EquipToSlot(dragRef.Item, Slot);
    }
}

/// <summary>Opakowanie Item (C#) w GodotObject, by przenieść go przez Variant w drag&drop.</summary>
public partial class GodotObjectRef : RefCounted
{
    public Item Item;
}

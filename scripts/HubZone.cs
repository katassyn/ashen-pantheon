using Godot;
using AshenPantheon.Core;

/// <summary>Strefa interakcji w hubie (vendor/stash): podejdź i wciśnij E.</summary>
public partial class HubZone : Area2D
{
    [Export] public string Kind = "vendor"; // "vendor" | "stash"
    [Export] public float Radius = 70f;

    private bool _playerInside;
    private Label _hint;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1;
        var shape = new CollisionShape2D { Shape = new CircleShape2D { Radius = Radius } };
        AddChild(shape);
        BodyEntered += b => { if (b is PlayerController p && p.IsMultiplayerAuthority()) { _playerInside = true; ShowHint(true); } };
        BodyExited += b => { if (b is PlayerController p && p.IsMultiplayerAuthority()) { _playerInside = false; ShowHint(false); } };

        _hint = new Label { Text = "[E]", Position = new Vector2(-12, -70), Visible = false };
        AddChild(_hint);
    }

    private void ShowHint(bool on) => _hint.Visible = on;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_playerInside) return;
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.E)
        {
            if (Kind == "vendor") VendorPanel.Toggle(GetTree());
            else if (Kind == "stash") StashPanel.Toggle(GetTree());
            else QuestNpc.Interact(Kind, GetTree()); // Kind = npcId (amuun/guildmaster/...)
            GetViewport().SetInputAsHandled();
        }
    }
}

/// <summary>Sprzedaż do NPC: klik = sprzedaj za złoto (ekonomia gotowa pod przyszły AH).</summary>
public partial class VendorPanel : CanvasLayer
{
    private Panel _root;
    private VBoxContainer _list;
    private Label _gold;

    public static void Toggle(SceneTree tree)
    {
        if (tree.Root.GetNodeOrNull<VendorPanel>("VendorPanel") is { } existing) { existing.QueueFree(); return; }
        var p = new VendorPanel { Name = "VendorPanel", Layer = 7 };
        tree.Root.AddChild(p);
    }

    public override void _Ready()
    {
        _root = UiKit.Window(this, "VENDOR — click = sell    [E/Esc] close");
        var vb = _root.GetNode<VBoxContainer>("VB");
        _gold = new Label();
        vb.AddChild(_gold);

        var bulk = new HBoxContainer();
        bulk.AddThemeConstantOverride("separation", 8);
        var sellJunk = new Button { Text = "Sell all Normal + Magic" };
        sellJunk.Pressed += () => SellWhere(i => i.Rarity <= Rarity.Magic);
        var sellRare = new Button { Text = "Sell all up to Rare" };
        sellRare.Pressed += () => SellWhere(i => i.Rarity <= Rarity.Rare);
        bulk.AddChild(sellJunk);
        bulk.AddChild(sellRare);
        vb.AddChild(bulk);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vb.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_list);
        Refresh();
    }

    private void SellWhere(System.Func<Item, bool> match)
    {
        var toSell = new System.Collections.Generic.List<Item>();
        foreach (var placed in GameState.Bag.Placed)
            if (match(placed.Item)) toSell.Add(placed.Item);

        long earned = 0;
        foreach (var item in toSell)
        {
            earned += Vendor.SellPrice(item);
            GameState.Bag.Remove(item);
        }
        if (earned > 0) { GameState.Wallet.Gold += earned; GameState.Save(); }
        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode is Key.E or Key.Escape)
        {
            QueueFree();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Refresh()
    {
        _gold.Text = $"Your gold: {GameState.Wallet.Gold}";
        foreach (Node c in _list.GetChildren()) c.QueueFree();
        foreach (var placed in GameState.Bag.Placed)
        {
            var item = placed.Item;
            long price = Vendor.SellPrice(item);
            var b = new Button { Text = $"{item.Name}  [{item.Rarity}]  —  {price} gold", TooltipText = CharacterPanel.Describe(item) };
            b.Modulate = ItemPickup.RarityColor(item.Rarity);
            b.Pressed += () =>
            {
                GameState.Bag.Remove(item);
                GameState.Wallet.Gold += price;
                GameState.Save();
                Refresh();
            };
            _list.AddChild(b);
        }
    }
}

/// <summary>Skrytka (jedna zakładka): klik przenosi item plecak↔skrytka.</summary>
public partial class StashPanel : CanvasLayer
{
    private Panel _root;
    private VBoxContainer _bagList, _stashList;

    public static void Toggle(SceneTree tree)
    {
        if (tree.Root.GetNodeOrNull<StashPanel>("StashPanel") is { } existing) { existing.QueueFree(); return; }
        var p = new StashPanel { Name = "StashPanel", Layer = 7 };
        tree.Root.AddChild(p);
    }

    public override void _Ready()
    {
        _root = UiKit.Window(this, "STASH — click moves an item    [E/Esc] close");
        var hb = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        hb.AddThemeConstantOverride("separation", 24);
        _root.GetNode<VBoxContainer>("VB").AddChild(hb);

        _bagList = MakeColumn(hb, "BAG");
        _stashList = MakeColumn(hb, "STASH");
        Refresh();
    }

    private static VBoxContainer MakeColumn(HBoxContainer parent, string title)
    {
        var vb = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        vb.AddChild(new Label { Text = title });
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vb.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(list);
        parent.AddChild(vb);
        return list;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode is Key.E or Key.Escape)
        {
            QueueFree();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Refresh()
    {
        Fill(_bagList, GameState.Bag, GameState.Stash);
        Fill(_stashList, GameState.Stash, GameState.Bag);
    }

    private void Fill(VBoxContainer list, GridInventory from, GridInventory to)
    {
        foreach (Node c in list.GetChildren()) c.QueueFree();
        foreach (var placed in from.Placed)
        {
            var item = placed.Item;
            var b = new Button { Text = $"{item.Name}  [{item.Rarity}]", TooltipText = CharacterPanel.Describe(item) };
            b.Modulate = ItemPickup.RarityColor(item.Rarity);
            b.Pressed += () =>
            {
                if (!to.TryAutoPlace(item)) return; // brak miejsca
                from.Remove(item);
                GameState.Save();
                Refresh();
            };
            list.AddChild(b);
        }
    }
}

/// <summary>Wspólny szkielet okienka UI budowanego w kodzie.</summary>
public static class UiKit
{
    public static Panel Window(CanvasLayer layer, string title)
    {
        UiPanels.CloseAllExcept(layer.GetTree(), null); // zamknij panele C/I/K pod spodem
        var root = new Panel
        {
            AnchorLeft = 0.5f, AnchorTop = 0f, AnchorRight = 0.5f, AnchorBottom = 1f,
            OffsetLeft = -380, OffsetTop = 36, OffsetRight = 380, OffsetBottom = -150,
        };
        UiPanels.Solidify(root);
        layer.AddChild(root);
        var vb = new VBoxContainer { Name = "VB", AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = 16, OffsetTop = 14, OffsetRight = -16, OffsetBottom = -14 };
        vb.AddThemeConstantOverride("separation", 10);
        root.AddChild(vb);
        vb.AddChild(new Label { Text = title });
        return root;
    }
}

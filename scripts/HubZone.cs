using Godot;
using AshenPantheon.Core;

/// <summary>Strefa interakcji w hubie (vendor/stash): podejdź i wciśnij E.</summary>
public partial class HubZone : Area2D
{
    [Export] public string Kind = "vendor"; // "vendor" | "stash"
    [Export] public float Radius = 70f;

    private bool _playerInside;
    private Label _hint;
    private Label _questMark; // '!' quest do wzięcia / '?' do oddania (tylko NPC)
    private float _markTimer;

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

        if (Kind is not ("vendor" or "stash" or "ah" or "endgame"))
        {
            _questMark = new Label { Position = new Vector2(-9, -104), Visible = false };
            _questMark.AddThemeFontSizeOverride("font_size", 26);
            _questMark.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
            _questMark.AddThemeColorOverride("font_outline_color", Colors.Black);
            _questMark.AddThemeConstantOverride("outline_size", 4);
            AddChild(_questMark);
        }
    }

    public override void _Process(double delta)
    {
        if (_questMark == null) return;
        _markTimer -= (float)delta;
        if (_markTimer > 0f) return;
        _markTimer = 0.5f; // tani polling — stan questów zmienia się rzadko
        char? mark = QuestNpc.Indicator(Kind);
        _questMark.Visible = mark.HasValue;
        if (mark.HasValue) _questMark.Text = mark.Value.ToString();
    }

    private void ShowHint(bool on) => _hint.Visible = on;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_playerInside) return;
        if (@event is InputEventKey k && k.Pressed && !k.Echo && Keybinds.Matches(k, "interact"))
        {
            if (Kind == "vendor") VendorPanel.Toggle(GetTree());
            else if (Kind == "stash") StashPanel.Toggle(GetTree());
            else if (Kind == "ah") AuctionPanel.Toggle(GetTree());
            else if (Kind == "endgame") EndgamePanel.Toggle(GetTree());
            else QuestNpc.Interact(Kind, GetTree()); // Kind = npcId (amuun/guildmaster/...)
            GetViewport().SetInputAsHandled();
        }
    }
}

/// <summary>Vendor MMO: zakładki SELL (sprzedaż z plecaka) / BUY (asortyment rolowany na poziom gracza)
/// / BUYBACK (odkup pomyłkowo sprzedanych — ta sesja).</summary>
public partial class VendorPanel : CanvasLayer
{
    private Panel _root;
    private VBoxContainer _list;
    private Label _gold;
    private HBoxContainer _bulk;
    private string _mode = "sell"; // sell | buy | buyback

    // asortyment/odkup wspólne dla sesji (statyczne — panel jest tworzony na nowo przy każdym otwarciu)
    private static readonly System.Collections.Generic.List<Item> Stock = new();
    private static int _stockLevel = -1;
    private static readonly System.Collections.Generic.List<Item> Buyback = new();

    public static void Toggle(SceneTree tree)
    {
        if (tree.Root.GetNodeOrNull<VendorPanel>("VendorPanel") is { } existing) { existing.QueueFree(); return; }
        var p = new VendorPanel { Name = "VendorPanel", Layer = 7 };
        tree.Root.AddChild(p);
    }

    public override void _Ready()
    {
        _root = UiKit.Window(this, "VENDOR    [E/Esc] close");
        var vb = _root.GetNode<VBoxContainer>("VB");
        _gold = new Label();
        vb.AddChild(_gold);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 8);
        foreach (var (label, mode) in new[] { ("Sell", "sell"), ("Buy", "buy"), ("Buyback", "buyback") })
        {
            var t = new Button { Text = label, CustomMinimumSize = new Vector2(120, 0) };
            string captured = mode;
            t.Pressed += () => { _mode = captured; Refresh(); };
            tabs.AddChild(t);
        }
        vb.AddChild(tabs);

        _bulk = new HBoxContainer();
        _bulk.AddThemeConstantOverride("separation", 8);
        var sellJunk = new Button { Text = "Sell all Normal + Magic" };
        sellJunk.Pressed += () => SellWhere(i => i.Rarity <= Rarity.Magic);
        var sellRare = new Button { Text = "Sell all up to Rare" };
        sellRare.Pressed += () => SellWhere(i => i.Rarity <= Rarity.Rare);
        _bulk.AddChild(sellJunk);
        _bulk.AddChild(sellRare);
        vb.AddChild(_bulk);

        var scroll = UiKit.VScroll();
        vb.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_list);
        Refresh();
    }

    private static long BuyPrice(Item i) => System.Math.Max(10, Vendor.SellPrice(i) * 5);

    /// <summary>Asortyment na poziom gracza (przeroluje się po wbiciu poziomu).</summary>
    private static void EnsureStock()
    {
        int lvl = GameState.Progress.Level;
        if (_stockLevel == lvl && Stock.Count > 0) return;
        RollStock(lvl);
    }

    private static void RollStock(int lvl)
    {
        _stockLevel = lvl;
        Stock.Clear();
        var g = new LootGenerator();
        for (int i = 0; i < 3; i++) Stock.Add(g.Generate(Rarity.Normal, lvl));
        for (int i = 0; i < 4; i++) Stock.Add(g.Generate(Rarity.Magic, lvl));
        Stock.Add(g.Generate(Rarity.Rare, lvl));
    }

    /// <summary>Każda sprzedaż trafia do odkupu (ochrona przed pomyłką) — ostatnie 12 sztuk.</summary>
    private static void NoteSold(Item item)
    {
        Buyback.Add(item);
        if (Buyback.Count > 12) Buyback.RemoveAt(0);
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
            NoteSold(item);
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
        _bulk.Visible = _mode == "sell";
        foreach (Node c in _list.GetChildren()) c.QueueFree();
        switch (_mode)
        {
            case "buy": RefreshBuy(); break;
            case "buyback": RefreshBuyback(); break;
            default: RefreshSell(); break;
        }
    }

    private void RefreshSell()
    {
        _list.AddChild(new Label { Text = "Click an item to sell it:" });
        foreach (var placed in GameState.Bag.Placed)
        {
            var item = placed.Item;
            long price = Vendor.SellPrice(item);
            var b = new Button { Text = $"{item.Name}  [{item.Rarity}]  —  sell for {price} gold", TooltipText = CharacterPanel.Describe(item) };
            b.Modulate = ItemPickup.RarityColor(item.Rarity);
            b.Pressed += () =>
            {
                GameState.Bag.Remove(item);
                GameState.Wallet.Gold += price;
                NoteSold(item);
                GameState.Save();
                Refresh();
            };
            _list.AddChild(b);
        }
    }

    private void RefreshBuy()
    {
        EnsureStock();
        _list.AddChild(new Label { Text = $"Wares for level {_stockLevel}:" });
        foreach (var item in new System.Collections.Generic.List<Item>(Stock))
        {
            long price = BuyPrice(item);
            var b = new Button
            {
                Text = $"{item.Name}  [{item.Rarity}]  —  buy for {price} gold",
                TooltipText = CharacterPanel.Describe(item),
                Disabled = GameState.Wallet.Gold < price,
            };
            b.Modulate = ItemPickup.RarityColor(item.Rarity);
            b.Pressed += () =>
            {
                if (GameState.Wallet.Gold < price) return;
                if (!GameState.Bag.TryAutoPlace(item)) { Net.SendChatLocal("Bag is full."); return; }
                GameState.Wallet.Gold -= price;
                Stock.Remove(item);
                GameState.Save();
                Refresh();
            };
            _list.AddChild(b);
        }

        var restock = new Button { Text = "New stock — 25 gold", Disabled = GameState.Wallet.Gold < 25 };
        restock.Pressed += () =>
        {
            if (GameState.Wallet.Gold < 25) return;
            GameState.Wallet.Gold -= 25;
            RollStock(GameState.Progress.Level);
            GameState.Save();
            Refresh();
        };
        _list.AddChild(restock);
    }

    private void RefreshBuyback()
    {
        _list.AddChild(new Label { Text = "Recently sold (this session) — buy back at the price you got:" });
        if (Buyback.Count == 0) _list.AddChild(new Label { Text = "  Nothing sold yet." });
        foreach (var item in new System.Collections.Generic.List<Item>(Buyback))
        {
            long price = Vendor.SellPrice(item);
            var b = new Button
            {
                Text = $"{item.Name}  [{item.Rarity}]  —  buy back for {price} gold",
                TooltipText = CharacterPanel.Describe(item),
                Disabled = GameState.Wallet.Gold < price,
            };
            b.Modulate = ItemPickup.RarityColor(item.Rarity);
            b.Pressed += () =>
            {
                if (GameState.Wallet.Gold < price) return;
                if (!GameState.Bag.TryAutoPlace(item)) { Net.SendChatLocal("Bag is full."); return; }
                GameState.Wallet.Gold -= price;
                Buyback.Remove(item);
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
        var scroll = UiKit.VScroll();
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
    /// <summary>Okno PEŁNOEKRANOWE (nad dolnym paskiem) — GUI ma być w 100% widoczne, bez suwaków poziomych.</summary>
    public static Panel Window(CanvasLayer layer, string title)
    {
        UiPanels.CloseAllExcept(layer.GetTree(), null); // zamknij panele C/I/K pod spodem
        var root = new Panel
        {
            AnchorLeft = 0f, AnchorTop = 0f, AnchorRight = 1f, AnchorBottom = 1f,
            OffsetLeft = 40, OffsetTop = 36, OffsetRight = -40, OffsetBottom = -170,
        };
        UiPanels.Solidify(root);
        layer.AddChild(root);
        var vb = new VBoxContainer { Name = "VB", AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = 20, OffsetTop = 14, OffsetRight = -20, OffsetBottom = -14 };
        vb.AddThemeConstantOverride("separation", 10);
        root.AddChild(vb);
        vb.AddChild(new Label { Text = title });
        return root;
    }

    /// <summary>ScrollContainer tylko pionowy (poziome suwaki zakazane w GUI).</summary>
    public static ScrollContainer VScroll() => new()
    {
        SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
    };
}

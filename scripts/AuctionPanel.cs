using System.Text.Json;
using Godot;
using AshenPantheon.Core;

/// <summary>Dom aukcyjny — DOSTĘP TYLKO przez blok AH w mieście. Rynek co-op (post/kup/anuluj).
/// Cross-lobby, trwały rynek dojdzie z meta-serwerem.</summary>
public partial class AuctionPanel : CanvasLayer
{
    private Panel _root;
    private VBoxContainer _listings, _bag;
    private Label _gold;

    public static void Toggle(SceneTree tree)
    {
        if (tree.Root.GetNodeOrNull<AuctionPanel>("AuctionPanel") is { } existing) { existing.QueueFree(); return; }
        tree.Root.AddChild(new AuctionPanel { Name = "AuctionPanel", Layer = 7 });
    }

    public override void _Ready()
    {
        _root = UiKit.Window(this, "AUCTION HOUSE  (town only)    [E/Esc] close");
        var vb = _root.GetNode<VBoxContainer>("VB");
        _gold = new Label();
        vb.AddChild(_gold);

        var cols = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 16);
        vb.AddChild(cols);

        _listings = MakeColumn(cols, "LISTINGS");
        _bag = MakeColumn(cols, "YOUR BAG — set price & post");

        Net.MarketChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree() => Net.MarketChanged -= Refresh;

    private static VBoxContainer MakeColumn(HBoxContainer parent, string title)
    {
        var vb = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        vb.AddChild(new Label { Text = title });
        var scroll = UiKit.VScroll();
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(list);
        vb.AddChild(scroll);
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
        if (_gold == null || !IsInstanceValid(_gold)) return;
        _gold.Text = $"Your gold: {GameState.Wallet.Gold}" + (Net.Online ? "" : "    (market is local to this lobby)");

        // oferty
        foreach (Node c in _listings.GetChildren()) c.QueueFree();
        if (Net.Market.Count == 0)
            _listings.AddChild(new Label { Text = "  No active listings." });
        foreach (var l in Net.Market)
        {
            var dto = JsonSerializer.Deserialize<ItemDto>(l.ItemJson);
            var row = new HBoxContainer();
            var name = new Label { Text = $"{dto?.Name} [{dto?.Rarity}]  —  {l.Price}g  ({l.SellerName})", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            if (dto != null) name.Modulate = ItemPickup.RarityColor(System.Enum.Parse<Rarity>(dto.Rarity));
            row.AddChild(name);
            var id = l.Id;
            if (l.Seller == Net.MyId)
            {
                var cancel = new Button { Text = "Cancel" };
                cancel.Pressed += () => Net.MarketCancel(id);
                row.AddChild(cancel);
            }
            else
            {
                var buy = new Button { Text = "Buy", Disabled = GameState.Wallet.Gold < l.Price };
                buy.Pressed += () => Net.MarketBuy(id);
                row.AddChild(buy);
            }
            _listings.AddChild(row);
        }

        // plecak → wystaw
        foreach (Node c in _bag.GetChildren()) c.QueueFree();
        foreach (var placed in GameState.Bag.Placed)
        {
            var item = placed.Item;
            var row = new HBoxContainer();
            var name = new Label { Text = $"{item.Name} [{item.Rarity}]", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            name.Modulate = ItemPickup.RarityColor(item.Rarity);
            row.AddChild(name);
            var price = new SpinBox { MinValue = 1, MaxValue = 9999999, Value = System.Math.Max(1, Vendor.SellPrice(item) * 3), CustomMinimumSize = new Vector2(110, 0) };
            row.AddChild(price);
            var post = new Button { Text = "Post" }; // działa też solo (rynek lokalny hosta)
            post.Pressed += () =>
            {
                GameState.Bag.Remove(item); // escrow
                GameState.Save();
                Net.MarketPost(JsonSerializer.Serialize(ItemMapper.ToDto(item)), (long)price.Value);
                Refresh();
            };
            row.AddChild(post);
            _bag.AddChild(row);
        }
    }
}

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
        if (AccountSession.LoggedIn) { RefreshGlobal(); return; } // online realm = GLOBALNY rynek serwera
        _gold.Text = $"Your gold: {GameState.Wallet.Gold}" + (Net.Online ? "" : "    (market is local to this lobby — log in for the global market)");

        // oferty
        foreach (Node c in _listings.GetChildren()) c.QueueFree();
        if (Net.Market.Count == 0)
            _listings.AddChild(new Label { Text = "  No active listings." });
        foreach (var l in Net.Market)
        {
            var dto = JsonSerializer.Deserialize<ItemDto>(l.ItemJson);
            var row = new HBoxContainer();
            var name = new Label { Text = $"{dto?.Name} [{dto?.Rarity}]  —  {l.Price}g  ({l.SellerName})", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            if (dto != null)
            {
                name.Modulate = ItemPickup.RarityColor(System.Enum.Parse<Rarity>(dto.Rarity));
                row.AddChild(UiIcons.Chip(System.Enum.Parse<ItemKind>(dto.Kind), System.Enum.Parse<Rarity>(dto.Rarity)));
            }
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
            row.AddChild(UiIcons.Chip(item.Kind, item.Rarity));
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

    // ── GLOBALNY rynek (meta-serwer): escrow itemu w ogłoszeniu, wpływy sprzedawcy POCZTĄ ──

    private void RefreshGlobal()
    {
        _gold.Text = $"GLOBAL MARKET (online realm)    Your gold: {GameState.Wallet.Gold}";
        foreach (Node c in _listings.GetChildren()) c.QueueFree();
        foreach (Node c in _bag.GetChildren()) c.QueueFree();

        var refreshBtn = new Button { Text = "↻ Refresh listings" };
        refreshBtn.Pressed += Refresh;
        _listings.AddChild(refreshBtn);

        var json = AccountClient.GetJson("/market");
        if (json == null) { _listings.AddChild(new Label { Text = "  (server unavailable)" }); return; }
        using var doc = JsonDocument.Parse(json);
        long me = doc.RootElement.GetProperty("Me").GetInt64();
        var arr = doc.RootElement.GetProperty("Listings");
        if (arr.GetArrayLength() == 0) _listings.AddChild(new Label { Text = "  No active listings." });
        foreach (var l in arr.EnumerateArray())
        {
            long id = l.GetProperty("Id").GetInt64();
            long sellerId = l.GetProperty("SellerId").GetInt64();
            string seller = l.GetProperty("Seller").GetString() ?? "?";
            long price = l.GetProperty("Price").GetInt64();
            var dto = JsonSerializer.Deserialize<ItemDto>(l.GetProperty("ItemJson").GetString() ?? "{}", JsonGameStateRepository.Options);
            if (dto == null) continue;
            var item = ItemMapper.FromDto(dto);

            var row = new HBoxContainer();
            var name = new Label
            {
                Text = $"{item.Name} [{item.Rarity}]  —  {price}g  ({seller})",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TooltipText = CharacterPanel.Describe(item),
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            name.Modulate = ItemPickup.RarityColor(item.Rarity);
            row.AddChild(UiIcons.Chip(item.Kind, item.Rarity));
            row.AddChild(name);
            if (sellerId == me)
            {
                var cancel = new Button { Text = "Cancel" };
                cancel.Pressed += () => GlobalCancel(id);
                row.AddChild(cancel);
            }
            else
            {
                var buy = new Button { Text = "Buy", Disabled = GameState.Wallet.Gold < price };
                buy.Pressed += () => GlobalBuy(id, price);
                row.AddChild(buy);
            }
            _listings.AddChild(row);
        }

        // plecak → wystaw globalnie
        foreach (var placed in GameState.Bag.Placed)
        {
            var item = placed.Item;
            var row = new HBoxContainer();
            var name = new Label { Text = $"{item.Name} [{item.Rarity}]", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            name.Modulate = ItemPickup.RarityColor(item.Rarity);
            row.AddChild(UiIcons.Chip(item.Kind, item.Rarity));
            row.AddChild(name);
            var price = new SpinBox { MinValue = 1, MaxValue = 99_999_999, Value = System.Math.Max(1, Vendor.SellPrice(item) * 3), CustomMinimumSize = new Vector2(110, 0) };
            row.AddChild(price);
            var post = new Button { Text = "Post" };
            post.Pressed += () => GlobalPost(item, (long)price.Value);
            row.AddChild(post);
            _bag.AddChild(row);
        }
    }

    private void GlobalPost(Item item, long price)
    {
        GameState.Bag.Remove(item); // escrow lokalny — wraca przy błędzie serwera
        var (json, err) = AccountClient.PostJson("/market/list", new
        {
            ItemJson = JsonSerializer.Serialize(ItemMapper.ToDto(item), JsonGameStateRepository.Options),
            Price = price,
        });
        if (json == null)
        {
            GameState.Bag.TryAutoPlace(item);
            Net.SendChatLocal($"AH: {err}");
        }
        else Net.SendChatLocal($"Listed {item.Name} for {price} gold.");
        GameState.Save();
        Refresh();
    }

    private void GlobalBuy(long id, long price)
    {
        if (GameState.Wallet.Gold < price) return;
        var (json, err) = AccountClient.PostJson("/market/buy", new { Id = id });
        if (json == null) { Net.SendChatLocal($"AH: {err}"); Refresh(); return; }
        using var doc = JsonDocument.Parse(json);
        long paid = doc.RootElement.GetProperty("Price").GetInt64();
        var dto = JsonSerializer.Deserialize<ItemDto>(doc.RootElement.GetProperty("ItemJson").GetString() ?? "{}", JsonGameStateRepository.Options);
        GameState.Wallet.Gold -= paid;
        if (dto != null) GiveOrDrop(ItemMapper.FromDto(dto));
        GameState.Save();
        Refresh();
    }

    private void GlobalCancel(long id)
    {
        var (json, err) = AccountClient.PostJson("/market/cancel", new { Id = id });
        if (json == null) { Net.SendChatLocal($"AH: {err}"); Refresh(); return; }
        using var doc = JsonDocument.Parse(json);
        var dto = JsonSerializer.Deserialize<ItemDto>(doc.RootElement.GetProperty("ItemJson").GetString() ?? "{}", JsonGameStateRepository.Options);
        if (dto != null) GiveOrDrop(ItemMapper.FromDto(dto));
        GameState.Save();
        Refresh();
    }

    private void GiveOrDrop(Item item)
    {
        if (GameState.Bag.TryAutoPlace(item))
        {
            Net.SendChatLocal($"Received: {item.Name} [{item.Rarity}]");
        }
        else if (PlayerController.Local is { } pl)
        {
            ItemPickup.Spawn(pl.GetParent(), pl.GlobalPosition, item);
            Net.SendChatLocal($"Bag full — {item.Name} dropped at your feet.");
        }
    }
}

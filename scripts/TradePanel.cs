using System.Collections.Generic;
using System.Text.Json;
using Godot;
using AshenPantheon.Core;

/// <summary>Handel gracz-gracz (P2P). T przy innym graczu = żądanie. Escrow lokalny (itemy/złoto wychodzą
/// z plecaka do oferty), dwustronny Confirm, atomowa wymiana po obu potwierdzeniach.</summary>
public partial class TradePanel : CanvasLayer
{
    private long _partner = -1;
    private long _pendingTo = -1;

    private readonly List<Item> _myOffer = new();
    private long _myGold;
    private readonly List<ItemDto> _theirItems = new();
    private long _theirGold;
    private bool _myConfirm, _theirConfirm;
    private bool _done;

    private Panel _root;
    private VBoxContainer _mine, _theirs, _bag;
    private Label _status;
    private SpinBox _goldSpin;
    private Button _confirmBtn;

    public override void _Ready()
    {
        Layer = 12;
        AddToGroup("trade");
        // WYŁĄCZNIE nazwane handlery — lambdy na statycznych eventach wyciekłyby między scenami
        Net.TradeRequested += OnRequested;
        Net.TradeAccepted += OnAccepted;
        Net.TradeDeclined += OnDeclined;
        Net.TradeOffer += OnPartnerOffer;
        Net.TradePartnerConfirm += OnPartnerConfirm;
        Net.TradeCancelled += OnCancelled;
        Net.SessionChanged += OnSession;
        BuildWindow();
    }

    public override void _ExitTree()
    {
        Net.TradeRequested -= OnRequested;
        Net.TradeAccepted -= OnAccepted;
        Net.TradeDeclined -= OnDeclined;
        Net.TradeOffer -= OnPartnerOffer;
        Net.TradePartnerConfirm -= OnPartnerConfirm;
        Net.TradeCancelled -= OnCancelled;
        Net.SessionChanged -= OnSession;
        if (_partner >= 0) { ReturnEscrow(); Reset(); } // nie gub itemów przy zmianie sceny
    }

    private void OnDeclined(long partner) => Toast("Trade declined.");
    private void OnCancelled() => CloseTrade(theyCancelled: true);
    private void OnSession() { if (_partner >= 0) { ReturnEscrow(); Reset(); } }

    // ── inicjacja (z menu kontekstowego PPM na nicku gracza) ──

    /// <summary>Rozpocznij handel z graczem z tego samego lobby (PPM na nicku → Trade).</summary>
    public void RequestTradeWith(long partner)
    {
        if (!Net.Online || partner == Net.MyId) { Toast("Trading needs another player in your lobby."); return; }
        if (_partner >= 0) { Toast("You're already in a trade."); return; }
        _pendingTo = partner;
        Net.TradeRequest(partner);
        Toast($"Trade request sent to {Net.NameOf(partner)}…");
    }

    private void OnRequested(long from)
    {
        if (_partner >= 0) { Net.TradeDecline(from); return; } // zajęty
        ShowAcceptPrompt(from);
    }

    private void OnAccepted(long partner)
    {
        if (_pendingTo == partner) OpenTrade(partner);
    }

    // ── okno ──

    private void OpenTrade(long partner)
    {
        _partner = partner;
        _pendingTo = -1;
        _myOffer.Clear(); _theirItems.Clear();
        _myGold = 0; _theirGold = 0;
        _myConfirm = _theirConfirm = false; _done = false;
        _root.Visible = true;
        Rebuild();
    }

    private void BuildWindow()
    {
        _root = new Panel
        {
            AnchorLeft = 0f, AnchorTop = 0f, AnchorRight = 1f, AnchorBottom = 1f,
            OffsetLeft = 40, OffsetTop = 36, OffsetRight = -40, OffsetBottom = -170,
            Visible = false,
        };
        UiPanels.Solidify(_root);
        AddChild(_root);

        var vb = new VBoxContainer { AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = 14, OffsetTop = 12, OffsetRight = -14, OffsetBottom = -12 };
        vb.AddThemeConstantOverride("separation", 8);
        _root.AddChild(vb);
        _status = new Label { Text = "TRADE" };
        vb.AddChild(_status);

        var cols = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 16);
        vb.AddChild(cols);

        _mine = Column(cols, "YOUR OFFER");
        _theirs = Column(cols, "THEIR OFFER");

        var goldRow = new HBoxContainer();
        goldRow.AddChild(new Label { Text = "Your gold offer:" });
        _goldSpin = new SpinBox { MinValue = 0, MaxValue = 0, Step = 1 };
        _goldSpin.ValueChanged += v => SetGold((long)v);
        goldRow.AddChild(_goldSpin);
        vb.AddChild(goldRow);

        vb.AddChild(new Label { Text = "Your bag — click to add:" });
        var bagScroll = UiKit.VScroll();
        _bag = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        bagScroll.AddChild(_bag);
        vb.AddChild(bagScroll);

        var btns = new HBoxContainer();
        btns.AddThemeConstantOverride("separation", 10);
        _confirmBtn = new Button { Text = "Confirm" };
        _confirmBtn.Pressed += ToggleMyConfirm;
        var cancel = new Button { Text = "Cancel" };
        cancel.Pressed += () => CloseTrade(theyCancelled: false);
        btns.AddChild(_confirmBtn);
        btns.AddChild(cancel);
        vb.AddChild(btns);
    }

    private static VBoxContainer Column(HBoxContainer parent, string title)
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

    private void Rebuild()
    {
        if (_partner < 0) return;
        _status.Text = $"TRADE with {Net.NameOf(_partner)}    " +
                       $"[You: {(_myConfirm ? "✔ confirmed" : "not confirmed")}]   " +
                       $"[Them: {(_theirConfirm ? "✔ confirmed" : "waiting")}]";
        _confirmBtn.Text = _myConfirm ? "Un-confirm" : "Confirm";

        // moja oferta
        foreach (Node c in _mine.GetChildren()) c.QueueFree();
        foreach (var item in _myOffer)
        {
            var it = item;
            var b = new Button { Text = $"{it.Name} [{it.Rarity}]", TooltipText = "click to return to bag" };
            UiIcons.DecorateItemButton(b, it.Kind, it.Rarity);
            b.Pressed += () => RemoveFromOffer(it);
            _mine.AddChild(b);
        }
        if (_myGold > 0) _mine.AddChild(new Label { Text = $"+ {_myGold} gold" });

        // ich oferta
        foreach (Node c in _theirs.GetChildren()) c.QueueFree();
        foreach (var it in _theirItems)
        {
            var rr = System.Enum.Parse<Rarity>(it.Rarity);
            var row = new HBoxContainer();
            row.AddChild(UiIcons.Chip(System.Enum.Parse<ItemKind>(it.Kind), rr));
            var lbl = new Label { Text = $"{it.Name} [{it.Rarity}]", VerticalAlignment = VerticalAlignment.Center };
            lbl.Modulate = ItemPickup.RarityColor(rr);
            row.AddChild(lbl);
            _theirs.AddChild(row);
        }
        if (_theirGold > 0) _theirs.AddChild(new Label { Text = $"+ {_theirGold} gold" });

        // plecak (co mogę dodać)
        foreach (Node c in _bag.GetChildren()) c.QueueFree();
        foreach (var placed in GameState.Bag.Placed)
        {
            var it = placed.Item;
            var b = new Button { Text = $"{it.Name} [{it.Rarity}]" };
            UiIcons.DecorateItemButton(b, it.Kind, it.Rarity);
            b.Pressed += () => AddToOffer(it);
            _bag.AddChild(b);
        }

        _goldSpin.MaxValue = GameState.Wallet.Gold + _myGold;
        _goldSpin.SetValueNoSignal(_myGold);
    }

    // ── zmiany oferty (escrow) ──

    private void AddToOffer(Item item)
    {
        GameState.Bag.Remove(item);
        _myOffer.Add(item);
        OnOfferChanged();
    }

    private void RemoveFromOffer(Item item)
    {
        _myOffer.Remove(item);
        if (!GameState.Bag.TryAutoPlace(item)) _myOffer.Add(item); // brak miejsca → zostaw w ofercie
        OnOfferChanged();
    }

    private void SetGold(long g)
    {
        g = System.Math.Clamp(g, 0, GameState.Wallet.Gold + _myGold);
        long delta = g - _myGold;
        GameState.Wallet.Gold -= delta;
        _myGold = g;
        OnOfferChanged();
    }

    /// <summary>Każda zmiana oferty kasuje OBA potwierdzenia (anty-scam) i wysyła nową ofertę.</summary>
    private void OnOfferChanged()
    {
        _myConfirm = false; _theirConfirm = false;
        Net.TradeSendOffer(_partner, SerializeOffer(), _myGold);
        Net.TradeSendConfirm(_partner, false);
        Rebuild();
    }

    private void OnPartnerOffer(string itemsJson, long gold)
    {
        if (_partner < 0) return;
        _theirItems.Clear();
        var dtos = JsonSerializer.Deserialize<List<ItemDto>>(itemsJson) ?? new();
        _theirItems.AddRange(dtos);
        _theirGold = gold;
        _myConfirm = false; _theirConfirm = false; // zmiana ich oferty = re-confirm
        Rebuild();
    }

    private void ToggleMyConfirm()
    {
        if (_partner < 0) return;
        _myConfirm = !_myConfirm;
        Net.TradeSendConfirm(_partner, _myConfirm);
        Rebuild();
        if (_myConfirm && _theirConfirm) Execute();
    }

    private void OnPartnerConfirm(bool on)
    {
        if (_partner < 0) return;
        _theirConfirm = on;
        Rebuild();
        if (_myConfirm && _theirConfirm) Execute();
    }

    // ── finalizacja ──

    private void Execute()
    {
        if (_done || _partner < 0) return;
        _done = true;

        // oddaję escrow (itemy już poza plecakiem, złoto już odjęte); przyjmuję ich ofertę
        foreach (var dto in _theirItems)
        {
            var item = ItemMapper.FromDto(dto);
            if (!GameState.Bag.TryAutoPlace(item) && GetTree().CurrentScene != null && PlayerController.Local != null)
                ItemPickup.Spawn(GetTree().CurrentScene, PlayerController.Local.GlobalPosition, item); // brak miejsca → na ziemię
        }
        GameState.Wallet.Gold += _theirGold;
        GameState.Save();
        PlayerController.Local?.Refresh();
        Toast($"Trade complete with {Net.NameOf(_partner)}.");
        Reset();
    }

    private void CloseTrade(bool theyCancelled)
    {
        if (_partner < 0) return;
        if (!theyCancelled) Net.TradeSendCancel(_partner);
        ReturnEscrow();
        Toast(theyCancelled ? "Partner cancelled the trade." : "Trade cancelled.");
        Reset();
    }

    private void ReturnEscrow()
    {
        foreach (var item in _myOffer)
            if (!GameState.Bag.TryAutoPlace(item) && GetTree().CurrentScene != null && PlayerController.Local != null)
                ItemPickup.Spawn(GetTree().CurrentScene, PlayerController.Local.GlobalPosition, item);
        GameState.Wallet.Gold += _myGold;
        GameState.Save();
    }

    private void Reset()
    {
        _partner = -1; _pendingTo = -1;
        _myOffer.Clear(); _theirItems.Clear();
        _myGold = 0; _theirGold = 0; _myConfirm = _theirConfirm = false;
        _root.Visible = false;
    }

    private string SerializeOffer()
    {
        var dtos = new List<ItemDto>();
        foreach (var it in _myOffer) dtos.Add(ItemMapper.ToDto(it));
        return JsonSerializer.Serialize(dtos);
    }

    // ── prompty ──

    private void ShowAcceptPrompt(long from)
    {
        var layer = new CanvasLayer { Name = "TradePrompt", Layer = 13 };
        var panel = new Panel { AnchorLeft = 0.5f, AnchorTop = 0.5f, AnchorRight = 0.5f, AnchorBottom = 0.5f, OffsetLeft = -180, OffsetTop = -70, OffsetRight = 180, OffsetBottom = 70 };
        UiPanels.Solidify(panel);
        layer.AddChild(panel);
        var vb = new VBoxContainer { AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = 14, OffsetTop = 12, OffsetRight = -14, OffsetBottom = -12 };
        panel.AddChild(vb);
        vb.AddChild(new Label { Text = $"{Net.NameOf(from)} wants to trade.", HorizontalAlignment = HorizontalAlignment.Center });
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        var acc = new Button { Text = "Accept" };
        acc.Pressed += () => { layer.QueueFree(); Net.TradeAccept(from); OpenTrade(from); };
        var dec = new Button { Text = "Decline" };
        dec.Pressed += () => { layer.QueueFree(); Net.TradeDecline(from); };
        row.AddChild(acc); row.AddChild(dec);
        vb.AddChild(row);
        AddChild(layer);
    }

    private void Toast(string msg) => Net.SendChatLocal(msg);
}

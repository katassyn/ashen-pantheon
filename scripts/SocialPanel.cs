using System.Text.Json;
using Godot;
using AshenPantheon.Core;

/// <summary>Panel społecznościowy (O): znajomi (z presence) + guildia + POCZTA z meta-serwera.
/// Tylko online (po zalogowaniu).</summary>
public partial class SocialPanel : CanvasLayer
{
    private Panel _root;
    private VBoxContainer _friends, _guild, _mail;
    private LineEdit _friendName, _guildInput, _mailTo, _mailBody;
    private SpinBox _mailGold;
    private OptionButton _mailItem;
    private readonly System.Collections.Generic.List<Item> _composeItems = new();
    private Label _status;

    public override void _Ready()
    {
        Layer = 9;
        _root = new Panel
        {
            AnchorLeft = 0f, AnchorTop = 0f, AnchorRight = 1f, AnchorBottom = 1f,
            OffsetLeft = 40, OffsetTop = 36, OffsetRight = -40, OffsetBottom = -170,
            Visible = false,
        };
        UiPanels.Solidify(_root);
        AddChild(_root);

        var vb = new VBoxContainer { AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = 16, OffsetTop = 12, OffsetRight = -16, OffsetBottom = -12 };
        vb.AddThemeConstantOverride("separation", 8);
        _root.AddChild(vb);
        vb.AddChild(new Label { Text = "FRIENDS · GUILD · MAIL    [O] close" });
        _status = new Label { Modulate = new Color(0.9f, 0.8f, 0.5f) };
        vb.AddChild(_status);

        var cols = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 18);
        vb.AddChild(cols);

        // ── znajomi ──
        var fcol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        fcol.AddChild(ColHeader("people", "FRIENDS", new Color(0.6f, 0.9f, 1f)));
        var addRow = new HBoxContainer();
        _friendName = new LineEdit { PlaceholderText = "username", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MaxLength = 24 };
        var addBtn = new Button { Text = "Add" };
        addBtn.Pressed += () => Do(AccountClient.Post("/friends/request", new { Username = _friendName.Text.StripEdges() }));
        addRow.AddChild(_friendName); addRow.AddChild(addBtn);
        fcol.AddChild(addRow);
        var fscroll = UiKit.VScroll();
        _friends = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        fscroll.AddChild(_friends);
        fcol.AddChild(fscroll);
        cols.AddChild(fcol);

        // ── guildia ──
        var gcol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        gcol.AddChild(ColHeader("banner", "GUILD", new Color(1f, 0.85f, 0.5f)));
        var gRow = new HBoxContainer();
        _guildInput = new LineEdit { PlaceholderText = "guild name / invite username", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MaxLength = 24 };
        gRow.AddChild(_guildInput);
        gcol.AddChild(gRow);
        var gscroll = UiKit.VScroll();
        _guild = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        gscroll.AddChild(_guild);
        gcol.AddChild(gscroll);
        cols.AddChild(gcol);

        // ── poczta ──
        var mcol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        mcol.AddChild(ColHeader("mail", "MAIL", new Color(1f, 0.9f, 0.6f)));
        _mailTo = new LineEdit { PlaceholderText = "to (username)", MaxLength = 24 };
        mcol.AddChild(_mailTo);
        _mailBody = new LineEdit { PlaceholderText = "message", MaxLength = 300 };
        mcol.AddChild(_mailBody);
        var attachRow = new HBoxContainer();
        attachRow.AddThemeConstantOverride("separation", 6);
        _mailGold = new SpinBox { MinValue = 0, MaxValue = 99_999_999, Step = 1, CustomMinimumSize = new Vector2(110, 0), TooltipText = "gold attachment" };
        attachRow.AddChild(_mailGold);
        _mailItem = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, TooltipText = "item attachment (from bag)" };
        attachRow.AddChild(_mailItem);
        var sendBtn = new Button { Text = "Send" };
        sendBtn.Pressed += SendMail;
        attachRow.AddChild(sendBtn);
        mcol.AddChild(attachRow);
        var mscroll = UiKit.VScroll();
        _mail = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _mail.AddThemeConstantOverride("separation", 8);
        mscroll.AddChild(_mail);
        mcol.AddChild(mscroll);
        cols.AddChild(mcol);
    }

    /// <summary>Wysyłka z ESCROW lokalnym: złoto/item schodzą przed requestem, wracają przy błędzie.</summary>
    private void SendMail()
    {
        string to = _mailTo.Text.StripEdges();
        if (to.Length < 3) { _status.Text = "Recipient name min 3 chars."; return; }
        long gold = (long)_mailGold.Value;
        if (gold > GameState.Wallet.Gold) { _status.Text = "Not enough gold."; return; }
        int idx = _mailItem.Selected - 1; // 0 = "no item"
        Item attach = idx >= 0 && idx < _composeItems.Count ? _composeItems[idx] : null;

        if (attach != null) GameState.Bag.Remove(attach);
        GameState.Wallet.Gold -= gold;

        var (ok, err) = AccountClient.Post("/mail/send", new
        {
            To = to,
            Body = _mailBody.Text,
            Gold = gold,
            ItemJson = attach == null ? null : JsonSerializer.Serialize(ItemMapper.ToDto(attach), JsonGameStateRepository.Options),
        });
        if (!ok)
        {
            GameState.Wallet.Gold += gold;
            if (attach != null) GameState.Bag.TryAutoPlace(attach);
            _status.Text = err;
        }
        else
        {
            _status.Text = $"Mail sent to {to}.";
            _mailBody.Text = "";
            _mailGold.Value = 0;
            GameState.Save();
        }
        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey k || !k.Pressed || k.Echo) return;
        if (Keybinds.Matches(k, "social"))
        {
            if (_root.Visible) _root.Visible = false;
            else { _root.Visible = true; Refresh(); }
            GetViewport().SetInputAsHandled();
        }
        else if (k.PhysicalKeycode == Key.Escape && _root.Visible)
        {
            _root.Visible = false;
            GetViewport().SetInputAsHandled();
        }
    }

    private void Do((bool Ok, string Error) r)
    {
        _status.Text = r.Ok ? "Done." : r.Error;
        Refresh();
    }

    /// <summary>Nagłówek kolumny: glif + tytuł w kolorze.</summary>
    private static HBoxContainer ColHeader(string glyph, string text, Color col)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 8);
        h.AddChild(new GlyphIcon { Kind = glyph, IconColor = col, CustomMinimumSize = new Vector2(22, 22), MouseFilter = Control.MouseFilterEnum.Ignore });
        var l = new Label { Text = text, Modulate = col };
        l.AddThemeFontSizeOverride("font_size", 16);
        h.AddChild(l);
        return h;
    }

    private void Refresh()
    {
        foreach (Node c in _friends.GetChildren()) c.QueueFree();
        foreach (Node c in _guild.GetChildren()) c.QueueFree();
        foreach (Node c in _mail.GetChildren()) c.QueueFree();

        if (!AccountSession.LoggedIn)
        {
            _friends.AddChild(new Label { Text = "Log in to the online realm to use\nfriends, guilds and mail.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
            return;
        }

        RefreshFriends();
        RefreshGuild();
        RefreshMail();
    }

    private void RefreshMail()
    {
        // item do załącznika — z plecaka (escrow przy wysyłce)
        _composeItems.Clear();
        _mailItem.Clear();
        _mailItem.AddItem("no item attachment");
        foreach (var placed in GameState.Bag.Placed)
        {
            _composeItems.Add(placed.Item);
            _mailItem.AddItem($"{placed.Item.Name} [{placed.Item.Rarity}]");
        }
        _mailGold.MaxValue = GameState.Wallet.Gold;

        var json = AccountClient.GetJson("/mail");
        if (json == null) { _mail.AddChild(new Label { Text = "(server unavailable)" }); return; }
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("Mail");
        if (arr.GetArrayLength() == 0) _mail.AddChild(new Label { Text = "  Inbox empty." });
        foreach (var m in arr.EnumerateArray())
        {
            long id = m.GetProperty("Id").GetInt64();
            string from = m.GetProperty("From").GetString() ?? "?";
            string body = m.GetProperty("Body").GetString() ?? "";
            long gold = m.GetProperty("Gold").GetInt64();
            bool hasItem = m.GetProperty("HasItem").GetBoolean();
            bool claimed = m.GetProperty("Claimed").GetBoolean();

            var box = new VBoxContainer();
            string attach = (gold > 0 ? $"  [+{gold}g]" : "") + (hasItem ? "  [+item]" : "");
            var headRow = new HBoxContainer();
            headRow.AddThemeConstantOverride("separation", 6);
            var envCol = claimed ? new Color(0.6f, 0.6f, 0.65f) : new Color(1f, 0.9f, 0.6f);
            headRow.AddChild(new GlyphIcon { Kind = "mail", IconColor = envCol, CustomMinimumSize = new Vector2(20, 20), MouseFilter = Control.MouseFilterEnum.Ignore });
            var head = new Label { Text = $"{from}{attach}" + (claimed ? "   (claimed)" : ""), Modulate = envCol };
            headRow.AddChild(head);
            box.AddChild(headRow);
            if (body.Length > 0)
            {
                var b = new Label { Text = body, AutowrapMode = TextServer.AutowrapMode.WordSmart };
                b.Modulate = new Color(0.8f, 0.8f, 0.85f);
                box.AddChild(b);
            }

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            if (!claimed && (gold > 0 || hasItem))
            {
                var claim = new Button { Text = "Claim" };
                long cid = id;
                claim.Pressed += () => ClaimMail(cid);
                row.AddChild(claim);
            }
            var del = new Button { Text = "Delete" };
            long did = id;
            del.Pressed += () => Do(AccountClient.Post("/mail/delete", new { Id = did }));
            row.AddChild(del);
            box.AddChild(row);
            _mail.AddChild(box);
        }
    }

    private void ClaimMail(long id)
    {
        var (json, err) = AccountClient.PostJson("/mail/claim", new { Id = id });
        if (json == null) { _status.Text = err; return; }
        using var doc = JsonDocument.Parse(json);
        long gold = doc.RootElement.GetProperty("Gold").GetInt64();
        GameState.Wallet.Gold += gold;
        if (doc.RootElement.TryGetProperty("ItemJson", out var ij) && ij.ValueKind == JsonValueKind.String)
        {
            var dto = JsonSerializer.Deserialize<ItemDto>(ij.GetString()!, JsonGameStateRepository.Options);
            if (dto != null)
            {
                var item = ItemMapper.FromDto(dto);
                if (!GameState.Bag.TryAutoPlace(item) && PlayerController.Local is { } pl)
                {
                    ItemPickup.Spawn(pl.GetParent(), pl.GlobalPosition, item);
                    Net.SendChatLocal("Bag full — mail item dropped at your feet.");
                }
            }
        }
        GameState.Save();
        _status.Text = gold > 0 ? $"Claimed +{gold} gold." : "Claimed.";
        Refresh();
    }

    private void RefreshFriends()
    {
        var json = AccountClient.GetJson("/friends");
        if (json == null) { _friends.AddChild(new Label { Text = "(server unavailable)" }); return; }
        using var doc = JsonDocument.Parse(json);
        var reqs = doc.RootElement.GetProperty("Requests");
        if (reqs.GetArrayLength() > 0) _friends.AddChild(new Label { Text = "Requests:", Modulate = new Color(1f, 0.9f, 0.5f) });
        foreach (var r in reqs.EnumerateArray())
        {
            string name = r.GetString()!;
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            var acc = new Button { Text = "Accept" };
            acc.Pressed += () => Do(AccountClient.Post("/friends/accept", new { Username = name }));
            var dec = new Button { Text = "✕" };
            dec.Pressed += () => Do(AccountClient.Post("/friends/remove", new { Username = name }));
            row.AddChild(acc); row.AddChild(dec);
            _friends.AddChild(row);
        }

        _friends.AddChild(new Label { Text = "Friends:", Modulate = new Color(0.6f, 0.9f, 1f) });
        var list = doc.RootElement.GetProperty("Friends");
        if (list.GetArrayLength() == 0) _friends.AddChild(new Label { Text = "  none yet" });
        foreach (var f in list.EnumerateArray())
        {
            string name = f.GetProperty("Name").GetString()!;
            bool online = f.GetProperty("Online").GetBoolean();
            var row = new HBoxContainer();
            row.AddChild(new StatusDot { Online = online, CustomMinimumSize = new Vector2(18, 18), MouseFilter = Control.MouseFilterEnum.Ignore });
            var lbl = new Label { Text = name + (online ? "" : "   (offline)"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            lbl.Modulate = online ? new Color(0.45f, 0.9f, 0.45f) : new Color(0.55f, 0.55f, 0.6f);
            row.AddChild(lbl);
            var mailBtn = new Button { Text = "Mail", TooltipText = "prefill recipient" };
            mailBtn.Pressed += () => _mailTo.Text = name;
            row.AddChild(mailBtn);
            var rem = new Button { Text = "Remove" };
            rem.Pressed += () => Do(AccountClient.Post("/friends/remove", new { Username = name }));
            row.AddChild(rem);
            _friends.AddChild(row);
        }
    }

    private void RefreshGuild()
    {
        var json = AccountClient.GetJson("/guild");
        if (json == null) { _guild.AddChild(new Label { Text = "(server unavailable)" }); return; }
        using var doc = JsonDocument.Parse(json);
        var guild = doc.RootElement.GetProperty("Guild");

        // zaproszenia
        var invites = doc.RootElement.GetProperty("Invites");
        foreach (var inv in invites.EnumerateArray())
        {
            long id = inv.GetProperty("Id").GetInt64();
            string name = inv.GetProperty("Name").GetString()!;
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = $"Invite: {name}", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
            var acc = new Button { Text = "Join" };
            acc.Pressed += () => Do(AccountClient.Post("/guild/accept", new { GuildId = id }));
            row.AddChild(acc);
            _guild.AddChild(row);
        }

        if (guild.ValueKind == JsonValueKind.Null)
        {
            var create = new Button { Text = "Create guild (uses name field)" };
            create.Pressed += () => Do(AccountClient.Post("/guild/create", new { Username = _guildInput.Text.StripEdges() }));
            _guild.AddChild(create);
            return;
        }

        bool isLeader = guild.GetProperty("IsLeader").GetBoolean();
        _guild.AddChild(new Label { Text = $"« {guild.GetProperty("Name").GetString()} »" + (isLeader ? "  (leader)" : ""), Modulate = new Color(1f, 0.85f, 0.5f) });
        if (isLeader)
        {
            var inviteBtn = new Button { Text = "Invite player (uses name field)" };
            inviteBtn.Pressed += () => Do(AccountClient.Post("/guild/invite", new { Username = _guildInput.Text.StripEdges() }));
            _guild.AddChild(inviteBtn);
        }
        foreach (var m in guild.GetProperty("Members").EnumerateArray())
        {
            bool leader = m.GetProperty("Leader").GetBoolean();
            var mrow = new HBoxContainer();
            mrow.AddThemeConstantOverride("separation", 6);
            mrow.AddChild(new GlyphIcon
            {
                Kind = leader ? "crown" : "people",
                IconColor = leader ? new Color(0.95f, 0.8f, 0.35f) : new Color(0.6f, 0.7f, 0.85f),
                CustomMinimumSize = new Vector2(18, 18), MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            mrow.AddChild(new Label { Text = m.GetProperty("Name").GetString() });
            _guild.AddChild(mrow);
        }

        var leave = new Button { Text = isLeader ? "Disband guild (leave as leader)" : "Leave guild" };
        leave.Pressed += () => Do(AccountClient.Post("/guild/leave", new { }));
        _guild.AddChild(leave);
    }
}

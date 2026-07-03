using System.Text.Json;
using Godot;

/// <summary>Panel społecznościowy (O): znajomi + guildia z meta-serwera. Tylko online (po zalogowaniu).</summary>
public partial class SocialPanel : CanvasLayer
{
    private Panel _root;
    private VBoxContainer _friends, _guild;
    private LineEdit _friendName, _guildInput;
    private Label _status;

    public override void _Ready()
    {
        Layer = 9;
        _root = new Panel
        {
            AnchorLeft = 0.5f, AnchorTop = 0.5f, AnchorRight = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -360, OffsetTop = -320, OffsetRight = 360, OffsetBottom = 320,
            Visible = false,
        };
        UiPanels.Solidify(_root);
        AddChild(_root);

        var vb = new VBoxContainer { AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = 16, OffsetTop = 12, OffsetRight = -16, OffsetBottom = -12 };
        vb.AddThemeConstantOverride("separation", 8);
        _root.AddChild(vb);
        vb.AddChild(new Label { Text = "FRIENDS & GUILD    [O] close" });
        _status = new Label { Modulate = new Color(0.9f, 0.8f, 0.5f) };
        vb.AddChild(_status);

        var cols = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 18);
        vb.AddChild(cols);

        // ── znajomi ──
        var fcol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        fcol.AddChild(new Label { Text = "FRIENDS" });
        var addRow = new HBoxContainer();
        _friendName = new LineEdit { PlaceholderText = "username", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MaxLength = 24 };
        var addBtn = new Button { Text = "Add" };
        addBtn.Pressed += () => Do(AccountClient.Post("/friends/request", new { Username = _friendName.Text.StripEdges() }));
        addRow.AddChild(_friendName); addRow.AddChild(addBtn);
        fcol.AddChild(addRow);
        var fscroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _friends = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        fscroll.AddChild(_friends);
        fcol.AddChild(fscroll);
        cols.AddChild(fcol);

        // ── guildia ──
        var gcol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        gcol.AddChild(new Label { Text = "GUILD" });
        var gRow = new HBoxContainer();
        _guildInput = new LineEdit { PlaceholderText = "guild name / invite username", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MaxLength = 24 };
        gRow.AddChild(_guildInput);
        gcol.AddChild(gRow);
        var gscroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _guild = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        gscroll.AddChild(_guild);
        gcol.AddChild(gscroll);
        cols.AddChild(gcol);
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

    private void Refresh()
    {
        foreach (Node c in _friends.GetChildren()) c.QueueFree();
        foreach (Node c in _guild.GetChildren()) c.QueueFree();

        if (!AccountSession.LoggedIn)
        {
            _friends.AddChild(new Label { Text = "Log in to the online realm to use\nfriends and guilds.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
            return;
        }

        RefreshFriends();
        RefreshGuild();
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
            string name = f.GetString()!;
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = "• " + name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
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
            _guild.AddChild(new Label { Text = (m.GetProperty("Leader").GetBoolean() ? "★ " : "• ") + m.GetProperty("Name").GetString() });

        var leave = new Button { Text = isLeader ? "Disband guild (leave as leader)" : "Leave guild" };
        leave.Pressed += () => Do(AccountClient.Post("/guild/leave", new { }));
        _guild.AddChild(leave);
    }
}

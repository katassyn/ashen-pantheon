using Godot;

/// <summary>Kontener graczy w scenie: spawnuje lokalnego gracza + puppety pozostałych peerów.
/// Nazwa node'a gracza = peer id (identyczne ścieżki RPC). Po zmianie sesji (join zmienia NASZE id!)
/// przebudowuje wszystkich graczy od zera.</summary>
public partial class NetPlayers : Node2D
{
    [Export] public bool CombatEnabled = true;

    private PackedScene _playerScene;

    public override void _Ready()
    {
        _playerScene = GD.Load<PackedScene>("res://scenes/Player.tscn");
        Rebuild();
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Net.SessionChanged += OnSessionChanged;
    }

    public override void _ExitTree()
    {
        Multiplayer.PeerConnected -= OnPeerConnected;
        Multiplayer.PeerDisconnected -= OnPeerDisconnected;
        Net.SessionChanged -= OnSessionChanged;
    }

    private void OnSessionChanged() => CallDeferred(nameof(Rebuild));

    private void OnPeerConnected(long peer) => SpawnFor((int)peer);

    private void OnPeerDisconnected(long peer) =>
        GetNodeOrNull(peer.ToString())?.QueueFree();

    private void Rebuild()
    {
        foreach (Node c in GetChildren())
        {
            c.Name = c.Name + "_old";
            c.QueueFree();
        }
        SpawnFor(Net.MyId);
        foreach (int peer in Multiplayer.GetPeers()) SpawnFor(peer);
    }

    private void SpawnFor(int peer)
    {
        if (GetNodeOrNull(peer.ToString()) != null) return;
        var p = _playerScene.Instantiate<PlayerController>();
        p.Name = peer.ToString();
        p.SetMultiplayerAuthority(peer);
        p.CombatEnabled = CombatEnabled;
        p.Position = new Vector2(60f * GetChildCount(), 40f * GetChildCount());
        p.AddChild(new Nameplate { Peer = peer, LocalPlayer = peer == Net.MyId });
        if (peer == Net.MyId) p.AddChild(new QuestArrow()); // wskaźnik kierunku do celu questa (tylko lokalny)
        AddChild(p);
        GD.Print($"[net] spawn gracza: peer {peer}{(peer == Net.MyId ? " (lokalny)" : " (puppet)")}");
    }
}

/// <summary>Złota strzałka orbitująca wokół gracza — wskazuje NAJBLIŻSZY widoczny obiekt questowy
/// w scenie (znaczniki reach/interact, eskorta, obrona, survive). Znika, gdy cel blisko lub go brak.</summary>
public partial class QuestArrow : Node2D
{
    private float _timer;
    private Vector2 _target;
    private bool _hasTarget;

    public override void _Ready()
    {
        ZIndex = 25;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        _timer -= (float)delta;
        if (_timer <= 0f)
        {
            _timer = 0.4f;
            FindTarget();
        }
        if (!_hasTarget || GetParent() is not Node2D player) { Visible = false; return; }

        var dir = _target - player.GlobalPosition;
        if (dir.Length() < 280f) { Visible = false; return; } // cel na ekranie — strzałka zbędna
        Visible = true;
        Rotation = dir.Angle();
        Position = dir.Normalized() * 58f;
    }

    private void FindTarget()
    {
        _hasTarget = false;
        if (GetParent() is not Node2D player) return;
        float best = float.MaxValue;
        foreach (Node n in GetTree().GetNodesInGroup("minimap_objective"))
        {
            // QuestMarkerNode gasi Visible, gdy jego cel nie jest aktywny — filtr za darmo
            if (n is not Node2D n2 || !IsInstanceValid(n2) || !n2.Visible) continue;
            float d = n2.GlobalPosition.DistanceSquaredTo(player.GlobalPosition);
            if (d < best) { best = d; _target = n2.GlobalPosition; _hasTarget = true; }
        }
    }

    public override void _Draw() =>
        DrawColoredPolygon(
            new[] { new Vector2(16, 0), new Vector2(-7, -9), new Vector2(-7, 9) },
            new Color(1f, 0.85f, 0.3f, 0.9f));
}

/// <summary>Nick nad głową gracza (MMO nameplate). U INNYCH graczy PPM na nicku = menu
/// Trade/Whisper/Invite to Guild — ten sam wzorzec co party frame (decyzja: interakcje przez PPM na nicku).</summary>
public partial class Nameplate : Label
{
    public long Peer;
    public bool LocalPlayer;

    private PopupMenu _menu;
    private float _timer;

    public override void _Ready()
    {
        ZIndex = 30;
        HorizontalAlignment = HorizontalAlignment.Center;
        MouseFilter = LocalPlayer ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
        AddThemeFontSizeOverride("font_size", 13);
        AddThemeColorOverride("font_color", LocalPlayer ? new Color(0.72f, 0.9f, 1f) : new Color(1f, 1f, 0.85f));
        AddThemeColorOverride("font_outline_color", Colors.Black);
        AddThemeConstantOverride("outline_size", 4);

        if (!LocalPlayer)
        {
            TooltipText = "RMB: Trade / Whisper / Guild";
            _menu = new PopupMenu();
            _menu.AddItem("Trade", 0);
            _menu.AddItem("Whisper", 1);
            _menu.AddItem("Invite to Guild", 2);
            _menu.IdPressed += OnMenu;
            AddChild(_menu);
        }
        Refresh();
    }

    public override void _Process(double delta)
    {
        _timer -= (float)delta;
        if (_timer > 0f) return;
        _timer = 0.5f; // nicki spływają asynchronicznie (AnnounceName)
        Refresh();
    }

    private void Refresh()
    {
        Text = LocalPlayer ? GameState.CharacterName : Net.NameOf(Peer);
        Position = new Vector2(-Size.X / 2f, -66f);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
        {
            // pozycja ekranowa (viewport), nie światowa — PopupMenu żyje w screen space
            _menu.Position = (Vector2I)GetViewport().GetMousePosition();
            _menu.Popup();
            AcceptEvent();
        }
    }

    private void OnMenu(long id)
    {
        switch (id)
        {
            case 0:
                if (GetTree().GetFirstNodeInGroup("trade") is TradePanel tp) tp.RequestTradeWith(Peer);
                break;
            case 1:
                WhisperDialog.Open(this, Peer);
                break;
            case 2:
                Net.SendChatLocal("Open Friends/Guild (O) to invite by account name (online realm).");
                break;
        }
    }
}

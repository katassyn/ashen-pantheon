using Godot;

/// <summary>Lobby w hubie: hostuj / dołącz po IP / rozłącz + status i liczba graczy.</summary>
public partial class LobbyPanel : CanvasLayer
{
    private Label _status;
    private LineEdit _ip;
    private Button _host, _join, _leave;

    public override void _Ready()
    {
        var box = new PanelContainer
        {
            AnchorLeft = 1f, AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = -270f, OffsetRight = -10f, OffsetTop = -320f, OffsetBottom = -180f,
        };
        var style = new StyleBoxFlat { BgColor = new Color(0.08f, 0.07f, 0.12f, 0.95f), BorderColor = new Color(0.4f, 0.35f, 0.55f) };
        style.SetBorderWidthAll(2);
        box.AddThemeStyleboxOverride("panel", style);
        AddChild(box);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 6);
        box.AddChild(vb);

        vb.AddChild(new Label { Text = "CO-OP (max 4)" });
        _status = new Label { Text = Net.Status };
        vb.AddChild(_status);

        _host = new Button { Text = "Host game" };
        _host.Pressed += () => Net.HostGame();
        vb.AddChild(_host);

        var hb = new HBoxContainer();
        _ip = new LineEdit { Text = "127.0.0.1", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _join = new Button { Text = "Join" };
        _join.Pressed += () => Net.JoinGame(_ip.Text.StripEdges());
        hb.AddChild(_ip);
        hb.AddChild(_join);
        vb.AddChild(hb);

        _leave = new Button { Text = "Disconnect" };
        _leave.Pressed += () => Net.Leave();
        vb.AddChild(_leave);
    }

    public override void _Process(double delta)
    {
        _status.Text = $"{Net.Status}\ngraczy: {Net.PlayerCount()}";
        bool online = Net.Online;
        _host.Disabled = online;
        _join.Disabled = online;
        _leave.Disabled = !online;
    }
}

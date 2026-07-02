using Godot;

/// <summary>Panel konta online (hub): rejestracja/logowanie → postać przenosi się na meta-serwer.
/// Bez logowania gra działa w pełni offline (lokalny JSON).</summary>
public partial class LoginPanel : CanvasLayer
{
    private Label _status;
    private LineEdit _server, _user, _pass;
    private Button _register, _login, _logout;

    public override void _Ready()
    {
        var box = new PanelContainer
        {
            AnchorLeft = 1f, AnchorRight = 1f, AnchorTop = 0f, AnchorBottom = 0f,
            OffsetLeft = -270f, OffsetRight = -10f, OffsetTop = 160f, OffsetBottom = 360f,
        };
        var style = new StyleBoxFlat { BgColor = new Color(0.08f, 0.07f, 0.12f, 0.95f), BorderColor = new Color(0.4f, 0.35f, 0.55f) };
        style.SetBorderWidthAll(2);
        box.AddThemeStyleboxOverride("panel", style);
        AddChild(box);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 6);
        box.AddChild(vb);

        vb.AddChild(new Label { Text = "KONTO ONLINE (opcjonalne)" });
        _status = new Label { Text = "offline — zapis lokalny", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        vb.AddChild(_status);

        _server = new LineEdit { Text = AccountSession.ServerUrl, PlaceholderText = "adres serwera" };
        _user = new LineEdit { PlaceholderText = "nazwa konta" };
        _pass = new LineEdit { PlaceholderText = "hasło", Secret = true };
        vb.AddChild(_server);
        vb.AddChild(_user);
        vb.AddChild(_pass);

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 6);
        _register = new Button { Text = "Rejestruj" };
        _login = new Button { Text = "Zaloguj" };
        _logout = new Button { Text = "Wyloguj" };
        _register.Pressed += () => Auth("/auth/register");
        _login.Pressed += () => Auth("/auth/login");
        _logout.Pressed += Logout;
        hb.AddChild(_register);
        hb.AddChild(_login);
        hb.AddChild(_logout);
        vb.AddChild(hb);
    }

    private void Auth(string endpoint)
    {
        AccountSession.ServerUrl = _server.Text.StripEdges().TrimEnd('/');
        var (ok, msg) = AccountClient.RegisterOrLogin(endpoint, _user.Text.StripEdges(), _pass.Text);
        _status.Text = msg;
        if (!ok) return;

        // postać z serwera (lub migracja lokalnej na świeże konto)
        GameState.SwitchRepository(new HttpGameStateRepository());
        PlayerController.Local?.Refresh();
        _status.Text = $"zalogowano: {AccountSession.Username}\npostać: meta-serwer";
    }

    private void Logout()
    {
        if (!AccountSession.LoggedIn) return;
        if (GameState.Repository is HttpGameStateRepository h) { GameState.Save(); h.FlushBlocking(); }
        AccountSession.Token = null;
        AccountSession.Username = null;

        string path = OS.GetUserDataDir() + "/save.json";
        GameState.SwitchRepository(new AshenPantheon.Core.JsonGameStateRepository(path));
        PlayerController.Local?.Refresh();
        _status.Text = "wylogowano — zapis lokalny";
    }

    public override void _Process(double delta)
    {
        bool online = AccountSession.LoggedIn;
        _register.Disabled = online;
        _login.Disabled = online;
        _logout.Disabled = !online;
    }
}

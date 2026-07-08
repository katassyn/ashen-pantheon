using System.IO;
using System.Text.Json;
using Godot;
using AshenPantheon.Core;

/// <summary>Menu główne: realm → sloty postaci (12) → kreator (nick + klasa) → gra.</summary>
public partial class MainMenu : Control
{
    public const int SlotCount = 12;

    private OptionButton _realm;
    private VBoxContainer _slotList;
    private VBoxContainer _creator;
    private LineEdit _nick;
    private Label _status;
    private int _selectedSlot = -1;

    private string RealmId => "ashen"; // na razie jeden realm lokalny; online-realmy = faza meta-serwera
    private string SlotPath(int i) => $"{OS.GetUserDataDir()}/characters/{RealmId}/slot{i}.json";

    public override void _Ready()
    {
        DataLoader.LoadAll();

        AddChild(new AmbientBackground { Seed = 11 }); // animowane tło menu (gwiazdy/iskry/aura)

        // czytelna karta-tło pod treścią menu (żeby napisy nie ginęły na animowanym tle)
        var card = new Panel
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -400, OffsetRight = 400, OffsetTop = 18, OffsetBottom = -18,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var cardStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.06f, 0.11f, 0.82f),
            BorderColor = new Color(0.45f, 0.38f, 0.62f, 0.9f),
            ShadowColor = new Color(0f, 0f, 0f, 0.5f), ShadowSize = 12,
        };
        cardStyle.SetBorderWidthAll(2);
        cardStyle.SetCornerRadiusAll(10);
        card.AddThemeStyleboxOverride("panel", cardStyle);
        AddChild(card);
        card.AddChild(new PanelDecor { Name = "PanelDecor" }); // narożniki + akcent

        AddChild(new SceneFadeIn());                   // miękkie wejście (na wierzchu)

        var center = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -360, OffsetRight = 360, OffsetTop = 30, OffsetBottom = -30,
        };
        center.AddThemeConstantOverride("separation", 10);
        AddChild(center);

        var title = new Label { Text = "ASHEN PANTHEON", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 40);
        title.AddThemeColorOverride("font_color", new Color(0.92f, 0.82f, 1f));
        title.AddThemeColorOverride("font_outline_color", new Color(0.35f, 0.2f, 0.5f));
        title.AddThemeConstantOverride("outline_size", 6);
        center.AddChild(title);

        var sub = new Label { Text = "· a dark-fantasy loot ARPG ·", HorizontalAlignment = HorizontalAlignment.Center };
        sub.AddThemeFontSizeOverride("font_size", 13);
        sub.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.72f));
        center.AddChild(sub);
        center.AddChild(new TitleRule { CustomMinimumSize = new Vector2(0, 14) });

        var realmRow = new HBoxContainer();
        realmRow.AddThemeConstantOverride("separation", 8);
        realmRow.AddChild(new Label { Text = "Realm:" });
        _realm = new OptionButton();
        _realm.AddItem("Ashen (local)");
        _realm.AddItem("Online (meta-server)");
        _realm.ItemSelected += _ => RefreshRealmView();
        realmRow.AddChild(_realm);
        center.AddChild(realmRow);

        // ── realm lokalny: sloty postaci ──
        _localView = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _localView.AddChild(new Label { Text = "Characters:" });
        var scroll = UiKit.VScroll();
        _slotList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_slotList);
        _localView.AddChild(scroll);
        center.AddChild(_localView);

        // ── realm online: logowanie/rejestracja ──
        BuildOnlineView(center);

        // kreator (pokazywany po wybraniu pustego slotu)
        _creator = new VBoxContainer { Visible = false };
        _creator.AddThemeConstantOverride("separation", 8);
        _creator.AddChild(new Label { Text = "NEW CHARACTER" });
        _nick = new LineEdit { PlaceholderText = "name (3-16 characters)", MaxLength = 16 };
        _creator.AddChild(_nick);
        var classRow = new HBoxContainer();
        classRow.AddThemeConstantOverride("separation", 8);
        classRow.AddChild(ClassPickButton("ranger", "Ranger", "Hunter — Concentration, Mark", false));
        classRow.AddChild(ClassPickButton("dragonknight", "Dragonknight (soon)", "Coming soon", true));
        classRow.AddChild(ClassPickButton("spellweaver", "Spellweaver (soon)", "Coming soon", true));
        _creator.AddChild(classRow);
        var create = new Button { Text = "Create & play" };
        create.Pressed += CreateAndPlay;
        _creator.AddChild(create);
        center.AddChild(_creator);

        _status = new Label();
        center.AddChild(_status);

        RefreshSlots();
        RefreshRealmView();
    }

    private VBoxContainer _localView, _onlineView;
    private LineEdit _srv, _user, _pass;

    private void BuildOnlineView(VBoxContainer parent)
    {
        _onlineView = new VBoxContainer { Visible = false };
        _onlineView.AddThemeConstantOverride("separation", 8);
        _onlineView.AddChild(new Label { Text = "Log in to the online realm (server-backed character):" });

        _srv = new LineEdit { Text = AccountSession.ServerUrl, PlaceholderText = "server url" };
        _user = new LineEdit { PlaceholderText = "username (3-24)", MaxLength = 24 };
        _pass = new LineEdit { PlaceholderText = "password (min 6)", Secret = true, MaxLength = 64 };
        _onlineView.AddChild(new Label { Text = "Server:" });
        _onlineView.AddChild(_srv);
        _onlineView.AddChild(_user);
        _onlineView.AddChild(_pass);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var login = new Button { Text = "Log in", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        login.Pressed += () => OnlineAuth("/auth/login");
        var register = new Button { Text = "Register", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        register.Pressed += () => OnlineAuth("/auth/register");
        row.AddChild(login);
        row.AddChild(register);
        _onlineView.AddChild(row);
        parent.AddChild(_onlineView);
    }

    private void RefreshRealmView()
    {
        bool online = _realm.Selected == 1;
        _localView.Visible = !online;
        _onlineView.Visible = online;
        _creator.Visible = false;
    }

    private void OnlineAuth(string endpoint)
    {
        if (_user.Text.StripEdges().Length < 3 || _pass.Text.Length < 6)
        { _status.Text = "Username min 3, password min 6."; return; }

        AccountSession.ServerUrl = _srv.Text.StripEdges().TrimEnd('/');
        _status.Text = "Connecting…";
        var (ok, msg) = AccountClient.RegisterOrLogin(endpoint, _user.Text.StripEdges(), _pass.Text);
        if (!ok) { _status.Text = msg; return; }

        var repo = new HttpGameStateRepository();
        var existing = repo.Load();
        if (existing != null)
        {
            GameState.SwitchRepository(repo); // wczytaj postać z serwera
            EnterGame();
        }
        else
        {
            // brak postaci na koncie → kreator (nick + klasa), zapis PUT na serwer
            _onlineView.Visible = false;
            _selectedSlot = -2; // sygnał: online creator
            _creator.Visible = true;
            _status.Text = "New account — create your character.";
        }
    }

    private void RefreshSlots()
    {
        foreach (Node c in _slotList.GetChildren()) c.QueueFree();

        for (int i = 0; i < SlotCount; i++)
        {
            var meta = ReadMeta(SlotPath(i));
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);

            var main = new Button
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 48),
                Alignment = HorizontalAlignment.Left,
                Text = meta == null
                    ? $"        {i + 1}.   — empty slot —"
                    : $"        {i + 1}.   {meta.Name}      {ClassName(meta.ClassId)}",
            };
            StyleSlot(main, meta != null);
            // herb klasy po lewej (złoty dla postaci, "+" szary dla pustego slotu)
            main.AddChild(new ClassCrestIcon
            {
                ClassId = meta?.ClassId ?? "",
                IconColor = meta != null ? new Color(0.9f, 0.78f, 0.45f) : new Color(0.45f, 0.42f, 0.55f),
                AnchorTop = 0.5f, AnchorBottom = 0.5f, OffsetTop = -15, OffsetBottom = 15,
                OffsetLeft = 10, Size = new Vector2(30, 30),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            if (meta != null) // żeton poziomu po prawej
                main.AddChild(new LevelBadge
                {
                    Level = meta.Level,
                    AnchorLeft = 1f, AnchorRight = 1f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
                    OffsetLeft = -44, OffsetRight = -12, OffsetTop = -16, OffsetBottom = 16,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                });
            int slot = i;
            main.Pressed += () => SelectSlot(slot, meta != null);
            row.AddChild(main);

            if (meta != null)
            {
                var del = new Button { Text = "Delete", TooltipText = "Deletes the character permanently" };
                StyleDanger(del);
                del.Pressed += () =>
                {
                    File.Delete(SlotPath(slot));
                    _status.Text = $"Character in slot {slot + 1} deleted.";
                    RefreshSlots();
                };
                row.AddChild(del);
            }
            _slotList.AddChild(row);
        }
    }

    private static string ClassName(string id) =>
        GameData.Classes.TryGetValue(id, out var c) ? c.Name : id;

    /// <summary>Kafelek wyboru klasy w kreatorze: herb + nazwa + styl (nieaktywne klasy przygaszone).</summary>
    private static Button ClassPickButton(string classId, string label, string tip, bool disabled)
    {
        var b = new Button
        {
            Text = "      " + label, TooltipText = tip, Disabled = disabled,
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new Vector2(0, 44),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        b.AddChild(new ClassCrestIcon
        {
            ClassId = classId,
            IconColor = disabled ? new Color(0.5f, 0.48f, 0.58f) : new Color(0.55f, 0.85f, 0.5f),
            AnchorTop = 0.5f, AnchorBottom = 0.5f, OffsetTop = -13, OffsetBottom = 13,
            OffsetLeft = 6, Size = new Vector2(26, 26),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        return b;
    }

    /// <summary>Styl karty slotu postaci (normal/hover/pressed) — zamiast płaskiego przycisku.</summary>
    private static void StyleSlot(Button b, bool occupied)
    {
        var bg = occupied ? new Color(0.12f, 0.11f, 0.17f, 0.95f) : new Color(0.09f, 0.085f, 0.13f, 0.9f);
        var border = occupied ? new Color(0.5f, 0.44f, 0.68f) : new Color(0.3f, 0.28f, 0.4f);
        void S(string name, Color fill, Color bord, int w)
        {
            var sb = new StyleBoxFlat { BgColor = fill, BorderColor = bord };
            sb.SetBorderWidthAll(w); sb.SetCornerRadiusAll(6); sb.SetContentMarginAll(6);
            b.AddThemeStyleboxOverride(name, sb);
        }
        S("normal", bg, border, 2);
        S("hover", bg.Lightened(0.08f), border.Lightened(0.2f), 2);
        S("pressed", bg.Darkened(0.1f), border, 2);
        b.AddThemeColorOverride("font_color", occupied ? new Color(0.92f, 0.9f, 1f) : new Color(0.6f, 0.58f, 0.7f));
    }

    /// <summary>Styl przycisku-zagrożenia (Delete) — czerwonawy.</summary>
    private static void StyleDanger(Button b)
    {
        b.CustomMinimumSize = new Vector2(80, 0);
        var col = new Color(0.42f, 0.14f, 0.16f);
        void S(string n, Color f)
        {
            var sb = new StyleBoxFlat { BgColor = f, BorderColor = new Color(0.7f, 0.3f, 0.3f) };
            sb.SetBorderWidthAll(2); sb.SetCornerRadiusAll(6); sb.SetContentMarginAll(6);
            b.AddThemeStyleboxOverride(n, sb);
        }
        S("normal", col); S("hover", col.Lightened(0.12f)); S("pressed", col.Darkened(0.1f));
        b.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.85f));
    }

    private void SelectSlot(int slot, bool occupied)
    {
        _selectedSlot = slot;
        if (occupied)
        {
            var repo = new JsonGameStateRepository(SlotPath(slot));
            GameState.SwitchRepository(repo);
            EnterGame();
        }
        else
        {
            _creator.Visible = true;
            _status.Text = $"Creating a character in slot {slot + 1}.";
        }
    }

    private void CreateAndPlay()
    {
        string nick = _nick.Text.StripEdges();
        if (nick.Length < 3) { _status.Text = "Name must be at least 3 characters."; return; }

        IGameStateRepository repo = _selectedSlot == -2
            ? new HttpGameStateRepository()                 // online realm (server-backed)
            : _selectedSlot >= 0 ? new JsonGameStateRepository(SlotPath(_selectedSlot)) : null;
        if (repo == null) return;

        GameState.NewCharacter(nick, "ranger", repo);
        EnterGame();
    }

    private void EnterGame()
    {
        PlayerController.Local?.Refresh();
        GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
    }

    /// <summary>Lekki odczyt metadanych slotu (nazwa/klasa/poziom) bez ładowania całej postaci.</summary>
    private static SaveData ReadMeta(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path), JsonGameStateRepository.Options);
        }
        catch { return null; }
    }
}

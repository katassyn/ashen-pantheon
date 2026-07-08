using Godot;
using AshenPantheon.Core;

/// <summary>Panel statystyk (C): pełny arkusz + wydawanie punktów atrybutów + respec atrybutów za złoto.</summary>
public partial class StatsPanel : CanvasLayer, IUiPanel
{
    private Panel _root;
    private Label _text;
    private HBoxContainer _buttons;
    private PlayerController _player;

    // wiersze statów z ikonami (budowane raz, wartości aktualizowane w _Process)
    private readonly System.Collections.Generic.Dictionary<string, Label> _vals = new();

    public void CloseUi() => _root.Visible = false;
    public bool IsOpen => _root != null && _root.Visible;

    public override void _Ready()
    {
        _root = GetNode<Panel>("%Root");
        _text = GetNode<Label>("%Text");
        AddToGroup(UiPanels.Group);
        UiPanels.Solidify(_root);
        BuildStatRows();

        _buttons = new HBoxContainer();
        _buttons.AddThemeConstantOverride("separation", 8);
        // przypięte do dołu panelu (niezależnie od jego wysokości)
        _buttons.AnchorTop = 1f; _buttons.AnchorBottom = 1f; _buttons.AnchorLeft = 0f; _buttons.AnchorRight = 0f;
        _buttons.OffsetLeft = 20f; _buttons.OffsetTop = -46f; _buttons.OffsetBottom = -12f;
        _buttons.GrowVertical = Control.GrowDirection.Begin;
        _root.AddChild(_buttons);

        AddAttrButton("+ Str", () => GameState.Spent.Strength++);
        AddAttrButton("+ Dex", () => GameState.Spent.Dexterity++);
        AddAttrButton("+ Int", () => GameState.Spent.Intelligence++);

        var respec = new Button { Text = "Respec attributes" };
        respec.Pressed += () =>
        {
            int spent = GameState.Spent.Strength + GameState.Spent.Dexterity + GameState.Spent.Intelligence;
            long cost = Respec.AttributeCost(spent);
            if (spent == 0 || GameState.Wallet.Gold < cost) return;
            GameState.Wallet.Gold -= cost;
            GameState.Progress.AttributePoints += spent;
            GameState.Spent = new Attributes();
            AfterChange();
        };
        _buttons.AddChild(respec);

        _root.Visible = false;
    }

    private void AddAttrButton(string label, System.Action apply)
    {
        var b = new Button { Text = label };
        b.Pressed += () =>
        {
            if (GameState.Progress.AttributePoints <= 0) return;
            GameState.Progress.AttributePoints--;
            apply();
            AfterChange();
        };
        _buttons.AddChild(b);
    }

    private void AfterChange()
    {
        GameState.Save();
        _player = PlayerController.Local;
        _player?.Refresh();
    }

    /// <summary>Buduje siatkę statów z ikonami (raz). Trzy kolumny: obrona · ofensywa · atrybuty+resisty.</summary>
    private void BuildStatRows()
    {
        var cols = new HBoxContainer { AnchorLeft = 0f, AnchorRight = 1f, OffsetLeft = 20, OffsetTop = 96, OffsetRight = -20 };
        cols.AddThemeConstantOverride("separation", 28);
        _text.GetParent().AddChild(cols);

        VBoxContainer col() { var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; v.AddThemeConstantOverride("separation", 5); cols.AddChild(v); return v; }
        var c1 = col(); var c2 = col(); var c3 = col();

        void row(VBoxContainer parent, string kind, string name, Color icol)
        {
            var r = new HBoxContainer();
            r.AddThemeConstantOverride("separation", 7);
            r.AddChild(new StatIcon { Kind = kind, IconColor = icol, CustomMinimumSize = new Vector2(20, 20) });
            r.AddChild(new Label { Text = name, CustomMinimumSize = new Vector2(96, 0) });
            var val = new Label { Text = "—" };
            val.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.7f));
            r.AddChild(val);
            _vals[kind] = val;
            parent.AddChild(r);
        }

        c1.AddChild(new Label { Text = "— Defense —", Modulate = new Color(0.7f, 0.8f, 1f) });
        row(c1, "life", "Life", new Color(0.9f, 0.3f, 0.3f));
        row(c1, "mana", "Mana", new Color(0.4f, 0.6f, 1f));
        row(c1, "es", "Energy Shield", new Color(0.5f, 0.85f, 1f));
        row(c1, "armour", "Armour", new Color(0.75f, 0.75f, 0.8f));
        row(c1, "evasion", "Evasion", new Color(0.6f, 0.9f, 0.6f));

        c2.AddChild(new Label { Text = "— Offense —", Modulate = new Color(1f, 0.8f, 0.6f) });
        row(c2, "damage", "Attack dmg", new Color(1f, 0.7f, 0.4f));
        row(c2, "crit", "Crit", new Color(1f, 0.85f, 0.3f));
        row(c2, "hit", "Hit chance", new Color(0.8f, 0.85f, 0.9f));
        row(c2, "speed", "Move speed", new Color(0.7f, 0.9f, 1f));

        c3.AddChild(new Label { Text = "— Attributes & Resists —", Modulate = new Color(0.8f, 1f, 0.8f) });
        row(c3, "str", "Strength", new Color(1f, 0.5f, 0.4f));
        row(c3, "dex", "Dexterity", new Color(0.5f, 1f, 0.6f));
        row(c3, "int", "Intelligence", new Color(0.5f, 0.7f, 1f));
        row(c3, "fire", "Fire res", new Color(1f, 0.5f, 0.3f));
        row(c3, "cold", "Cold res", new Color(0.5f, 0.8f, 1f));
        row(c3, "light", "Light res", new Color(1f, 0.9f, 0.4f));
        row(c3, "chaos", "Chaos res", new Color(0.8f, 0.4f, 0.9f));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && Keybinds.Matches(k, "stats"))
        {
            if (_root.Visible) { _root.Visible = false; }
            else
            {
                UiPanels.CloseAllExcept(GetTree(), this);
                _root.Visible = true;
            }
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!_root.Visible) return;
        var s = GameState.BuildSheet();
        var p = GameState.Progress;
        int spentAttr = GameState.Spent.Strength + GameState.Spent.Dexterity + GameState.Spent.Intelligence;
        _text.Text =
            "STATS    [C] close\n" +
            $"Level: {p.Level}    XP: {p.Xp}/{PlayerProgress.XpToNext(p.Level)}    Gold: {GameState.Wallet.Gold}\n" +
            $"Attribute points: {p.AttributePoints}    Skill points: {p.SkillPoints}    (respec: {Respec.AttributeCost(spentAttr)}g)";

        void set(string k, string v) { if (_vals.TryGetValue(k, out var l)) l.Text = v; }
        set("life", $"{s.MaxLife:0}");
        set("mana", $"{s.MaxMana:0}");
        set("es", $"{s.MaxEnergyShield:0}");
        set("armour", $"{s.Armour:0}");
        set("evasion", $"{s.EvasionRating:0}");
        set("damage", $"x{s.AttackDamageMultiplier:0.00}");
        set("crit", $"{s.CritChance * 100f:0}%  x{s.CritMultiplier:0.00}");
        set("hit", $"{s.HitChance:0}%");
        set("speed", $"x{s.MoveSpeedMult:0.00}");
        set("str", $"{s.Attributes.Strength}  (+{GameState.Spent.Strength})");
        set("dex", $"{s.Attributes.Dexterity}  (+{GameState.Spent.Dexterity})");
        set("int", $"{s.Attributes.Intelligence}  (+{GameState.Spent.Intelligence})");
        set("fire", $"{s.Resistances.Effective(DamageType.Fire, s.Level):0}%");
        set("cold", $"{s.Resistances.Effective(DamageType.Cold, s.Level):0}%");
        set("light", $"{s.Resistances.Effective(DamageType.Lightning, s.Level):0}%");
        set("chaos", $"{s.Resistances.Effective(DamageType.Chaos, s.Level):0}%");
    }
}

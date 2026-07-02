using Godot;
using AshenPantheon.Core;

/// <summary>Panel statystyk (C): pełny arkusz + wydawanie punktów atrybutów + respec atrybutów za złoto.</summary>
public partial class StatsPanel : CanvasLayer, IUiPanel
{
    private Panel _root;
    private Label _text;
    private HBoxContainer _buttons;
    private PlayerController _player;

    public void CloseUi() => _root.Visible = false;

    public override void _Ready()
    {
        _root = GetNode<Panel>("%Root");
        _text = GetNode<Label>("%Text");
        AddToGroup(UiPanels.Group);
        UiPanels.Solidify(_root);

        _buttons = new HBoxContainer();
        _buttons.AddThemeConstantOverride("separation", 8);
        // przypięte do dołu panelu (niezależnie od jego wysokości)
        _buttons.AnchorTop = 1f; _buttons.AnchorBottom = 1f; _buttons.AnchorLeft = 0f; _buttons.AnchorRight = 0f;
        _buttons.OffsetLeft = 20f; _buttons.OffsetTop = -46f; _buttons.OffsetBottom = -12f;
        _buttons.GrowVertical = Control.GrowDirection.Begin;
        _root.AddChild(_buttons);

        AddAttrButton("+ Siła", () => GameState.Spent.Strength++);
        AddAttrButton("+ Dex", () => GameState.Spent.Dexterity++);
        AddAttrButton("+ Int", () => GameState.Spent.Intelligence++);

        var respec = new Button { Text = "Respec atrybutów" };
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

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.C)
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
            "STATYSTYKI    [C] zamknij\n\n" +
            $"Poziom: {p.Level}    XP: {p.Xp}/{PlayerProgress.XpToNext(p.Level)}    Złoto: {GameState.Wallet.Gold}\n" +
            $"Punkty atrybutów: {p.AttributePoints}    Punkty skilli: {p.SkillPoints}\n\n" +
            $"Życie:            {s.MaxLife:0}\n" +
            $"Mana:             {s.MaxMana:0}\n" +
            $"Energy Shield:    {s.MaxEnergyShield:0}\n" +
            $"Armour:           {s.Armour:0}\n" +
            $"Evasion:          {s.EvasionRating:0}\n" +
            $"Hit chance:       {s.HitChance:0}%\n" +
            $"Krytyk:           {s.CritChance * 100f:0}%  ×{s.CritMultiplier:0.00}\n" +
            $"Attack dmg:       ×{s.AttackDamageMultiplier:0.00}\n\n" +
            $"Siła {s.Attributes.Strength} (wydane {GameState.Spent.Strength})   " +
            $"Dex {s.Attributes.Dexterity} (wydane {GameState.Spent.Dexterity})   " +
            $"Int {s.Attributes.Intelligence} (wydane {GameState.Spent.Intelligence})\n\n" +
            $"Resisty: Fire {s.Resistances.Effective(DamageType.Fire, s.Level):0}%  " +
            $"Cold {s.Resistances.Effective(DamageType.Cold, s.Level):0}%  " +
            $"Lightning {s.Resistances.Effective(DamageType.Lightning, s.Level):0}%  " +
            $"Chaos {s.Resistances.Effective(DamageType.Chaos, s.Level):0}%\n" +
            $"Respec atrybutów kosztuje: {Respec.AttributeCost(spentAttr)} złota";
    }
}

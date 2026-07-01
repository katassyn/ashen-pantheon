using Godot;
using AshenPantheon.Core;

/// <summary>Panel statystyk postaci — klawisz C, wszędzie. Tylko podgląd (arkusz na żywo).</summary>
public partial class StatsPanel : CanvasLayer
{
    private Panel _root;
    private Label _text;

    public override void _Ready()
    {
        _root = GetNode<Panel>("%Root");
        _text = GetNode<Label>("%Text");
        _root.Visible = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.C)
        {
            _root.Visible = !_root.Visible;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!_root.Visible) return;
        var s = GameState.BuildSheet();
        _text.Text =
            "STATYSTYKI    [C] zamknij\n\n" +
            $"Życie:            {s.MaxLife:0}\n" +
            $"Mana:             {s.MaxMana:0}\n" +
            $"Energy Shield:    {s.MaxEnergyShield:0}   (rośnie z gearu ES × INT%)\n" +
            $"Armour:           {s.Armour:0}   (redukcja fizyczna)\n" +
            $"Evasion:          {s.EvasionRating:0}\n" +
            $"Hit chance:       {s.HitChance:0}%\n" +
            $"Krytyk:           {s.CritChance * 100f:0}%   ×{s.CritMultiplier:0.00}\n" +
            $"Attack dmg:       ×{s.AttackDamageMultiplier:0.00}\n" +
            $"Atk / Cast speed: {s.AttackSpeed:0.00} / {s.CastSpeed:0.00}\n\n" +
            $"Siła {s.Attributes.Strength}      Dexterity {s.Attributes.Dexterity}      Inteligencja {s.Attributes.Intelligence}\n\n" +
            $"Resisty:  Fire {s.Resistances.Effective(DamageType.Fire, s.Level):0}%   " +
            $"Cold {s.Resistances.Effective(DamageType.Cold, s.Level):0}%   " +
            $"Lightning {s.Resistances.Effective(DamageType.Lightning, s.Level):0}%   " +
            $"Chaos {s.Resistances.Effective(DamageType.Chaos, s.Level):0}%";
    }
}

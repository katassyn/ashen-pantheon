using Godot;
using AshenPantheon.Core;

public partial class Dummy : Area2D, IHittable
{
    private readonly Combatant _combatant = new() { MaxHealth = 200f, Health = 200f };
    private Sprite2D _sprite;

    public override void _Ready()
    {
        _sprite = GetNode<Sprite2D>("Sprite2D");
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_combatant.StatusTimeLeft > 0f)
        {
            _combatant.StatusTimeLeft -= (float)delta;
            if (_combatant.ActiveStatus == StatusType.Burn)
                _combatant.Health -= 8f * (float)delta; // DoT

            if (_combatant.StatusTimeLeft <= 0f)
            {
                _combatant.ActiveStatus = StatusType.None;
                UpdateTint();
            }

            QueueRedraw();
            if (_combatant.IsDead) QueueFree();
        }
    }

    /// <summary>Wywoływane przez gracza przy trafieniu.</summary>
    public void ReceiveHit(ResolvedSkill skill)
    {
        CombatResolver.ApplyHit(skill, _combatant);
        UpdateTint();
        QueueRedraw();
        if (_combatant.IsDead)
            QueueFree();
    }

    private void UpdateTint()
    {
        if (_sprite == null) return;
        _sprite.Modulate = _combatant.ActiveStatus switch
        {
            StatusType.Burn => new Color(1f, 0.4f, 0.2f),
            StatusType.Chill => new Color(0.4f, 0.7f, 1f),
            _ => Colors.White
        };
    }

    public override void _Draw()
    {
        float frac = Mathf.Clamp(_combatant.Health / _combatant.MaxHealth, 0f, 1f);
        var size = new Vector2(48f, 6f);
        var pos = new Vector2(-24f, -42f);
        DrawRect(new Rect2(pos, size), new Color(0f, 0f, 0f, 0.7f));
        DrawRect(new Rect2(pos, new Vector2(size.X * frac, size.Y)), new Color(0.9f, 0.2f, 0.2f));
    }
}

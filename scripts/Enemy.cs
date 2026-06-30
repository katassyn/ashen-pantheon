using Godot;
using AshenPantheon.Core;

/// <summary>Husk: goni gracza, w zwarciu zadaje obrażenia, ginie od skilli gracza.</summary>
public partial class Enemy : Area2D, IHittable
{
    [Export] public float Speed = 95f;
    [Export] public float MaxHealth = 60f;
    [Export] public float ContactDamage = 12f;
    [Export] public float AttackCooldown = 0.8f;
    [Export] public float AttackRange = 36f;

    private Combatant _combatant;
    private PlayerController _player;
    private Sprite2D _sprite;
    private float _atkCd;

    public override void _Ready()
    {
        _combatant = new Combatant { MaxHealth = MaxHealth, Health = MaxHealth };
        _player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        _sprite = GetNode<Sprite2D>("Sprite2D");
        UpdateTint();
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_atkCd > 0f) _atkCd -= dt;

        if (_combatant.StatusTimeLeft > 0f)
        {
            _combatant.StatusTimeLeft -= dt;
            if (_combatant.ActiveStatus == StatusType.Burn) _combatant.Health -= 8f * dt;
            if (_combatant.StatusTimeLeft <= 0f) { _combatant.ActiveStatus = StatusType.None; UpdateTint(); }
            QueueRedraw();
            if (_combatant.IsDead) { QueueFree(); return; }
        }

        if (_player == null || !IsInstanceValid(_player)) return;

        Vector2 to = _player.GlobalPosition - GlobalPosition;
        float dist = to.Length();
        float slow = _combatant.IsChilled ? 0.5f : 1f;

        if (dist > AttackRange)
        {
            GlobalPosition += to.Normalized() * Speed * slow * dt;
        }
        else if (_atkCd <= 0f)
        {
            _atkCd = AttackCooldown;
            _player.TakeDamage(ContactDamage);
        }
    }

    public void ReceiveHit(ResolvedSkill skill)
    {
        CombatResolver.ApplyHit(skill, _combatant);
        UpdateTint();
        QueueRedraw();
        if (_combatant.IsDead) QueueFree();
    }

    private void UpdateTint()
    {
        if (_sprite == null) return;
        _sprite.Modulate = _combatant.ActiveStatus switch
        {
            StatusType.Burn => new Color(1f, 0.4f, 0.2f),
            StatusType.Chill => new Color(0.4f, 0.7f, 1f),
            _ => new Color(0.85f, 0.3f, 0.3f)
        };
    }

    public override void _Draw()
    {
        if (_combatant == null) return;
        float frac = Mathf.Clamp(_combatant.Health / _combatant.MaxHealth, 0f, 1f);
        var size = new Vector2(40f, 5f);
        var pos = new Vector2(-20f, -34f);
        DrawRect(new Rect2(pos, size), new Color(0f, 0f, 0f, 0.7f));
        DrawRect(new Rect2(pos, new Vector2(size.X * frac, size.Y)), new Color(0.9f, 0.2f, 0.2f));
    }
}

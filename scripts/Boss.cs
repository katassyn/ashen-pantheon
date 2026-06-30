using Godot;
using AshenPantheon.Core;

/// <summary>Mini-boss "Ashen Warden": trzyma dystans, co kilka sekund rzuca telegrafowany atak (slam/cone/line).</summary>
public partial class Boss : Area2D, IHittable
{
    [Export] public float MaxHealth = 450f;
    [Export] public float Speed = 55f;
    [Export] public float AttackInterval = 2.2f;
    [Export] public float PreferredRange = 150f;

    private Combatant _combatant;
    private PlayerController _player;
    private Sprite2D _sprite;
    private float _atkTimer;
    private int _attackIndex;

    public override void _Ready()
    {
        _combatant = new Combatant { MaxHealth = MaxHealth, Health = MaxHealth };
        _player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        _sprite = GetNode<Sprite2D>("Sprite2D");
        _atkTimer = AttackInterval;
        UpdateTint();
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

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

        if (dist > PreferredRange + 20f)
            GlobalPosition += to.Normalized() * Speed * slow * dt;

        _atkTimer -= dt * slow;
        if (_atkTimer <= 0f)
        {
            _atkTimer = AttackInterval;
            DoAttack(to);
        }
    }

    private void DoAttack(Vector2 toPlayer)
    {
        var tg = new Telegraph();
        GetParent().AddChild(tg);

        switch (_attackIndex % 3)
        {
            case 0: // SLAM — krąg pod aktualną pozycją gracza (musisz odejść)
                tg.Shape = TelegraphShape.Circle;
                tg.Radius = 85f;
                tg.Damage = 26f;
                tg.GlobalPosition = _player.GlobalPosition;
                break;
            case 1: // SWEEP — stożek w stronę gracza
                tg.Shape = TelegraphShape.Cone;
                tg.Radius = 230f;
                tg.HalfAngleDeg = 34f;
                tg.Damage = 24f;
                tg.GlobalPosition = GlobalPosition;
                tg.Rotation = toPlayer.Angle();
                break;
            default: // CHARGE — linia przez gracza
                tg.Shape = TelegraphShape.Line;
                tg.Radius = 340f;
                tg.HalfWidth = 28f;
                tg.Damage = 28f;
                tg.GlobalPosition = GlobalPosition;
                tg.Rotation = toPlayer.Angle();
                break;
        }
        _attackIndex++;
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
            _ => new Color(0.7f, 0.35f, 0.85f)
        };
    }

    public override void _Draw()
    {
        if (_combatant == null) return;
        float frac = Mathf.Clamp(_combatant.Health / _combatant.MaxHealth, 0f, 1f);
        var size = new Vector2(80f, 7f);
        var pos = new Vector2(-40f, -56f);
        DrawRect(new Rect2(pos, size), new Color(0f, 0f, 0f, 0.7f));
        DrawRect(new Rect2(pos, new Vector2(size.X * frac, size.Y)), new Color(0.85f, 0.2f, 0.5f));
    }
}

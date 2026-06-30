using Godot;

/// <summary>Mini-boss "Ashen Warden": trzyma dystans, telegrafuje ataki (slam/cone/line), rani też w kontakcie (baza).</summary>
public partial class Boss : EnemyBase
{
    [Export] public float Speed = 55f;
    [Export] public float AttackInterval = 2.2f;
    [Export] public float PreferredRange = 150f;

    private float _atkTimer;
    private int _attackIndex;

    protected override Color BaseTint => new(0.7f, 0.35f, 0.85f);

    protected override void Behavior(float dt, Vector2 toPlayer, float dist)
    {
        float slow = IsChilled ? 0.5f : 1f;

        if (_atkTimer <= 0f) _atkTimer = AttackInterval;

        if (dist > PreferredRange + 20f)
        {
            Velocity = toPlayer.Normalized() * Speed * slow;
            MoveAndSlide();
        }
        else
        {
            Velocity = Vector2.Zero;
        }

        _atkTimer -= dt * slow;
        if (_atkTimer <= 0f)
        {
            _atkTimer = AttackInterval;
            DoAttack(toPlayer);
        }
    }

    private void DoAttack(Vector2 toPlayer)
    {
        var tg = new Telegraph();
        GetParent().AddChild(tg);

        switch (_attackIndex % 3)
        {
            case 0: // SLAM — krąg pod aktualną pozycją gracza
                tg.Shape = TelegraphShape.Circle;
                tg.Radius = 85f;
                tg.Damage = 26f;
                tg.GlobalPosition = Player.GlobalPosition;
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
}

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

    public Boss()
    {
        XpReward = AshenPantheon.Core.RunGenerator.BossXp;
        LootChance = 1f; // boss zawsze dropi
    }

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
        // telegraf replikowany: każda maszyna symuluje go dla SWOJEGO gracza
        switch (_attackIndex % 3)
        {
            case 0: // SLAM — krąg pod celowanym graczem
                Net.SpawnTelegraph((int)TelegraphShape.Circle, 85f, 0f, 0f, 26f * DmgMult,
                    CurrentTarget?.GlobalPosition ?? GlobalPosition, 0f);
                break;
            case 1: // SWEEP — stożek w stronę gracza
                Net.SpawnTelegraph((int)TelegraphShape.Cone, 230f, 34f, 0f, 24f * DmgMult,
                    GlobalPosition, toPlayer.Angle());
                break;
            default: // CHARGE — linia przez gracza
                Net.SpawnTelegraph((int)TelegraphShape.Line, 340f, 0f, 28f, 28f * DmgMult,
                    GlobalPosition, toPlayer.Angle());
                break;
        }
        _attackIndex++;
    }
}

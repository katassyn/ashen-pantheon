using Godot;

/// <summary>Husk — wzorcowy wróg-szablon: goni, telegrafuje atak (windup), uderza. Pełne stany animacji.</summary>
public partial class Enemy : EnemyBase
{
    [Export] public float Speed = 95f;
    [Export] public float WindupTime = 0.35f;
    [Export] public float AttackReach = 55f;

    private enum State { Chase, Windup, Recover }
    private State _state = State.Chase;
    private float _timer;

    protected override Color BaseTint => new(0.85f, 0.3f, 0.3f);

    public Enemy()
    {
        ContactRange = 0f; // kontaktowe dmg zastąpione telegrafowanym atakiem
    }

    protected override void Behavior(float dt, Vector2 toPlayer, float dist)
    {
        float slow = IsChilled ? 0.5f : 1f;

        switch (_state)
        {
            case State.Chase:
                if (dist <= AttackReach * 0.8f)
                {
                    _state = State.Windup;
                    _timer = WindupTime / slow;
                    Animator?.Play("windup");
                    Velocity = Vector2.Zero;
                }
                else
                {
                    Velocity = toPlayer.Normalized() * Speed * slow;
                    MoveAndSlide();
                    Animator?.Play("walk");
                }
                break;

            case State.Windup:
                Velocity = Vector2.Zero;
                _timer -= dt;
                if (_timer <= 0f)
                {
                    Animator?.Play("attack");
                    if (dist <= AttackReach) // gracz mógł odejść — atak do uniknięcia
                        Player.TakeDamage(ContactDamage);
                    _state = State.Recover;
                    _timer = 0.45f;
                }
                break;

            case State.Recover:
                Velocity = Vector2.Zero;
                _timer -= dt;
                if (_timer <= 0f) _state = State.Chase;
                break;
        }
    }
}

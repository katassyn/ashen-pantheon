using Godot;

/// <summary>Husk — goni gracza i rani w zwarciu (obrażenia kontaktowe z bazy).</summary>
public partial class Enemy : EnemyBase
{
    [Export] public float Speed = 95f;

    protected override Color BaseTint => new(0.85f, 0.3f, 0.3f);

    protected override void Behavior(float dt, Vector2 toPlayer, float dist)
    {
        float slow = IsChilled ? 0.5f : 1f;
        if (dist > ContactRange - 4f)
        {
            Velocity = toPlayer.Normalized() * Speed * slow;
            MoveAndSlide();
        }
        else
        {
            Velocity = Vector2.Zero;
        }
    }
}

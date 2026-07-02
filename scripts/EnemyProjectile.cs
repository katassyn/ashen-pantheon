using Godot;

/// <summary>Pocisk wroga: replikowany na wszystkie maszyny (deterministyczny lot),
/// każda maszyna sprawdza trafienie tylko SWOJEGO gracza (jak telegrafy).</summary>
public partial class EnemyProjectile : Node2D
{
    public Vector2 Direction = Vector2.Right;
    public float Speed = 320f;
    public float Damage = 10f;

    private float _life = 2.5f;
    private const float HitRadius = 18f;

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Position += Direction * Speed * dt;
        _life -= dt;
        if (_life <= 0f) { QueueFree(); return; }

        var p = PlayerController.Local;
        if (p != null && IsInstanceValid(p) && !p.Dead && !p.IsInvulnerable
            && GlobalPosition.DistanceTo(p.GlobalPosition) <= HitRadius)
        {
            p.TakeDamage(Damage);
            QueueFree();
        }
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 6f, new Color(0.9f, 0.4f, 0.9f));
        DrawCircle(Vector2.Zero, 3f, new Color(1f, 0.8f, 1f));
    }
}

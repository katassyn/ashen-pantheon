using Godot;
using AshenPantheon.Core;

/// <summary>Mina: uzbraja się, po wejściu wroga wybucha — dmg + stun + oznaczenie w promieniu.</summary>
public partial class Mine : Node2D
{
    private ResolvedSkill _skill;
    private const float ArmDelay = 0.4f;
    private const float TriggerRadius = 55f;
    private const float ExplodeRadius = 120f;
    private const float Lifetime = 12f;

    private float _t;
    private bool _armed;

    public void Setup(ResolvedSkill skill) => _skill = skill;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        QueueRedraw();

        if (!_armed)
        {
            if (_t >= ArmDelay) _armed = true;
            return;
        }

        foreach (var e in EnemyBase.All(GetTree()))
            if (GlobalPosition.DistanceTo(e.GlobalPosition) <= TriggerRadius)
            {
                Explode();
                return;
            }

        if (_t >= Lifetime) QueueFree();
    }

    private void Explode()
    {
        foreach (var e in EnemyBase.All(GetTree()))
            if (GlobalPosition.DistanceTo(e.GlobalPosition) <= ExplodeRadius)
                e.ReceiveHit(_skill);
        QueueFree();
    }

    public override void _Draw()
    {
        Color c = _armed ? new Color(1f, 0.3f, 0.2f) : new Color(0.7f, 0.7f, 0.3f);
        DrawCircle(Vector2.Zero, 10f, c);
        DrawArc(Vector2.Zero, TriggerRadius, 0f, Mathf.Tau, 32, new Color(1f, 0.3f, 0.2f, 0.35f), 1.5f);
    }
}

using Godot;
using AshenPantheon.Core;

/// <summary>Deszcz strzał: po chwili uderza w obszar, potem zostaje pole spowolnienia.</summary>
public partial class GroundZone : Node2D
{
    private ResolvedSkill _skill;
    private float _radius = 120f;
    private float _t;
    private bool _armed;
    private float _slowTick;
    private const float ArmDelay = 0.5f;
    private const float Lifetime = 3.5f;

    public void Setup(ResolvedSkill skill, float radius)
    {
        _skill = skill;
        _radius = radius;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        QueueRedraw();

        if (!_armed && _t >= ArmDelay)
        {
            _armed = true;
            foreach (var e in EnemyBase.All(GetTree()))
                if (Inside(e)) e.ReceiveHit(_skill);
        }

        if (_armed)
        {
            _slowTick -= dt;
            if (_slowTick <= 0f)
            {
                _slowTick = 0.4f;
                var slow = new ResolvedSkill { Id = "slow", Damage = 0f, Shape = SkillShape.Nova, OnHitStatus = StatusType.Chill, StatusDuration = 0.8f };
                foreach (var e in EnemyBase.All(GetTree()))
                    if (Inside(e)) e.ReceiveHit(slow);
            }
        }

        if (_t >= Lifetime) QueueFree();
    }

    private bool Inside(EnemyBase e) => GlobalPosition.DistanceTo(e.GlobalPosition) <= _radius;

    public override void _Draw()
    {
        float prog = Mathf.Clamp(_t / ArmDelay, 0f, 1f);
        Color fill = _armed ? new Color(0.4f, 0.7f, 1f, 0.16f) : new Color(1f, 0.3f, 0.2f, 0.1f + 0.3f * prog);
        Color line = _armed ? new Color(0.5f, 0.8f, 1f, 0.6f) : new Color(1f, 0.4f, 0.3f, 0.85f);
        DrawCircle(Vector2.Zero, _radius, fill);
        DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 48, line, 2f);
    }
}

using Godot;
using AshenPantheon.Core;

/// <summary>Deszcz strzał: po chwili uderza w obszar, potem zostaje pole spowolnienia.
/// Wariant Vharosa (rain_blood): krwawy deszcz leczy gracza stojącego w środku.</summary>
public partial class GroundZone : Node2D
{
    private ResolvedSkill _skill;
    private float _radius = 120f;
    private float _t;
    private bool _armed;
    private float _tick;
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
                if (Inside(e.GlobalPosition)) e.ReceiveHit(_skill);
        }

        if (_armed)
        {
            _tick -= dt;
            if (_tick <= 0f)
            {
                _tick = 0.4f;
                var slow = new ResolvedSkill
                {
                    Id = "slow", Damage = 0f, Shape = SkillShape.Nova,
                    OnHitStatus = StatusType.Chill,
                    StatusDuration = 0.8f * (_skill?.DurationMult ?? 1f),
                };
                foreach (var e in EnemyBase.All(GetTree()))
                    if (Inside(e.GlobalPosition)) e.ReceiveHit(slow);

                // krwawy deszcz: każda maszyna leczy SWOJEGO gracza stojącego w strefie (co-op synergy)
                if (_skill?.VariantTag == "rain_blood"
                    && PlayerController.Local is { } p && !p.Dead
                    && Inside(p.GlobalPosition))
                    p.Heal(4f);
            }
        }

        if (_t >= Lifetime) QueueFree();
    }

    private bool Inside(Vector2 pos) => GlobalPosition.DistanceTo(pos) <= _radius;

    public override void _Draw()
    {
        bool blood = _skill?.VariantTag == "rain_blood";
        float prog = Mathf.Clamp(_t / ArmDelay, 0f, 1f);
        Color fill = _armed
            ? (blood ? new Color(0.8f, 0.15f, 0.2f, 0.18f) : new Color(0.4f, 0.7f, 1f, 0.16f))
            : new Color(1f, 0.3f, 0.2f, 0.1f + 0.3f * prog);
        Color line = _armed
            ? (blood ? new Color(0.9f, 0.25f, 0.3f, 0.7f) : new Color(0.5f, 0.8f, 1f, 0.6f))
            : new Color(1f, 0.4f, 0.3f, 0.85f);
        DrawCircle(Vector2.Zero, _radius, fill);
        DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 48, line, 2f);
    }
}

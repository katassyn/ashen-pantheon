using Godot;
using AshenPantheon.Core;

/// <summary>Kolczasta przesieka: linia na ziemi, mocno spowalnia i rani wchodzących.
/// Wariant Vharosa (hedge_drain): drenuje — leczy gracza za każdego ranionego wroga.</summary>
public partial class HedgeZone : Node2D
{
    private ResolvedSkill _skill;
    private float _length = 340f;
    private float _halfWidth = 26f;
    private const float Lifetime = 4f;

    private float _t;
    private float _tick;

    public void Setup(ResolvedSkill skill, Vector2 direction, float length)
    {
        _skill = skill;
        _length = length;
        _halfWidth = 26f * (skill?.DurationMult ?? 1f); // węzeł hedge_wide poszerza
        Rotation = direction.Angle();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _t += dt;
        QueueRedraw();

        _tick -= dt;
        if (_tick <= 0f)
        {
            _tick = 0.4f;
            var hit = new ResolvedSkill
            {
                Id = "hedge", Damage = _skill.Damage, Shape = SkillShape.Line,
                OnHitStatus = _skill.OnHitStatus == StatusType.None ? StatusType.Chill : _skill.OnHitStatus,
                StatusDuration = _skill.OnHitStatus == StatusType.None ? 0.8f : _skill.StatusDuration,
                HealOnHit = _skill.VariantTag == "hedge_drain" ? _skill.HealOnHit : 0f,
            };
            foreach (var e in EnemyBase.All(GetTree()))
            {
                Vector2 p = ToLocal(e.GlobalPosition);
                if (p.X >= 0f && p.X <= _length && Mathf.Abs(p.Y) <= _halfWidth)
                    e.ReceiveHit(hit);
            }
        }

        if (_t >= Lifetime) QueueFree();
    }

    public override void _Draw()
    {
        bool drain = _skill?.VariantTag == "hedge_drain";
        var rect = new Rect2(new Vector2(0f, -_halfWidth), new Vector2(_length, _halfWidth * 2f));
        DrawRect(rect, drain ? new Color(0.7f, 0.2f, 0.25f, 0.22f) : new Color(0.4f, 0.7f, 0.3f, 0.22f));
        DrawRect(rect, drain ? new Color(0.9f, 0.3f, 0.35f, 0.7f) : new Color(0.5f, 0.9f, 0.4f, 0.7f), false, 2f);
    }
}

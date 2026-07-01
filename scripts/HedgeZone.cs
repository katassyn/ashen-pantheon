using Godot;
using AshenPantheon.Core;

/// <summary>Kolczasta przesieka: linia na ziemi, mocno spowalnia i zadaje dmg wchodzącym.</summary>
public partial class HedgeZone : Node2D
{
    private ResolvedSkill _skill;
    private float _length = 340f;
    private const float HalfWidth = 26f;
    private const float Lifetime = 4f;

    private float _t;
    private float _tick;

    public void Setup(ResolvedSkill skill, Vector2 direction, float length)
    {
        _skill = skill;
        _length = length;
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
            var hit = new ResolvedSkill { Id = "hedge", Damage = _skill.Damage, Shape = SkillShape.Line, OnHitStatus = StatusType.Chill, StatusDuration = 0.8f };
            foreach (var e in EnemyBase.All(GetTree()))
            {
                Vector2 p = ToLocal(e.GlobalPosition);
                if (p.X >= 0f && p.X <= _length && Mathf.Abs(p.Y) <= HalfWidth)
                    e.ReceiveHit(hit);
            }
        }

        if (_t >= Lifetime) QueueFree();
    }

    public override void _Draw()
    {
        var rect = new Rect2(new Vector2(0f, -HalfWidth), new Vector2(_length, HalfWidth * 2f));
        DrawRect(rect, new Color(0.4f, 0.7f, 0.3f, 0.22f));
        DrawRect(rect, new Color(0.5f, 0.9f, 0.4f, 0.7f), false, 2f);
    }
}

using Godot;
using AshenPantheon.Core;

/// <summary>Jastrząb: po chwili uderza — dmg + stun + Mark; ×3 dmg / ×2 stun na oznaczonym.
/// Wariant Vharosa (hawk_all): uderza WSZYSTKICH oznaczonych naraz.</summary>
public partial class Hawk : Node2D
{
    private ResolvedSkill _skill;
    private bool _strikeAllMarked;
    private const float Delay = 0.6f;
    private float _t;
    private bool _struck;

    public void Setup(ResolvedSkill skill, bool strikeAllMarked = false)
    {
        _skill = skill;
        _strikeAllMarked = strikeAllMarked;
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        QueueRedraw();

        if (!_struck && _t >= Delay)
        {
            _struck = true;
            Strike();
        }
        if (_t >= Delay + 0.3f) QueueFree();
    }

    private void Strike()
    {
        if (_strikeAllMarked)
        {
            bool any = false;
            foreach (var e in EnemyBase.All(GetTree()))
                if (e.IsMarked)
                {
                    any = true;
                    var hit = CloneSkill();
                    hit.StunDuration *= 2f;
                    e.ReceiveHit(hit);
                }
            if (any) return;
            // brak oznaczonych → uderz najbliższego normalnie
        }

        EnemyBase target = null;
        float best = float.MaxValue;
        foreach (var e in EnemyBase.All(GetTree()))
        {
            float d = GlobalPosition.DistanceTo(e.GlobalPosition);
            if (d < best) { best = d; target = e; }
        }
        if (target == null) return;

        GlobalPosition = target.GlobalPosition;
        var s = CloneSkill();
        if (target.IsMarked) s.StunDuration *= 2f;
        target.ReceiveHit(s);
    }

    private ResolvedSkill CloneSkill() => new()
    {
        Id = _skill.Id, Damage = _skill.Damage, Shape = _skill.Shape,
        AppliesMark = _skill.AppliesMark, MarkDuration = _skill.MarkDuration,
        MarkedMultiplier = _skill.MarkedMultiplier, StunDuration = _skill.StunDuration,
        HealOnHit = _skill.HealOnHit,
    };

    public override void _Draw()
    {
        var c = new Color(0.9f, 0.85f, 0.5f);
        var tri = new Vector2[] { new(-10f, -6f), new(10f, -6f), new(0f, 8f) };
        DrawColoredPolygon(tri, c);
    }
}

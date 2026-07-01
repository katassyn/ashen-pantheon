using Godot;
using AshenPantheon.Core;

/// <summary>Jastrząb: po chwili uderza w najbliższego wroga — dmg + stun + oznaczenie. Na oznaczonym ×3 dmg / ×2 stun.</summary>
public partial class Hawk : Node2D
{
    private ResolvedSkill _skill;
    private const float Delay = 0.6f;
    private float _t;
    private bool _struck;

    public void Setup(ResolvedSkill skill) => _skill = skill;

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
        EnemyBase target = null;
        float best = float.MaxValue;
        foreach (var e in EnemyBase.All(GetTree()))
        {
            float d = GlobalPosition.DistanceTo(e.GlobalPosition);
            if (d < best) { best = d; target = e; }
        }
        if (target == null) return;

        GlobalPosition = target.GlobalPosition;
        if (target.IsMarked) _skill.StunDuration *= 2f; // ×2 stun (dmg ×3 przez MarkedMultiplier w CombatResolver)
        target.ReceiveHit(_skill);
    }

    public override void _Draw()
    {
        var c = new Color(0.9f, 0.85f, 0.5f);
        var tri = new Vector2[] { new(-10f, -6f), new(10f, -6f), new(0f, 8f) };
        DrawColoredPolygon(tri, c);
    }
}

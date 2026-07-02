using Godot;

/// <summary>WIELKI jastrząb-pet (Dzikie Ostępy): walczy u boku gracza w zwarciu przez określony czas.</summary>
public partial class Pet : Node2D
{
    [Export] public float Speed = 260f;
    [Export] public float AttackRange = 34f;
    [Export] public float AttackCooldown = 0.8f;
    [Export] public float Lifetime = 12f;

    public float Damage = 20f;
    public int CasterPeer = 1;

    private float _life;
    private float _atkCd;

    public override void _Ready() => _life = Lifetime;

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _life -= dt;
        if (_life <= 0f) { QueueFree(); return; }
        if (_atkCd > 0f) _atkCd -= dt;

        EnemyBase target = null;
        float best = float.MaxValue;
        foreach (var e in EnemyBase.All(GetTree()))
        {
            float d = GlobalPosition.DistanceTo(e.GlobalPosition);
            if (d < best) { best = d; target = e; }
        }

        if (target == null)
        {
            // wracaj do właściciela (node gracza nazwany peer-id)
            var owner = GetTree().CurrentScene?.GetNodeOrNull<PlayerController>($"Players/{CasterPeer}");
            if (owner != null)
            {
                Vector2 to = owner.GlobalPosition - GlobalPosition;
                if (to.Length() > 60f) GlobalPosition += to.Normalized() * Speed * dt;
            }
            return;
        }

        Vector2 toTarget = target.GlobalPosition - GlobalPosition;
        if (toTarget.Length() > AttackRange)
        {
            GlobalPosition += toTarget.Normalized() * Speed * dt;
        }
        else if (_atkCd <= 0f)
        {
            _atkCd = AttackCooldown;
            target.ReceiveHit(new AshenPantheon.Core.ResolvedSkill
            {
                Id = "pet", Damage = Damage, Shape = AshenPantheon.Core.SkillShape.SingleTarget
            });
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var c = new Color(0.85f, 0.75f, 0.35f);
        var tri = new Vector2[] { new(-14f, -9f), new(14f, -9f), new(0f, 12f) };
        DrawColoredPolygon(tri, c);
        DrawArc(Vector2.Zero, 16f, 0f, Mathf.Tau, 20, new Color(0.9f, 0.85f, 0.5f, 0.5f), 1.5f);
    }
}

using Godot;
using AshenPantheon.Core;

public partial class PlayerController : CharacterBody2D
{
    [Export] public float Speed = 220f;
    [Export] public float ArriveDistance = 6f;

    [Export] public float DashSpeed = 700f;
    [Export] public float DashDuration = 0.15f;
    [Export] public float DashCooldown = 0.8f;
    [Export] public float IFrameDuration = 0.2f;

    [Export] public float MaxHealth = 100f;
    public float Health { get; private set; }

    public God ActiveGod = GodCatalog.Pyr;

    private Vector2 _targetPosition;
    private bool _hasTarget;

    private float _dashTimeLeft;
    private float _dashCdLeft;
    private float _iFrameLeft;
    private Vector2 _dashDirection;

    private float _strikeCd;
    private float _boltCd;

    private PackedScene _projectileScene;

    public bool IsInvulnerable => _iFrameLeft > 0f;

    public override void _Ready()
    {
        _targetPosition = GlobalPosition;
        _projectileScene = GD.Load<PackedScene>("res://scenes/Projectile.tscn");
        Health = MaxHealth;
    }

    /// <summary>Obrażenia od wroga. Dash daje i-frames (nietykalność).</summary>
    public void TakeDamage(float amount)
    {
        if (IsInvulnerable) return;
        Health -= amount;
        if (Health <= 0f)
        {
            Health = 0f;
            GetTree().ReloadCurrentScene();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("move_click"))
        {
            _targetPosition = GetGlobalMousePosition();
            _hasTarget = true;
        }

        if (@event.IsActionPressed("dash") && _dashCdLeft <= 0f && _dashTimeLeft <= 0f)
        {
            Vector2 dir = (GetGlobalMousePosition() - GlobalPosition).Normalized();
            if (dir == Vector2.Zero) dir = Vector2.Down;
            _dashDirection = dir;
            _dashTimeLeft = DashDuration;
            _iFrameLeft = IFrameDuration;
            _dashCdLeft = DashCooldown;
            _hasTarget = false;
        }

        if (@event.IsActionPressed("skill_q") && _strikeCd <= 0f)
        {
            CastStrike();
            _strikeCd = GodCatalog.Strike.Cooldown;
        }
        if (@event.IsActionPressed("skill_w") && _boltCd <= 0f)
        {
            CastBolt();
            _boltCd = GodCatalog.Bolt.Cooldown;
        }

        // Przełączanie boga na klawiszach 1/2 (czytane bezpośrednio, bez input map)
        if (@event is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.PhysicalKeycode == Key.Key1) ActiveGod = GodCatalog.Pyr;
            else if (k.PhysicalKeycode == Key.Key2) ActiveGod = GodCatalog.Vael;
        }
    }

    private Vector2 AimDirection()
    {
        Vector2 dir = GetGlobalMousePosition() - GlobalPosition;
        return dir == Vector2.Zero ? Vector2.Right : dir.Normalized();
    }

    private static Color ElementTint(ResolvedSkill skill, float alpha) =>
        skill.OnHitStatus switch
        {
            StatusType.Burn => new Color(1f, 0.5f, 0.2f, alpha),
            StatusType.Chill => new Color(0.4f, 0.8f, 1f, alpha),
            _ => new Color(1f, 1f, 1f, alpha)
        };

    private void CastBolt()
    {
        Vector2 dir = AimDirection();
        ResolvedSkill resolved = GodModifierSystem.Resolve(GodCatalog.Bolt, ActiveGod);

        var proj = _projectileScene.Instantiate<Projectile>();
        proj.Setup(resolved, dir);
        GetParent().AddChild(proj);
        proj.GlobalPosition = GlobalPosition + dir * 20f;
    }

    private void CastStrike()
    {
        Vector2 dir = AimDirection();
        ResolvedSkill resolved = GodModifierSystem.Resolve(GodCatalog.Strike, ActiveGod);

        bool cone = resolved.Shape == SkillShape.Cone;
        float range = 95f;
        float halfAngleDeg = cone ? 65f : 28f;
        float halfAngleRad = Mathf.DegToRad(halfAngleDeg);

        foreach (Node node in GetTree().GetNodesInGroup("hittable"))
        {
            if (node is Node2D n && n is IHittable target)
            {
                Vector2 to = n.GlobalPosition - GlobalPosition;
                if (to.Length() <= range && Mathf.Abs(dir.AngleTo(to.Normalized())) <= halfAngleRad)
                    target.ReceiveHit(resolved);
            }
        }

        var arc = new MeleeArc();
        GetParent().AddChild(arc);
        arc.GlobalPosition = GlobalPosition;
        arc.Setup(range, halfAngleDeg, ElementTint(resolved, 0.5f), dir.Angle());
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_dashCdLeft > 0f) _dashCdLeft -= dt;
        if (_iFrameLeft > 0f) _iFrameLeft -= dt;
        if (_strikeCd > 0f) _strikeCd -= dt;
        if (_boltCd > 0f) _boltCd -= dt;

        if (_dashTimeLeft > 0f)
        {
            _dashTimeLeft -= dt;
            Velocity = _dashDirection * DashSpeed;
            MoveAndSlide();
            return;
        }

        if (_hasTarget && GlobalPosition.DistanceTo(_targetPosition) > ArriveDistance)
        {
            Vector2 direction = (_targetPosition - GlobalPosition).Normalized();
            Velocity = direction * Speed;
        }
        else
        {
            Velocity = Vector2.Zero;
            _hasTarget = false;
        }

        MoveAndSlide();
    }
}

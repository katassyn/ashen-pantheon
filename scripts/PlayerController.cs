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

    public God ActiveGod = GodCatalog.Pyr;

    private Vector2 _targetPosition;
    private bool _hasTarget;

    private float _dashTimeLeft;
    private float _dashCdLeft;
    private float _iFrameLeft;
    private Vector2 _dashDirection;

    public bool IsInvulnerable => _iFrameLeft > 0f;

    public override void _Ready()
    {
        _targetPosition = GlobalPosition;
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

        if (@event.IsActionPressed("skill_q")) CastOnNearestDummy(GodCatalog.Strike);
        if (@event.IsActionPressed("skill_w")) CastOnNearestDummy(GodCatalog.Bolt);

        // Przełączanie boga na klawiszach 1/2 (czytane bezpośrednio, bez input map)
        if (@event is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.PhysicalKeycode == Key.Key1) ActiveGod = GodCatalog.Pyr;
            else if (k.PhysicalKeycode == Key.Key2) ActiveGod = GodCatalog.Vael;
        }
    }

    private void CastOnNearestDummy(SkillDefinition def)
    {
        ResolvedSkill resolved = GodModifierSystem.Resolve(def, ActiveGod);

        Dummy nearest = null;
        float best = float.MaxValue;
        foreach (Node node in GetTree().GetNodesInGroup("dummies"))
        {
            if (node is Dummy d)
            {
                float dist = GlobalPosition.DistanceTo(d.GlobalPosition);
                if (dist < best) { best = dist; nearest = d; }
            }
        }
        nearest?.ReceiveHit(resolved);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_dashCdLeft > 0f) _dashCdLeft -= dt;
        if (_iFrameLeft > 0f) _iFrameLeft -= dt;

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

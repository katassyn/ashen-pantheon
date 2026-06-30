using System.Collections.Generic;
using Godot;
using AshenPantheon.Core;

public partial class Projectile : Area2D
{
    [Export] public float Speed = 520f;
    [Export] public float Lifetime = 1.5f;
    [Export] public float ExplodeRadius = 110f;

    private ResolvedSkill _skill;
    private Vector2 _direction = Vector2.Right;
    private float _life;
    private Sprite2D _sprite;
    private readonly HashSet<ulong> _hit = new();

    public void Setup(ResolvedSkill skill, Vector2 direction)
    {
        _skill = skill;
        _direction = direction.Normalized();
        Rotation = _direction.Angle();
    }

    public override void _Ready()
    {
        _sprite = GetNode<Sprite2D>("Sprite2D");
        TintByElement();
        AreaEntered += OnAreaEntered;
        _life = Lifetime;
    }

    private void TintByElement()
    {
        if (_sprite == null || _skill == null) return;
        _sprite.Modulate = _skill.OnHitStatus switch
        {
            StatusType.Burn => new Color(1f, 0.5f, 0.2f),
            StatusType.Chill => new Color(0.4f, 0.8f, 1f),
            _ => Colors.White
        };
    }

    public override void _PhysicsProcess(double delta)
    {
        Position += _direction * Speed * (float)delta;
        _life -= (float)delta;
        if (_life <= 0f) QueueFree();
    }

    private void OnAreaEntered(Area2D area)
    {
        if (area is not Dummy dummy) return;
        if (_hit.Contains(area.GetInstanceId())) return;
        _hit.Add(area.GetInstanceId());

        dummy.ReceiveHit(_skill);

        if (_skill.Explodes)
        {
            ExplodeAround();
            QueueFree();
        }
        else if (!_skill.Pierces)
        {
            QueueFree();
        }
        // Pierces → leci dalej i może trafić kolejne cele
    }

    private void ExplodeAround()
    {
        foreach (Node node in GetTree().GetNodesInGroup("dummies"))
        {
            if (node is Dummy d && !_hit.Contains(d.GetInstanceId())
                && GlobalPosition.DistanceTo(d.GlobalPosition) <= ExplodeRadius)
            {
                _hit.Add(d.GetInstanceId());
                d.ReceiveHit(_skill);
            }
        }
    }
}

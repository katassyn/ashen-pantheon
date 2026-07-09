using System.Collections.Generic;
using Godot;
using AshenPantheon.Core;

public partial class Projectile : Area2D
{
    [Export] public float Speed = 560f;
    [Export] public float Lifetime = 1.5f;
    [Export] public float ExplodeRadius = 110f;

    private ResolvedSkill _skill;
    private Vector2 _direction = Vector2.Right;
    private Color _tint = Colors.White;
    private float _life;
    private Sprite2D _sprite;
    private readonly HashSet<ulong> _hit = new();

    public void Setup(ResolvedSkill skill, Vector2 direction, Color tint)
    {
        _skill = skill;
        _direction = direction.Normalized();
        _tint = tint;
        Rotation = _direction.Angle();
    }

    public override void _Ready()
    {
        _sprite = GetNode<Sprite2D>("Sprite2D");
        if (_sprite != null) _sprite.Modulate = _tint;
        AreaEntered += TryHit;
        BodyEntered += TryHit;
        _life = Lifetime;
    }

    public override void _PhysicsProcess(double delta)
    {
        Position += _direction * Speed * (float)delta;
        _life -= (float)delta;
        if (_life <= 0f) QueueFree();
    }

    private void TryHit(Node node)
    {
        // ściana/przeszkoda (StaticBody na warstwie terenu 4) — pocisk pęka o mur
        if (node is StaticBody2D)
        {
            if (_skill.Explodes) ExplodeAround();
            QueueFree();
            return;
        }
        if (node is not IHittable target) return;
        if (_hit.Contains(node.GetInstanceId())) return;
        _hit.Add(node.GetInstanceId());

        bool wasMarked = node is EnemyBase eb && eb.IsMarked;

        target.ReceiveHit(_skill);

        if (_skill.Explodes)
        {
            ExplodeAround();
            QueueFree();
        }
        else if (_skill.PierceMarkedOnly)
        {
            // Egzekutor: przebija tylko dopóki trafiał oznaczonych; na nieoznaczonym się zatrzymuje
            if (!wasMarked) QueueFree();
        }
        else if (!_skill.Pierces)
        {
            QueueFree();
        }
    }

    private void ExplodeAround()
    {
        foreach (Node node in GetTree().GetNodesInGroup("hittable"))
        {
            if (node is Node2D n && n is IHittable target && !_hit.Contains(n.GetInstanceId())
                && GlobalPosition.DistanceTo(n.GlobalPosition) <= ExplodeRadius)
            {
                _hit.Add(n.GetInstanceId());
                target.ReceiveHit(_skill);
            }
        }
    }
}

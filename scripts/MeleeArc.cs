using Godot;

/// <summary>Czysto wizualny łuk ataku w zwarciu (hit liczy PlayerController). Znika po chwili.</summary>
public partial class MeleeArc : Node2D
{
    private float _radius = 90f;
    private float _halfAngle = Mathf.DegToRad(30f);
    private Color _tint = new(1f, 1f, 1f, 0.5f);
    private float _life = 0.15f;
    private const float MaxLife = 0.15f;

    public void Setup(float radius, float halfAngleDeg, Color tint, float facing)
    {
        _radius = radius;
        _halfAngle = Mathf.DegToRad(halfAngleDeg);
        _tint = tint;
        Rotation = facing;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _life -= (float)delta;
        Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_life / MaxLife, 0f, 1f));
        if (_life <= 0f) QueueFree();
    }

    public override void _Draw()
    {
        const int steps = 16;
        var pts = new Vector2[steps + 2];
        pts[0] = Vector2.Zero;
        for (int i = 0; i <= steps; i++)
        {
            float a = -_halfAngle + (2f * _halfAngle) * i / steps;
            pts[i + 1] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _radius;
        }
        DrawColoredPolygon(pts, _tint);
    }
}

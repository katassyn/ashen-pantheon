using Godot;

public enum TelegraphShape { Circle, Cone, Line }

/// <summary>Ostrzeżenie ataku rysowane na ziemi. Po windupie zadaje obrażenia graczowi, jeśli stoi w kształcie.</summary>
public partial class Telegraph : Node2D
{
    public TelegraphShape Shape = TelegraphShape.Circle;
    public float Radius = 90f;        // circle: promień · cone: długość · line: długość
    public float HalfAngleDeg = 35f;  // cone
    public float HalfWidth = 26f;     // line
    public float Windup = 0.9f;
    public float Damage = 24f;
    public AshenPantheon.Core.DamageType DamageType = AshenPantheon.Core.DamageType.Physical;

    private float _t;
    private bool _resolved;

    public override void _Process(double delta)
    {
        _t += (float)delta;
        QueueRedraw();

        if (!_resolved && _t >= Windup)
        {
            _resolved = true;
            Resolve();
        }
        if (_t >= Windup + 0.12f)
            QueueFree();
    }

    private void Resolve()
    {
        // multiplayer: każda maszyna sprawdza tylko SWOJEGO gracza (telegraf jest zreplikowany wszędzie)
        var player = PlayerController.Local;
        if (player == null || !IsInstanceValid(player) || player.Dead) return;

        Vector2 local = ToLocal(player.GlobalPosition);
        if (HitsLocal(local))
            player.TakeDamage(Damage, DamageType);
    }

    private bool HitsLocal(Vector2 p)
    {
        return Shape switch
        {
            TelegraphShape.Circle => p.Length() <= Radius,
            TelegraphShape.Cone => p.Length() <= Radius &&
                Mathf.Abs(new Vector2(1f, 0f).AngleTo(p == Vector2.Zero ? Vector2.Right : p.Normalized())) <= Mathf.DegToRad(HalfAngleDeg),
            TelegraphShape.Line => p.X >= 0f && p.X <= Radius && Mathf.Abs(p.Y) <= HalfWidth,
            _ => false
        };
    }

    public override void _Draw()
    {
        float prog = Mathf.Clamp(_t / Windup, 0f, 1f);
        Color fill = _resolved
            ? new Color(1f, 0.9f, 0.3f, 0.6f)                     // błysk uderzenia
            : new Color(1f, 0.2f, 0.15f, 0.12f + 0.4f * prog);    // narastające ostrzeżenie
        Color outline = new Color(1f, 0.3f, 0.2f, 0.95f);

        switch (Shape)
        {
            case TelegraphShape.Circle:
                DrawCircle(Vector2.Zero, Radius, fill);
                DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, 48, outline, 2f);
                break;
            case TelegraphShape.Cone:
                DrawCone(fill, outline);
                break;
            case TelegraphShape.Line:
                var rect = new Rect2(new Vector2(0f, -HalfWidth), new Vector2(Radius, HalfWidth * 2f));
                DrawRect(rect, fill);
                DrawRect(rect, outline, false, 2f);
                break;
        }
    }

    private void DrawCone(Color fill, Color outline)
    {
        const int steps = 20;
        float ha = Mathf.DegToRad(HalfAngleDeg);
        var pts = new Vector2[steps + 2];
        pts[0] = Vector2.Zero;
        for (int i = 0; i <= steps; i++)
        {
            float a = -ha + 2f * ha * i / steps;
            pts[i + 1] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius;
        }
        DrawColoredPolygon(pts, fill);
        DrawPolyline(pts, outline, 2f);
    }
}

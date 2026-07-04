using Godot;

/// <summary>Radar centrowany na lokalnym graczu: sojusznicy, wrogowie, waystone, portale, cele questowe.
/// Dwa warianty (Hud): mały stały w rogu + duży pod TAB (podąża za graczem).</summary>
public partial class MinimapView : Control
{
    /// <summary>Ile jednostek świata od środka do krawędzi radaru.</summary>
    public float WorldRadius = 1400f;
    /// <summary>Duża mapa pod TAB (toggle), false = stała rogowa.</summary>
    public bool Toggleable;
    /// <summary>Rogowa minimapa — chowana, gdy duża jest otwarta (nigdy dwie naraz).</summary>
    public MinimapView Corner;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        if (Toggleable) Visible = false;
    }

    public override void _Process(double delta) { if (Visible) QueueRedraw(); }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Toggleable) return;
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.Tab)
        {
            Visible = !Visible;                             // TAB = toggle
            if (Corner != null) Corner.Visible = !Visible;  // duża zastępuje rogową
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Draw()
    {
        float r = Size.X * 0.5f;
        var c = new Vector2(r, r);
        float scale = r / WorldRadius;

        // tło
        DrawCircle(c, r, new Color(0.04f, 0.05f, 0.08f, Toggleable ? 0.82f : 0.6f));
        DrawArc(c, r, 0, Mathf.Tau, 64, new Color(0.4f, 0.5f, 0.7f, 0.7f), 2f);

        var player = PlayerController.Local;
        if (player == null || !IsInstanceValid(player)) return;
        Vector2 origin = player.GlobalPosition;

        DotGroup("minimap_waystone", origin, c, scale, r, new Color(0.4f, 0.75f, 1f), 4.5f, diamond: true);
        DotGroup("minimap_portal", origin, c, scale, r, new Color(0.5f, 0.9f, 0.5f), 5f, diamond: true);
        DotGroup("minimap_objective", origin, c, scale, r, new Color(1f, 0.85f, 0.4f), 4.5f, diamond: true);
        DotGroup("enemies", origin, c, scale, r, new Color(0.9f, 0.3f, 0.3f), 3f);

        // gracze (sojusznicy niebiescy), lokalny na końcu jako biała strzałka w centrum
        foreach (Node n in GetTree().GetNodesInGroup("players"))
            if (n is PlayerController p && IsInstanceValid(p) && p != player)
                Dot(p.GlobalPosition, origin, c, scale, r, new Color(0.5f, 0.8f, 1f), 4f);

        DrawCircle(c, 4.5f, Colors.White);
    }

    private void DotGroup(string group, Vector2 origin, Vector2 c, float scale, float r, Color col, float size, bool diamond = false)
    {
        foreach (Node n in GetTree().GetNodesInGroup(group))
            if (n is Node2D nd && IsInstanceValid(nd))
                Dot(nd.GlobalPosition, origin, c, scale, r, col, size, diamond);
    }

    private void Dot(Vector2 world, Vector2 origin, Vector2 c, float scale, float r, Color col, float size, bool diamond = false)
    {
        Vector2 rel = (world - origin) * scale;
        if (rel.Length() > r - size) rel = rel.Normalized() * (r - size); // clamp na krawędź
        Vector2 p = c + rel;
        if (diamond)
        {
            var pts = new[] { p + new Vector2(0, -size), p + new Vector2(size, 0), p + new Vector2(0, size), p + new Vector2(-size, 0) };
            DrawColoredPolygonSafe(pts, col);
        }
        else DrawCircle(p, size, col);
    }

    private void DrawColoredPolygonSafe(Vector2[] pts, Color col) => DrawColoredPolygon(pts, col);
}

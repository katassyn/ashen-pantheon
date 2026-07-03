using Godot;

/// <summary>Waystone: fizyczny punkt fast-travel (miasto + każda strefa). Wejście + E = mapa świata.</summary>
public partial class Waystone : Area2D
{
    private bool _inside;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 70f } });
        AddChild(new Label { Text = "◆ Waystone [E]", Position = new Vector2(-60, -60), Modulate = new Color(0.55f, 0.85f, 1f) });
        BodyEntered += b => { if (b is PlayerController p && p.IsMultiplayerAuthority()) _inside = true; };
        BodyExited += b => { if (b is PlayerController p && p.IsMultiplayerAuthority()) _inside = false; };
        ZIndex = 3;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_inside) return;
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.E)
        {
            WorldMapPanel.Toggle(GetTree());
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 16f, new Color(0.3f, 0.55f, 0.85f));
        DrawArc(Vector2.Zero, 22f, 0, Mathf.Tau, 32, new Color(0.5f, 0.8f, 1f, 0.6f), 2f);
    }
}

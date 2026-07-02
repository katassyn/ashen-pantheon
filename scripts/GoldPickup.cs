using Godot;

/// <summary>Złoto na ziemi (instancjonowane per-gracz jak itemy). Wejście = do portfela.</summary>
public partial class GoldPickup : Area2D
{
    public long Amount = 5;
    private bool _taken;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1;
        var shape = new CollisionShape2D { Shape = new CircleShape2D { Radius = 18f } };
        AddChild(shape);
        var label = new Label
        {
            Text = $"{Amount}g",
            Position = new Vector2(-16f, -34f),
        };
        label.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.25f));
        AddChild(label);
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_taken || body is not PlayerController p || !p.IsMultiplayerAuthority()) return;
        _taken = true;
        GameState.Wallet.Gold += Amount;
        GameState.Save();
        QueueFree();
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 7f, new Color(1f, 0.8f, 0.2f));
        DrawCircle(Vector2.Zero, 4f, new Color(1f, 0.95f, 0.55f));
    }
}

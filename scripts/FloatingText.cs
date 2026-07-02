using Godot;

/// <summary>Unoszący się napis (liczby obrażeń, MISS, heal). Krótki, wygasa sam.</summary>
public partial class FloatingText : Node2D
{
    private string _text = "";
    private Color _color = Colors.White;
    private int _size = 14;
    private float _life = 0.75f;
    private const float MaxLife = 0.75f;
    private Vector2 _drift;

    public static void Spawn(Node parent, Vector2 globalPos, string text, Color color, int size = 14)
    {
        if (parent == null) return;
        var ft = new FloatingText
        {
            _text = text, _color = color, _size = size,
            _drift = new Vector2((float)GD.RandRange(-18.0, 18.0), -55f),
        };
        parent.AddChild(ft);
        ft.GlobalPosition = globalPos + new Vector2((float)GD.RandRange(-8.0, 8.0), -20f);
        ft.ZIndex = 200;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _life -= dt;
        Position += _drift * dt;
        Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(_life / (MaxLife * 0.6f), 0f, 1f));
        if (_life <= 0f) QueueFree();
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawString(ThemeDB.FallbackFont, Vector2.Zero, _text,
            HorizontalAlignment.Center, -1, _size, _color);
    }
}

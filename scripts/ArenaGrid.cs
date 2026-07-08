using Godot;

/// <summary>Tymczasowe tło M0: siatka statycznych znaczników, by widać było ruch postaci.</summary>
public partial class ArenaGrid : Node2D
{
    [Export] public int HalfCount = 4;
    [Export] public float Spacing = 160f;

    public override void _Ready()
    {
        var tex = GD.Load<Texture2D>("res://icon.svg");
        for (int x = -HalfCount; x <= HalfCount; x++)
        for (int y = -HalfCount; y <= HalfCount; y++)
        {
            var s = new Sprite2D
            {
                Texture = tex,
                Position = new Vector2(x * Spacing, y * Spacing),
                Scale = new Vector2(0.1f, 0.1f),
                Modulate = new Color(0.35f, 0.30f, 0.5f, 0.7f)
            };
            AddChild(s);
        }
    }
}

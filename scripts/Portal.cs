using Godot;

/// <summary>Wejście do innej sceny: gdy gracz wejdzie w obszar, ładuje TargetScene.</summary>
public partial class Portal : Area2D
{
    [Export] public string TargetScene = "res://scenes/Arena.tscn";

    private bool _used;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_used || body is not PlayerController) return;
        _used = true;
        CallDeferred(nameof(Go));
    }

    private void Go()
    {
        GetTree().ChangeSceneToFile(TargetScene);
    }
}

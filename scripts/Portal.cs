using Godot;

/// <summary>Portal do runu. Multiplayer: wejście GRACZA-HOSTA zabiera całą drużynę (wspólny seed).</summary>
public partial class Portal : Area2D
{
    [Export] public string TargetScene = "res://scenes/Arena.tscn";
    /// <summary>Id strefy mapy świata (dla WorldZone.tscn).</summary>
    [Export] public string TargetZone = "";

    private bool _used;

    public override void _Ready()
    {
        AddToGroup("minimap_portal");
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_used || !Net.IsServer) return;
        if (body is not PlayerController pc || pc.GetMultiplayerAuthority() != 1) return;
        _used = true;
        CallDeferred(nameof(Go));
    }

    private void Go()
    {
        int seed = (int)(GD.Randi() % int.MaxValue);
        if (seed == 0) seed = 1;
        Net.TravelAll(TargetScene, seed, TargetZone);
    }
}

using Godot;
using AshenPantheon.Core;

/// <summary>Leżący na ziemi drop. Gracz wchodzi → zakłada item (auto) i przelicza postać.</summary>
public partial class ItemPickup : Area2D
{
    public Item Item;
    private bool _taken;

    public static void SpawnRandom(Node parent, Vector2 globalPos)
    {
        var scene = GD.Load<PackedScene>("res://scenes/ItemPickup.tscn");
        var pickup = scene.Instantiate<ItemPickup>();
        pickup.Item = LootFactory.Random();
        pickup.Position = globalPos;
        parent.AddChild(pickup);
    }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        var label = GetNodeOrNull<Label>("Label");
        if (label != null && Item != null) label.Text = Item.Name;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_taken || body is not PlayerController player) return;
        _taken = true;
        player.PickUp(Item);
        GD.Print($"Podniesiono: {Item.Name}");
        QueueFree();
    }
}

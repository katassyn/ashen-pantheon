using Godot;
using AshenPantheon.Core;

/// <summary>Drop na ziemi: kolor wg rzadkości, nazwa nad itemem. Wejście = do plecaka (tetris).</summary>
public partial class ItemPickup : Area2D
{
    public Item Item;
    private bool _taken;

    public static Color RarityColor(Rarity r) => r switch
    {
        Rarity.Normal => new Color(0.85f, 0.85f, 0.85f),
        Rarity.Magic => new Color(0.35f, 0.55f, 1f),
        Rarity.Rare => new Color(1f, 0.85f, 0.25f),
        Rarity.Legendary => new Color(1f, 0.5f, 0.15f),
        Rarity.Unique => new Color(0.75f, 0.3f, 0.9f),
        Rarity.Mythic => new Color(1f, 0.15f, 0.3f),
        _ => Colors.White
    };

    public static void Spawn(Node parent, Vector2 globalPos, Item item)
    {
        var scene = GD.Load<PackedScene>("res://scenes/ItemPickup.tscn");
        var pickup = scene.Instantiate<ItemPickup>();
        pickup.Item = item;
        pickup.Position = globalPos;
        parent.AddChild(pickup);
    }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        var label = GetNodeOrNull<Label>("Label");
        if (Item != null)
        {
            var color = RarityColor(Item.Rarity);
            if (sprite != null) sprite.Modulate = color;
            if (label != null)
            {
                label.Text = Item.Name;
                label.AddThemeColorOverride("font_color", color);
            }
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        // loot jest instancjonowany: podnosi tylko LOKALNY gracz (puppet sojusznika nie zbiera twoich dropów)
        if (_taken || body is not PlayerController player || !player.IsMultiplayerAuthority()) return;
        if (!GameState.Bag.TryAutoPlace(Item))
        {
            GD.Print("Bag full!");
            return; // item zostaje na ziemi
        }
        _taken = true;
        GameState.Save();
        QueueFree();
    }
}

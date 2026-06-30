using Godot;

public partial class Hud : CanvasLayer
{
    private PlayerController _player;
    private Label _label;

    public override void _Ready()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        _label = GetNode<Label>("%Info");
    }

    public override void _Process(double delta)
    {
        if (_player == null || _label == null) return;
        _label.Text =
            $"HP: {_player.Health:0} / {_player.MaxHealth:0}     Bóg: {_player.ActiveGod.Name}\n" +
            "[1] Pyr - ogień     [2] Vael - mróz\n" +
            "[Q] Strike   [W] Bolt   [Spacja] dash";
    }
}

using Godot;
using AshenPantheon.Core;

public partial class GodSelect : CanvasLayer
{
    private PlayerController _player;

    public override void _Ready()
    {
        _player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        GetNode<Button>("%PyrButton").Pressed += () => SelectGod(GodCatalog.Pyr, "Pyr (ogień)");
        GetNode<Button>("%VaelButton").Pressed += () => SelectGod(GodCatalog.Vael, "Vael (mróz)");
    }

    private void SelectGod(God god, string label)
    {
        if (_player != null) _player.ActiveGod = god;
        GetNode<Label>("%CurrentGod").Text = $"Bóg: {label}";
        GD.Print($"Wybrano boga: {god.Name}");
    }
}

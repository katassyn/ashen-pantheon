using Godot;

public partial class Hud : CanvasLayer
{
	private PlayerController _player;
	private ArenaManager _arena;
	private Label _info;
	private Label _center;

	public override void _Ready()
	{
		_info = GetNode<Label>("%Info");
		_center = GetNode<Label>("%Center");
	}

	public override void _Process(double delta)
	{
		_player ??= GetTree().GetFirstNodeInGroup("player") as PlayerController;
		_arena ??= GetTree().GetFirstNodeInGroup("arena") as ArenaManager;

		if (_info != null && _player != null)
		{
			string wave = _arena != null ? _arena.TopStatus : "";
			string god = _player.GodActive ? $"{_player.GodName} [G: wł]" : "— [G: wył]";
			_info.Text =
				$"HP {_player.Health:0}/{_player.MaxHealth:0}    Koncentracja {_player.Concentration:0}/{_player.MaxConcentration:0}    Bóg: {god}    {wave}\n" +
				"WASD ruch · mysz cel · LPM Strzał (oznacza) · PPM Rozbryzg · Q Egzekutor (×2+przebicie na oznaczonych) · G bóg";
		}

		if (_center != null)
			_center.Text = _arena != null ? _arena.CenterMessage : "";
	}
}

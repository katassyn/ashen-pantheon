using Godot;
using AshenPantheon.Core;

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

			string stats = "";
			var s = _player.Sheet;
			if (s != null)
			{
				float f = s.Resistances.Effective(DamageType.Fire, s.Level);
				float c = s.Resistances.Effective(DamageType.Cold, s.Level);
				float l = s.Resistances.Effective(DamageType.Lightning, s.Level);
				stats = $"Życie {s.MaxLife:0} · ES {s.MaxEnergyShield:0} · Armour {s.Armour:0} · Evasion {s.EvasionRating:0} · Res F/C/L {f:0}/{c:0}/{l:0} · atk ×{s.AttackDamageMultiplier:0.00}\n";
			}

			_info.Text =
				$"HP {_player.Health:0}/{_player.MaxHealth:0}    Koncentracja {_player.Concentration:0}/{_player.MaxConcentration:0}    Bóg: {god}    {wave}\n" +
				stats +
				"WASD · mysz · LPM/PPM/Q/E/R/F skille · Spacja Dash · X Adrenalina · Z Jastrząb · C staty · I ekwipunek · K skille(baza/bóg)";
		}

		if (_center != null)
			_center.Text = _arena != null ? _arena.CenterMessage : "";
	}
}

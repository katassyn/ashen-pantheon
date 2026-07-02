using Godot;
using AshenPantheon.Core;

public partial class Hud : CanvasLayer
{
	private PlayerController _player;
	private ArenaManager _arena;
	private Label _info;
	private Label _center;

	private ProgressBar _hpBar, _resBar, _xpBar;
	private Label _goldLabel;
	private readonly SkillSlotUi[] _slots = new SkillSlotUi[Loadout.SlotCount];

	public override void _Ready()
	{
		_info = GetNode<Label>("%Info");
		_center = GetNode<Label>("%Center");
		BuildBottomBar();
	}

	private void BuildBottomBar()
	{
		var root = new VBoxContainer
		{
			AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1f, AnchorBottom = 1f,
			OffsetLeft = -320f, OffsetRight = 320f, OffsetTop = -132f, OffsetBottom = -10f,
		};
		root.AddThemeConstantOverride("separation", 4);
		AddChild(root);

		_hpBar = MakeBar(new Color(0.75f, 0.2f, 0.2f));
		_resBar = MakeBar(new Color(0.25f, 0.55f, 0.85f));
		_xpBar = MakeBar(new Color(0.7f, 0.6f, 0.2f));
		_xpBar.CustomMinimumSize = new Vector2(0, 8);
		root.AddChild(_hpBar);
		root.AddChild(_resBar);

		var slotRow = new HBoxContainer();
		slotRow.AddThemeConstantOverride("separation", 8);
		slotRow.Alignment = BoxContainer.AlignmentMode.Center;
		root.AddChild(slotRow);

		for (int i = 0; i < Loadout.SlotCount; i++)
		{
			var slot = new SkillSlotUi { SlotIndex = i, CustomMinimumSize = new Vector2(110, 52) };
			_slots[i] = slot;
			slotRow.AddChild(slot);
		}

		root.AddChild(_xpBar);
		_goldLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		root.AddChild(_goldLabel);
	}

	private static ProgressBar MakeBar(Color color)
	{
		var bar = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 14) };
		var fill = new StyleBoxFlat { BgColor = color };
		var bg = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.55f) };
		bar.AddThemeStyleboxOverride("fill", fill);
		bar.AddThemeStyleboxOverride("background", bg);
		return bar;
	}

	public override void _Process(double delta)
	{
		_player = PlayerController.Local;
		_arena ??= GetTree().GetFirstNodeInGroup("arena") as ArenaManager;

		if (_info != null && _player != null)
		{
			string wave = _arena != null ? _arena.TopStatus : "";
			var p = GameState.Progress;
			string net = Net.Online ? $"   [{Net.Status} · {Net.PlayerCount()}/4]" : "";
			_info.Text =
				$"Lv {p.Level}   Bóg: {Gods.Name(GameState.PledgedGod)}   {wave}{net}\n" +
				"C staty · I ekwipunek · K skille/drzewka · [E przy znacznikach w mieście = interakcja]";
		}

		if (_center != null)
			_center.Text = _arena != null ? _arena.CenterMessage : "";

		if (_player != null)
		{
			_hpBar.MaxValue = _player.MaxHealth;
			_hpBar.Value = _player.Health;
			_resBar.MaxValue = _player.MaxResource;
			_resBar.Value = _player.Resource;
		}

		var prog = GameState.Progress;
		_xpBar.MaxValue = PlayerProgress.XpToNext(prog.Level);
		_xpBar.Value = prog.Xp;
		_goldLabel.Text = $"Złoto: {GameState.Wallet.Gold}    XP: {prog.Xp}/{PlayerProgress.XpToNext(prog.Level)}    pkt atrybutów: {prog.AttributePoints}   pkt skilli: {prog.SkillPoints}";

		foreach (var slot in _slots) slot.Refresh(_player);
	}
}

/// <summary>Slot paska skilli: pokazuje przypisany skill + cooldown, przyjmuje drop z panelu skilli (K).</summary>
public partial class SkillSlotUi : PanelContainer
{
	public int SlotIndex;
	private Label _label;

	public override void _Ready()
	{
		_label = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		AddChild(_label);
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.07f, 0.12f, 0.9f),
			BorderColor = new Color(0.4f, 0.35f, 0.55f),
		};
		style.SetBorderWidthAll(2);
		AddThemeStyleboxOverride("panel", style);
	}

	public void Refresh(PlayerController player)
	{
		string key = Loadout.SlotKeys[SlotIndex];
		string skillId = GameState.Loadout.Slots[SlotIndex];
		if (skillId == null)
		{
			_label.Text = $"[{key}]\n—";
			Modulate = Colors.White;
			return;
		}
		var info = GameState.Class.Skill(skillId);
		float cd = player?.CooldownLeft(skillId) ?? 0f;
		bool god = GameState.GodSkills.Contains(skillId) && GameState.PledgedGod != GodId.None;
		_label.Text = cd > 0f
			? $"[{key}] {info?.Name}\n{cd:0.0}s"
			: $"[{key}] {info?.Name}{(god ? " ✦" : "")}";
		Modulate = cd > 0f ? new Color(0.55f, 0.55f, 0.55f) : Colors.White;
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data) =>
		data.VariantType == Variant.Type.String && GameState.Class.Skill(data.AsString()) != null;

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		GameState.Loadout.Assign(SlotIndex, data.AsString());
		GameState.Save();
	}
}

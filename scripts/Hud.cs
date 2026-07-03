using Godot;
using AshenPantheon.Core;

public partial class Hud : CanvasLayer
{
	private PlayerController _player;
	private ArenaManager _arena;
	private Label _info;
	private Label _center;

	private ProgressBar _hpBar, _resBar, _xpBar, _esBar;
	private Label _hpNum, _resNum, _xpNum, _esNum;
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
			OffsetLeft = -440f, OffsetRight = 440f, OffsetTop = -150f, OffsetBottom = -10f,
		};
		root.AddThemeConstantOverride("separation", 4);
		AddChild(root);

		_esBar = MakeBar(new Color(0.35f, 0.75f, 0.95f)); // Energy Shield nad HP (absorbuje pierwszy)
		_esBar.CustomMinimumSize = new Vector2(0, 7);
		_hpBar = MakeBar(new Color(0.75f, 0.2f, 0.2f));
		_resBar = MakeBar(new Color(0.25f, 0.55f, 0.85f));
		_xpBar = MakeBar(new Color(0.7f, 0.6f, 0.2f));
		_xpBar.CustomMinimumSize = new Vector2(0, 8);
		root.AddChild(_esBar);
		root.AddChild(_hpBar);
		root.AddChild(_resBar);
		_esNum = AddBarNumber(_esBar);
		_hpNum = AddBarNumber(_hpBar);
		_resNum = AddBarNumber(_resBar);

		var slotRow = new HBoxContainer();
		slotRow.AddThemeConstantOverride("separation", 8);
		slotRow.Alignment = BoxContainer.AlignmentMode.Center;
		root.AddChild(slotRow);

		for (int i = 0; i < Loadout.SlotCount; i++)
		{
			var slot = new SkillSlotUi { SlotIndex = i, CustomMinimumSize = new Vector2(150, 58) };
			_slots[i] = slot;
			slotRow.AddChild(slot);
		}

		root.AddChild(_xpBar);
		_xpNum = AddBarNumber(_xpBar);
		_goldLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		root.AddChild(_goldLabel);

		AddChild(new PauseMenu());      // ESC: zamyka panele / otwiera pauzę
		AddChild(new WorldMapPanel());  // M: mapa świata / fast-travel
		AddChild(new PartyFrame());     // co-op: ramki drużyny (lewy górny)
		AddChild(new QuestJournal());   // J: dziennik questów
		AddChild(new BuffBar());        // aktywne buffy gracza
		AddChild(new ChatBox());        // czat co-op (Enter)

		// minimapa: stała rogowa + duża pod TAB (obie centrowane na graczu)
		var corner = new MinimapView { WorldRadius = 1400f };
		corner.AnchorLeft = 1f; corner.AnchorRight = 1f;
		corner.OffsetLeft = -206; corner.OffsetTop = 16; corner.OffsetRight = -16; corner.OffsetBottom = 206;
		AddChild(corner);

		var tab = new MinimapView { WorldRadius = 2600f, Toggleable = true };
		tab.AnchorLeft = 0.5f; tab.AnchorRight = 0.5f; tab.AnchorTop = 0.5f; tab.AnchorBottom = 0.5f;
		tab.OffsetLeft = -320; tab.OffsetTop = -320; tab.OffsetRight = 320; tab.OffsetBottom = 320;
		AddChild(tab);
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

	/// <summary>Tracker aktywnego questa (pierwszy z dziennika) pod górnym infem.</summary>
	private static string QuestTracker()
	{
		foreach (var questId in GameState.Quests.Active.Keys)
		{
			var q = QuestCatalog.Find(questId);
			if (q == null) continue;
			var lines = new System.Text.StringBuilder($"\n◆ {q.Name}");
			foreach (var o in q.Objectives)
			{
				int cur = GameState.Quests.Progress(q.Id, o.Id);
				lines.Append($"\n   {(cur >= o.Amount ? "✔" : "•")} {o.Description}  {cur}/{o.Amount}");
			}
			if (GameState.Quests.ReadyToTurnIn(q))
				lines.Append($"\n   → turn in at: {QuestNpc.NpcName(q.TurnIn)}");
			return lines.ToString();
		}
		return "";
	}

	/// <summary>Liczba na środku paska ("123/456").</summary>
	private static Label AddBarNumber(ProgressBar bar)
	{
		var label = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AnchorRight = 1f, AnchorBottom = 1f,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color", Colors.White);
		label.AddThemeConstantOverride("outline_size", 3);
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
		bar.AddChild(label);
		return label;
	}

	public override void _Process(double delta)
	{
		_player = PlayerController.Local;
		_arena ??= GetTree().GetFirstNodeInGroup("arena") as ArenaManager;
		var worldZone = _arena == null ? GetTree().GetFirstNodeInGroup("arena") as WorldZoneManager : null;

		if (_info != null && _player != null)
		{
			string wave = _arena?.TopStatus ?? worldZone?.TopStatus ?? "";
			var p = GameState.Progress;
			string net = Net.Online ? $"   [{Net.Status} · {Net.PlayerCount()}/4]" : "";
			_info.Text =
				$"Lv {p.Level}   God: {Gods.Name(GameState.PledgedGod)}   {wave}{net}\n" +
				"C stats · I inventory · K skills · J journal · M map · TAB big map · E interact" +
				QuestTracker();
		}

		if (_center != null)
		{
			string center = _arena != null ? _arena.CenterMessage : "";
			if (string.IsNullOrEmpty(center) && _player is { Dead: true })
				center = "DEFEATED\nyou will rise when your party clears the room";
			_center.Text = center;
		}

		if (_player != null)
		{
			_hpBar.MaxValue = _player.MaxHealth;
			_hpBar.Value = _player.Health;
			_resBar.MaxValue = _player.MaxResource;
			_resBar.Value = _player.Resource;
			_hpNum.Text = $"{_player.Health:0} / {_player.MaxHealth:0}";
			_resNum.Text = $"{_player.Resource:0} / {_player.MaxResource:0}";
			bool hasEs = _player.MaxEnergyShield > 0f;
			_esBar.Visible = hasEs;
			if (hasEs)
			{
				_esBar.MaxValue = _player.MaxEnergyShield;
				_esBar.Value = _player.EnergyShield;
				_esNum.Text = $"{_player.EnergyShield:0} / {_player.MaxEnergyShield:0}";
			}
		}

		var prog = GameState.Progress;
		long need = PlayerProgress.XpToNext(prog.Level);
		long toGo = System.Math.Max(0, need - prog.Xp);
		float pct = need > 0 ? prog.Xp / (float)need * 100f : 0f;
		_xpBar.MaxValue = need;
		_xpBar.Value = prog.Xp;
		_xpNum.Text = $"Lv {prog.Level}   {prog.Xp} / {need} XP   ({pct:0.#}%)   {toGo} to level {prog.Level + 1}";
		_xpBar.TooltipText = $"{toGo} XP to reach level {prog.Level + 1}";
		_goldLabel.Text = $"Gold: {GameState.Wallet.Gold}    attribute pts: {prog.AttributePoints}    skill pts: {prog.SkillPoints}";

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
		int reqLvl = GameState.ClassSpec.Skill(skillId)?.RequiredLevel ?? 1;
		bool locked = reqLvl > GameState.Progress.Level;
		_label.Text = locked
			? $"[{key}] {info?.Name}\n🔒 lvl {reqLvl}"
			: cd > 0f
				? $"[{key}] {info?.Name}\n{cd:0.0}s"
				: $"[{key}] {info?.Name}{(god ? " ✦" : "")}";
		Modulate = locked || cd > 0f ? new Color(0.55f, 0.55f, 0.55f) : Colors.White;
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data) =>
		data.VariantType == Variant.Type.String && GameState.Class.Skill(data.AsString()) != null;

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		GameState.Loadout.Assign(SlotIndex, data.AsString());
		GameState.Save();
	}
}

/// <summary>ESC: najpierw zamyka otwarte panele, potem otwiera pauzę (Wznów/Opcje/Menu główne/Wyjdź).</summary>
public partial class PauseMenu : CanvasLayer
{
	private Panel _root;
	private CheckBox _fullscreen;

	public override void _Ready()
	{
		Layer = 20;
		_root = new Panel
		{
			AnchorLeft = 0.5f, AnchorTop = 0.5f, AnchorRight = 0.5f, AnchorBottom = 0.5f,
			OffsetLeft = -170, OffsetTop = -160, OffsetRight = 170, OffsetBottom = 160,
			Visible = false,
		};
		UiPanels.Solidify(_root);
		AddChild(_root);

		var vb = new VBoxContainer { AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = 20, OffsetTop = 16, OffsetRight = -20, OffsetBottom = -16 };
		vb.AddThemeConstantOverride("separation", 10);
		_root.AddChild(vb);

		vb.AddChild(new Label { Text = "PAUSED", HorizontalAlignment = HorizontalAlignment.Center });

		var resume = new Button { Text = "Resume" };
		resume.Pressed += () => _root.Visible = false;
		vb.AddChild(resume);

		_fullscreen = new CheckBox { Text = "Fullscreen" };
		_fullscreen.Toggled += on => DisplayServer.WindowSetMode(
			on ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
		vb.AddChild(_fullscreen);

		// rozdzielczość okna
		var resRow = new HBoxContainer();
		resRow.AddChild(new Label { Text = "Resolution" });
		var res = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var sizes = new[] { new Vector2I(1280, 720), new Vector2I(1600, 900), new Vector2I(1920, 1080), new Vector2I(2560, 1440) };
		foreach (var s in sizes) res.AddItem($"{s.X} x {s.Y}");
		res.ItemSelected += i =>
		{
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
			DisplayServer.WindowSetSize(sizes[i]);
			var screen = DisplayServer.ScreenGetSize();
			DisplayServer.WindowSetPosition((screen - sizes[i]) / 2);
			if (_fullscreen != null) _fullscreen.ButtonPressed = false;
		};
		resRow.AddChild(res);
		vb.AddChild(resRow);

		var volRow = new HBoxContainer();
		volRow.AddChild(new Label { Text = "Volume" });
		var vol = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05f, Value = 1, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		vol.ValueChanged += v => AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb((float)v));
		volRow.AddChild(vol);
		vb.AddChild(volRow);

		var toMenu = new Button { Text = "Main menu" };
		toMenu.Pressed += () =>
		{
			GameState.Save();
			Net.Leave(goHub: false);
			GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
		};
		vb.AddChild(toMenu);

		var quit = new Button { Text = "Quit game" };
		quit.Pressed += () =>
		{
			GameState.Save();
			if (GameState.Repository is HttpGameStateRepository http) http.FlushBlocking();
			GetTree().Quit();
		};
		vb.AddChild(quit);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey k || !k.Pressed || k.Echo || k.PhysicalKeycode != Key.Escape) return;

		if (_root.Visible)
		{
			_root.Visible = false;
		}
		else if (UiPanels.AnyOpen(GetTree()))
		{
			UiPanels.CloseAllExcept(GetTree(), null); // ESC zamyka aktywne okno
		}
		else
		{
			_root.Visible = true;
		}
		GetViewport().SetInputAsHandled();
	}
}

/// <summary>Party frames (co-op): list of players with HP; hidden in solo.</summary>
public partial class PartyFrame : CanvasLayer
{
	private VBoxContainer _list;

	public override void _Ready()
	{
		Layer = 3;
		_list = new VBoxContainer { AnchorLeft = 0f, AnchorTop = 0f, OffsetLeft = 12, OffsetTop = 96 };
		_list.AddThemeConstantOverride("separation", 4);
		AddChild(_list);
	}

	public override void _Process(double delta)
	{
		var players = new System.Collections.Generic.List<PlayerController>();
		foreach (Node n in GetTree().GetNodesInGroup("players"))
			if (n is PlayerController p) players.Add(p);

		bool show = Net.Online && players.Count > 1;
		_list.Visible = show;
		if (!show) { if (_list.GetChildCount() > 0) foreach (Node c in _list.GetChildren()) c.QueueFree(); return; }

		if (_list.GetChildCount() != players.Count)
		{
			foreach (Node c in _list.GetChildren()) c.QueueFree();
			foreach (var _ in players) _list.AddChild(new PartyRow());
		}

		players.Sort((a, b) => a.GetMultiplayerAuthority().CompareTo(b.GetMultiplayerAuthority()));
		for (int i = 0; i < players.Count && i < _list.GetChildCount(); i++)
			if (_list.GetChild(i) is PartyRow row) row.Update(players[i]);
	}
}

public partial class PartyRow : PanelContainer
{
	private Label _name;
	private ProgressBar _hp;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(190, 0);
		var style = new StyleBoxFlat { BgColor = new Color(0.05f, 0.05f, 0.08f, 0.7f) };
		style.SetContentMarginAll(4);
		AddThemeStyleboxOverride("panel", style);
		var vb = new VBoxContainer();
		AddChild(vb);
		_name = new Label();
		_name.AddThemeFontSizeOverride("font_size", 12);
		vb.AddChild(_name);
		_hp = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) };
		_hp.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.75f, 0.2f, 0.2f) });
		_hp.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.6f) });
		vb.AddChild(_hp);
	}

	public void Update(PlayerController p)
	{
		bool local = p.IsMultiplayerAuthority();
		string who = local ? GameState.CharacterName : Net.NameOf(p.GetMultiplayerAuthority());
		_name.Text = (local ? "* " : "   ") + who + (p.Dead ? "  (down)" : "");
		_name.Modulate = local ? new Color(0.7f, 0.9f, 1f) : Colors.White;
		_hp.Value = p.Dead ? 0f : p.HealthFraction;
	}
}

/// <summary>Active player buffs (chips above the skill bar): God pledge + Adrenaline w/ countdown.</summary>
public partial class BuffBar : CanvasLayer
{
	private HBoxContainer _row;

	public override void _Ready()
	{
		Layer = 3;
		_row = new HBoxContainer
		{
			AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1f, AnchorBottom = 1f,
			OffsetLeft = -200, OffsetRight = 200, OffsetTop = -196, OffsetBottom = -168,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		_row.AddThemeConstantOverride("separation", 6);
		AddChild(_row);
	}

	public override void _Process(double delta)
	{
		foreach (Node c in _row.GetChildren()) c.QueueFree();
		var p = PlayerController.Local;

		if (GameState.PledgedGod != GodId.None)
			AddChip(Gods.Name(GameState.PledgedGod), new Color(0.5f, 0.35f, 0.7f));

		if (p != null && p.AdrenalineActive)
			AddChip($"Adrenaline {p.AdrenalineTimeLeft:0.0}s", new Color(0.8f, 0.4f, 0.2f));
	}

	private void AddChip(string text, Color col)
	{
		var chip = new PanelContainer();
		var style = new StyleBoxFlat { BgColor = new Color(col.R, col.G, col.B, 0.85f) };
		style.SetContentMarginAll(4);
		style.SetCornerRadiusAll(4);
		chip.AddThemeStyleboxOverride("panel", style);
		var lbl = new Label { Text = text };
		lbl.AddThemeFontSizeOverride("font_size", 12);
		chip.AddChild(lbl);
		_row.AddChild(chip);
	}
}

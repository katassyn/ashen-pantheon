using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using AshenPantheon.Core;

/// <summary>Panel skilli (K): lista skilli klasy (z wymogami poziomu, drag na pasek) + GRAF drzewka
/// wybranego skilla (linie prereq, wykluczenia, koszty/poziomy) + szczegóły z LIVE skalowaniem builda.</summary>
public partial class SkillPanel : CanvasLayer, IUiPanel
{
    private Panel _root;
    private VBoxContainer _skillList;
    private Label _header;
    private SkillGraphCanvas _graph;
    private ClassTreeCanvas _classCanvas;
    private Button _backBtn;
    private RichTextLabel _details;
    private string _selectedSkill = "basic";
    /// <summary>Widok: "class" = główne drzewo klasy (DSO-style), "skill" = mini-drzewko ulepszeń.</summary>
    private string _mode = "class";

    public void CloseUi() => _root.Visible = false;
    public bool IsOpen => _root != null && _root.Visible;

    public override void _Ready()
    {
        _root = GetNode<Panel>("%Root");
        AddToGroup(UiPanels.Group);
        UiPanels.Solidify(_root);
        BuildLayout();
        _root.Visible = false;
    }

    private void BuildLayout()
    {
        var vb = new VBoxContainer { AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = 14, OffsetTop = 12, OffsetRight = -14, OffsetBottom = -12 };
        vb.AddThemeConstantOverride("separation", 8);
        _root.AddChild(vb);

        vb.AddChild(new Label { Text = "SKILLS & TALENTS    [K] close    ≡ drag a skill onto the bottom bar" });
        _header = new Label();
        vb.AddChild(_header);

        var godRow = new HBoxContainer();
        godRow.AddThemeConstantOverride("separation", 8);
        vb.AddChild(godRow);
        foreach (var god in Gods.All)
        {
            var b = new Button { Text = Gods.Name(god), TooltipText = Gods.Passive(god) };
            var captured = god;
            b.Pressed += () => { GameState.PledgedGod = captured; GameState.Save(); PlayerController.Local?.Refresh(); Refresh(); };
            godRow.AddChild(b);
        }

        var hb = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        hb.AddThemeConstantOverride("separation", 14);
        vb.AddChild(hb);

        var listScroll = new ScrollContainer { CustomMinimumSize = new Vector2(230, 0) };
        _skillList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        listScroll.AddChild(_skillList);
        hb.AddChild(listScroll);

        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 8);
        hb.AddChild(right);

        _backBtn = new Button { Text = "← back to class tree", Visible = false };
        _backBtn.Pressed += () => { _mode = "class"; Refresh(); };
        right.AddChild(_backBtn);

        var graphScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var graphHost = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _classCanvas = new ClassTreeCanvas { Panel = this };
        _graph = new SkillGraphCanvas { Panel = this };
        graphHost.AddChild(_classCanvas);
        graphHost.AddChild(_graph);
        graphScroll.AddChild(graphHost);
        right.AddChild(graphScroll);

        _details = new RichTextLabel { BbcodeEnabled = true, FitContent = true, CustomMinimumSize = new Vector2(0, 168), ScrollActive = true };
        right.AddChild(_details);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && Keybinds.Matches(k, "skills"))
        {
            if (_root.Visible) { _root.Visible = false; }
            else
            {
                UiPanels.CloseAllExcept(GetTree(), this);
                _root.Visible = true;
                Refresh();
            }
            GetViewport().SetInputAsHandled();
        }
    }

    public void Refresh()
    {
        var prog = GameState.Progress;
        int treeCost = (int)Respec.SkillTreeCost(GameState.Trees.SpentPoints);
        _header.Text = $"Pledge: {Gods.Name(GameState.PledgedGod)} — {Gods.Passive(GameState.PledgedGod)}\n" +
                       $"Level: {prog.Level}   Skill points: {prog.SkillPoints}   Gold: {GameState.Wallet.Gold}" +
                       (GameState.Trees.SpentPoints > 0 ? $"   (tree reset: {treeCost} gold)" : "");

        // lista skilli klasy
        foreach (Node c in _skillList.GetChildren()) c.QueueFree();

        if (GameState.Trees.SpentPoints > 0)
        {
            var respec = new Button { Text = $"Reset trees ({treeCost}g)" };
            respec.Pressed += () =>
            {
                if (GameState.Wallet.Gold < treeCost) return;
                GameState.Wallet.Gold -= treeCost;
                GameState.Progress.SkillPoints += GameState.Trees.ResetAll();
                GameState.Save();
                Refresh();
            };
            _skillList.AddChild(respec);
        }

        foreach (var spec in GameState.ClassSpec.Skills)
        {
            bool locked = spec.RequiredLevel > prog.Level;
            int? slot = GameState.Loadout.SlotOf(spec.Id);
            bool godOn = GameState.GodSkills.Contains(spec.Id);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);

            var drag = new SkillDragSource
            {
                SkillId = spec.Id,
                Text = $"≡ {spec.Name}{(locked ? $" 🔒{spec.RequiredLevel}" : "")}{(slot.HasValue ? $" [{Loadout.SlotKeys[slot.Value]}]" : "")}",
                Disabled = locked,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TooltipText = locked ? $"Unlocks at level {spec.RequiredLevel}" : "Click = open tree · drag onto the bar",
            };
            var sid = spec.Id;
            drag.Pressed += () => OpenSkillTree(sid);
            if (spec.Id == _selectedSkill) drag.Modulate = new Color(1f, 0.9f, 0.5f);
            row.AddChild(drag);

            var toggle = new Button
            {
                Text = godOn ? "✦" : "—",
                TooltipText = godOn ? "GOD version (click: base)" : "BASE version (click: god)",
                Disabled = GameState.PledgedGod == GodId.None,
                CustomMinimumSize = new Vector2(34, 0),
            };
            toggle.Pressed += () =>
            {
                if (!GameState.GodSkills.Add(sid)) GameState.GodSkills.Remove(sid);
                GameState.Save();
                Refresh();
            };
            row.AddChild(toggle);
            _skillList.AddChild(row);
        }

        bool skillMode = _mode == "skill";
        _backBtn.Visible = skillMode;
        _graph.Visible = skillMode;
        _classCanvas.Visible = !skillMode;
        if (skillMode)
        {
            _graph.ShowSkill(_selectedSkill);
            ShowSkillDetails(_selectedSkill);
        }
        else
        {
            _classCanvas.Rebuild();
            _details.Text = "[b]Class tree[/b] — skills unlock by level; passives on the tracks are bought with skill points.\nClick a SKILL to open its upgrade mini-tree.";
        }
    }

    /// <summary>Wejście w mini-drzewko ulepszeń skilla (z listy lub z węzła drzewa klasy).</summary>
    public void OpenSkillTree(string skillId)
    {
        _selectedSkill = skillId;
        _mode = "skill";
        Refresh();
    }

    public void TryAllocatePassive(ClassTreeNode node)
    {
        var prog = GameState.Progress;
        string reason = ClassTree.BlockReason(GameState.ClassId, node.Id, prog.Level, prog.SkillPoints, GameState.PassiveNodes);
        if (GameState.PassiveNodes.Contains(node.Id) || reason != null)
        {
            ShowPassiveDetails(node, reason);
            return;
        }
        GameState.PassiveNodes.Add(node.Id);
        prog.SkillPoints -= node.Cost;
        GameState.Save();
        PlayerController.Local?.Refresh();
        Refresh();
        ShowPassiveDetails(node, null);
    }

    public void ShowPassiveDetails(ClassTreeNode node, string reason)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[b]{node.Name}[/b]  [passive · cost {node.Cost} pt{(node.RequiredLevel > 0 ? $" · req. level {node.RequiredLevel}" : "")}]");
        sb.AppendLine(node.Description);
        if (node.ExclusiveGroup != null)
        {
            var siblings = ClassTree.Trees[GameState.ClassId]
                .Where(n => n.ExclusiveGroup == node.ExclusiveGroup && n.Id != node.Id).Select(n => n.Name);
            sb.AppendLine($"[color=#d47070]Mutually exclusive with: {string.Join(", ", siblings)}[/color]");
        }
        sb.AppendLine(GameState.PassiveNodes.Contains(node.Id) ? "[color=#8fd48f]✔ OWNED[/color]"
            : reason != null ? $"[color=#d4a050]🔒 {reason}[/color]"
            : "[color=#f0e080]Click to buy[/color]");
        _details.Text = sb.ToString();
    }

    // ── szczegóły: LIVE skalowanie z aktualnego builda (broń+bóg+drzewko+arkusz) ──

    public void ShowSkillDetails(string skillId)
    {
        var spec = GameState.ClassSpec.Skill(skillId);
        if (spec == null) return;
        var s = PlayerController.Local?.BuildSkill(skillId);

        var sb = new StringBuilder();
        sb.Append($"[b]{spec.Name}[/b]  (req. level {spec.RequiredLevel})   ");
        sb.AppendLine(GameState.GodSkills.Contains(skillId) && GameState.PledgedGod != GodId.None
            ? $"[color=#c9a0ff]version: {Gods.Name(GameState.PledgedGod)} ✦[/color]" : "version: base");
        sb.AppendLine(spec.Description);

        if (s != null)
        {
            sb.Append($"[color=#f0c060]Damage: {s.Damage:0.#} ({s.DamageType})[/color]");
            if (spec.WeaponScaling > 0) sb.Append($"   [color=#9a9a9a](base {spec.BaseDamage:0.#} + {spec.WeaponScaling:P0} weapon damage)[/color]");
            sb.AppendLine();
            sb.Append($"Cost: {s.ConcentrationCost * s.CostMult:0.#} {GameState.Class.ResourceName}");
            sb.Append($"   CD: {spec.Cooldown * s.CdMult:0.##}s");
            sb.AppendLine($"   Cast time: {s.CastTime:0.##}s ({(spec.UsesAttackSpeed ? "atk" : "cast")} speed)");

            var extras = new List<string>();
            if (s.AppliesMark) extras.Add($"marks ({s.MarkDuration:0.#}s)");
            if (s.MarkedMultiplier > 1f) extras.Add($"x{s.MarkedMultiplier:0.##} vs Marked");
            if (s.Pierces) extras.Add("pierces");
            if (s.PierceMarkedOnly) extras.Add("pierces Marked");
            if (s.Explodes) extras.Add("explodes");
            if (s.ExtraProjectiles > 0) extras.Add($"+{s.ExtraProjectiles} projectiles");
            if (s.OnHitStatus != StatusType.None) extras.Add($"{s.OnHitStatus} {s.StatusDps:0.#}/s for {s.StatusDuration:0.#}s");
            if (s.StunDuration > 0) extras.Add($"stun {s.StunDuration:0.#}s");
            if (s.HealOnHit > 0) extras.Add($"heals {s.HealOnHit:0.#} per hit");
            if (s.AoeMult != 1f) extras.Add($"area x{s.AoeMult:0.##}");
            if (extras.Count > 0) sb.AppendLine("[color=#8fd48f]" + string.Join(" · ", extras) + "[/color]");
        }

        _details.Text = sb.ToString();
    }

    public void ShowNodeDetails(SkillNode node, string skillId)
    {
        var prog = GameState.Progress;
        string reason = GameState.Trees.BlockReason(skillId, node.Id, prog.Level, prog.SkillPoints);
        bool allocated = GameState.Trees.IsAllocated(skillId, node.Id);

        var sb = new StringBuilder();
        sb.AppendLine($"[b]{node.Name}[/b]  [cost: {node.Cost} pt{(node.RequiredLevel > 0 ? $" · req. level {node.RequiredLevel}" : "")}]");
        sb.AppendLine(node.Description);
        if (node.Requires != null)
            sb.AppendLine($"[color=#9a9a9a]Requires node: {GameData.FindNode(skillId, node.Requires)?.Name}[/color]");
        if (node.ExclusiveGroup != null)
        {
            var siblings = GameData.Trees[skillId].Where(n => n.ExclusiveGroup == node.ExclusiveGroup && n.Id != node.Id).Select(n => n.Name);
            sb.AppendLine($"[color=#d47070]Mutually exclusive with: {string.Join(", ", siblings)}[/color]");
        }
        sb.AppendLine(allocated ? "[color=#8fd48f]✔ ALLOCATED[/color]"
            : reason != null ? $"[color=#d4a050]🔒 {reason}[/color]"
            : "[color=#f0e080]Click again to allocate[/color]");
        _details.Text = sb.ToString();
    }

    public void TryAllocate(string skillId, SkillNode node)
    {
        var prog = GameState.Progress;
        if (GameState.Trees.IsAllocated(skillId, node.Id)) { ShowNodeDetails(node, skillId); return; }
        string reason = GameState.Trees.BlockReason(skillId, node.Id, prog.Level, prog.SkillPoints);
        if (reason != null) { ShowNodeDetails(node, skillId); return; }

        if (GameState.Trees.Allocate(skillId, node.Id))
        {
            prog.SkillPoints -= node.Cost;
            GameState.Save();
            Refresh();
            ShowNodeDetails(node, skillId);
        }
    }
}

/// <summary>Canvas grafu drzewka: węzły w tierach (głębokość prereq), linie zależności, wykluczenia.</summary>
public partial class SkillGraphCanvas : Control
{
    public SkillPanel Panel;

    private const float TierW = 200f, RowH = 78f, NodeW = 180f, NodeH = 62f, Pad = 16f;
    private string _skillId = "";
    private readonly Dictionary<string, Button> _buttons = new();

    public void ShowSkill(string skillId)
    {
        _skillId = skillId;
        foreach (Node c in GetChildren()) c.QueueFree();
        _buttons.Clear();

        if (!GameData.Trees.TryGetValue(skillId, out var nodes)) { QueueRedraw(); return; }

        // tier = głębokość łańcucha prereq
        int Depth(SkillNode n) => n.Requires == null ? 0
            : 1 + Depth(nodes.First(x => x.Id == n.Requires));

        var byTier = nodes.GroupBy(Depth).OrderBy(g => g.Key).ToList();
        var prog = GameState.Progress;
        float maxY = 0;

        foreach (var tier in byTier)
        {
            int row = 0;
            foreach (var node in tier)
            {
                bool allocated = GameState.Trees.IsAllocated(skillId, node.Id);
                string reason = allocated ? null : GameState.Trees.BlockReason(skillId, node.Id, prog.Level, prog.SkillPoints);

                var btn = new Button
                {
                    Text = $"{node.Name}\n[{node.Cost} pt{(node.RequiredLevel > 0 ? $" · lvl {node.RequiredLevel}" : "")}]",
                    Position = new Vector2(Pad + tier.Key * TierW, Pad + row * RowH),
                    Size = new Vector2(NodeW, NodeH),
                    ClipText = true,
                };
                var style = new StyleBoxFlat
                {
                    BgColor = allocated ? new Color(0.12f, 0.3f, 0.14f)
                        : reason == null ? new Color(0.24f, 0.22f, 0.1f)
                        : new Color(0.1f, 0.1f, 0.13f),
                    BorderColor = allocated ? new Color(0.35f, 0.9f, 0.4f)
                        : reason == null ? new Color(0.95f, 0.85f, 0.4f)
                        : new Color(0.35f, 0.33f, 0.45f),
                };
                style.SetBorderWidthAll(2);
                style.SetCornerRadiusAll(4);
                btn.AddThemeStyleboxOverride("normal", style);
                btn.AddThemeStyleboxOverride("hover", style);
                btn.AddThemeStyleboxOverride("pressed", style);
                if (reason != null && !allocated) btn.Modulate = new Color(0.75f, 0.75f, 0.8f);

                var captured = node;
                btn.Pressed += () => Panel.TryAllocate(_skillId, captured);
                btn.MouseEntered += () => Panel.ShowNodeDetails(captured, _skillId);

                AddChild(btn);
                _buttons[node.Id] = btn;
                row++;
                maxY = Mathf.Max(maxY, btn.Position.Y + NodeH);
            }
        }

        CustomMinimumSize = new Vector2(Pad * 2 + byTier.Count * TierW, maxY + Pad);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!GameData.Trees.TryGetValue(_skillId, out var nodes)) return;

        // linie prereq
        foreach (var node in nodes)
        {
            if (node.Requires == null || !_buttons.TryGetValue(node.Id, out var to) || !_buttons.TryGetValue(node.Requires, out var from))
                continue;
            bool bothOn = GameState.Trees.IsAllocated(_skillId, node.Id);
            var a = from.Position + new Vector2(NodeW, NodeH / 2f);
            var b = to.Position + new Vector2(0, NodeH / 2f);
            DrawLine(a, b, bothOn ? new Color(0.35f, 0.9f, 0.4f) : new Color(0.45f, 0.42f, 0.6f), 2f);
        }

        // wykluczenia (czerwona przerywana pionowa między rodzeństwem)
        foreach (var group in nodes.Where(n => n.ExclusiveGroup != null).GroupBy(n => n.ExclusiveGroup))
        {
            var members = group.Where(n => _buttons.ContainsKey(n.Id)).Select(n => _buttons[n.Id]).OrderBy(b => b.Position.Y).ToList();
            for (int i = 0; i + 1 < members.Count; i++)
            {
                var a = members[i].Position + new Vector2(NodeW / 2f, NodeH);
                var b = members[i + 1].Position + new Vector2(NodeW / 2f, 0);
                DrawDashedLine(a, b, new Color(0.9f, 0.35f, 0.35f, 0.8f), 2f, 6f);
                DrawString(ThemeDB.FallbackFont, (a + b) / 2f + new Vector2(6, 4), "OR",
                    HorizontalAlignment.Left, -1, 11, new Color(0.9f, 0.45f, 0.45f));
            }
        }
    }
}

/// <summary>Canvas GŁÓWNEGO drzewa klasy (DSO-style, pionowe): START u góry, skille jako duże węzły,
/// pasywki-wybory na trackach między nimi. Klik skill = otwiera jego mini-drzewko.</summary>
public partial class ClassTreeCanvas : Control
{
    public SkillPanel Panel;

    private const float RowH = 96f, ColW = 210f, SkillW = 190f, SkillH = 60f, PassW = 170f, PassH = 52f, Pad = 20f;
    private readonly Dictionary<string, Button> _buttons = new();

    public void Rebuild()
    {
        foreach (Node c in GetChildren()) c.QueueFree();
        _buttons.Clear();
        if (!ClassTree.Trees.TryGetValue(GameState.ClassId, out var nodes)) { QueueRedraw(); return; }

        int Depth(ClassTreeNode n) => n.Requires == null ? 0
            : 1 + Depth(nodes.First(x => x.Id == n.Requires));

        var prog = GameState.Progress;
        var byDepth = nodes.GroupBy(Depth).OrderBy(g => g.Key).ToList();
        float maxW = 0;

        foreach (var tier in byDepth)
        {
            var members = tier.ToList();
            float totalW = members.Count * ColW;
            float startX = Pad + Mathf.Max(0, (900f - totalW) / 2f);
            int col = 0;
            foreach (var node in members)
            {
                bool isSkill = node.Type == "skill";
                bool isStart = node.Type == "start";
                var spec = isSkill ? GameState.ClassSpec.Skill(node.SkillId) : null;
                bool satisfied = ClassTree.NodeSatisfied(GameState.ClassId, node, prog.Level, GameState.PassiveNodes);
                string reason = node.Type == "passive"
                    ? ClassTree.BlockReason(GameState.ClassId, node.Id, prog.Level, prog.SkillPoints, GameState.PassiveNodes)
                    : null;

                var btn = new Button
                {
                    Position = new Vector2(startX + col * ColW, Pad + tier.Key * RowH),
                    Size = isSkill || isStart ? new Vector2(SkillW, SkillH) : new Vector2(PassW, PassH),
                    ClipText = true,
                    Text = isStart ? "★ START"
                        : isSkill ? $"{spec?.Name ?? node.SkillId}\n{(satisfied ? "▶ upgrade tree" : $"🔒 level {spec?.RequiredLevel}")}"
                        : $"◈ {node.Name}\n[{node.Cost} pt{(node.RequiredLevel > 0 ? $" · lvl {node.RequiredLevel}" : "")}]",
                };

                var style = new StyleBoxFlat();
                style.SetBorderWidthAll(2);
                style.SetCornerRadiusAll(isSkill || isStart ? 4 : 14);
                if (isStart) { style.BgColor = new Color(0.25f, 0.2f, 0.35f); style.BorderColor = new Color(0.8f, 0.7f, 1f); }
                else if (isSkill)
                {
                    style.BgColor = satisfied ? new Color(0.13f, 0.2f, 0.3f) : new Color(0.1f, 0.1f, 0.13f);
                    style.BorderColor = satisfied ? new Color(0.45f, 0.7f, 1f) : new Color(0.35f, 0.33f, 0.45f);
                }
                else
                {
                    bool bought = GameState.PassiveNodes.Contains(node.Id);
                    style.BgColor = bought ? new Color(0.12f, 0.3f, 0.14f)
                        : reason == null ? new Color(0.24f, 0.22f, 0.1f)
                        : new Color(0.1f, 0.1f, 0.13f);
                    style.BorderColor = bought ? new Color(0.35f, 0.9f, 0.4f)
                        : reason == null ? new Color(0.95f, 0.85f, 0.4f)
                        : new Color(0.35f, 0.33f, 0.45f);
                }
                btn.AddThemeStyleboxOverride("normal", style);
                btn.AddThemeStyleboxOverride("hover", style);
                btn.AddThemeStyleboxOverride("pressed", style);

                var captured = node;
                if (isSkill && satisfied)
                    btn.Pressed += () => Panel.OpenSkillTree(captured.SkillId);
                else if (node.Type == "passive")
                {
                    btn.Pressed += () => Panel.TryAllocatePassive(captured);
                    btn.MouseEntered += () => Panel.ShowPassiveDetails(captured,
                        ClassTree.BlockReason(GameState.ClassId, captured.Id, GameState.Progress.Level, GameState.Progress.SkillPoints, GameState.PassiveNodes));
                }

                AddChild(btn);
                _buttons[node.Id] = btn;
                col++;
                maxW = Mathf.Max(maxW, btn.Position.X + btn.Size.X);
            }
        }

        CustomMinimumSize = new Vector2(maxW + Pad, Pad * 2 + byDepth.Count * RowH);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!ClassTree.Trees.TryGetValue(GameState.ClassId, out var nodes)) return;
        foreach (var node in nodes)
        {
            if (node.Requires == null || !_buttons.TryGetValue(node.Id, out var to) || !_buttons.TryGetValue(node.Requires, out var from))
                continue;
            var a = from.Position + new Vector2(from.Size.X / 2f, from.Size.Y);
            var b = to.Position + new Vector2(to.Size.X / 2f, 0);
            bool active = ClassTree.NodeSatisfied(GameState.ClassId, node, GameState.Progress.Level, GameState.PassiveNodes);
            DrawLine(a, b, active ? new Color(0.45f, 0.85f, 0.5f) : new Color(0.45f, 0.42f, 0.6f), 2f);
        }

        foreach (var group in nodes.Where(n => n.ExclusiveGroup != null).GroupBy(n => n.ExclusiveGroup))
        {
            var members = group.Where(n => _buttons.ContainsKey(n.Id)).Select(n => _buttons[n.Id]).OrderBy(b => b.Position.X).ToList();
            for (int i = 0; i + 1 < members.Count; i++)
            {
                var a = members[i].Position + new Vector2(members[i].Size.X, members[i].Size.Y / 2f);
                var b = members[i + 1].Position + new Vector2(0, members[i + 1].Size.Y / 2f);
                DrawDashedLine(a, b, new Color(0.9f, 0.35f, 0.35f, 0.8f), 2f, 6f);
                DrawString(ThemeDB.FallbackFont, (a + b) / 2f + new Vector2(-16, -6), "OR",
                    HorizontalAlignment.Left, -1, 11, new Color(0.9f, 0.45f, 0.45f));
            }
        }
    }
}

/// <summary>Źródło drag&drop: niesie id skilla na slot paska.</summary>
public partial class SkillDragSource : Button
{
    public string SkillId = "";

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Disabled) return default;
        var preview = new Label { Text = Text };
        SetDragPreview(preview);
        return SkillId;
    }
}

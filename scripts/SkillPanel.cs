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
    private RichTextLabel _details;
    private string _selectedSkill = "basic";

    public void CloseUi() => _root.Visible = false;

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

        vb.AddChild(new Label { Text = "SKILLE I DRZEWKA    [K] zamknij    ≡ przeciągnij skill na pasek na dole" });
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

        var graphScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _graph = new SkillGraphCanvas { Panel = this };
        graphScroll.AddChild(_graph);
        right.AddChild(graphScroll);

        _details = new RichTextLabel { BbcodeEnabled = true, FitContent = true, CustomMinimumSize = new Vector2(0, 168), ScrollActive = true };
        right.AddChild(_details);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.K)
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
                       $"Poziom: {prog.Level}   Punkty skilli: {prog.SkillPoints}   Złoto: {GameState.Wallet.Gold}" +
                       (GameState.Trees.SpentPoints > 0 ? $"   (reset drzewek: {treeCost} złota)" : "");

        // lista skilli klasy
        foreach (Node c in _skillList.GetChildren()) c.QueueFree();

        if (GameState.Trees.SpentPoints > 0)
        {
            var respec = new Button { Text = $"Reset drzewek ({treeCost}g)" };
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
                TooltipText = locked ? $"Odblokowanie na poziomie {spec.RequiredLevel}" : "Kliknij = pokaż drzewko · przeciągnij na pasek",
            };
            var sid = spec.Id;
            drag.Pressed += () => { _selectedSkill = sid; Refresh(); };
            if (spec.Id == _selectedSkill) drag.Modulate = new Color(1f, 0.9f, 0.5f);
            row.AddChild(drag);

            var toggle = new Button
            {
                Text = godOn ? "✦" : "—",
                TooltipText = godOn ? "Wersja BOGA (kliknij: baza)" : "Wersja BAZOWA (kliknij: bóg)",
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

        _graph.ShowSkill(_selectedSkill);
        ShowSkillDetails(_selectedSkill);
    }

    // ── szczegóły: LIVE skalowanie z aktualnego builda (broń+bóg+drzewko+arkusz) ──

    public void ShowSkillDetails(string skillId)
    {
        var spec = GameState.ClassSpec.Skill(skillId);
        if (spec == null) return;
        var s = PlayerController.Local?.BuildSkill(skillId);

        var sb = new StringBuilder();
        sb.Append($"[b]{spec.Name}[/b]  (wym. poziom {spec.RequiredLevel})   ");
        sb.AppendLine(GameState.GodSkills.Contains(skillId) && GameState.PledgedGod != GodId.None
            ? $"[color=#c9a0ff]wersja: {Gods.Name(GameState.PledgedGod)} ✦[/color]" : "wersja: bazowa");
        sb.AppendLine(spec.Description);

        if (s != null)
        {
            sb.Append($"[color=#f0c060]Obrażenia: {s.Damage:0.#} ({s.DamageType})[/color]");
            if (spec.WeaponScaling > 0) sb.Append($"   [color=#9a9a9a](baza {spec.BaseDamage:0.#} + {spec.WeaponScaling:P0} obrażeń broni)[/color]");
            sb.AppendLine();
            sb.Append($"Koszt: {s.ConcentrationCost * s.CostMult:0.#} {GameState.Class.ResourceName}");
            sb.Append($"   CD: {spec.Cooldown * s.CdMult:0.##}s");
            sb.AppendLine($"   Czas rzucenia: {s.CastTime:0.##}s ({(spec.UsesAttackSpeed ? "atk" : "cast")} speed)");

            var extras = new List<string>();
            if (s.AppliesMark) extras.Add($"oznacza ({s.MarkDuration:0.#}s)");
            if (s.MarkedMultiplier > 1f) extras.Add($"×{s.MarkedMultiplier:0.##} na oznaczonych");
            if (s.Pierces) extras.Add("przebija");
            if (s.PierceMarkedOnly) extras.Add("przebija oznaczonych");
            if (s.Explodes) extras.Add("eksploduje");
            if (s.ExtraProjectiles > 0) extras.Add($"+{s.ExtraProjectiles} pociski");
            if (s.OnHitStatus != StatusType.None) extras.Add($"{s.OnHitStatus} {s.StatusDps:0.#}/s przez {s.StatusDuration:0.#}s");
            if (s.StunDuration > 0) extras.Add($"stun {s.StunDuration:0.#}s");
            if (s.HealOnHit > 0) extras.Add($"leczy {s.HealOnHit:0.#} za trafienie");
            if (s.AoeMult != 1f) extras.Add($"obszar ×{s.AoeMult:0.##}");
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
        sb.AppendLine($"[b]{node.Name}[/b]  [koszt: {node.Cost} pkt{(node.RequiredLevel > 0 ? $" · wym. poziom {node.RequiredLevel}" : "")}]");
        sb.AppendLine(node.Description);
        if (node.Requires != null)
            sb.AppendLine($"[color=#9a9a9a]Wymaga węzła: {GameData.FindNode(skillId, node.Requires)?.Name}[/color]");
        if (node.ExclusiveGroup != null)
        {
            var siblings = GameData.Trees[skillId].Where(n => n.ExclusiveGroup == node.ExclusiveGroup && n.Id != node.Id).Select(n => n.Name);
            sb.AppendLine($"[color=#d47070]Wyklucza się z: {string.Join(", ", siblings)}[/color]");
        }
        sb.AppendLine(allocated ? "[color=#8fd48f]✔ ODBLOKOWANY[/color]"
            : reason != null ? $"[color=#d4a050]🔒 {reason}[/color]"
            : "[color=#f0e080]Kliknij ponownie, aby odblokować[/color]");
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
                    Text = $"{node.Name}\n[{node.Cost} pkt{(node.RequiredLevel > 0 ? $" · poz.{node.RequiredLevel}" : "")}]",
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
                DrawString(ThemeDB.FallbackFont, (a + b) / 2f + new Vector2(6, 4), "ALBO",
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

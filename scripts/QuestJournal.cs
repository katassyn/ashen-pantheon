using System.Linq;
using System.Text;
using Godot;
using AshenPantheon.Core;

/// <summary>Dziennik questów (J): aktywne z celami/postępem/nagrodami + lista ukończonych.</summary>
public partial class QuestJournal : CanvasLayer
{
    private Panel _root;
    private RichTextLabel _text;

    public override void _Ready()
    {
        Layer = 8;
        _root = new Panel
        {
            AnchorLeft = 0f, AnchorTop = 0f, AnchorRight = 1f, AnchorBottom = 1f,
            OffsetLeft = 40, OffsetTop = 36, OffsetRight = -40, OffsetBottom = -170,
            Visible = false,
        };
        UiPanels.Solidify(_root);
        AddChild(_root);

        var vb = new VBoxContainer { AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = 16, OffsetTop = 12, OffsetRight = -16, OffsetBottom = -12 };
        vb.AddThemeConstantOverride("separation", 8);
        _root.AddChild(vb);
        vb.AddChild(new Label { Text = "QUEST JOURNAL    [J] close" });

        var scroll = UiKit.VScroll();
        _text = new RichTextLabel { BbcodeEnabled = true, FitContent = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_text);
        vb.AddChild(scroll);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey k || !k.Pressed || k.Echo) return;
        if (Keybinds.Matches(k, "journal"))
        {
            if (_root.Visible) { _root.Visible = false; }
            else { UiPanels.CloseAllExcept(GetTree(), null); _root.Visible = true; Rebuild(); }
            GetViewport().SetInputAsHandled();
        }
        else if (k.PhysicalKeycode == Key.Escape && _root.Visible)
        {
            _root.Visible = false;
            GetViewport().SetInputAsHandled();
        }
    }

    private void Rebuild()
    {
        var log = GameState.Quests;
        var sb = new StringBuilder();

        sb.AppendLine("[b][color=#f0e080]ACTIVE[/color][/b]");
        if (log.Active.Count == 0) sb.AppendLine("[color=#9a9a9a]  No active quests. Seek out quest givers (◈) in the world.[/color]");
        foreach (var questId in log.Active.Keys)
        {
            var q = QuestCatalog.Find(questId);
            if (q == null) continue;
            sb.AppendLine($"\n[b]{q.Name}[/b]  [color=#9a9a9a](lvl {q.RequiredLevel})[/color]");
            sb.AppendLine($"[color=#c0c0c0]{q.Description}[/color]");
            foreach (var o in q.Objectives)
            {
                int cur = log.Progress(q.Id, o.Id);
                bool done = cur >= o.Amount;
                string mark = done ? "[color=#8fd48f]✔[/color]" : "•";
                sb.AppendLine($"   {mark} {o.Description}  [color=#e0d090]{cur}/{o.Amount}[/color]");
            }
            if (log.ReadyToTurnIn(q))
                sb.AppendLine($"   [color=#8fd48f]→ Ready! Turn in at: {QuestNpc.NpcName(q.TurnIn)}[/color]");
            sb.AppendLine($"   [color=#9a9a9a]Reward: {q.RewardXp} XP, {q.RewardGold} gold[/color]");
        }

        sb.AppendLine($"\n[b][color=#7fae7f]COMPLETED ({log.Completed.Count})[/color][/b]");
        var names = log.Completed.Select(id => QuestCatalog.Find(id)?.Name ?? id).OrderBy(n => n);
        sb.AppendLine("[color=#8a8a8a]" + (names.Any() ? string.Join(" · ", names) : "  none yet") + "[/color]");

        _text.Text = sb.ToString();
    }
}

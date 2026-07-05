using System.Linq;
using System.Text;
using Godot;
using AshenPantheon.Core;

/// <summary>Rozmowa z NPC questowym (E przy znaczniku) — INTERAKTYWNY dialog:
/// oferta questa = Accept/Decline, gotowy quest = Complete, w toku = postęp + Close.
/// Nic nie dzieje się automatycznie — gracz klika.</summary>
public static class QuestNpc
{
    public static void Interact(string npcId, SceneTree tree)
    {
        var log = GameState.Quests;
        int level = GameState.Progress.Level;

        // cel "Talk" zalicza się samą rozmową (może domknąć quest przed sprawdzeniem turn-in)
        if (log.OnTalk(npcId)) GameState.Save();

        // 1) quest gotowy do oddania u tego NPC → przycisk Complete
        var ready = log.Active.Keys
            .Select(QuestCatalog.Find)
            .FirstOrDefault(q => q != null && q.TurnIn == npcId && log.ReadyToTurnIn(q));
        if (ready != null)
        {
            string text = string.Join("\n", ready.DialogueCompletion)
                + $"\n\n✔ {ready.Name} — all objectives done."
                + $"\nRewards: +{ready.RewardXp} XP, +{ready.RewardGold} gold";
            Dialog(tree, npcId, text,
                ("Complete quest", () => CompleteQuest(ready, npcId, tree)),
                ("Close", null));
            return;
        }

        // 2) NPC ma quest do zaoferowania → Accept / Decline
        var offered = QuestCatalog.Quests.Values
            .FirstOrDefault(q => q.QuestGiver == npcId && log.CanAccept(q, level));
        if (offered != null)
        {
            OfferDialog(tree, npcId, offered);
            return;
        }

        // 3) quest w toku od/do tego NPC → pokaż postęp
        var inProgress = log.Active.Keys
            .Select(QuestCatalog.Find)
            .FirstOrDefault(q => q != null && (q.QuestGiver == npcId || q.TurnIn == npcId));
        if (inProgress != null)
        {
            var sb = new StringBuilder($"How goes it? Your task is not finished yet.\n\n▶ {inProgress.Name}\n");
            foreach (var o in inProgress.Objectives)
            {
                int cur = log.Progress(inProgress.Id, o.Id);
                sb.AppendLine($"   {(cur >= o.Amount ? "✔" : "•")} {o.Description}  {cur}/{o.Amount}");
            }
            Dialog(tree, npcId, sb.ToString(), ("Close", null));
            return;
        }

        Dialog(tree, npcId, "I have no tasks for you now. Return stronger.", ("Close", null));
    }

    /// <summary>Oferta questa: opis + cele + nagrody, przyciski Accept/Decline.
    /// prefix = np. podsumowanie właśnie oddanego questa (łańcuch).</summary>
    private static void OfferDialog(SceneTree tree, string npcId, QuestDefinition q, string prefix = "")
    {
        var sb = new StringBuilder(prefix);
        sb.AppendLine(string.Join("\n", q.DialogueStart));
        sb.AppendLine($"\n▶ {q.Name}   (lvl {q.RequiredLevel})");
        sb.AppendLine("Objectives:");
        foreach (var o in q.Objectives)
            sb.AppendLine($"   • {o.Description}" + (o.Amount > 1 ? $"  (x{o.Amount})" : ""));
        sb.Append($"Rewards: +{q.RewardXp} XP, +{q.RewardGold} gold");

        Dialog(tree, npcId, sb.ToString(),
            ("Accept quest", () =>
            {
                if (!GameState.Quests.Accept(q, GameState.Progress.Level)) return;
                GameState.Quests.OnTalk(npcId); // rozmowa u questgivera zalicza cel Talk
                GameState.Save();
                Net.SendChatLocal($"Quest accepted: {q.Name}");
            }),
            ("Decline", null));
    }

    /// <summary>Klik "Complete quest": nagrody + jeśli łańcuch ma następny quest — od razu jego oferta.</summary>
    private static void CompleteQuest(QuestDefinition q, string npcId, SceneTree tree)
    {
        var log = GameState.Quests;
        if (!log.ReadyToTurnIn(q)) return; // stan mógł się zmienić — nie oddajemy na siłę
        var next = log.TurnIn(q);
        GameState.Progress.GainXp(q.RewardXp);
        GameState.Wallet.Gold += q.RewardGold;
        GameState.Save();
        PlayerController.Local?.Refresh();
        Net.SendChatLocal($"Quest completed: {q.Name}  (+{q.RewardXp} XP, +{q.RewardGold} gold)");

        string summary = $"✔ Completed: {q.Name}   (+{q.RewardXp} XP, +{q.RewardGold} gold)";
        if (next != null && log.CanAccept(next, GameState.Progress.Level))
            OfferDialog(tree, npcId, next, summary + "\n\n"); // łańcuch: od razu oferta następnego
        else
            Dialog(tree, npcId, summary, ("Close", null));
    }

    /// <summary>Wskaźnik nad NPC: '?' = quest do oddania, '!' = quest do wzięcia, null = nic.</summary>
    public static char? Indicator(string npcId)
    {
        var log = GameState.Quests;
        if (log.Active.Keys.Select(QuestCatalog.Find)
            .Any(q => q != null && q.TurnIn == npcId && log.ReadyToTurnIn(q))) return '?';
        if (QuestCatalog.Quests.Values
            .Any(q => q.QuestGiver == npcId && log.CanAccept(q, GameState.Progress.Level))) return '!';
        return null;
    }

    public static string NpcName(string npcId) => npcId switch
    {
        "amuun" => "Amuun the Mystic",
        "guildmaster" => "Guildmaster",
        _ => npcId,
    };

    /// <summary>Okno dialogu z dowolnym zestawem przycisków (akcja null = tylko zamknij).
    /// E/Esc zawsze zamyka bez akcji (Decline/Close).</summary>
    private static void Dialog(SceneTree tree, string npcId, string text,
        params (string Label, System.Action OnPressed)[] buttons)
    {
        // jedno okno naraz — stare przemianuj przed QueueFree, żeby nowe zachowało nazwę
        if (tree.Root.GetNodeOrNull<CanvasLayer>("QuestDialog") is { } old)
        {
            old.Name = "QuestDialogOld";
            old.QueueFree();
        }

        var layer = new CanvasLayer { Name = "QuestDialog", Layer = 15 };
        var panel = new Panel
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -420, OffsetRight = 420, OffsetTop = -190, OffsetBottom = 190,
        };
        UiPanels.Solidify(panel);
        layer.AddChild(panel);

        var vb = new VBoxContainer { AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = 14, OffsetTop = 10, OffsetRight = -14, OffsetBottom = -10 };
        vb.AddThemeConstantOverride("separation", 8);
        panel.AddChild(vb);
        var title = new Label { Text = $"— {NpcName(npcId)} —" };
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.5f));
        vb.AddChild(title);
        vb.AddChild(new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 12);
        vb.AddChild(row);
        foreach (var (label, action) in buttons)
        {
            var btn = new Button { Text = label, CustomMinimumSize = new Vector2(150, 0) };
            var captured = action;
            btn.Pressed += () =>
            {
                layer.Name = "QuestDialogOld"; // akcja może otworzyć kolejny dialog
                layer.QueueFree();
                captured?.Invoke();
            };
            row.AddChild(btn);
        }

        var closer = new DialogCloser { Target = layer };
        layer.AddChild(closer);
        tree.Root.AddChild(layer);
    }
}

public partial class DialogCloser : Node
{
    public CanvasLayer Target;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode is Key.Escape or Key.E)
        {
            Target?.QueueFree();
            GetViewport().SetInputAsHandled();
        }
    }
}

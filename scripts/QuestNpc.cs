using System.Linq;
using Godot;
using AshenPantheon.Core;

/// <summary>Logika rozmowy z NPC questowym (E przy znaczniku): oddaj gotowe → przyjmij dostępne → cel Talk.
/// Dialog jako proste okno tekstowe.</summary>
public static class QuestNpc
{
    public static void Interact(string npcId, SceneTree tree)
    {
        var log = GameState.Quests;
        int level = GameState.Progress.Level;

        // 1) oddanie gotowego questa u tego NPC
        var ready = log.Active.Keys
            .Select(QuestCatalog.Find)
            .FirstOrDefault(q => q != null && q.TurnIn == npcId && log.ReadyToTurnIn(q));
        if (ready != null)
        {
            var next = log.TurnIn(ready);
            GameState.Progress.GainXp(ready.RewardXp);
            GameState.Wallet.Gold += ready.RewardGold;
            GameState.Save();
            PlayerController.Local?.Refresh();
            string text = string.Join("\n", ready.DialogueCompletion)
                + $"\n\n✔ Ukończono: {ready.Name}   (+{ready.RewardXp} XP, +{ready.RewardGold} złota)";
            if (next != null && log.Accept(next, level))
            {
                GameState.Save();
                text += $"\n\n▶ Nowy quest: {next.Name}\n" + string.Join("\n", next.DialogueStart);
            }
            Dialog(tree, NpcName(npcId), text);
            return;
        }

        // 2) przyjęcie dostępnego questa od tego NPC
        var offered = QuestCatalog.Quests.Values
            .FirstOrDefault(q => q.QuestGiver == npcId && log.CanAccept(q, level));
        if (offered != null)
        {
            log.Accept(offered, level);
            log.OnTalk(npcId); // rozmowa u questgivera zalicza cel Talk
            GameState.Save();
            Dialog(tree, NpcName(npcId), string.Join("\n", offered.DialogueStart) + $"\n\n▶ Przyjęto: {offered.Name}");
            return;
        }

        // 3) cel Talk w aktywnych questach
        if (log.OnTalk(npcId)) { GameState.Save(); Dialog(tree, NpcName(npcId), "Dobrze, że jesteś. Rób swoje."); return; }

        Dialog(tree, NpcName(npcId), "Nie mam teraz dla ciebie zadań. Wróć silniejszy.");
    }

    public static string NpcName(string npcId) => npcId switch
    {
        "amuun" => "Amuun, Mistyk",
        "guildmaster" => "Mistrz Gildii",
        _ => npcId,
    };

    private static void Dialog(SceneTree tree, string npcName, string text)
    {
        // jedno okno naraz
        tree.Root.GetNodeOrNull<CanvasLayer>("QuestDialog")?.QueueFree();

        var layer = new CanvasLayer { Name = "QuestDialog", Layer = 15 };
        var panel = new Panel
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = -420, OffsetRight = 420, OffsetTop = -330, OffsetBottom = -170,
        };
        UiPanels.Solidify(panel);
        layer.AddChild(panel);

        var vb = new VBoxContainer { AnchorRight = 1f, AnchorBottom = 1f, OffsetLeft = 14, OffsetTop = 10, OffsetRight = -14, OffsetBottom = -10 };
        panel.AddChild(vb);
        var title = new Label { Text = $"— {npcName} —" };
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.5f));
        vb.AddChild(title);
        vb.AddChild(new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        var close = new Button { Text = "Zamknij [E/Esc]" };
        close.Pressed += () => layer.QueueFree();
        vb.AddChild(close);

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

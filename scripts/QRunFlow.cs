using Godot;
using AshenPantheon.Core;

/// <summary>Domykanie runu Q (The Proving): auto-quest oddaje się SAM po spełnieniu wszystkich celów
/// (bez NPC — nagrody + wpisy do czatu) i odblokowuje następny stopień u całej drużyny.
/// Wołane po każdym zdarzeniu questowym, które może dopiąć run (kill / interact / clear).</summary>
public static class QRunFlow
{
    public static void CheckAutoComplete()
    {
        string challenge = Net.TravelChallengeId;
        if (!EndgameCatalog.TryParseQ(challenge, out int q, out var qd)) return;
        var run = EndgameCatalog.RunFor(q);
        var quest = run == null ? null : QuestCatalog.Find(run.Quest);
        if (quest == null || !GameState.Quests.ReadyToTurnIn(quest)) return;

        GameState.Quests.TurnIn(quest);
        GameState.Progress.GainXp(quest.RewardXp);
        GameState.Wallet.Gold += quest.RewardGold;
        // nagroda kanoniczna: Elite Lootbox (do Sakwy — otwierasz w EQ → Pouch)
        GameState.Pouch.Add("elite_lootbox");
        GameState.Save();
        PlayerController.Local?.Refresh();
        Net.SendChatLocal($"Quest completed: {quest.Name}  (+{quest.RewardXp} XP, +{quest.RewardGold} gold, +1 Elite Lootbox)");
        Net.SendChatLocal($"Q{q} complete! Open the map [M] to return to town.");
        Net.BroadcastEndgameClear(challenge);
    }
}

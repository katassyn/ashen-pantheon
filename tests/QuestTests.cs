using System.Linq;
using AshenPantheon.Core;
using Xunit;

public class QuestTests
{
    public QuestTests() => TestData.EnsureLoaded();

    private static QuestDefinition Q(string id) => QuestCatalog.Find(id)!;

    [Fact]
    public void Chain_PrerequisitesGateAcceptance()
    {
        var log = new QuestLog();
        Assert.False(log.CanAccept(Q("swerdfield_02"), 99)); // wymaga 01
        Assert.True(log.Accept(Q("swerdfield_01"), 1));
        log.OnTalk("amuun");
        log.OnKill("husk"); // za mało — cel 10
        Assert.False(log.ReadyToTurnIn(Q("swerdfield_01")));
        for (int i = 0; i < 9; i++) log.OnKill("husk");
        Assert.True(log.ReadyToTurnIn(Q("swerdfield_01")));

        var next = log.TurnIn(Q("swerdfield_01"));
        Assert.Equal("swerdfield_02", next!.Id);
        Assert.True(log.IsCompleted("swerdfield_01"));
        Assert.True(log.CanAccept(next, 2));
    }

    [Fact]
    public void Kill_CountsOnlyMatchingTargetAndCaps()
    {
        var log = new QuestLog();
        log.Accept(Q("swerdfield_01"), 1);
        log.OnKill("spitter"); // nie ten cel
        Assert.Equal(0, log.Progress("swerdfield_01", "kill_villagers"));
        for (int i = 0; i < 15; i++) log.OnKill("husk");
        Assert.Equal(10, log.Progress("swerdfield_01", "kill_villagers")); // cap na amount
    }

    [Fact]
    public void Reach_IsOneShot()
    {
        var log = new QuestLog();
        log.Completed.Add("swerdfield_01");
        log.Accept(Q("swerdfield_02"), 2);
        Assert.True(log.OnReach("old_shrine"));
        Assert.Equal(1, log.Progress("swerdfield_02", "reach_shrine"));
    }

    [Fact]
    public void Clear_CompletesDungeonQuest()
    {
        var log = new QuestLog();
        log.Completed.Add("swerdfield_01");
        log.Completed.Add("swerdfield_02");
        log.Accept(Q("swerdfield_03"), 3);
        log.OnClear("ashen_wastes");
        Assert.True(log.ReadyToTurnIn(Q("swerdfield_03")));
    }

    [Fact]
    public void Escort_FailureResetsProgress()
    {
        QuestCatalog.Load("""
            { "quests": [ { "id": "t_escort", "name": "T", "objectives": [
                { "id": "esc", "type": "Escort", "target": "pilgrim", "amount": 1 } ] } ] }
            """);
        var log = new QuestLog();
        log.Accept(Q("t_escort"), 1);
        log.OnEscortArrived("pilgrim");
        Assert.True(log.ReadyToTurnIn(Q("t_escort")));
        // symulacja: nowy przebieg — porażka zeruje
        log.Active["t_escort"]["esc"] = 1;
        log.OnEscortFailed("pilgrim");
        Assert.Equal(0, log.Progress("t_escort", "esc"));
    }

    [Fact]
    public void DefendAndSurvive_TypesAccumulate()
    {
        QuestCatalog.Load("""
            { "quests": [ { "id": "t_def", "name": "T", "objectives": [
                { "id": "waves", "type": "Defend", "target": "gate", "amount": 3 },
                { "id": "time", "type": "Survive", "target": "gate", "amount": 30 } ] } ] }
            """);
        var log = new QuestLog();
        log.Accept(Q("t_def"), 1);
        log.OnDefendWave("gate");
        log.OnDefendWave("gate");
        Assert.Equal(2, log.Progress("t_def", "waves"));
        log.OnSurviveSeconds("gate", 30);
        Assert.Equal(30, log.Progress("t_def", "time"));
    }
}

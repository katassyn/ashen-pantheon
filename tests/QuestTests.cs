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
        log.OnKill("undead_villager"); // za mało — cel 10
        Assert.False(log.ReadyToTurnIn(Q("swerdfield_01")));
        for (int i = 0; i < 9; i++) log.OnKill("undead_villager");
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
        for (int i = 0; i < 15; i++) log.OnKill("undead_villager");
        Assert.Equal(10, log.Progress("swerdfield_01", "kill_villagers")); // cap na amount
    }

    [Fact]
    public void Abandon_DropsProgressAndAllowsReaccept()
    {
        var log = new QuestLog();
        log.Accept(Q("swerdfield_01"), 1);
        for (int i = 0; i < 5; i++) log.OnKill("undead_villager");
        Assert.Equal(5, log.Progress("swerdfield_01", "kill_villagers"));

        Assert.True(log.Abandon("swerdfield_01"));
        Assert.False(log.IsActive("swerdfield_01"));
        Assert.False(log.Abandon("swerdfield_01")); // drugi raz nie ma czego porzucać

        Assert.True(log.CanAccept(Q("swerdfield_01"), 1)); // wraca do questgivera
        log.Accept(Q("swerdfield_01"), 1);
        Assert.Equal(0, log.Progress("swerdfield_01", "kill_villagers")); // postęp od zera
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
    public void SilfmoorChain_LoadsWithEscortAndDefend()
    {
        // realne dane kampanii: łańcuch kill -> escort -> defend
        var escort = Q("silfmoor_02").Objectives.Single();
        Assert.Equal(ObjectiveType.Escort, escort.Kind);
        Assert.Equal("silfmoor_escort", escort.Target);

        var defend = Q("silfmoor_03").Objectives.Single();
        Assert.Equal(ObjectiveType.Defend, defend.Kind);
        Assert.Equal(3, defend.Amount);

        Assert.Contains("silfmoor_01", Q("silfmoor_02").Prerequisites);
    }

    [Fact]
    public void SilfmoorZone_HasEscortAndDefendMarkers()
    {
        var zone = WorldMaps.Zone("silfmoor");
        Assert.Contains(zone.Markers, m => m.Type == "escort" && m.Id == "silfmoor_escort");
        var defend = zone.Markers.Single(m => m.Type == "defend");
        Assert.Equal(3, defend.Waves);
        Assert.NotEmpty(defend.WaveMonsters);
    }

    [Fact]
    public void Collect_AccumulatesFromQuestItems()
    {
        var log = new QuestLog();
        log.Completed.Add("teganswall_01");
        log.Accept(Q("teganswall_02"), 17);
        log.OnKill("longhelm_guard"); // wrong drop
        Assert.Equal(0, log.Progress("teganswall_02", "collect_seals"));
        log.OnCollect("watch_seal");
        log.OnCollect("watch_seal");
        log.OnCollect("watch_seal");
        Assert.True(log.ReadyToTurnIn(Q("teganswall_02")));
    }

    [Fact]
    public void Survive_AccumulatesSecondsToTarget()
    {
        var log = new QuestLog();
        log.Completed.Add("eternal_01");
        log.Accept(Q("eternal_02"), 22);
        for (int s = 0; s < 40; s++) log.OnSurviveSeconds("eternal_survive", 1);
        Assert.True(log.ReadyToTurnIn(Q("eternal_02")));
    }

    [Fact]
    public void FullCampaignChain_LinksSwerdfieldToMystra()
    {
        // każdy quest wskazuje istniejący nextQuest / prereq — brak zerwanych ogniw
        string[] chain =
        {
            "swerdfield_01","swerdfield_02","swerdfield_03",
            "silfmoor_01","silfmoor_02","silfmoor_03",
            "teganswall_01","teganswall_02","teganswall_03",
            "eternal_01","eternal_02","eternal_03",
            "mystra_01","mystra_02","mystra_03",
            "temple_01","temple_02","temple_03",
            "stalgard_01","stalgard_02","stalgard_03",
            "nahuatlan_01","nahuatlan_02","nahuatlan_03",
            "desert_01","desert_02","desert_03",
        };
        foreach (var id in chain)
        {
            var q = QuestCatalog.Find(id);
            Assert.NotNull(q);
            if (q!.NextQuest.Length > 0) Assert.NotNull(QuestCatalog.Find(q.NextQuest));
            foreach (var pre in q.Prerequisites) Assert.NotNull(QuestCatalog.Find(pre));
        }
    }

    [Fact]
    public void AllQuestTargets_ResolveToRealMobsOrMarkers()
    {
        foreach (var q in QuestCatalog.Quests.Values)
            foreach (var o in q.Objectives)
            {
                if (o.Kind == ObjectiveType.Kill && o.Target != "*")
                    Assert.True(Bestiary.Monsters.ContainsKey(o.Target), $"{q.Id}/{o.Id}: brak moba {o.Target}");
                if (o.Kind == ObjectiveType.Clear)
                    Assert.True(Bestiary.Zones.ContainsKey(o.Target), $"{q.Id}/{o.Id}: brak strefy bossa {o.Target}");
            }
    }

    [Fact]
    public void BossZones_HaveValidBossAndMonsters()
    {
        foreach (var zoneId in new[] { "ashen_wastes", "desert_tomb" })
        {
            var z = Bestiary.Zone(zoneId);
            Assert.True(Bestiary.Monsters.ContainsKey(z.Boss), $"{zoneId}: brak bossa {z.Boss}");
            Assert.True(Bestiary.Monster(z.Boss).IsBoss, $"{z.Boss} nie ma faz");
            foreach (var sw in z.Monsters)
                Assert.True(Bestiary.Monsters.ContainsKey(sw.Id), $"{zoneId}: brak moba {sw.Id}");
        }
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

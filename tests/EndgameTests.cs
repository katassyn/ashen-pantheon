using System.Linq;
using AshenPantheon.Core;
using Xunit;

public class EndgameTests
{
    public EndgameTests() => TestData.EnsureLoaded();

    [Fact]
    public void Catalog_LoadsDungeonsAndDifficulties()
    {
        Assert.True(EndgameCatalog.Loaded);
        Assert.Equal(5, EndgameCatalog.Dungeons.Count); // T1-T5 (kanon DsoCraft)
        Assert.Equal("odyssey_of_shadows", EndgameCatalog.Dungeons[0].Id);
        Assert.True(EndgameCatalog.Dungeons[0].Enabled);
        Assert.Equal(new[] { "blood", "hell", "infernal" }, EndgameCatalog.Difficulties.Select(d => d.Id));
        Assert.Equal(2, EndgameCatalog.QRuns.Count); // q1 (arena) + q2 (world)
        Assert.Equal("arena", EndgameCatalog.RunFor(1)!.Mode);
        Assert.Equal("world", EndgameCatalog.RunFor(2)!.Mode);
        Assert.Equal("final_proving_run", EndgameCatalog.RunFor(7)!.Quest); // brak wpisu → fallback do q1
        Assert.Equal(10, EndgameCatalog.QMax);
    }

    [Fact]
    public void QMaps_ChainM1ToM3()
    {
        Assert.Equal("final_proving_m2", EndgameCatalog.NextQMap("final_proving_m1"));
        Assert.Equal("final_proving_m3", EndgameCatalog.NextQMap("final_proving_m2"));
        Assert.Null(EndgameCatalog.NextQMap("final_proving_m3")); // M3 = finał
        Assert.Null(EndgameCatalog.NextQMap("swerdfield"));       // spoza runu
        Assert.Equal(3, EndgameCatalog.QMapIndex("final_proving_m3"));
        Assert.Equal("q2_m3", EndgameCatalog.NextQMap("q2_m2")); // runy niezależne
    }

    [Fact]
    public void Q2Run_MapsMobsAndGatesExist()
    {
        var run = EndgameCatalog.RunFor(2)!;
        foreach (var map in run.Maps) Assert.True(WorldMaps.Zones.ContainsKey(map)); // world-mode = strefy świata
        var quest = QuestCatalog.Find(run.Quest)!;
        Assert.Equal(5, quest.Objectives.Count);
        Assert.Equal("mushrooms", quest.Objectives[1].After); // eliksir dopiero po grzybach
        foreach (var o in quest.Objectives.Where(o => o.Kind == ObjectiveType.Kill))
            Assert.True(Bestiary.Monsters.ContainsKey(o.Target)); // bossy istnieją
        Assert.True(Bestiary.Monster("arachnia_scourge").IsBoss);
    }

    [Fact]
    public void Q2Quest_WildcardMushroomsAndAfterGate()
    {
        var log = new QuestLog();
        var q = QuestCatalog.Find("q2_run")!;
        Assert.True(log.Accept(q, 50));

        log.OnInteract("q2_cauldron"); // bramka: bez grzybów kocioł nie liczy się
        Assert.Equal(0, log.Progress(q.Id, "potion"));

        for (int i = 1; i <= 5; i++) log.OnInteract($"q2_mushroom_{i}"); // wildcard zbiera po 1
        Assert.Equal(5, log.Progress(q.Id, "mushrooms"));

        log.OnInteract("q2_cauldron");
        Assert.Equal(1, log.Progress(q.Id, "potion"));

        log.OnKill("xarib_hunchback");
        log.OnKill("arkhus_the_mad");
        log.OnKill("arachnia_scourge");
        Assert.True(log.ReadyToTurnIn(q));
    }

    [Fact]
    public void QRunQuest_TracksMapsAndIsRepeatable()
    {
        var q = QuestCatalog.Find("final_proving_run")!;
        var maps = EndgameCatalog.RunFor(1)!.Maps;
        Assert.Equal(3, q.Objectives.Count);
        // cele = Clear kolejnych map runu (auto-quest prowadzi gracza M1→M2→M3)
        Assert.Equal(maps, q.Objectives.Select(o => o.Target));
        foreach (var o in q.Objectives) Assert.Equal(ObjectiveType.Clear, o.Kind);
        // każda mapa musi istnieć w bestiariuszu (run musi dać się odpalić)
        foreach (var map in maps) Assert.True(Bestiary.Zones.ContainsKey(map));

        var log = new QuestLog();
        Assert.True(log.Accept(q, 50));
        log.OnClear("final_proving_m1");
        log.OnClear("final_proving_m2");
        Assert.False(log.ReadyToTurnIn(q));
        log.OnClear("final_proving_m3");
        Assert.True(log.ReadyToTurnIn(q));
        log.TurnIn(q);
        Assert.True(log.IsCompleted(q.Id));

        // powtarzalność: zdjęcie z Completed pozwala wziąć ponownie (wzorzec EndgamePanel.StartQRunQuest)
        log.Completed.Remove(q.Id);
        Assert.True(log.CanAccept(q, 50));
    }

    [Fact]
    public void Difficulties_ScaleMonotonically()
    {
        var d = EndgameCatalog.Difficulties;
        for (int i = 1; i < d.Count; i++)
        {
            Assert.True(d[i].HpMult > d[i - 1].HpMult);
            Assert.True(d[i].GoldFee > d[i - 1].GoldFee);
            Assert.True(d[i].ItemLevel > d[i - 1].ItemLevel);
        }
        Assert.Null(EndgameCatalog.Previous("blood"));
        Assert.Equal("hell", EndgameCatalog.Previous("infernal")!.Id);
    }

    [Fact]
    public void QScale_GrowsWithStage()
    {
        var q1 = EndgameCatalog.QScale(1);
        var q10 = EndgameCatalog.QScale(10);
        Assert.True(q10.Hp > q1.Hp);
        Assert.True(q10.Fee > q1.Fee);
        Assert.Equal(52, q1.ItemLevel);
        Assert.Equal(70, q10.ItemLevel);
    }

    [Fact]
    public void ChallengeIds_ParseRoundTrip()
    {
        Assert.True(EndgameCatalog.TryParseQ(EndgameCatalog.QChallenge(3), out int q) && q == 3);
        Assert.False(EndgameCatalog.TryParseQ("q:99", out _)); // ponad QMax
        Assert.True(EndgameCatalog.TryParseGroup(
            EndgameCatalog.GroupChallenge("odyssey_of_shadows", "hell"), out var dun, out var diff));
        Assert.Equal("The Odyssey of Shadows", dun!.Name);
        Assert.Equal("Hell", diff!.Name);
        Assert.False(EndgameCatalog.TryParseGroup("g:fake/blood", out _, out _));
    }

    [Fact]
    public void Validator_RejectsBogusEndgameProgress()
    {
        var save = new SaveData { Name = "T", ClassId = "ranger", Level = 50, EndgameQ = 99 };
        Assert.False(SaveValidator.Validate(save).Ok);

        save.EndgameQ = 4;
        save.EndgameCleared.Add("fake_dungeon/blood");
        Assert.False(SaveValidator.Validate(save).Ok);

        save.EndgameCleared.Clear();
        save.EndgameCleared.Add("odyssey_of_shadows/blood");
        Assert.True(SaveValidator.Validate(save).Ok);
    }

    [Fact]
    public void EndgameZones_ExistInBestiary()
    {
        Assert.True(Bestiary.Zones.ContainsKey("odyssey_of_shadows"));
        Assert.Equal("commander_emberwing", Bestiary.Zone("odyssey_of_shadows").Boss);
        // bossowie mają fazy (BossBar + trudność), moby stref istnieją
        Assert.True(Bestiary.Monster("commander_emberwing").IsBoss);
        foreach (var m in Bestiary.Zone("odyssey_of_shadows").Monsters)
            Assert.True(Bestiary.Monsters.ContainsKey(m.Id));

        // run Q: M1/M2 kończą mini-bossy, M3 = główny boss (wszyscy fazowi)
        Assert.True(Bestiary.Monster("black_furred_berserk").IsBoss);
        Assert.True(Bestiary.Monster("black_furred_mauler").IsBoss);
        Assert.Equal("god_of_death", Bestiary.Zone("final_proving_m3").Boss);
        Assert.True(Bestiary.Monster("god_of_death").IsBoss);
    }
}

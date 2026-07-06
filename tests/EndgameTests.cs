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
        Assert.Equal(10, EndgameCatalog.QRuns.Count); // Q1-Q10 KOMPLET (world-mode, kanon Parallel World)
        Assert.Equal("world", EndgameCatalog.RunFor(1)!.Mode);
        Assert.Equal("world", EndgameCatalog.RunFor(2)!.Mode);
        Assert.Equal("q7_run", EndgameCatalog.RunFor(7)!.Quest); // każdy Q ma własny run
        Assert.Equal("q1_run", EndgameCatalog.RunFor(99)!.Quest); // brak wpisu → fallback do q1
        Assert.Equal(10, EndgameCatalog.QMax);
    }

    [Fact]
    public void QMaps_ChainM1ToM3()
    {
        Assert.Equal("q1_m2", EndgameCatalog.NextQMap("q1_m1"));
        Assert.Equal("q1_m3", EndgameCatalog.NextQMap("q1_m2"));
        Assert.Null(EndgameCatalog.NextQMap("q1_m3"));      // M3 = finał
        Assert.Null(EndgameCatalog.NextQMap("swerdfield")); // spoza runu
        Assert.Equal(3, EndgameCatalog.QMapIndex("q1_m3"));
        Assert.Equal("q2_m3", EndgameCatalog.NextQMap("q2_m2")); // runy niezależne
    }

    [Fact]
    public void Pouch_AddTakeAndValidator()
    {
        var pouch = new Pouch();
        pouch.Add("ips", 7);
        pouch.Add("ips", 5);
        Assert.Equal(12, pouch.Count("ips"));
        Assert.True(pouch.TryTake("ips", 10));      // wejściówka Q
        Assert.Equal(2, pouch.Count("ips"));
        Assert.False(pouch.TryTake("ips", 5));       // za mało — bez zmian
        Assert.Equal(2, pouch.Count("ips"));
        Assert.True(pouch.TryTake("ips", 2));
        Assert.Equal(0, pouch.Count("ips"));         // zerowe → usunięte z licznika

        // walidator: nieznany składnik i ujemna ilość = 400
        var bad = new SaveData { Name = "T", ClassId = "ranger", Level = 50 };
        bad.Pouch["totally_fake_ingredient"] = 5;
        Assert.False(SaveValidator.Validate(bad).Ok);
        var neg = new SaveData { Name = "T", ClassId = "ranger", Level = 50 };
        neg.Pouch["ips"] = -3;
        Assert.False(SaveValidator.Validate(neg).Ok);
        var ok = new SaveData { Name = "T", ClassId = "ranger", Level = 50 };
        ok.Pouch["ips"] = 40;
        Assert.True(SaveValidator.Validate(ok).Ok);
    }

    [Fact]
    public void Ingredients_CatalogAndCategories()
    {
        Assert.True(IngredientCatalog.Loaded);
        Assert.Equal("Fragment of Infernal Passage", IngredientCatalog.Find("ips")!.Name);
        Assert.Equal("dungeon", IngredientCatalog.Find("ips")!.Category);
        // klucze T1-T5 istnieją (wejściówki dungeonów grupowych)
        for (int t = 1; t <= 5; t++) Assert.NotNull(IngredientCatalog.Find($"t{t}_key"));
        Assert.Contains("currency", IngredientCatalog.Categories);
    }

    [Fact]
    public void EliteLootbox_TableRollsValidIngredients()
    {
        Assert.True(LootTables.Tables.ContainsKey("elite_lootbox"));
        var rng = new System.Random(123);
        // każdy wyrolowany składnik musi istnieć w katalogu (drop nie stworzy fantomu)
        for (int i = 0; i < 40; i++)
            foreach (var drop in LootTables.Roll("elite_lootbox", rng, new LootGenerator(i), 56))
                if (drop.Ingredient.Length > 0)
                {
                    Assert.NotNull(IngredientCatalog.Find(drop.Ingredient));
                    Assert.True(drop.IngredientCount >= 1);
                }
    }

    [Fact]
    public void AllQRuns_AreInternallyConsistent()
    {
        // uniwersalny strażnik: każdy run Q ma istniejący quest, mapy, targety i dropujące moby
        foreach (var run in EndgameCatalog.QRuns)
        {
            var quest = QuestCatalog.Find(run.Quest);
            Assert.True(quest != null, $"brak questa {run.Quest}");
            Assert.Equal(3, run.Maps.Count);
            foreach (var map in run.Maps)
                Assert.True(run.Mode == "world" ? WorldMaps.Zones.ContainsKey(map) : Bestiary.Zones.ContainsKey(map),
                    $"brak mapy {map}");

            foreach (var o in quest!.Objectives)
                switch (o.Kind)
                {
                    case ObjectiveType.Kill:
                        Assert.True(Bestiary.Monsters.ContainsKey(o.Target), $"{run.Quest}: brak moba {o.Target}");
                        break;
                    case ObjectiveType.Collect:
                        Assert.True(Bestiary.Monsters.Values.Any(m => m.QuestItem == o.Target),
                            $"{run.Quest}: nikt nie dropi {o.Target}");
                        break;
                }
        }
    }

    [Fact]
    public void Q6_DaggerPartsAnyOrder_AndQ8AfterGate()
    {
        var log = new QuestLog();
        var q6 = QuestCatalog.Find("q6_run")!;
        log.Accept(q6, 50);
        // dowolna kolejność części sztyletu (kanon: nie wolno blokować gracza kolejnością)
        log.OnCollect("dagger_gem");
        log.OnCollect("dagger_hilt");
        log.OnCollect("dagger_blade");
        Assert.Equal(1, log.Progress(q6.Id, "gem"));
        Assert.Equal(1, log.Progress(q6.Id, "hilt"));

        var log8 = new QuestLog();
        var q8 = QuestCatalog.Find("q8_run")!;
        log8.Accept(q8, 50);
        log8.OnInteract("q8_conduit"); // bez odłamków konduit nie działa (after-gate)
        Assert.Equal(0, log8.Progress(q8.Id, "conduit"));
        for (int i = 0; i < 5; i++) log8.OnCollect("electric_shard");
        log8.OnInteract("q8_conduit");
        Assert.Equal(1, log8.Progress(q8.Id, "conduit"));
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
    public void Q1Run_CanonFlowAndRepeatable()
    {
        // kanon z MyDungeonTeleportPlugin: Forgotten Circle → 25+25 flamecultów → Dragonknight → Grimmor
        var q = QuestCatalog.Find("q1_run")!;
        var maps = EndgameCatalog.RunFor(1)!.Maps;
        Assert.Equal(5, q.Objectives.Count);
        foreach (var map in maps) Assert.True(WorldMaps.Zones.ContainsKey(map));
        Assert.True(Bestiary.Monster("grimmor_the_risen").IsBoss);
        Assert.True(Bestiary.Monster("parallel_dragonknight").IsBoss);

        var log = new QuestLog();
        Assert.True(log.Accept(q, 50));
        log.OnReach("q1_forgotten_circle");
        for (int i = 0; i < 25; i++) { log.OnKill("flamecult_servant"); log.OnKill("flamecult_archer"); }
        log.OnKill("parallel_dragonknight");
        Assert.False(log.ReadyToTurnIn(q));
        log.OnKill("grimmor_the_risen");
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
    public void QDifficulties_CanonInfHellBlood()
    {
        Assert.Equal(new[] { "inf", "hell", "blood" }, EndgameCatalog.QDifficulties.Select(d => d.Id));
        var inf = EndgameCatalog.QDifficulty("inf")!;
        var hell = EndgameCatalog.QDifficulty("hell")!;
        var blood = EndgameCatalog.QDifficulty("blood")!;
        // kanon gate poziomów i opłat IPS (ListenerQ*: 50/65/80, 10/25/50)
        Assert.Equal((50, 10), (inf.LevelReq, inf.IpsFee));
        Assert.Equal((65, 25), (hell.LevelReq, hell.IpsFee));
        Assert.Equal((80, 50), (blood.LevelReq, blood.IpsFee));
        // skala rośnie monotonicznie (HP z YML: x1/x3/x10)
        Assert.True(blood.HpMult > hell.HpMult && hell.HpMult > inf.HpMult);
        Assert.True(blood.ItemLevel > hell.ItemLevel && hell.ItemLevel > inf.ItemLevel);
        Assert.Same(inf, EndgameCatalog.DefaultQDifficulty); // Infernal = domyślna
    }

    [Fact]
    public void ChallengeIds_ParseWithDifficulty()
    {
        // "q:3:hell" → (3, Hell)
        Assert.True(EndgameCatalog.TryParseQ(EndgameCatalog.QChallenge(3, "hell"), out int q, out var qd));
        Assert.Equal(3, q);
        Assert.Equal("hell", qd!.Id);
        // wstecznie "q:3" (bez trudności) → Infernal
        Assert.True(EndgameCatalog.TryParseQ("q:3", out _, out var qd2));
        Assert.Equal("inf", qd2!.Id);
        Assert.False(EndgameCatalog.TryParseQ("q:99:hell", out _, out _)); // ponad QMax
        // dungeon grupowy bez zmian
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

        // run Q: M1/M2 kończą mini-bossy, M3 = główny boss (roster 1:1 z MythicMobs q?_inf.yml)
        Assert.True(Bestiary.Monster("xarib_hunchback").IsBoss);
        Assert.True(Bestiary.Monster("arkhus_the_mad").IsBoss);
        Assert.True(Bestiary.Monster("raazgor_corrupter").IsBoss); // elitka M1 Q1 (kanon: spawn obok Circle)
        Assert.True(Bestiary.Monster("arachnia_scourge").IsBoss);
    }
}

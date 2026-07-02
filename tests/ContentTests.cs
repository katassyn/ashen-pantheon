using System;
using System.Linq;
using AshenPantheon.Core;
using Xunit;

public class BestiaryTests
{
    private const string BossJson = """
        { "id":"t_boss", "name":"Test Boss", "hp":400, "attackInterval":2.0,
          "phases":[
            { "hpBelow":1.0, "attackInterval":2.0, "abilities":[ {"type":"tele_circle","damage":20} ] },
            { "hpBelow":0.5, "attackInterval":1.2, "abilities":[ {"type":"summon","monsterId":"husk","count":2}, {"type":"tele_line","damage":30} ] }
          ] }
        """;

    [Fact]
    public void Parses_MonsterFromJson()
    {
        Bestiary.LoadMonster("""{ "id":"t_husk", "name":"T", "hp":55, "movement":"chase", "xp":10, "lootTable":"common", "abilities":[{"type":"melee","damage":9,"reach":50}] }""");
        var def = Bestiary.Monster("t_husk");
        Assert.Equal(55f, def.Hp);
        Assert.False(def.IsBoss);
        Assert.Single(def.Abilities);
        Assert.Equal("melee", def.Abilities[0].Type);
    }

    [Fact]
    public void BossPhases_SwitchByHpFraction()
    {
        Bestiary.LoadMonster(BossJson);
        var def = Bestiary.Monster("t_boss");
        Assert.True(def.IsBoss);

        var (full, fullInterval) = def.ActiveSet(0.9f);
        Assert.Single(full);
        Assert.Equal(2.0f, fullInterval);

        var (low, lowInterval) = def.ActiveSet(0.3f);
        Assert.Equal(2, low.Count);
        Assert.Equal(1.2f, lowInterval);
        Assert.Contains(low, a => a.Type == "summon" && a.MonsterId == "husk");
    }

    [Fact]
    public void Zone_RollsOnlyFromPool()
    {
        Bestiary.LoadZone("""{ "id":"t_zone", "monsters":[{"id":"a","weight":1},{"id":"b","weight":3}], "boss":"t_boss" }""");
        var zone = Bestiary.Zone("t_zone");
        var rng = new Random(5);
        for (int i = 0; i < 50; i++)
            Assert.Contains(zone.RollMonster(rng), new[] { "a", "b" });
    }
}

public class LootTableTests
{
    private static void LoadTables()
    {
        LootTables.Load("""
            { "id":"t_common", "rolls":1, "entries":[
                {"type":"nothing","weight":50},
                {"type":"gold","weight":25,"goldMin":3,"goldMax":10},
                {"type":"item","weight":25} ] }
            """);
        LootTables.Load("""
            { "id":"t_boss", "rolls":3, "entries":[
                {"type":"gold","weight":30,"goldMin":20,"goldMax":40},
                {"type":"item","weight":40,"rarity":"Rare"},
                {"type":"table","weight":30,"table":"t_common"} ] }
            """);
    }

    [Fact]
    public void Roll_IsDeterministicForSeed()
    {
        LoadTables();
        var a = LootTables.Roll("t_boss", new Random(7), new LootGenerator(7));
        var b = LootTables.Roll("t_boss", new Random(7), new LootGenerator(7));
        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Gold, b[i].Gold);
            Assert.Equal(a[i].Item?.Name, b[i].Item?.Name);
        }
    }

    [Fact]
    public void NestedTable_ResolvesRecursively()
    {
        LoadTables();
        // wiele rzutów — zagnieżdżona t_common musi się kiedyś wylosować i rozwiązać bez błędu
        var rng = new Random(3);
        var gen = new LootGenerator(3);
        for (int i = 0; i < 40; i++)
            LootTables.Roll("t_boss", rng, gen);
    }

    [Fact]
    public void ForcedRarity_IsRespected()
    {
        LoadTables();
        var rng = new Random(11);
        var gen = new LootGenerator(11);
        for (int i = 0; i < 60; i++)
            foreach (var drop in LootTables.Roll("t_boss", rng, gen).Where(d => d.Item != null))
                if (drop.Item!.Rarity is Rarity.Rare)
                    return; // znaleziono wymuszony Rare → OK
        Assert.Fail("wymuszona rzadkość Rare nigdy nie wypadła");
    }

    [Fact]
    public void UnknownTable_ReturnsEmpty()
    {
        Assert.Empty(LootTables.Roll("nie_ma", new Random(1), new LootGenerator(1)));
    }
}

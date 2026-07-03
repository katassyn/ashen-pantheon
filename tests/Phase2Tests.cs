using System.IO;
using System.Linq;
using AshenPantheon.Core;
using Xunit;

public class GridInventoryTests
{
    private static Item ItemOf(ItemKind kind) => new() { Name = "t", Kind = kind };

    [Fact]
    public void PlaceAt_RejectsOverlap()
    {
        var g = new GridInventory(4, 4);
        Assert.True(g.PlaceAt(ItemOf(ItemKind.Helmet), 0, 0));   // 2x2
        Assert.False(g.CanPlaceAt(ItemOf(ItemKind.Ring), 1, 1)); // środek hełmu
        Assert.True(g.CanPlaceAt(ItemOf(ItemKind.Ring), 2, 0));
    }

    [Fact]
    public void PlaceAt_RejectsOutOfBounds()
    {
        var g = new GridInventory(4, 4);
        Assert.False(g.CanPlaceAt(ItemOf(ItemKind.TwoHandWeapon), 3, 0)); // 2x4 nie mieści się od x=3
        Assert.True(g.CanPlaceAt(ItemOf(ItemKind.TwoHandWeapon), 0, 0));
    }

    [Fact]
    public void AutoPlace_FillsUntilFull()
    {
        var g = new GridInventory(2, 2);
        Assert.True(g.TryAutoPlace(ItemOf(ItemKind.Ring)));
        Assert.True(g.TryAutoPlace(ItemOf(ItemKind.Ring)));
        Assert.True(g.TryAutoPlace(ItemOf(ItemKind.Belt))); // 2x1 w drugim rzędzie
        Assert.False(g.TryAutoPlace(ItemOf(ItemKind.Helmet))); // 2x2 już się nie zmieści
        Assert.Equal(3, g.Count);
    }

    [Fact]
    public void At_FindsItemByAnyCoveredCell()
    {
        var g = new GridInventory(4, 4);
        var body = ItemOf(ItemKind.BodyArmour); // 2x3
        g.PlaceAt(body, 1, 0);
        Assert.Equal(body, g.At(2, 2)!.Item);
        Assert.Null(g.At(0, 0));
    }
}

public class LootGeneratorTests
{
    [Fact]
    public void SeededGenerator_IsDeterministic()
    {
        var a = new LootGenerator(42).Generate();
        var b = new LootGenerator(42).Generate();
        Assert.Equal(a.Name, b.Name);
        Assert.Equal(a.Rarity, b.Rarity);
        Assert.Equal(a.Affixes.Count, b.Affixes.Count);
    }

    [Fact]
    public void HighTiers_ComeFromUniqueCatalog()
    {
        var gen = new LootGenerator(7);
        var item = gen.Generate(Rarity.Mythic);
        Assert.NotNull(item.UniqueId);
        Assert.Equal(Rarity.Mythic, item.Rarity);
    }

    [Fact]
    public void AffixCounts_MatchRarity()
    {
        var gen = new LootGenerator(1);
        Assert.Empty(gen.Generate(Rarity.Normal).Affixes);
        Assert.InRange(gen.Generate(Rarity.Magic).Affixes.Count, 1, 2);
        Assert.InRange(gen.Generate(Rarity.Rare).Affixes.Count, 3, 4);
    }
}

public class ProgressionTests
{
    [Fact]
    public void GainXp_LevelsUpAndGrantsPoints()
    {
        var p = new PlayerProgress();
        long need = PlayerProgress.XpToNext(1);
        int gained = p.GainXp(need);
        Assert.Equal(1, gained);
        Assert.Equal(2, p.Level);
        Assert.Equal(2, p.AttributePoints);
        Assert.Equal(1, p.SkillPoints);
    }

    [Fact]
    public void GainXp_CanMultiLevel()
    {
        var p = new PlayerProgress();
        p.GainXp(PlayerProgress.XpToNext(1) + PlayerProgress.XpToNext(2));
        Assert.Equal(3, p.Level);
    }
}

public class SkillTreeTests
{
    public SkillTreeTests() => TestData.EnsureLoaded();

    private static ResolvedSkill Resolve(string skillId, GodId god, SkillTreeState trees) =>
        SkillResolver.Resolve(GameData.Class("ranger").Skill(skillId)!, GameData.God(god), trees, new CasterContext());

    [Fact]
    public void ExclusiveGroup_BlocksSecondPick()
    {
        var state = new SkillTreeState();
        Assert.True(state.Allocate("basic", "basic_dmg")); // prereq gałęzi b1
        Assert.True(state.Allocate("basic", "basic_pierce"));
        Assert.False(state.CanAllocate("basic", "basic_twin")); // ta sama grupa b1
    }

    [Fact]
    public void Requires_BlocksWithoutPrerequisite()
    {
        var state = new SkillTreeState();
        Assert.False(state.CanAllocate("basic", "basic_pierce")); // wymaga basic_dmg
        state.Allocate("basic", "basic_dmg");
        Assert.True(state.CanAllocate("basic", "basic_pierce"));
    }

    [Fact]
    public void ApplyTo_ModifiesSkill()
    {
        var state = new SkillTreeState();
        state.Allocate("basic", "basic_dmg");
        float before = Resolve("basic", GodId.None, null).Damage;
        float after = Resolve("basic", GodId.None, state).Damage;
        Assert.Equal(before * 1.3f, after, 2);
    }

    [Fact]
    public void ResetAll_RefundsPoints()
    {
        var state = new SkillTreeState();
        state.Allocate("basic", "basic_dmg");
        state.Allocate("rain", "rain_cd");
        Assert.Equal(2, state.ResetAll());
        Assert.Equal(0, state.SpentPoints);
    }

    [Fact]
    public void AllNineSkills_HaveTrees()
    {
        foreach (var spec in GameData.Class("ranger").Skills)
            Assert.True(GameData.Trees.ContainsKey(spec.Id), $"brak drzewka: {spec.Id}");
    }

    [Fact]
    public void RequiredLevel_GatesNode()
    {
        var state = new SkillTreeState();
        state.Allocate("basic", "basic_dmg");
        // basic_pierce wymaga poziomu 5
        Assert.False(state.CanAllocate("basic", "basic_pierce", playerLevel: 4));
        Assert.True(state.CanAllocate("basic", "basic_pierce", playerLevel: 5));
        Assert.Contains("level", state.BlockReason("basic", "basic_pierce", 4, 10));
    }

    [Fact]
    public void BlockReason_ExplainsExclusive()
    {
        var state = new SkillTreeState();
        state.Allocate("basic", "basic_dmg");
        state.Allocate("basic", "basic_pierce");
        Assert.Contains("exclusive", state.BlockReason("basic", "basic_twin", 99, 10));
    }
}

public class GodVariantTests
{
    public GodVariantTests() => TestData.EnsureLoaded();

    [Fact]
    public void EveryskillDiffersUnderEachGod()
    {
        var cls = GameData.Class("ranger");
        foreach (var spec in cls.Skills)
        {
            var baseS = SkillResolver.Resolve(spec, null, null, new CasterContext());
            foreach (var god in new[] { GodId.Wilds, GodId.Blood })
            {
                var g = SkillResolver.Resolve(spec, GameData.God(god), null, new CasterContext());
                bool differs =
                    g.Damage != baseS.Damage || g.Pierces != baseS.Pierces || g.ExtraProjectiles != baseS.ExtraProjectiles ||
                    g.OnHitStatus != baseS.OnHitStatus || g.MarkDuration != baseS.MarkDuration ||
                    g.MarkedMultiplier != baseS.MarkedMultiplier || g.HealOnHit != baseS.HealOnHit ||
                    g.CdMult != baseS.CdMult || g.AoeMult != baseS.AoeMult || g.DurationMult != baseS.DurationMult ||
                    g.StunDuration != baseS.StunDuration || g.VariantTag != baseS.VariantTag || g.Shape != baseS.Shape;
                Assert.True(differs, $"skill {spec.Id} nie różni się pod bogiem {god}");
            }
        }
    }

    [Fact]
    public void WeaponScaling_FeedsSkillDamage()
    {
        var spec = GameData.Class("ranger").Skill("basic")!;
        var noWeapon = SkillResolver.Resolve(spec, null, null, new CasterContext());
        var withWeapon = SkillResolver.Resolve(spec, null, null, new CasterContext { WeaponDamage = 20f });
        Assert.True(withWeapon.Damage > noWeapon.Damage);
    }

    [Fact]
    public void AttackSpeed_ShortensCastTime()
    {
        var spec = GameData.Class("ranger").Skill("basic")!;
        var slow = SkillResolver.Resolve(spec, null, null, new CasterContext { AttackSpeed = 1f });
        var fast = SkillResolver.Resolve(spec, null, null, new CasterContext { AttackSpeed = 2f });
        Assert.True(fast.CastTime < slow.CastTime);
    }
}

public class CombatPipelineTests
{
    [Fact]
    public void Evasion_CausesMiss()
    {
        var target = new Combatant { MaxHealth = 100f, Health = 100f, EvadeChance = 1f }; // zawsze unika
        var skill = new ResolvedSkill { Id = "x", Damage = 50f, Shape = SkillShape.Projectile, HitChance = 100f };
        Assert.False(CombatResolver.ApplyHitRolled(skill, target, 0.5f));
        Assert.Equal(100f, target.Health);
    }

    [Fact]
    public void StatusDps_ComesFromSkillData()
    {
        var target = new Combatant { MaxHealth = 100f, Health = 100f };
        var skill = new ResolvedSkill { Id = "x", Damage = 1f, Shape = SkillShape.Projectile,
            OnHitStatus = StatusType.Poison, StatusDuration = 3f, StatusDps = 6f };
        CombatResolver.ApplyHit(skill, target);
        Assert.Equal(6f, System.Linq.Enumerable.Single(target.Statuses).Dps);
    }
}

public class PersistenceTests
{
    [Fact]
    public void SaveLoad_RoundTripsCoreFields()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ap_test_{Path.GetRandomFileName()}.json");
        try
        {
            var repo = new JsonGameStateRepository(path);
            var data = new SaveData
            {
                Level = 7, Xp = 123, Gold = 456, PledgedGod = "Wilds",
                GodSkills = { "basic", "hawk" },
                Loadout = { "basic", "spread", null, "dash", "hawk" },
                TreeNodes = { ["basic"] = new() { "basic_dmg" } },
                Bag = { new PlacedItemDto { Item = ItemMapper.ToDto(new LootGenerator(3).Generate(Rarity.Rare)), X = 2, Y = 1 } },
                Equipment = { ["Weapon"] = ItemMapper.ToDto(UniqueCatalog.ById("bow_pantheon")!) },
            };
            repo.Save(data);
            var loaded = repo.Load();
            Assert.NotNull(loaded);
            Assert.Equal(7, loaded!.Level);
            Assert.Equal(456, loaded.Gold);
            Assert.Equal("Wilds", loaded.PledgedGod);
            Assert.Contains("hawk", loaded.GodSkills);
            Assert.Equal("basic", loaded.Loadout[0]);
            Assert.Single(loaded.TreeNodes["basic"]);
            Assert.Equal(2, loaded.Bag[0].X);

            // unik odtwarza się z katalogu z efektem mechanicznym
            var bow = ItemMapper.FromDto(loaded.Equipment["Weapon"]);
            Assert.Equal(UniqueEffect.MarkOnHit, bow.Effect);
            Assert.Equal(Rarity.Mythic, bow.Rarity);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var repo = new JsonGameStateRepository(Path.Combine(Path.GetTempPath(), "ap_nope_xyz.json"));
        Assert.Null(repo.Load());
    }
}

public class VendorTests
{
    [Fact]
    public void SellPrice_ScalesWithRarity()
    {
        var gen = new LootGenerator(5);
        Assert.True(Vendor.SellPrice(gen.Generate(Rarity.Rare)) > Vendor.SellPrice(gen.Generate(Rarity.Normal)));
        Assert.True(Vendor.SellPrice(UniqueCatalog.ById("bow_pantheon")!) >= 800);
    }
}

public class RunGeneratorTests
{
    private static ZoneDefinition TestZone() => new()
    {
        Id = "test", Boss = "ashen_warden",
        Monsters = { new SpawnWeight { Id = "husk", Weight = 3 }, new SpawnWeight { Id = "spitter", Weight = 1 } },
        RoomsMin = 4, RoomsMax = 5, BaseSpawnCount = 4, SpawnCountPerRoom = 2,
    };

    [Fact]
    public void Generate_EndsWithBossAndScales()
    {
        var plan = RunGenerator.Generate(11, playerLevel: 5, TestZone());
        Assert.True(plan.Count >= 5);
        Assert.True(plan[^1].Boss);
        Assert.Equal("ashen_warden", plan[^1].BossId);
        Assert.All(plan.SkipLast(1), r => Assert.False(r.Boss));
        Assert.True(plan[^1].HpMult > plan[0].HpMult);
        Assert.All(plan, r => Assert.All(r.Spawns, id => Assert.Contains(id, new[] { "husk", "spitter" })));
    }

    [Fact]
    public void Generate_IsSeedDeterministic()
    {
        var a = RunGenerator.Generate(99, 1, TestZone());
        var b = RunGenerator.Generate(99, 1, TestZone());
        Assert.Equal(a.Count, b.Count);
        Assert.Equal(a[0].Spawns, b[0].Spawns);
    }
}

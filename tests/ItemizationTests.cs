using System;
using System.Linq;
using AshenPantheon.Core;
using Xunit;

public class ItemLevelScalingTests
{
    public ItemLevelScalingTests() => TestData.EnsureLoaded();

    [Fact]
    public void ScaleFor_IsMonotonicAndCapped()
    {
        Assert.True(AffixRanges.ScaleFor(1) < AffixRanges.ScaleFor(25));
        Assert.True(AffixRanges.ScaleFor(25) < AffixRanges.ScaleFor(50));
        Assert.Equal(AffixRanges.ScaleFor(50), AffixRanges.ScaleFor(100)); // cap na 50+
        Assert.Equal(AffixRanges.ScaleFor(50), AffixRanges.ScaleFor(0));   // legacy (0) = pełna skala
    }

    [Fact]
    public void LowLevelDrops_RollLowerValuesThanCap()
    {
        var gen = new LootGenerator(123);
        for (int i = 0; i < 40; i++)
        {
            var item = gen.Generate(Rarity.Rare, 1);
            Assert.Equal(1, item.ItemLevel);
            foreach (var a in item.Affixes)
            {
                var capMax = AffixRanges.Bounds[a.Stat].Max;
                Assert.True(a.Value <= capMax * AffixRanges.ScaleFor(1) + 0.001f,
                    $"{a.Stat}={a.Value} przekracza skalę ilvl1");
            }
        }
    }

    [Fact]
    public void Validator_RejectsCapValueOnLowIlvl_AcceptsOnHigh()
    {
        var dto = new ItemDto
        {
            Name = "t", Kind = "Ring", Rarity = "Magic", ItemLevel = 1,
            Affixes = { new AffixDto { Stat = "FlatLife", Value = 39f } }, // cap ilvl50
        };
        Assert.False(SaveValidator.ValidateItem(dto).Ok);
        dto.ItemLevel = 50;
        Assert.True(SaveValidator.ValidateItem(dto).Ok);
    }

    [Fact]
    public void Validator_LegacyItemsWithoutIlvl_StillPass()
    {
        var dto = new ItemDto
        {
            Name = "old", Kind = "Ring", Rarity = "Magic", ItemLevel = 0, // stary zapis
            Affixes = { new AffixDto { Stat = "FlatLife", Value = 39f } },
        };
        Assert.True(SaveValidator.ValidateItem(dto).Ok);
    }

    [Fact]
    public void VendorPrice_GrowsWithIlvl()
    {
        var low = new Item { Name = "a", Kind = ItemKind.Ring, Rarity = Rarity.Rare, ItemLevel = 1 };
        var high = new Item { Name = "b", Kind = ItemKind.Ring, Rarity = Rarity.Rare, ItemLevel = 50 };
        Assert.True(Vendor.SellPrice(high) > Vendor.SellPrice(low));
    }
}

public class SocketsAndJewelsTests
{
    public SocketsAndJewelsTests() => TestData.EnsureLoaded();

    [Fact]
    public void SocketCounts_NeverExceedKindCap()
    {
        var gen = new LootGenerator(7);
        for (int i = 0; i < 80; i++)
        {
            var item = gen.Generate(Rarity.Rare, 50);
            Assert.InRange(item.Sockets, 0, Item.MaxSocketsFor(item.Kind));
        }
    }

    [Fact]
    public void TrySocket_RespectsFreeSocketsAndKind()
    {
        var weapon = new Item { Name = "w", Kind = ItemKind.TwoHandWeapon, Sockets = 2 };
        var jewel1 = JewelCatalog.Roll(new Random(1), 50);
        var jewel2 = JewelCatalog.Roll(new Random(2), 50);
        var jewel3 = JewelCatalog.Roll(new Random(3), 50);
        Assert.True(weapon.TrySocket(jewel1));
        Assert.True(weapon.TrySocket(jewel2));
        Assert.False(weapon.TrySocket(jewel3)); // brak wolnych
        Assert.False(weapon.TrySocket(new Item { Name = "x", Kind = ItemKind.Ring })); // nie-jewel
    }

    [Fact]
    public void BuildSheet_IncludesSocketedJewelAffixes()
    {
        var eq = new Equipment();
        var body = new Item { Name = "b", Kind = ItemKind.BodyArmour, Sockets = 1 };
        var heartroot = new Item
        {
            Name = "Heartroot Ruby", Kind = ItemKind.Jewel, JewelId = "heartroot", ItemLevel = 50,
            Affixes = { new Affix { Stat = AffixStat.FlatLife, Value = 20f } },
        };
        Assert.True(body.TrySocket(heartroot));
        eq.Equip(body, EquipmentSlot.BodyArmour);

        float baseLife = new Equipment().BuildSheet(new Attributes(), 1).MaxLife;
        float withJewel = eq.BuildSheet(new Attributes(), 1).MaxLife;
        Assert.Equal(baseLife + 20f, withJewel, 1);
    }

    [Fact]
    public void JewelRoll_MatchesCatalogAndScales()
    {
        var rng = new Random(42);
        for (int i = 0; i < 30; i++)
        {
            var jewel = JewelCatalog.Roll(rng, 1);
            var def = JewelCatalog.Find(jewel.JewelId!)!;
            Assert.Single(jewel.Affixes);
            Assert.Equal(def.AffixStat, jewel.Affixes[0].Stat);
            Assert.True(jewel.Affixes[0].Value <= def.Max * AffixRanges.ScaleFor(1) + 0.001f);
        }
    }

    [Fact]
    public void Validator_RejectsFakeJewelAndOverstuffedSockets()
    {
        // jewel z podbitą wartością
        var fake = new ItemDto
        {
            Name = "Heartroot Ruby", Kind = "Jewel", Rarity = "Magic", JewelId = "heartroot", ItemLevel = 1,
            Affixes = { new AffixDto { Stat = "FlatLife", Value = 25f } }, // cap ilvl50, na ilvl1 za dużo
        };
        Assert.False(SaveValidator.ValidateItem(fake).Ok);

        // więcej jeweli niż socketów
        var item = new ItemDto
        {
            Name = "x", Kind = "Ring", Rarity = "Normal", ItemLevel = 50, Sockets = 0,
            Jewels = { new ItemDto { Name = "j", Kind = "Jewel", JewelId = "heartroot", ItemLevel = 50,
                Affixes = { new AffixDto { Stat = "FlatLife", Value = 10f } } } },
        };
        Assert.False(SaveValidator.ValidateItem(item).Ok);

        // sockety ponad cap rodzaju
        var ring = new ItemDto { Name = "r", Kind = "Ring", Rarity = "Normal", ItemLevel = 50, Sockets = 1 };
        Assert.False(SaveValidator.ValidateItem(ring).Ok);
    }

    [Fact]
    public void DtoRoundTrip_PreservesIlvlSocketsAndJewels()
    {
        var weapon = new Item { Name = "w", Kind = ItemKind.TwoHandWeapon, Rarity = Rarity.Rare, ItemLevel = 37, Sockets = 3 };
        weapon.TrySocket(JewelCatalog.Roll(new Random(9), 37));
        var back = ItemMapper.FromDto(ItemMapper.ToDto(weapon));
        Assert.Equal(37, back.ItemLevel);
        Assert.Equal(3, back.Sockets);
        Assert.Single(back.SocketedJewels);
        Assert.Equal(weapon.SocketedJewels[0].JewelId, back.SocketedJewels[0].JewelId);
        Assert.Equal(weapon.SocketedJewels[0].Affixes[0].Value, back.SocketedJewels[0].Affixes[0].Value, 3);
    }

    [Fact]
    public void AllMonsters_HaveLevelsInCampaignOrEndgameBands()
    {
        // kampania 1-50, moby epilogu/endgame do 60
        foreach (var m in Bestiary.Monsters.Values)
            Assert.InRange(m.Level, 1, 60);
    }
}

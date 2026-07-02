using AshenPantheon.Core;
using Xunit;

public class SaveValidatorTests
{
    private static SaveData FreshSave() => new(); // domyślna postać lvl 1

    [Fact]
    public void DefaultSave_IsValid()
    {
        Assert.True(SaveValidator.Validate(FreshSave()).Ok);
    }

    [Fact]
    public void GeneratedItems_AreValid()
    {
        var gen = new LootGenerator(123);
        var data = FreshSave();
        for (int i = 0; i < 50; i++)
            data.Bag.Add(new PlacedItemDto { Item = ItemMapper.ToDto(gen.Generate()), X = 0, Y = 0 });
        var (ok, err) = SaveValidator.Validate(data);
        Assert.True(ok, err);
    }

    [Fact]
    public void TamperedAffixValue_IsRejected()
    {
        var data = FreshSave();
        data.Bag.Add(new PlacedItemDto
        {
            Item = new ItemDto
            {
                Name = "Cheat", Kind = "Ring", Rarity = "Rare",
                Affixes = { new AffixDto { Stat = "FlatLife", Value = 9999f } }
            }
        });
        Assert.False(SaveValidator.Validate(data).Ok);
    }

    [Fact]
    public void FakeUnique_IsRejected()
    {
        var data = FreshSave();
        data.Bag.Add(new PlacedItemDto
        {
            Item = new ItemDto { Name = "Fałszywka", Kind = "Amulet", Rarity = "Mythic", UniqueId = "nie_istnieje" }
        });
        Assert.False(SaveValidator.Validate(data).Ok);
    }

    [Fact]
    public void TooManyAffixesForRarity_IsRejected()
    {
        var data = FreshSave();
        var item = new ItemDto { Name = "x", Kind = "Belt", Rarity = "Magic" };
        for (int i = 0; i < 4; i++) item.Affixes.Add(new AffixDto { Stat = "Strength", Value = 5 });
        data.Bag.Add(new PlacedItemDto { Item = item });
        Assert.False(SaveValidator.Validate(data).Ok);
    }

    [Fact]
    public void PointsExceedingLevel_AreRejected()
    {
        var data = FreshSave();
        data.Level = 2;
        data.AttributePoints = 50; // lvl 2 = max 2 pkt
        Assert.False(SaveValidator.Validate(data).Ok);
    }

    [Fact]
    public void LegitLeveledCharacter_IsValid()
    {
        var data = FreshSave();
        data.Level = 10;
        data.AttributePoints = 4; data.SpentStr = 8; data.SpentDex = 6; // 18 = 2*(10-1)
        data.SkillPoints = 5;
        data.TreeNodes["basic"] = new() { "basic_dmg", "basic_pierce" };
        data.TreeNodes["rain"] = new() { "rain_cd", "rain_slow" }; // 5+4 = 9 = 1*(10-1)
        var (ok, err) = SaveValidator.Validate(data);
        Assert.True(ok, err);
    }
}

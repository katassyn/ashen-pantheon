using AshenPantheon.Core;
using Xunit;

public class CharacterSheetTests
{
    [Fact]
    public void Strength_AddsLife()
    {
        var s = new CharacterSheet { BaseLife = 50f };
        s.Attributes.Strength = 10;
        Assert.Equal(70f, s.MaxLife); // 50 + 10*2
    }

    [Fact]
    public void Intelligence_AddsManaAndEnergyShield()
    {
        var s = new CharacterSheet { BaseMana = 40f, BaseEnergyShield = 100f };
        s.Attributes.Intelligence = 50;
        Assert.Equal(140f, s.MaxMana);            // 40 + 50*2
        Assert.Equal(150f, s.MaxEnergyShield);    // 100 * (1 + 50*0.01)
    }

    [Fact]
    public void Dexterity_AddsEvasionAndHitChance()
    {
        var s = new CharacterSheet { BaseHitChance = 75f };
        s.Attributes.Dexterity = 10;
        Assert.Equal(20f, s.EvasionRating);  // 10*2
        Assert.Equal(85f, s.HitChance);      // 75 + 10
    }

    [Fact]
    public void Resistance_IsCapped()
    {
        var r = new Resistances { Fire = 120f, Chaos = 120f };
        Assert.Equal(75f, r.Effective(DamageType.Fire, 1));
        Assert.Equal(60f, r.Effective(DamageType.Chaos, 1));
    }

    [Fact]
    public void ResistancePenalty_AppliesAtLevelMilestones()
    {
        var r = new Resistances { Fire = 75f };
        Assert.Equal(75f, r.Effective(DamageType.Fire, 49));   // brak kary
        Assert.Equal(55f, r.Effective(DamageType.Fire, 50));   // -20
        Assert.Equal(35f, r.Effective(DamageType.Fire, 75));   // -40
        Assert.Equal(15f, r.Effective(DamageType.Fire, 100));  // -60
    }

    [Fact]
    public void MoreArmour_ReducesPhysicalMore()
    {
        var low = new CharacterSheet { BaseArmour = 100f };
        var high = new CharacterSheet { BaseArmour = 2000f };
        Assert.True(high.PhysicalReduction(100f) > low.PhysicalReduction(100f));
        Assert.True(high.PhysicalReduction(100f) <= 0.90f);
    }

    [Fact]
    public void MitigatedDamage_AppliesResistance()
    {
        var s = new CharacterSheet();
        s.Resistances.Fire = 50f;
        Assert.Equal(50f, s.MitigatedDamage(DamageType.Fire, 100f)); // 100 * (1 - 0.5)
    }
}

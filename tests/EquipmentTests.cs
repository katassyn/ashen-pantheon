using AshenPantheon.Core;
using Xunit;

public class EquipmentTests
{
    private static Attributes BaseAttr() => new() { Strength = 0, Dexterity = 0, Intelligence = 0 };

    [Fact]
    public void FlatLifeAffix_IncreasesMaxLife()
    {
        var eq = new Equipment();
        eq.EquipAuto(new Item { Name = "Hełm Życia", Kind = ItemKind.Helmet,
            Affixes = { new Affix { Stat = AffixStat.FlatLife, Value = 20f } } });

        var sheet = eq.BuildSheet(BaseAttr(), 1);
        Assert.Equal(70f, sheet.MaxLife); // base 50 + 20
    }

    [Fact]
    public void StrengthAffix_FlowsThroughAttributes()
    {
        var eq = new Equipment();
        eq.EquipAuto(new Item { Name = "Pas Siły", Kind = ItemKind.Belt,
            Affixes = { new Affix { Stat = AffixStat.Strength, Value = 10f } } });

        var sheet = eq.BuildSheet(BaseAttr(), 1);
        Assert.Equal(10, sheet.Attributes.Strength);
        Assert.Equal(70f, sheet.MaxLife); // base 50 + Str 10*2
    }

    [Fact]
    public void TwoRings_BothApply()
    {
        var eq = new Equipment();
        Item ring() => new() { Name = "Pierścień", Kind = ItemKind.Ring,
            Affixes = { new Affix { Stat = AffixStat.FlatLife, Value = 15f } } };
        eq.EquipAuto(ring());
        eq.EquipAuto(ring());

        var sheet = eq.BuildSheet(BaseAttr(), 1);
        Assert.Equal(80f, sheet.MaxLife); // 50 + 15 + 15
    }

    [Fact]
    public void TwoHandWeapon_RemovesOffHand()
    {
        var eq = new Equipment();
        eq.Equip(new Item { Name = "Sztylet", Kind = ItemKind.OneHandWeapon }, EquipmentSlot.Weapon);
        eq.Equip(new Item { Name = "Tarcza", Kind = ItemKind.OffHand }, EquipmentSlot.OffHand);
        Assert.False(eq.IsEmpty(EquipmentSlot.OffHand));

        var removed = eq.Equip(new Item { Name = "Łuk", Kind = ItemKind.TwoHandWeapon }, EquipmentSlot.Weapon);

        Assert.True(eq.IsEmpty(EquipmentSlot.OffHand));   // off-hand wyparty
        Assert.Contains(removed, i => i.Kind == ItemKind.OffHand);
    }

    [Fact]
    public void OffHand_BlockedWithTwoHandEquipped()
    {
        var eq = new Equipment();
        eq.Equip(new Item { Name = "Łuk", Kind = ItemKind.TwoHandWeapon }, EquipmentSlot.Weapon);
        Assert.False(eq.CanEquip(new Item { Name = "Tarcza", Kind = ItemKind.OffHand }, EquipmentSlot.OffHand));
    }

    [Fact]
    public void ResistAffix_AppliesToSheet()
    {
        var eq = new Equipment();
        eq.EquipAuto(new Item { Name = "Buty Ognioodporne", Kind = ItemKind.Boots,
            Affixes = { new Affix { Stat = AffixStat.FireResist, Value = 40f } } });

        var sheet = eq.BuildSheet(BaseAttr(), 1);
        Assert.Equal(40f, sheet.Resistances.Effective(DamageType.Fire, 1));
    }
}

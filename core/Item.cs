using System.Collections.Generic;

namespace AshenPantheon.Core;

/// <summary>11 slotów ekwipunku (wg notatki). Ring w dwóch wariantach.</summary>
public enum EquipmentSlot
{
    Helmet, Shoulders, BodyArmour, Gloves, Boots, Belt,
    Amulet, Ring1, Ring2, Weapon, OffHand
}

public enum ItemKind
{
    Helmet, Shoulders, BodyArmour, Gloves, Boots, Belt,
    Amulet, Ring, OneHandWeapon, TwoHandWeapon, OffHand
}

/// <summary>Staty, które może dawać affix na itemie.</summary>
public enum AffixStat
{
    FlatLife, FlatMana, FlatEnergyShield, FlatArmour, FlatEvasion, FlatHitChance,
    Strength, Dexterity, Intelligence,
    IncreasedAttackDamage,
    FireResist, ColdResist, LightningResist, ChaosResist,
    LifeRegen, ManaRegen, CritChance, CritMultiplier, AttackSpeed, CastSpeed
}

public sealed class Affix
{
    public required AffixStat Stat { get; init; }
    public required float Value { get; init; }
}

public sealed class Item
{
    public required string Name { get; init; }
    public required ItemKind Kind { get; init; }
    public List<Affix> Affixes { get; init; } = new();

    /// <summary>Sloty, w które ten item może wejść.</summary>
    public static EquipmentSlot[] SlotsFor(ItemKind kind) => kind switch
    {
        ItemKind.Helmet => new[] { EquipmentSlot.Helmet },
        ItemKind.Shoulders => new[] { EquipmentSlot.Shoulders },
        ItemKind.BodyArmour => new[] { EquipmentSlot.BodyArmour },
        ItemKind.Gloves => new[] { EquipmentSlot.Gloves },
        ItemKind.Boots => new[] { EquipmentSlot.Boots },
        ItemKind.Belt => new[] { EquipmentSlot.Belt },
        ItemKind.Amulet => new[] { EquipmentSlot.Amulet },
        ItemKind.Ring => new[] { EquipmentSlot.Ring1, EquipmentSlot.Ring2 },
        ItemKind.OneHandWeapon => new[] { EquipmentSlot.Weapon },
        ItemKind.TwoHandWeapon => new[] { EquipmentSlot.Weapon },
        ItemKind.OffHand => new[] { EquipmentSlot.OffHand },
        _ => System.Array.Empty<EquipmentSlot>()
    };
}

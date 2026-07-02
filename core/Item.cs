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

/// <summary>Tier rzadkości. Normal/Magic/Rare = losowe affixy; Legendary/Unique/Mythic = itemy projektowane ręcznie.</summary>
public enum Rarity { Normal, Magic, Rare, Legendary, Unique, Mythic }

/// <summary>Specjalny, ręcznie projektowany efekt mechaniczny unika (konsumowany przez warstwę gry).</summary>
public enum UniqueEffect { None, MarkOnHit, SwiftDash, Overcharge }

public sealed class Item
{
    public required string Name { get; init; }
    public required ItemKind Kind { get; init; }
    public Rarity Rarity { get; init; } = Rarity.Normal;
    public UniqueEffect Effect { get; init; } = UniqueEffect.None;
    /// <summary>Id z UniqueCatalog dla itemów hand-authored (serializacja odtwarza z katalogu).</summary>
    public string? UniqueId { get; init; }
    public List<Affix> Affixes { get; init; } = new();

    /// <summary>Rozmiar w komórkach plecaka (tetris jak PoE).</summary>
    public (int W, int H) Size => SizeFor(Kind);

    public static (int W, int H) SizeFor(ItemKind kind) => kind switch
    {
        ItemKind.Helmet => (2, 2),
        ItemKind.Shoulders => (2, 2),
        ItemKind.BodyArmour => (2, 3),
        ItemKind.Gloves => (2, 2),
        ItemKind.Boots => (2, 2),
        ItemKind.Belt => (2, 1),
        ItemKind.Amulet => (1, 1),
        ItemKind.Ring => (1, 1),
        ItemKind.OneHandWeapon => (1, 3),
        ItemKind.TwoHandWeapon => (2, 4),
        ItemKind.OffHand => (2, 2),
        _ => (1, 1)
    };

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

using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenPantheon.Core;

public sealed class Wallet
{
    public long Gold { get; set; }
}

/// <summary>Wycena sprzedaży do NPC. Ekonomia zaprojektowana tak, by później dołożyć AH bez przepisywania.</summary>
public static class Vendor
{
    public static long SellPrice(Item item)
    {
        long baseP = item.Rarity switch
        {
            Rarity.Normal => 5, Rarity.Magic => 15, Rarity.Rare => 40,
            Rarity.Legendary => 150, Rarity.Unique => 300, Rarity.Mythic => 800, _ => 5
        };
        long price = baseP + 4L * item.Affixes.Count + 6L * item.Sockets;
        // cena rośnie z poziomem itemu (ekonomia progresji); hand-authored tiery = pełna skala
        float scale = item.Rarity >= Rarity.Legendary ? 1f : AffixRanges.ScaleFor(item.ItemLevel);
        return (long)(price * (0.5f + scale));
    }
}

/// <summary>Hand-authored itemy Legendary/Unique/Mythic — nazwane, ze stałymi affixami i efektami mechanicznymi.</summary>
public static class UniqueCatalog
{
    public static readonly List<Item> Items = new()
    {
        new Item { UniqueId = "boots_pursuit", Name = "Boots of Pursuit", Kind = ItemKind.Boots, Rarity = Rarity.Legendary, Effect = UniqueEffect.SwiftDash,
            Affixes = { new Affix { Stat = AffixStat.FlatEvasion, Value = 60 }, new Affix { Stat = AffixStat.Dexterity, Value = 8 } } },
        new Item { UniqueId = "belt_colossus", Name = "Colossus Belt", Kind = ItemKind.Belt, Rarity = Rarity.Legendary,
            Affixes = { new Affix { Stat = AffixStat.FlatLife, Value = 45 }, new Affix { Stat = AffixStat.Strength, Value = 10 }, new Affix { Stat = AffixStat.FlatArmour, Value = 40 } } },
        new Item { UniqueId = "amulet_overcharge", Name = "Overcharge Amulet", Kind = ItemKind.Amulet, Rarity = Rarity.Unique, Effect = UniqueEffect.Overcharge,
            Affixes = { new Affix { Stat = AffixStat.IncreasedAttackDamage, Value = 0.30f }, new Affix { Stat = AffixStat.Intelligence, Value = 8 } } },
        new Item { UniqueId = "ring_hunter", Name = "Hunter's Signet", Kind = ItemKind.Ring, Rarity = Rarity.Unique,
            Affixes = { new Affix { Stat = AffixStat.FlatHitChance, Value = 10 }, new Affix { Stat = AffixStat.IncreasedAttackDamage, Value = 0.15f }, new Affix { Stat = AffixStat.Dexterity, Value = 6 } } },
        new Item { UniqueId = "bow_pantheon", Name = "Will of the Pantheon", Kind = ItemKind.TwoHandWeapon, Rarity = Rarity.Mythic, Effect = UniqueEffect.MarkOnHit,
            Affixes = { new Affix { Stat = AffixStat.IncreasedAttackDamage, Value = 0.45f }, new Affix { Stat = AffixStat.Strength, Value = 10 }, new Affix { Stat = AffixStat.Dexterity, Value = 10 } } },
        new Item { UniqueId = "shroud_ashes", Name = "Shroud of Ashes", Kind = ItemKind.BodyArmour, Rarity = Rarity.Mythic,
            Affixes = { new Affix { Stat = AffixStat.FlatEnergyShield, Value = 80 }, new Affix { Stat = AffixStat.FlatLife, Value = 30 }, new Affix { Stat = AffixStat.Intelligence, Value = 12 } } },
    };

    public static Item? ById(string uniqueId) => Items.FirstOrDefault(i => i.UniqueId == uniqueId);
    public static List<Item> ByRarity(Rarity r) => Items.Where(i => i.Rarity == r).ToList();
}

/// <summary>Seedowany generator lootu: tier rzadkości → affixy losowe (N/M/R) albo unik z katalogu (L/U/My).</summary>
public sealed class LootGenerator
{
    private readonly Random _rng;
    public LootGenerator(int seed) => _rng = new Random(seed);
    public LootGenerator() => _rng = new Random();

    private static readonly ItemKind[] Kinds =
    {
        ItemKind.Helmet, ItemKind.Shoulders, ItemKind.BodyArmour, ItemKind.Gloves, ItemKind.Boots,
        ItemKind.Belt, ItemKind.Amulet, ItemKind.Ring, ItemKind.OneHandWeapon, ItemKind.TwoHandWeapon, ItemKind.OffHand
    };

    private static readonly AffixStat[] Pool =
    {
        AffixStat.FlatLife, AffixStat.FlatArmour, AffixStat.FlatEvasion, AffixStat.FlatEnergyShield,
        AffixStat.Strength, AffixStat.Dexterity, AffixStat.Intelligence,
        AffixStat.FireResist, AffixStat.ColdResist, AffixStat.LightningResist, AffixStat.ChaosResist,
        AffixStat.IncreasedAttackDamage, AffixStat.CritChance, AffixStat.FlatMana
    };

    public Rarity RollRarity()
    {
        double r = _rng.NextDouble() * 100.0;
        if (r < 40) return Rarity.Normal;
        if (r < 75) return Rarity.Magic;
        if (r < 93) return Rarity.Rare;
        if (r < 97) return Rarity.Legendary;
        if (r < 99.5) return Rarity.Unique;
        return Rarity.Mythic;
    }

    public Item Generate() => Generate(RollRarity(), 50);
    public Item Generate(Rarity rarity) => Generate(rarity, 50);

    /// <summary>Drop skalowany poziomem itemu (= poziom potwora/strefy): wartości affixów, sockety, obrażenia broni.</summary>
    public Item Generate(Rarity rarity, int itemLevel) => Generate(rarity, itemLevel, null);

    /// <summary>Jak wyżej, ale z WYMUSZONYM typem itemu (crafting u kowala: konkretny slot).</summary>
    public Item Generate(Rarity rarity, int itemLevel, ItemKind? forceKind)
    {
        itemLevel = Math.Clamp(itemLevel <= 0 ? 50 : itemLevel, 1, 100);

        if (rarity is Rarity.Legendary or Rarity.Unique or Rarity.Mythic)
        {
            var pool = UniqueCatalog.ByRarity(rarity);
            if (pool.Count > 0) return pool[_rng.Next(pool.Count)];
            rarity = Rarity.Rare; // fallback gdy katalog pusty
        }

        var kind = forceKind ?? Kinds[_rng.Next(Kinds.Length)];
        int affixCount = rarity switch
        {
            Rarity.Normal => 0,
            Rarity.Magic => 1 + _rng.Next(2),  // 1–2
            _ => 3 + _rng.Next(2),             // Rare: 3–4
        };

        var affixes = new List<Affix>();
        float f = AffixRanges.ScaleFor(itemLevel);

        // implicity broni: broń MUSI mieć obrażenia (bazy itemów) — skille skalują się nimi
        if (kind == ItemKind.OneHandWeapon)
            affixes.Add(new Affix { Stat = AffixStat.WeaponDamage, Value = (6f + (float)_rng.NextDouble() * 8f) * f });
        else if (kind == ItemKind.TwoHandWeapon)
        {
            affixes.Add(new Affix { Stat = AffixStat.WeaponDamage, Value = (12f + (float)_rng.NextDouble() * 14f) * f });
            affixes.Add(new Affix { Stat = AffixStat.WeaponAttackSpeed, Value = (float)_rng.NextDouble() * 0.1f * f });
        }

        for (int i = 0; i < affixCount; i++)
        {
            var stat = Pool[_rng.Next(Pool.Length)];
            affixes.Add(new Affix { Stat = stat, Value = RollValue(stat, itemLevel) });
        }

        // sockety: szansa rośnie z ilvl; cap wg rodzaju
        int maxSockets = Item.MaxSocketsFor(kind);
        int sockets = 0;
        for (int s = 0; s < maxSockets; s++)
            if (_rng.NextDouble() < 0.25 + 0.35 * Math.Min(1f, itemLevel / 50f)) sockets++;

        string prefix = rarity switch { Rarity.Magic => "Magic ", Rarity.Rare => "Rare ", _ => "" };
        return new Item
        {
            Name = prefix + KindName(kind), Kind = kind, Rarity = rarity, Affixes = affixes,
            ItemLevel = itemLevel, Sockets = sockets,
        };
    }

    public static string KindName(ItemKind kind) => kind switch
    {
        ItemKind.Helmet => "Helmet", ItemKind.Shoulders => "Shoulders", ItemKind.BodyArmour => "Body Armour",
        ItemKind.Gloves => "Gloves", ItemKind.Boots => "Boots", ItemKind.Belt => "Belt",
        ItemKind.Amulet => "Amulet", ItemKind.Ring => "Ring",
        ItemKind.OneHandWeapon => "One-Hand Weapon", ItemKind.TwoHandWeapon => "Two-Hand Weapon", ItemKind.OffHand => "Off-Hand",
        _ => kind.ToString()
    };

    private float RollValue(AffixStat stat, int itemLevel)
    {
        // wartości zawsze w AffixRanges·f(ilvl) — walidator serwera używa tych samych granic
        if (!AffixRanges.Bounds.TryGetValue(stat, out _)) return 5f;
        var (min, max) = AffixRanges.ScaledBounds(stat, itemLevel);
        return min + (float)_rng.NextDouble() * (max - min);
    }
}

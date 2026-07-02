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
        return baseP + 4L * item.Affixes.Count;
    }
}

/// <summary>Hand-authored itemy Legendary/Unique/Mythic — nazwane, ze stałymi affixami i efektami mechanicznymi.</summary>
public static class UniqueCatalog
{
    public static readonly List<Item> Items = new()
    {
        new Item { UniqueId = "boots_pursuit", Name = "Buty Pościgu", Kind = ItemKind.Boots, Rarity = Rarity.Legendary, Effect = UniqueEffect.SwiftDash,
            Affixes = { new Affix { Stat = AffixStat.FlatEvasion, Value = 60 }, new Affix { Stat = AffixStat.Dexterity, Value = 8 } } },
        new Item { UniqueId = "belt_colossus", Name = "Pas Kolosa", Kind = ItemKind.Belt, Rarity = Rarity.Legendary,
            Affixes = { new Affix { Stat = AffixStat.FlatLife, Value = 45 }, new Affix { Stat = AffixStat.Strength, Value = 10 }, new Affix { Stat = AffixStat.FlatArmour, Value = 40 } } },
        new Item { UniqueId = "amulet_overcharge", Name = "Amulet Przeciążenia", Kind = ItemKind.Amulet, Rarity = Rarity.Unique, Effect = UniqueEffect.Overcharge,
            Affixes = { new Affix { Stat = AffixStat.IncreasedAttackDamage, Value = 0.30f }, new Affix { Stat = AffixStat.Intelligence, Value = 8 } } },
        new Item { UniqueId = "ring_hunter", Name = "Sygnet Łowcy", Kind = ItemKind.Ring, Rarity = Rarity.Unique,
            Affixes = { new Affix { Stat = AffixStat.FlatHitChance, Value = 10 }, new Affix { Stat = AffixStat.IncreasedAttackDamage, Value = 0.15f }, new Affix { Stat = AffixStat.Dexterity, Value = 6 } } },
        new Item { UniqueId = "bow_pantheon", Name = "Wola Panteonu", Kind = ItemKind.TwoHandWeapon, Rarity = Rarity.Mythic, Effect = UniqueEffect.MarkOnHit,
            Affixes = { new Affix { Stat = AffixStat.IncreasedAttackDamage, Value = 0.45f }, new Affix { Stat = AffixStat.Strength, Value = 10 }, new Affix { Stat = AffixStat.Dexterity, Value = 10 } } },
        new Item { UniqueId = "shroud_ashes", Name = "Całun Popiołów", Kind = ItemKind.BodyArmour, Rarity = Rarity.Mythic,
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

    public Item Generate() => Generate(RollRarity());

    public Item Generate(Rarity rarity)
    {
        if (rarity is Rarity.Legendary or Rarity.Unique or Rarity.Mythic)
        {
            var pool = UniqueCatalog.ByRarity(rarity);
            if (pool.Count > 0) return pool[_rng.Next(pool.Count)];
            rarity = Rarity.Rare; // fallback gdy katalog pusty
        }

        var kind = Kinds[_rng.Next(Kinds.Length)];
        int affixCount = rarity switch
        {
            Rarity.Normal => 0,
            Rarity.Magic => 1 + _rng.Next(2),  // 1–2
            _ => 3 + _rng.Next(2),             // Rare: 3–4
        };

        var affixes = new List<Affix>();
        for (int i = 0; i < affixCount; i++)
        {
            var stat = Pool[_rng.Next(Pool.Length)];
            affixes.Add(new Affix { Stat = stat, Value = RollValue(stat) });
        }

        string prefix = rarity switch { Rarity.Magic => "Magiczny ", Rarity.Rare => "Rzadki ", _ => "" };
        return new Item { Name = prefix + KindName(kind), Kind = kind, Rarity = rarity, Affixes = affixes };
    }

    public static string KindName(ItemKind kind) => kind switch
    {
        ItemKind.Helmet => "Hełm", ItemKind.Shoulders => "Naramienniki", ItemKind.BodyArmour => "Zbroja",
        ItemKind.Gloves => "Rękawice", ItemKind.Boots => "Buty", ItemKind.Belt => "Pas",
        ItemKind.Amulet => "Amulet", ItemKind.Ring => "Pierścień",
        ItemKind.OneHandWeapon => "Broń 1H", ItemKind.TwoHandWeapon => "Broń 2H", ItemKind.OffHand => "Tarcza",
        _ => kind.ToString()
    };

    private float RollValue(AffixStat stat)
    {
        // wartości zawsze w AffixRanges — walidator serwera używa tych samych granic
        if (!AffixRanges.Bounds.TryGetValue(stat, out var b)) return 5f;
        return b.Min + (float)_rng.NextDouble() * (b.Max - b.Min);
    }
}

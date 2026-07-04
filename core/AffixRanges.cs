using System;
using System.Collections.Generic;

namespace AshenPantheon.Core;

/// <summary>Zakresy wartości affixów — JEDNO źródło prawdy dla generatora lootu i walidatora zapisu
/// (serwer odrzuca itemy z wartościami spoza reguł → ochrona przyszłej ekonomii/AH).
/// Wartości SKALUJĄ się z poziomem itemu (ilvl): pełny zakres na ilvl 50+, ~20% na ilvl 1.</summary>
public static class AffixRanges
{
    /// <summary>Zakresy CAP (ilvl 50+).</summary>
    public static readonly Dictionary<AffixStat, (float Min, float Max)> Bounds = new()
    {
        [AffixStat.FlatLife] = (10, 39),
        [AffixStat.FlatMana] = (8, 27),
        [AffixStat.FlatArmour] = (20, 99),
        [AffixStat.FlatEvasion] = (20, 99),
        [AffixStat.FlatEnergyShield] = (5, 34),
        [AffixStat.FlatHitChance] = (2, 12),
        [AffixStat.Strength] = (3, 10),
        [AffixStat.Dexterity] = (3, 10),
        [AffixStat.Intelligence] = (3, 10),
        [AffixStat.FireResist] = (5, 29),
        [AffixStat.ColdResist] = (5, 29),
        [AffixStat.LightningResist] = (5, 29),
        [AffixStat.ChaosResist] = (3, 17),
        [AffixStat.IncreasedAttackDamage] = (0.05f, 0.25f),
        [AffixStat.CritChance] = (0.01f, 0.04f),
        [AffixStat.CritMultiplier] = (0.05f, 0.3f),
        [AffixStat.LifeRegen] = (1, 6),
        [AffixStat.ManaRegen] = (1, 6),
        [AffixStat.AttackSpeed] = (0.03f, 0.15f),
        [AffixStat.CastSpeed] = (0.03f, 0.15f),
        [AffixStat.MoveSpeed] = (0.02f, 0.1f),
        [AffixStat.WeaponDamage] = (6f, 26f),
        [AffixStat.WeaponAttackSpeed] = (0.0f, 0.2f),
    };

    /// <summary>Mnożnik zakresu dla poziomu itemu: 20% na ilvl 1 → 100% na ilvl 50+ (liniowo).</summary>
    public static float ScaleFor(int itemLevel)
    {
        int lvl = itemLevel <= 0 ? 50 : itemLevel; // legacy zapisy bez ilvl = pełny zakres
        return 0.2f + 0.8f * MathF.Min(1f, lvl / 50f);
    }

    /// <summary>Zakres [Min..Max] przeskalowany do ilvl (generator losuje w tym oknie).</summary>
    public static (float Min, float Max) ScaledBounds(AffixStat stat, int itemLevel)
    {
        var b = Bounds[stat];
        float f = ScaleFor(itemLevel);
        return (b.Min * f, b.Max * f);
    }

    /// <summary>Walidacja wartości względem ilvl itemu (górny cap zależny od poziomu).</summary>
    public static bool InRange(AffixStat stat, float value, int itemLevel)
    {
        if (!Bounds.TryGetValue(stat, out var b)) return false;
        const float eps = 0.0001f;
        float max = b.Max * ScaleFor(itemLevel);
        // dolna granica: skalowany Min może być ostrzejszy niż stare itemy — akceptujemy od 0 w górę,
        // egzekwujemy tylko CAP (to on chroni ekonomię)
        return value >= -eps && value <= max + eps;
    }

    /// <summary>Kompatybilność: walidacja na pełnym zakresie (ilvl 50).</summary>
    public static bool InRange(AffixStat stat, float value) => InRange(stat, value, 50);
}

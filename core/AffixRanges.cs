using System.Collections.Generic;

namespace AshenPantheon.Core;

/// <summary>Zakresy wartości affixów — JEDNO źródło prawdy dla generatora lootu i walidatora zapisu
/// (serwer odrzuca itemy z wartościami spoza reguł → ochrona przyszłej ekonomii/AH).</summary>
public static class AffixRanges
{
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
    };

    public static bool InRange(AffixStat stat, float value)
    {
        if (!Bounds.TryGetValue(stat, out var b)) return false;
        const float eps = 0.0001f;
        return value >= b.Min - eps && value <= b.Max + eps;
    }
}

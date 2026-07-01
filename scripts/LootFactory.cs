using System.Collections.Generic;
using Godot;
using AshenPantheon.Core;

/// <summary>Prosty generator lootu (każdy wróg może zrzucić każdy typ — pod sprzedaż/AH później).</summary>
public static class LootFactory
{
    private static readonly ItemKind[] Kinds =
    {
        ItemKind.Helmet, ItemKind.Shoulders, ItemKind.BodyArmour, ItemKind.Gloves, ItemKind.Boots,
        ItemKind.Belt, ItemKind.Amulet, ItemKind.Ring, ItemKind.OneHandWeapon, ItemKind.TwoHandWeapon, ItemKind.OffHand
    };

    private static readonly AffixStat[] Pool =
    {
        AffixStat.FlatLife, AffixStat.FlatArmour, AffixStat.FlatEvasion, AffixStat.FlatEnergyShield,
        AffixStat.Strength, AffixStat.Dexterity, AffixStat.Intelligence,
        AffixStat.FireResist, AffixStat.ColdResist, AffixStat.LightningResist,
        AffixStat.IncreasedAttackDamage
    };

    public static Item Random()
    {
        var kind = Kinds[GD.Randi() % (uint)Kinds.Length];
        int count = 1 + (int)(GD.Randi() % 2); // 1–2 affixy

        var affixes = new List<Affix>();
        for (int i = 0; i < count; i++)
        {
            var stat = Pool[GD.Randi() % (uint)Pool.Length];
            affixes.Add(new Affix { Stat = stat, Value = RollValue(stat) });
        }

        return new Item { Name = kind.ToString(), Kind = kind, Affixes = affixes };
    }

    private static float RollValue(AffixStat stat) => stat switch
    {
        AffixStat.FlatLife => 10 + GD.Randi() % 30,
        AffixStat.FlatArmour => 20 + GD.Randi() % 80,
        AffixStat.FlatEvasion => 20 + GD.Randi() % 80,
        AffixStat.FlatEnergyShield => 5 + GD.Randi() % 30,
        AffixStat.Strength or AffixStat.Dexterity or AffixStat.Intelligence => 3 + GD.Randi() % 8,
        AffixStat.FireResist or AffixStat.ColdResist or AffixStat.LightningResist => 5 + GD.Randi() % 25,
        AffixStat.IncreasedAttackDamage => 0.05f + (GD.Randi() % 20) / 100f,
        _ => 5f
    };
}

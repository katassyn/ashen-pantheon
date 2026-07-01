using System;

namespace AshenPantheon.Core;

/// <summary>Odporności. Fire/Cold/Lightning max 75%, Chaos max 60%.
/// Kary progowe: na poziomach 50/75/100 gracz dostaje −20% do wszystkich (i chaos).</summary>
public sealed class Resistances
{
    public float Fire { get; set; }
    public float Cold { get; set; }
    public float Lightning { get; set; }
    public float Chaos { get; set; }

    public const float ElementalCap = 75f;
    public const float ChaosCap = 60f;

    /// <summary>Skumulowana kara od progów poziomu (−20% za każdy z 50/75/100).</summary>
    public static float PenaltyForLevel(int level)
    {
        int milestones = 0;
        if (level >= 50) milestones++;
        if (level >= 75) milestones++;
        if (level >= 100) milestones++;
        return -20f * milestones;
    }

    /// <summary>Efektywna odporność danego typu (z karą progową, ograniczona capem). Physical nie używa resistów (patrz armour).</summary>
    public float Effective(DamageType type, int level)
    {
        float penalty = PenaltyForLevel(level);
        return type switch
        {
            DamageType.Fire => MathF.Min(Fire + penalty, ElementalCap),
            DamageType.Cold => MathF.Min(Cold + penalty, ElementalCap),
            DamageType.Lightning => MathF.Min(Lightning + penalty, ElementalCap),
            DamageType.Chaos => MathF.Min(Chaos + penalty, ChaosCap),
            _ => 0f
        };
    }
}

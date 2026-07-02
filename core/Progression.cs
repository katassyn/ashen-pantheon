using System;

namespace AshenPantheon.Core;

/// <summary>XP, poziomy i punkty. Kary resistów projektowane pod progi 50/75/100 → cap 100.</summary>
public sealed class PlayerProgress
{
    public const int LevelCap = 100;
    public const int AttributePointsPerLevel = 2;
    public const int SkillPointsPerLevel = 1;

    public int Level { get; set; } = 1;
    public long Xp { get; set; }
    public int AttributePoints { get; set; }
    public int SkillPoints { get; set; }

    /// <summary>XP potrzebne do awansu z danego poziomu na następny.</summary>
    public static long XpToNext(int level) => (long)(40 * Math.Pow(level, 1.5)) + 60;

    /// <summary>Dodaje XP; zwraca liczbę zdobytych poziomów.</summary>
    public int GainXp(long amount)
    {
        if (Level >= LevelCap) return 0;
        Xp += amount;
        int gained = 0;
        while (Level < LevelCap && Xp >= XpToNext(Level))
        {
            Xp -= XpToNext(Level);
            Level++;
            AttributePoints += AttributePointsPerLevel;
            SkillPoints += SkillPointsPerLevel;
            gained++;
        }
        return gained;
    }
}

/// <summary>Koszty respecu (za złoto).</summary>
public static class Respec
{
    public static long AttributeCost(int spentPoints) => spentPoints == 0 ? 0 : 25L * spentPoints + 50;
    public static long SkillTreeCost(int spentPoints) => spentPoints == 0 ? 0 : 25L * spentPoints + 50;
}

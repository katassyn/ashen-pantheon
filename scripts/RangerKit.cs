using AshenPantheon.Core;

/// <summary>Skille Rangera (1/2/3) + wariant boga "Dzikie Ostępy". Każdy skill ma wersję bazową i boga.</summary>
public static class RangerKit
{
    public const float MarkBonus = 1.3f;     // oznaczeni obrywają +30% od skilli Rangera
    public const string GodName = "Dzikie Ostępy";
    public const float GodMoveSpeedBonus = 1.15f; // pasywka boga

    // 1. BASIC — 0 koncentracji, nakłada Oznaczenie, nie przebija. Wariant boga: przebija (masowe oznaczanie).
    public static ResolvedSkill BasicShot(bool god) => new()
    {
        Id = "basic",
        Damage = 14f,
        Shape = SkillShape.Projectile,
        AppliesMark = true,
        MarkDuration = god ? 8f : 5f,   // pasywka/wariant: dłuższe oznaczenie
        Pierces = god,                  // wariant boga: przebija wszystkich
        ConcentrationCost = 0f,
    };

    // 2. ROZBRYZG — koszt, wide, niższe skalowanie, korzysta z oznaczenia. Wariant boga: więcej pocisków.
    public static ResolvedSkill Spread(bool god) => new()
    {
        Id = "spread",
        Damage = 8f,
        Shape = SkillShape.Projectile,
        MarkedMultiplier = MarkBonus,
        ConcentrationCost = 12f,
    };
    public static int SpreadCount(bool god) => god ? 5 : 3;

    // 3. EGZEKUTOR — duży koszt, bez CD. Na oznaczonych: ×2 i przebija oznaczonych. Wariant boga: mocniejszy.
    public static ResolvedSkill Executor(bool god) => new()
    {
        Id = "exec",
        Damage = god ? 32f : 26f,
        Shape = SkillShape.Projectile,
        MarkedMultiplier = 2f,
        PierceMarkedOnly = true,
        ConcentrationCost = 35f,
    };
}

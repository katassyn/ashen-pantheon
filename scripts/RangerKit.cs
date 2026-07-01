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

    // 4. DESZCZ STRZAŁ — AoE + slow zostający na ziemi (E)
    public const float RainCd = 4f;
    public static float RainRadius(bool god) => god ? 160f : 120f;
    public static ResolvedSkill Rain(bool god) => new()
    {
        Id = "rain", Damage = 18f, Shape = SkillShape.Nova,
        MarkedMultiplier = MarkBonus, ConcentrationCost = 25f,
    };

    // 5. MINA — wybuch: dmg + stun + oznaczenie w promieniu (R)
    public const float MineCd = 5f;
    public static ResolvedSkill Mine(bool god) => new()
    {
        Id = "mine", Damage = 30f, Shape = SkillShape.Nova,
        AppliesMark = true, MarkDuration = 6f,
        StunDuration = god ? 1.8f : 1.2f,
        MarkedMultiplier = MarkBonus, ConcentrationCost = 20f,
    };

    // 6. KOLCZASTA PRZESIEKA — linia, mocny slow + dmg over time (F)
    public const float HedgeCd = 6f;
    public static ResolvedSkill Hedge(bool god) => new()
    {
        Id = "hedge", Damage = 7f, Shape = SkillShape.Line, ConcentrationCost = 22f,
    };

    // 7. DASH — mobilność jako skill, i-frames, bez speed-buffa (Spacja)
    public const float DashCd = 1.6f;
    public const float DashConcentration = 10f;

    // 8. ADRENALINA — bez kosztu, długi CD: MS + atk/cast speed + ∞ koncentracji (X)
    public const float AdrenalineCd = 14f;
    public const float AdrenalineDuration = 5f;

    // 9. JASTRZĄB — po chwili dmg + stun + oznaczenie; ×3 dmg / ×2 stun na oznaczonym (Z)
    public const float HawkCd = 7f;
    public static ResolvedSkill Hawk(bool god) => new()
    {
        Id = "hawk", Damage = 45f, Shape = SkillShape.SingleTarget,
        AppliesMark = true, MarkDuration = 6f,
        MarkedMultiplier = 3f,   // ×3 na oznaczonym
        StunDuration = 1.5f,     // bazowo; ×2 na oznaczonym ustawia Hawk.cs
        ConcentrationCost = 25f,
    };
}

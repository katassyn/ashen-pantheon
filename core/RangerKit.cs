using System;
using System.Linq;

namespace AshenPantheon.Core;

/// <summary>Metadane skilla dla UI/loadoutu (cooldown i koszt bazowy — mnożniki nakłada ResolvedSkill).</summary>
public sealed record SkillInfo(string Id, string Name, float Cooldown, float Cost, string Description);

/// <summary>Definicja klasy — architektura gotowa pod N klas (inny zasób, inny kit).</summary>
public sealed class ClassDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ResourceName { get; init; }
    public required float ResourceMax { get; init; }
    public required float ResourceRegen { get; init; }
    public required SkillInfo[] Skills { get; init; }

    public SkillInfo? Skill(string id) => Skills.FirstOrDefault(s => s.Id == id);
}

/// <summary>Kit Rangera: 9 skilli, każdy z wariantem KAŻDEGO boga (choćby subtelnym) — zgodnie z ranger-class.md.</summary>
public static class RangerKit
{
    public const float MarkBonus = 1.3f;

    public static readonly ClassDefinition Class = new()
    {
        Id = "ranger",
        Name = "Ranger",
        ResourceName = "Koncentracja",
        ResourceMax = 100f,
        ResourceRegen = 30f,
        Skills = new SkillInfo[]
        {
            new("basic", "Strzał", 0f, 0f, "Podstawowy strzał. Nakłada Oznaczenie."),
            new("spread", "Rozbryzg", 0f, 12f, "Wachlarz strzał o średnim zasięgu."),
            new("exec", "Egzekutor", 0f, 35f, "Na oznaczonych: przebija i zadaje ×2."),
            new("rain", "Deszcz strzał", 4f, 25f, "AoE, spadające strzały + spowolnienie na ziemi."),
            new("mine", "Mina", 5f, 20f, "Wybuch: obrażenia + stun + Oznaczenie w promieniu."),
            new("hedge", "Przesieka", 6f, 22f, "Kolczasta linia: mocny slow + obrażenia."),
            new("dash", "Dash", 1.6f, 10f, "Unik z nietykalnością. Mobilność jako skill."),
            new("adrenaline", "Adrenalina", 14f, 0f, "MS + nielimitowana koncentracja przez chwilę."),
            new("hawk", "Jastrząb", 7f, 25f, "Uderzenie z góry: dmg + stun + Mark; ×3 na oznaczonym."),
        }
    };

    /// <summary>Buduje ResolvedSkill dla skilla w wersji bazowej lub wybranego boga.</summary>
    public static ResolvedSkill Get(string id, GodId god) => id switch
    {
        "basic" => Basic(god),
        "spread" => Spread(god),
        "exec" => Executor(god),
        "rain" => Rain(god),
        "mine" => Mine(god),
        "hedge" => Hedge(god),
        "dash" => Dash(god),
        "adrenaline" => Adrenaline(god),
        "hawk" => Hawk(god),
        _ => throw new ArgumentException($"Nieznany skill: {id}")
    };

    public static int SpreadCount(ResolvedSkill s) => 3 + s.ExtraProjectiles;

    // 1. BASIC — 0 kosztu, oznacza. Wilds: przebija (masowe oznaczanie). Blood: dokłada Bleed.
    private static ResolvedSkill Basic(GodId god) => new()
    {
        Id = "basic",
        Damage = 14f,
        Shape = SkillShape.Projectile,
        AppliesMark = true,
        MarkDuration = god == GodId.Wilds ? 8f : 5f,
        Pierces = god == GodId.Wilds,
        OnHitStatus = god == GodId.Blood ? StatusType.Bleed : StatusType.None,
        StatusDuration = god == GodId.Blood ? 2f : 0f,
    };

    // 2. ROZBRYZG — wide, niższe skalowanie. Wilds: +2 pociski. Blood: +30% dmg i Bleed.
    private static ResolvedSkill Spread(GodId god) => new()
    {
        Id = "spread",
        Damage = god == GodId.Blood ? 10.4f : 8f,
        Shape = SkillShape.Projectile,
        MarkedMultiplier = MarkBonus,
        ExtraProjectiles = god == GodId.Wilds ? 2 : 0,
        OnHitStatus = god == GodId.Blood ? StatusType.Bleed : StatusType.None,
        StatusDuration = god == GodId.Blood ? 2f : 0f,
        ConcentrationCost = 12f,
    };

    // 3. EGZEKUTOR — bez CD, duży koszt. Wilds: mocniejszy. Blood: ×2.5 na marked + lifesteal.
    private static ResolvedSkill Executor(GodId god) => new()
    {
        Id = "exec",
        Damage = god == GodId.Wilds ? 32f : 26f,
        Shape = SkillShape.Projectile,
        MarkedMultiplier = god == GodId.Blood ? 2.5f : 2f,
        PierceMarkedOnly = true,
        HealOnHit = god == GodId.Blood ? 4f : 0f,
        ConcentrationCost = 35f,
    };

    // 4. DESZCZ — AoE + slow. Wilds: większy obszar, dłuższe spowolnienie. Blood: krwawy deszcz leczy gracza w środku.
    private static ResolvedSkill Rain(GodId god) => new()
    {
        Id = "rain",
        Damage = 18f,
        Shape = SkillShape.Nova,
        MarkedMultiplier = MarkBonus,
        AoeMult = god == GodId.Wilds ? 1.35f : 1f,
        DurationMult = god == GodId.Wilds ? 1.5f : 1f,
        VariantTag = god == GodId.Blood ? "rain_blood" : null,
        ConcentrationCost = 25f,
    };

    // 5. MINA — dmg + stun + mark. Wilds: dokłada Poison. Blood: bez stuna, +50% dmg, Bleed, lifesteal.
    private static ResolvedSkill Mine(GodId god) => new()
    {
        Id = "mine",
        Damage = god == GodId.Blood ? 45f : 30f,
        Shape = SkillShape.Nova,
        AppliesMark = true,
        MarkDuration = god == GodId.Wilds ? 8f : 6f,
        StunDuration = god == GodId.Blood ? 0f : 1.2f,
        OnHitStatus = god switch { GodId.Wilds => StatusType.Poison, GodId.Blood => StatusType.Bleed, _ => StatusType.None },
        StatusDuration = god == GodId.None ? 0f : 3f,
        HealOnHit = god == GodId.Blood ? 3f : 0f,
        MarkedMultiplier = MarkBonus,
        ConcentrationCost = 20f,
    };

    // 6. PRZESIEKA — linia slow+dmg. Wilds (z designu): latająca bomba kolców, bez CD, truje, nie spowalnia. Blood: drenuje HP wrogów w środku.
    private static ResolvedSkill Hedge(GodId god) => new()
    {
        Id = "hedge",
        Damage = god == GodId.Wilds ? 16f : 7f,
        Shape = god == GodId.Wilds ? SkillShape.Projectile : SkillShape.Line,
        OnHitStatus = god == GodId.Wilds ? StatusType.Poison : StatusType.None,
        StatusDuration = god == GodId.Wilds ? 3f : 0f,
        CdMult = god == GodId.Wilds ? 0f : 1f,
        HealOnHit = god == GodId.Blood ? 2f : 0f,
        VariantTag = god switch { GodId.Wilds => "hedge_bomb", GodId.Blood => "hedge_drain", _ => null },
        ConcentrationCost = 22f,
    };

    // 7. DASH — unik. Wilds: zostawia kolczasty ślad. Blood: płacisz HP zamiast koncentracji, krótszy CD.
    private static ResolvedSkill Dash(GodId god) => new()
    {
        Id = "dash",
        Damage = 0f,
        Shape = SkillShape.SingleTarget,
        CdMult = god == GodId.Blood ? 0.75f : 1f,
        VariantTag = god switch { GodId.Wilds => "dash_trail", GodId.Blood => "dash_blood", _ => null },
        ConcentrationCost = 10f,
    };

    // 8. ADRENALINA — okno mocy. Wilds: +50% czasu trwania. Blood: natychmiast leczy 30 HP.
    private static ResolvedSkill Adrenaline(GodId god) => new()
    {
        Id = "adrenaline",
        Damage = 0f,
        Shape = SkillShape.SingleTarget,
        DurationMult = god == GodId.Wilds ? 1.5f : 1f,
        VariantTag = god == GodId.Blood ? "adrenaline_blood" : null,
        ConcentrationCost = 0f,
    };

    // 9. JASTRZĄB — Wilds (z designu): 3 WIELKIE jastrzębie jako pety walczące u boku. Blood: uderza WSZYSTKICH oznaczonych + lifesteal.
    private static ResolvedSkill Hawk(GodId god) => new()
    {
        Id = "hawk",
        Damage = 45f,
        Shape = SkillShape.SingleTarget,
        AppliesMark = true,
        MarkDuration = god == GodId.Wilds ? 8f : 6f,
        MarkedMultiplier = 3f,
        StunDuration = 1.5f,
        HealOnHit = god == GodId.Blood ? 3f : 0f,
        VariantTag = god switch { GodId.Wilds => "hawk_pets", GodId.Blood => "hawk_all", _ => null },
        ConcentrationCost = 25f,
    };
}

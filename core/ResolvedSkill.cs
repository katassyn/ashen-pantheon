namespace AshenPantheon.Core;

/// <summary>Skill po nałożeniu wariantu/modyfikatorów. Z tego korzysta warstwa Godota i CombatResolver.</summary>
public sealed class ResolvedSkill
{
    public required string Id { get; init; }
    public float Damage { get; set; }
    public SkillShape Shape { get; set; }

    // Statusy (żywioły/DoT) — zostaje z wcześniejszego modelu
    public StatusType OnHitStatus { get; set; } = StatusType.None;
    public float StatusDuration { get; set; }

    // Zachowanie pocisku
    public bool Explodes { get; set; }
    public bool Pierces { get; set; }

    // Ranger: Oznaczenie (Mark)
    public bool AppliesMark { get; set; }
    public float MarkDuration { get; set; }
    /// <summary>Mnożnik obrażeń, gdy cel jest oznaczony (np. 1.3 dla zwykłych, 2.0 dla egzekutora).</summary>
    public float MarkedMultiplier { get; set; } = 1f;
    /// <summary>Egzekutor: przebija tylko dopóki trafia oznaczonych; na nieoznaczonym się zatrzymuje.</summary>
    public bool PierceMarkedOnly { get; set; }

    /// <summary>Ogłuszenie celu na tyle sekund (mina, jastrząb).</summary>
    public float StunDuration { get; set; }

    // Zasób
    public float ConcentrationCost { get; set; }

    // Mnożniki nakładane przez drzewka skilli / bogów / uniki (konsumowane przez warstwę gry)
    public float CdMult { get; set; } = 1f;
    public float CostMult { get; set; } = 1f;
    public float AoeMult { get; set; } = 1f;
    public float DurationMult { get; set; } = 1f;
    public int ExtraProjectiles { get; set; }
    /// <summary>HP leczone rzucającemu za każde trafienie (bogowie krwi itp.).</summary>
    public float HealOnHit { get; set; }
    /// <summary>Znacznik wariantu zachowania — warstwa Godota zmienia implementację skilla po tym tagu.</summary>
    public string? VariantTag { get; set; }

    /// <summary>Id peera rzucającego (multiplayer): lifesteal/heale wracają do właściwego gracza. 1 = host/solo.</summary>
    public int CasterPeer { get; set; } = 1;

    // ── Pipeline v2 (skille jako dane) ──
    public DamageType DamageType { get; set; } = DamageType.Physical;
    /// <summary>Finalny czas rzucania w sekundach (po atk/cast speed) — pacing skilli.</summary>
    public float CastTime { get; set; }
    public float CastTimeMult { get; set; } = 1f;
    /// <summary>Ułamek obrażeń broni doliczany do bazy (broń wreszcie ma znaczenie).</summary>
    public float WeaponScaling { get; set; }
    /// <summary>Szansa trafienia rzucającego w % (celność vs unik celu w CombatResolver).</summary>
    public float HitChance { get; set; } = 100f;
    /// <summary>Dps nakładanego DoT-a (z danych, nie z kodu).</summary>
    public float StatusDps { get; set; }
    /// <summary>Czy ten cast wylosował krytyka (floating numbers na żółto).</summary>
    public bool IsCrit { get; set; }
}

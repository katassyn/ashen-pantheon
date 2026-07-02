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
}

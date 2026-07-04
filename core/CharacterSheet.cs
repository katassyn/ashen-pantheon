using System;

namespace AshenPantheon.Core;

/// <summary>Arkusz postaci: z atrybutów + baz + wkładu z gearu liczy staty pochodne.
/// Formuły są zgrubne (do strojenia) — ważna jest struktura i zależności z notatek.</summary>
public sealed class CharacterSheet
{
    public Attributes Attributes { get; init; } = new();
    public Resistances Resistances { get; init; } = new();
    public int Level { get; set; } = 1;

    // Bazy (przed atrybutami/gearem)
    public float BaseLife { get; set; } = 50f;
    public float BaseMana { get; set; } = 40f;
    public float BaseEnergyShield { get; set; } = 0f;
    public float BaseEvasion { get; set; } = 0f;
    public float BaseArmour { get; set; } = 0f;
    public float BaseHitChance { get; set; } = 75f; // %

    // Wkład z gearu (płaskie dodatki — agreguje je system ekwipunku w kroku 2)
    public float FlatLife { get; set; }
    public float FlatMana { get; set; }
    public float FlatEnergyShield { get; set; }
    public float FlatEvasion { get; set; }
    public float FlatArmour { get; set; }
    public float FlatHitChance { get; set; }
    public float IncreasedAttackDamage { get; set; } // ułamek, np. 0.2 = +20%
    public float LifeRegen { get; set; }
    public float ManaRegen { get; set; }
    public float CritChance { get; set; } = 0.05f;    // 5%
    public float CritMultiplier { get; set; } = 1.5f; // ×150%
    public float AttackSpeed { get; set; } = 1.0f;    // ataki/s
    public float CastSpeed { get; set; } = 1.0f;      // casty/s
    /// <summary>Średnie obrażenia założonej broni (skille skalują przez WeaponScaling).</summary>
    public float WeaponDamage { get; set; }
    /// <summary>Mnożnik szybkości ruchu z gearu/jeweli (1 = baza).</summary>
    public float MoveSpeedMult { get; set; } = 1f;

    // --- Staty pochodne ---

    public float MaxLife => BaseLife + Attributes.Strength * 2f + FlatLife;
    public float MaxMana => BaseMana + Attributes.Intelligence * 2f + FlatMana;
    public float MaxEnergyShield => (BaseEnergyShield + FlatEnergyShield) * (1f + Attributes.Intelligence * 0.01f);
    public float EvasionRating => BaseEvasion + Attributes.Dexterity * 2f + FlatEvasion;
    public float Armour => BaseArmour + FlatArmour;
    public float HitChance => Math.Clamp(BaseHitChance + Attributes.Dexterity * 1f + FlatHitChance, 0f, 100f);
    public float AttackDamageMultiplier => 1f + Attributes.Strength * 0.01f + IncreasedAttackDamage;

    // --- Formuły obronne (zgrubne, PoE-like, do strojenia) ---

    /// <summary>% redukcji obrażeń fizycznych wobec trafienia o sile rawHit (armour maleje względem dużych hitów). Cap 90%.</summary>
    public float PhysicalReduction(float rawHit)
    {
        if (rawHit <= 0f) return 0f;
        float r = Armour / (Armour + 10f * rawHit);
        return Math.Min(r, 0.90f);
    }

    /// <summary>Szansa uniku wobec celności atakującego (diminishing z natury wzoru). Cap 95%.</summary>
    public float EvadeChance(float attackerAccuracy)
    {
        if (EvasionRating <= 0f) return 0f;
        float e = EvasionRating / (EvasionRating + MathF.Max(1f, attackerAccuracy));
        return Math.Min(e, 0.95f);
    }

    /// <summary>Obrażenia po odporności/armour dla danego typu i siły trafienia.</summary>
    public float MitigatedDamage(DamageType type, float raw)
    {
        if (type == DamageType.Physical)
            return raw * (1f - PhysicalReduction(raw));

        float res = Resistances.Effective(type, Level) / 100f;
        return raw * (1f - res);
    }
}

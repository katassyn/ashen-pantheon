using System;

namespace AshenPantheon.Core;

/// <summary>Uniwersalny język modyfikatorów skilli — JEDEN format dla: bazowych flag skilla,
/// patchy bogów, węzłów drzewek i efektów uników. Dzięki temu cały build-system jest danymi.</summary>
public sealed class Effect
{
    /// <summary>Operacja — patrz EffectApplier.</summary>
    public string Op { get; set; } = "";
    public float Value { get; set; }
    /// <summary>Parametr tekstowy (typ statusu / tag wariantu / kształt).</summary>
    public string Str { get; set; } = "";
    public float Duration { get; set; }
    /// <summary>Siła statusu (dps DoT-a) — DoT-y są danymi, nie stałymi w kodzie.</summary>
    public float Magnitude { get; set; }
}

public static class EffectApplier
{
    public static void Apply(ResolvedSkill s, Effect e)
    {
        switch (e.Op)
        {
            case "damage_mult": s.Damage *= e.Value; break;
            case "damage_add": s.Damage += e.Value; break;
            case "cost_mult": s.CostMult *= e.Value; break;
            case "cd_mult": s.CdMult *= e.Value; break;
            case "aoe_mult": s.AoeMult *= e.Value; break;
            case "duration_mult": s.DurationMult *= e.Value; break;
            case "cast_time_mult": s.CastTimeMult *= e.Value; break;
            case "extra_proj": s.ExtraProjectiles += (int)e.Value; break;
            case "pierce": s.Pierces = true; break;
            case "explode": s.Explodes = true; break;
            case "pierce_marked_only": s.PierceMarkedOnly = true; break;
            case "marked_mult": s.MarkedMultiplier = Math.Max(s.MarkedMultiplier, e.Value); break;
            case "apply_mark": s.AppliesMark = true; if (e.Duration > 0) s.MarkDuration = Math.Max(s.MarkDuration, e.Duration); break;
            case "mark_duration": s.MarkDuration = Math.Max(s.MarkDuration, e.Value); break;
            case "status":
                if (Enum.TryParse<StatusType>(e.Str, true, out var st))
                {
                    s.OnHitStatus = st;
                    s.StatusDuration = Math.Max(s.StatusDuration, e.Duration);
                    if (e.Magnitude > 0) s.StatusDps = e.Magnitude;
                }
                break;
            case "stun": s.StunDuration = Math.Max(s.StunDuration, e.Value); break;
            case "heal_on_hit": s.HealOnHit += e.Value; break;
            case "variant": s.VariantTag = e.Str; break;
            case "shape":
                if (Enum.TryParse<SkillShape>(e.Str, true, out var sh)) s.Shape = sh;
                break;
            case "weapon_scaling": s.WeaponScaling = e.Value; break;
        }
    }
}

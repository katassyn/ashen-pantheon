using System;

namespace AshenPantheon.Core;

/// <summary>Parametry rzucającego (z arkusza + broni) wchodzące do rozwiązania skilla.</summary>
public sealed class CasterContext
{
    public float AttackDamageMultiplier = 1f;
    public float HitChance = 100f;
    public float WeaponDamage;
    public float AttackSpeed = 1f;
    public float CastSpeed = 1f;
    public int CasterPeer = 1;
}

/// <summary>⭐ Serce buildów: SkillSpec (dane) → patch boga → węzły drzewka → kontekst rzucającego
/// = gotowy ResolvedSkill. Jedna ścieżka dla wszystkich klas — nowa klasa/bóg/węzeł to tylko JSON.</summary>
public static class SkillResolver
{
    public static ResolvedSkill Resolve(SkillSpec spec, GodSpec? god, SkillTreeState? trees, CasterContext ctx)
    {
        var s = new ResolvedSkill
        {
            Id = spec.Id,
            Damage = spec.BaseDamage,
            Shape = Enum.TryParse<SkillShape>(spec.Shape, true, out var sh) ? sh : SkillShape.Projectile,
            DamageType = Enum.TryParse<DamageType>(spec.DamageType, true, out var dt) ? dt : DamageType.Physical,
            ConcentrationCost = spec.Cost,
            WeaponScaling = spec.WeaponScaling,
            CasterPeer = ctx.CasterPeer,
            HitChance = ctx.HitChance,
        };

        foreach (var e in spec.Base) EffectApplier.Apply(s, e);

        if (god != null && god.SkillPatches.TryGetValue(spec.Id, out var patch))
            foreach (var e in patch) EffectApplier.Apply(s, e);

        trees?.ApplyTo(spec.Id, s);

        // broń + ofensywa z arkusza
        if (s.Damage > 0f)
        {
            s.Damage += ctx.WeaponDamage * s.WeaponScaling;
            s.Damage *= ctx.AttackDamageMultiplier;
            if (god != null) s.Damage *= god.DamageMult;
        }

        // pacing: atk/cast speed wreszcie steruje tempem
        float speed = spec.UsesAttackSpeed ? ctx.AttackSpeed : ctx.CastSpeed;
        s.CastTime = spec.CastTime * s.CastTimeMult / MathF.Max(0.1f, speed);

        return s;
    }
}

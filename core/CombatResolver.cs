using System;

namespace AshenPantheon.Core;

public static class CombatResolver
{
    public const float ChillBonusMultiplier = 1.25f;

    /// <summary>Deterministyczne trafienie (bez rzutu na celność) — do testów i efektów gwarantowanych.</summary>
    public static void ApplyHit(ResolvedSkill skill, Combatant target) => ApplyHitRolled(skill, target, roll: 0f);

    /// <summary>Pipeline v2: rzut celność-vs-unik → bonusy chill/mark → mitygacja celu (armour/resisty wg typu)
    /// → obrażenia → statusy (multi-status) / mark / stun. Zwraca false przy uniku.</summary>
    public static bool ApplyHitRolled(ResolvedSkill skill, Combatant target, float roll)
    {
        float effectiveHit = (skill.HitChance / 100f) * (1f - target.EvadeChance);
        if (roll >= effectiveHit) return false; // unik/pudło

        float damage = skill.Damage;
        if (target.IsChilled) damage *= ChillBonusMultiplier;
        if (target.IsMarked) damage *= skill.MarkedMultiplier;
        damage = target.Mitigate(skill.DamageType, damage);

        target.Health -= damage;

        target.ApplyStatus(skill.OnHitStatus, skill.StatusDuration, skill.StatusDps);

        if (skill.AppliesMark)
        {
            target.Marked = true;
            target.MarkTimeLeft = skill.MarkDuration;
        }

        if (skill.StunDuration > target.StunTimeLeft)
            target.StunTimeLeft = skill.StunDuration;

        return true;
    }
}

using System;

namespace AshenPantheon.Core;

public static class CombatResolver
{
    public const float ChillBonusMultiplier = 1.25f;

    /// <summary>Deterministyczne trafienie (bez rzutu na celność) — do testów i efektów gwarantowanych.</summary>
    public static void ApplyHit(ResolvedSkill skill, Combatant target) => ApplyHitRolled(skill, target, roll: 0f);

    /// <summary>Pełny pipeline v2: rzut na trafienie (celność atakującego vs unik celu),
    /// bonusy chill/mark, obrażenia, statusy z danych. Zwraca false przy uniku.</summary>
    public static bool ApplyHitRolled(ResolvedSkill skill, Combatant target, float roll)
    {
        float effectiveHit = (skill.HitChance / 100f) * (1f - target.EvadeChance);
        if (roll >= effectiveHit) return false; // unik/pudło

        float damage = skill.Damage;
        if (target.IsChilled) damage *= ChillBonusMultiplier;
        if (target.IsMarked) damage *= skill.MarkedMultiplier;

        target.Health -= damage;

        if (skill.OnHitStatus != StatusType.None)
        {
            target.ActiveStatus = skill.OnHitStatus;
            target.StatusTimeLeft = skill.StatusDuration;
            target.StatusDps = skill.StatusDps;
        }

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

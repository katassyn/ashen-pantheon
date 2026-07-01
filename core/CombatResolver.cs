namespace AshenPantheon.Core;

public static class CombatResolver
{
    public const float ChillBonusMultiplier = 1.25f;

    /// <summary>Aplikuje pojedyncze trafienie skilla na cel: obrażenia (z bonusami za chill/mark) + status + oznaczenie.</summary>
    public static void ApplyHit(ResolvedSkill skill, Combatant target)
    {
        float damage = skill.Damage;
        if (target.IsChilled) damage *= ChillBonusMultiplier;
        if (target.IsMarked) damage *= skill.MarkedMultiplier;

        target.Health -= damage;

        if (skill.OnHitStatus != StatusType.None)
        {
            target.ActiveStatus = skill.OnHitStatus;
            target.StatusTimeLeft = skill.StatusDuration;
        }

        if (skill.AppliesMark)
        {
            target.Marked = true;
            target.MarkTimeLeft = skill.MarkDuration;
        }

        if (skill.StunDuration > target.StunTimeLeft)
            target.StunTimeLeft = skill.StunDuration;
    }
}

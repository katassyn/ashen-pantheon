namespace AshenPantheon.Core;

public static class CombatResolver
{
    public const float ChillBonusMultiplier = 1.25f;

    /// <summary>Aplikuje pojedyncze trafienie skilla na cel: obrażenia (z bonusem za chill) + status.</summary>
    public static void ApplyHit(ResolvedSkill skill, Combatant target)
    {
        float damage = skill.Damage;
        if (target.IsChilled)
            damage *= ChillBonusMultiplier;

        target.Health -= damage;

        if (skill.OnHitStatus != StatusType.None)
        {
            target.ActiveStatus = skill.OnHitStatus;
            target.StatusTimeLeft = skill.StatusDuration;
        }
    }
}

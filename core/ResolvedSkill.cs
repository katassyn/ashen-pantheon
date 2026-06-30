namespace AshenPantheon.Core;

/// <summary>Skill po nałożeniu modyfikatorów boga. Z tego korzysta warstwa Godota i CombatResolver.</summary>
public sealed class ResolvedSkill
{
    public required string Id { get; init; }
    public float Damage { get; set; }
    public SkillShape Shape { get; set; }
    public StatusType OnHitStatus { get; set; } = StatusType.None;
    public float StatusDuration { get; set; }
    public bool Explodes { get; set; }
    public bool Pierces { get; set; }
}

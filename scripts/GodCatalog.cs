using System.Collections.Generic;
using AshenPantheon.Core;

/// <summary>Definicje skilli klasy (placeholder "Acolyte") i bogów Pyr/Vael. Logikę liczy Core.</summary>
public static class GodCatalog
{
    public static readonly SkillDefinition Strike = new()
    {
        Id = "strike", BaseDamage = 18f, Cooldown = 0.4f,
        Shape = SkillShape.SingleTarget,
        Tags = new() { SkillTag.Damage, SkillTag.Melee }
    };

    public static readonly SkillDefinition Bolt = new()
    {
        Id = "bolt", BaseDamage = 12f, Cooldown = 0.6f,
        Shape = SkillShape.Projectile,
        Tags = new() { SkillTag.Damage, SkillTag.Projectile }
    };

    public static readonly God Pyr = new()
    {
        Name = "Pyr",
        Modifiers =
        {
            (def, r) => { if (def.Tags.Contains(SkillTag.Damage)) { r.OnHitStatus = StatusType.Burn; r.StatusDuration = 3f; } },
            (def, r) => { if (def.Tags.Contains(SkillTag.Projectile)) r.Explodes = true; },
            (def, r) => { if (def.Tags.Contains(SkillTag.Melee)) r.Shape = SkillShape.Cone; },
        }
    };

    public static readonly God Vael = new()
    {
        Name = "Vael",
        Modifiers =
        {
            (def, r) => { if (def.Tags.Contains(SkillTag.Damage)) { r.OnHitStatus = StatusType.Chill; r.StatusDuration = 2f; } },
            (def, r) => { if (def.Tags.Contains(SkillTag.Projectile)) r.Pierces = true; },
        }
    };
}

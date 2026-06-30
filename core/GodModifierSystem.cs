namespace AshenPantheon.Core;

public static class GodModifierSystem
{
    /// <summary>Buduje ResolvedSkill z bazowej definicji i (opcjonalnego) boga.</summary>
    public static ResolvedSkill Resolve(SkillDefinition def, God? god)
    {
        var resolved = new ResolvedSkill
        {
            Id = def.Id,
            Damage = def.BaseDamage,
            Shape = def.Shape,
        };

        if (god is not null)
            foreach (var modifier in god.Modifiers)
                modifier(def, resolved);

        return resolved;
    }
}

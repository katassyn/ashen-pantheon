using System.Collections.Generic;

namespace AshenPantheon.Core;

/// <summary>Modyfikator: dostaje bazową definicję (by sprawdzić tagi) i mutuje wynik.</summary>
public delegate void SkillModifier(SkillDefinition def, ResolvedSkill resolved);

public sealed class God
{
    public required string Name { get; init; }
    public List<SkillModifier> Modifiers { get; init; } = new();
}

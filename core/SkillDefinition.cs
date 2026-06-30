using System.Collections.Generic;

namespace AshenPantheon.Core;

/// <summary>Niezmienna, bazowa definicja skilla — "głupia", bez wiedzy o bogach.</summary>
public sealed class SkillDefinition
{
    public required string Id { get; init; }
    public required float BaseDamage { get; init; }
    public required float Cooldown { get; init; }
    public required SkillShape Shape { get; init; }
    public HashSet<SkillTag> Tags { get; init; } = new();
}

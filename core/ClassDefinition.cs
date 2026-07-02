using System.Linq;

namespace AshenPantheon.Core;

/// <summary>Metadane skilla dla UI/loadoutu.</summary>
public sealed record SkillInfo(string Id, string Name, float Cooldown, float Cost, string Description);

/// <summary>Lekka definicja klasy dla UI/loadoutu (pełny spec: ClassSpec w GameData).</summary>
public sealed class ClassDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ResourceName { get; init; }
    public required float ResourceMax { get; init; }
    public required float ResourceRegen { get; init; }
    public required SkillInfo[] Skills { get; init; }

    public SkillInfo? Skill(string id) => Skills.FirstOrDefault(s => s.Id == id);
}

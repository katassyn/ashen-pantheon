using System.Collections.Generic;
using System.Linq;

namespace AshenPantheon.Core;

/// <summary>Fasada drzewek (dane w GameData.Trees, ładowane z data/trees/*.json).</summary>
public static class RangerTrees
{
    public static Dictionary<string, List<SkillNode>> BySkill => GameData.Trees;

    public static SkillNode? Find(string skillId, string nodeId) => GameData.FindNode(skillId, nodeId);
}

/// <summary>Stan alokacji drzewek gracza. Egzekwuje wykluczające się gałęzie i wymagania poprzedników.</summary>
public sealed class SkillTreeState
{
    public Dictionary<string, HashSet<string>> Allocated { get; } = new();

    public int SpentPoints => Allocated.Values.Sum(v => v.Count);

    public bool IsAllocated(string skillId, string nodeId) =>
        Allocated.TryGetValue(skillId, out var set) && set.Contains(nodeId);

    public bool CanAllocate(string skillId, string nodeId)
    {
        var node = GameData.FindNode(skillId, nodeId);
        if (node is null || IsAllocated(skillId, nodeId)) return false;
        if (node.Requires != null && !IsAllocated(skillId, node.Requires)) return false;
        if (node.ExclusiveGroup is null) return true;
        return !GameData.Trees[skillId]
            .Where(n => n.ExclusiveGroup == node.ExclusiveGroup && n.Id != nodeId)
            .Any(n => IsAllocated(skillId, n.Id));
    }

    public bool Allocate(string skillId, string nodeId)
    {
        if (!CanAllocate(skillId, nodeId)) return false;
        if (!Allocated.TryGetValue(skillId, out var set)) Allocated[skillId] = set = new();
        set.Add(nodeId);
        return true;
    }

    /// <summary>Nakłada efekty odblokowanych węzłów (język efektów) na ResolvedSkill.</summary>
    public void ApplyTo(string skillId, ResolvedSkill skill)
    {
        if (!Allocated.TryGetValue(skillId, out var set)) return;
        foreach (var nodeId in set)
        {
            var node = GameData.FindNode(skillId, nodeId);
            if (node == null) continue;
            foreach (var e in node.Effects) EffectApplier.Apply(skill, e);
        }
    }

    public int ResetAll()
    {
        int refunded = SpentPoints;
        Allocated.Clear();
        return refunded;
    }
}

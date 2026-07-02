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

    /// <summary>Suma KOSZTÓW odblokowanych węzłów (węzły mogą kosztować >1 pkt).</summary>
    public int SpentPoints => Allocated.Sum(kv => kv.Value.Sum(id => GameData.FindNode(kv.Key, id)?.Cost ?? 1));

    public bool IsAllocated(string skillId, string nodeId) =>
        Allocated.TryGetValue(skillId, out var set) && set.Contains(nodeId);

    public bool CanAllocate(string skillId, string nodeId, int playerLevel = int.MaxValue)
    {
        var node = GameData.FindNode(skillId, nodeId);
        if (node is null || IsAllocated(skillId, nodeId)) return false;
        if (node.RequiredLevel > playerLevel) return false;
        if (node.Requires != null && !IsAllocated(skillId, node.Requires)) return false;
        if (node.ExclusiveGroup is null) return true;
        return !GameData.Trees[skillId]
            .Where(n => n.ExclusiveGroup == node.ExclusiveGroup && n.Id != nodeId)
            .Any(n => IsAllocated(skillId, n.Id));
    }

    /// <summary>Powód blokady węzła (UI) — null gdy można odblokować.</summary>
    public string? BlockReason(string skillId, string nodeId, int playerLevel, int availablePoints)
    {
        var node = GameData.FindNode(skillId, nodeId);
        if (node is null) return "nieznany węzeł";
        if (IsAllocated(skillId, nodeId)) return null;
        if (node.RequiredLevel > playerLevel) return $"wymaga poziomu {node.RequiredLevel}";
        if (node.Requires != null && !IsAllocated(skillId, node.Requires))
            return $"wymaga: {GameData.FindNode(skillId, node.Requires)?.Name ?? node.Requires}";
        if (node.ExclusiveGroup != null)
        {
            var taken = GameData.Trees[skillId]
                .FirstOrDefault(n => n.ExclusiveGroup == node.ExclusiveGroup && n.Id != nodeId && IsAllocated(skillId, n.Id));
            if (taken != null) return $"wyklucza się z: {taken.Name}";
        }
        if (availablePoints < node.Cost) return $"brak punktów (koszt {node.Cost})";
        return null;
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

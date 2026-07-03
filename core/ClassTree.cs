using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AshenPantheon.Core;

/// <summary>Węzeł GŁÓWNEGO drzewa klasy (jak w Drakensang): typy start/skill/passive.
/// Skille odblokowują się poziomem; pasywki kupuje się punktami skilli na "tracku" między nimi.</summary>
public sealed class ClassTreeNode
{
    public string Id { get; set; } = "";
    /// <summary>start | skill | passive</summary>
    public string Type { get; set; } = "passive";
    /// <summary>Dla type=skill: id skilla z klasy.</summary>
    public string SkillId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Requires { get; set; }
    public string? ExclusiveGroup { get; set; }
    public int RequiredLevel { get; set; }
    public int Cost { get; set; } = 1;
    /// <summary>Pasywka = staty jak affixy (ten sam pipeline co gear).</summary>
    public List<PassiveStat> Passives { get; set; } = new();
}

public sealed class PassiveStat
{
    public string Stat { get; set; } = "";
    public float Value { get; set; }
}

public sealed class ClassTreeFile
{
    public string ClassId { get; set; } = "";
    public List<ClassTreeNode> Nodes { get; set; } = new();
}

public static class ClassTree
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>classId → węzły głównego drzewa.</summary>
    public static readonly Dictionary<string, List<ClassTreeNode>> Trees = new();

    public static void Load(string json)
    {
        var file = JsonSerializer.Deserialize<ClassTreeFile>(json, Opts) ?? throw new ArgumentException("puste drzewo klasy");
        Trees[file.ClassId] = file.Nodes;
    }

    public static ClassTreeNode? Find(string classId, string nodeId) =>
        Trees.TryGetValue(classId, out var nodes) ? nodes.FirstOrDefault(n => n.Id == nodeId) : null;

    /// <summary>Czy węzeł jest "spełniony" jako poprzednik: start zawsze, skill = odblokowany poziomem, pasywka = kupiona.</summary>
    public static bool NodeSatisfied(string classId, ClassTreeNode node, int playerLevel, HashSet<string> passives)
    {
        return node.Type switch
        {
            "start" => true,
            "skill" => (GameData.Classes.GetValueOrDefault(classId)?.Skill(node.SkillId)?.RequiredLevel ?? 999) <= playerLevel,
            _ => passives.Contains(node.Id),
        };
    }

    /// <summary>Powód blokady kupna pasywki (null = można kupić).</summary>
    public static string? BlockReason(string classId, string nodeId, int playerLevel, int points, HashSet<string> passives)
    {
        var node = Find(classId, nodeId);
        if (node == null) return "unknown node";
        if (node.Type != "passive") return "not a passive";
        if (passives.Contains(nodeId)) return null;
        if (node.RequiredLevel > playerLevel) return $"requires level {node.RequiredLevel}";
        if (node.Requires != null)
        {
            var req = Find(classId, node.Requires);
            if (req == null || !NodeSatisfied(classId, req, playerLevel, passives))
                return $"requires: {req?.Name ?? node.Requires}";
        }
        if (node.ExclusiveGroup != null)
        {
            var taken = Trees[classId].FirstOrDefault(n =>
                n.ExclusiveGroup == node.ExclusiveGroup && n.Id != nodeId && passives.Contains(n.Id));
            if (taken != null) return $"mutually exclusive with: {taken.Name}";
        }
        if (points < node.Cost) return $"not enough points (cost {node.Cost})";
        return null;
    }

    /// <summary>Affixy ze wszystkich kupionych pasywek (wchodzą w arkusz tym samym pipeline co gear).</summary>
    public static IEnumerable<Affix> PassiveAffixes(string classId, HashSet<string> passives)
    {
        foreach (var id in passives)
        {
            var node = Find(classId, id);
            if (node == null) continue;
            foreach (var p in node.Passives)
                if (Enum.TryParse<AffixStat>(p.Stat, out var stat))
                    yield return new Affix { Stat = stat, Value = p.Value };
        }
    }
}

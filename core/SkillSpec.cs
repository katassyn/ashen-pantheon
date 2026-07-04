using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AshenPantheon.Core;

/// <summary>Pełna definicja skilla jako DANE (data/classes/*.json). Flagi/statusy/kształty w Base (język efektów).</summary>
public sealed class SkillSpec
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public float Cooldown { get; set; }
    public float Cost { get; set; }
    /// <summary>Poziom postaci wymagany do używania skilla (odblokowywanie z levelem).</summary>
    public int RequiredLevel { get; set; } = 1;
    public float BaseDamage { get; set; }
    public string DamageType { get; set; } = "Physical";
    public string Shape { get; set; } = "Projectile";
    /// <summary>Bazowy czas rzucenia (s) — dzielony przez atk/cast speed.</summary>
    public float CastTime { get; set; } = 0.25f;
    /// <summary>true = skaluje się atk speedem (ataki), false = cast speedem (czary).</summary>
    public bool UsesAttackSpeed { get; set; } = true;
    /// <summary>Ułamek średnich obrażeń broni doliczany do bazy.</summary>
    public float WeaponScaling { get; set; } = 0f;
    public List<Effect> Base { get; set; } = new();
}

/// <summary>Klasa jako dane: zasób, dozwolone bronie, pełny kit skilli.</summary>
public sealed class ClassSpec
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ResourceName { get; set; } = "Mana";
    public float ResourceMax { get; set; } = 100f;
    public float ResourceRegen { get; set; } = 20f;
    public List<string> WeaponKinds { get; set; } = new();
    public List<SkillSpec> Skills { get; set; } = new();

    public SkillSpec? Skill(string id) => Skills.FirstOrDefault(s => s.Id == id);

    public ClassDefinition ToDefinition() => new()
    {
        Id = Id, Name = Name, ResourceName = ResourceName,
        ResourceMax = ResourceMax, ResourceRegen = ResourceRegen,
        Skills = Skills.Select(s => new SkillInfo(s.Id, s.Name, s.Cooldown, s.Cost, s.Description)).ToArray(),
    };
}

/// <summary>Bóg jako dane: pasywki + patche per-skill (język efektów) — wariant KAŻDEGO skilla.</summary>
public sealed class GodSpec
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Passive { get; set; } = "";
    public float MoveSpeedMult { get; set; } = 1f;
    public float DamageMult { get; set; } = 1f;
    /// <summary>>0 = brakującą koncentrację płacisz HP (Vharos).</summary>
    public float BloodCostHpPerPoint { get; set; }
    public Dictionary<string, List<Effect>> SkillPatches { get; set; } = new();
}

/// <summary>Węzeł drzewka jako dane (efekty + wykluczenia + wymagania poprzednika).</summary>
public sealed class SkillNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string? ExclusiveGroup { get; set; }
    /// <summary>Id węzła wymaganego wcześniej (prawdziwa struktura drzewa).</summary>
    public string? Requires { get; set; }
    /// <summary>Wymagany poziom postaci.</summary>
    public int RequiredLevel { get; set; }
    /// <summary>Koszt w punktach skilli.</summary>
    public int Cost { get; set; } = 1;
    public List<Effect> Effects { get; set; } = new();
}

public sealed class TreeFile
{
    public string ClassId { get; set; } = "";
    public Dictionary<string, List<SkillNode>> Trees { get; set; } = new();
}

/// <summary>Runtime-katalog danych buildów: klasy + bogowie + drzewka. Ładowany z data/ (gra: res://, serwer/testy: dysk).</summary>
public static class GameData
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    public static readonly Dictionary<string, ClassSpec> Classes = new();
    public static readonly Dictionary<string, GodSpec> GodSpecs = new();
    /// <summary>skillId → węzły drzewka.</summary>
    public static readonly Dictionary<string, List<SkillNode>> Trees = new();

    public static bool Loaded => Classes.Count > 0;

    public static void LoadClass(string json)
    {
        var spec = JsonSerializer.Deserialize<ClassSpec>(json, Opts) ?? throw new ArgumentException("pusta klasa");
        Classes[spec.Id] = spec;
    }

    public static void LoadGod(string json)
    {
        var spec = JsonSerializer.Deserialize<GodSpec>(json, Opts) ?? throw new ArgumentException("pusty bóg");
        GodSpecs[spec.Id] = spec;
    }

    public static void LoadTrees(string json)
    {
        var file = JsonSerializer.Deserialize<TreeFile>(json, Opts) ?? throw new ArgumentException("puste drzewka");
        foreach (var (skillId, nodes) in file.Trees) Trees[skillId] = nodes;
    }

    public static ClassSpec Class(string id) =>
        Classes.TryGetValue(id, out var c) ? c : throw new KeyNotFoundException($"brak klasy: {id}");

    public static GodSpec? God(GodId god) => god switch
    {
        GodId.Wilds => GodSpecs.GetValueOrDefault("wilds"),
        GodId.Blood => GodSpecs.GetValueOrDefault("blood"),
        _ => null
    };

    public static SkillNode? FindNode(string skillId, string nodeId) =>
        Trees.TryGetValue(skillId, out var nodes) ? nodes.FirstOrDefault(n => n.Id == nodeId) : null;

    /// <summary>Ładowanie z dysku (serwer, testy). Gra używa res:// przez DataLoader.</summary>
    public static void LoadFromDirectory(string dataDir)
    {
        foreach (var f in SafeFiles(Path.Combine(dataDir, "classes"))) LoadClass(File.ReadAllText(f));
        foreach (var f in SafeFiles(Path.Combine(dataDir, "gods"))) LoadGod(File.ReadAllText(f));
        foreach (var f in SafeFiles(Path.Combine(dataDir, "trees"))) LoadTrees(File.ReadAllText(f));
        foreach (var f in SafeFiles(Path.Combine(dataDir, "classtrees"))) ClassTree.Load(File.ReadAllText(f));
        foreach (var f in SafeFiles(Path.Combine(dataDir, "quests"))) QuestCatalog.Load(File.ReadAllText(f));
        foreach (var f in SafeFiles(Path.Combine(dataDir, "jewels"))) JewelCatalog.Load(File.ReadAllText(f));
    }

    private static IEnumerable<string> SafeFiles(string dir) =>
        Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json") : Array.Empty<string>();
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AshenPantheon.Core;

/// <summary>Sakwa składników (kanon IngredientPouchPlugin): waluty, wejściówki, klucze, materiały —
/// NIE zajmują plecaka-tetris; liczniki per id, kategorie jako zakładki GUI.
/// Kanon nazw: ips = "Fragment of Infernal Passage"; Andermant/Draken przemianowane (DMCA).</summary>
public sealed class IngredientDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Zakładka GUI: currency | dungeon | crafting | quest.</summary>
    public string Category { get; set; } = "crafting";
    /// <summary>Kolor kwadracika w GUI (placeholder ikony).</summary>
    public string Tint { get; set; } = "#c0c0c0";
    public string Description { get; set; } = "";
}

public sealed class IngredientFile
{
    public List<IngredientDefinition> Ingredients { get; set; } = new();
}

public static class IngredientCatalog
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };
    public static readonly Dictionary<string, IngredientDefinition> Ingredients = new();
    public static bool Loaded => Ingredients.Count > 0;

    public static void Load(string json)
    {
        var file = JsonSerializer.Deserialize<IngredientFile>(json, Opts) ?? throw new ArgumentException("pusty plik składników");
        foreach (var i in file.Ingredients) Ingredients[i.Id] = i;
    }

    public static IngredientDefinition? Find(string id) => Ingredients.GetValueOrDefault(id);

    public static IEnumerable<string> Categories =>
        Ingredients.Values.Select(i => i.Category).Distinct();

    public static IEnumerable<IngredientDefinition> InCategory(string category) =>
        Ingredients.Values.Where(i => i.Category == category).OrderBy(i => i.Name);
}

/// <summary>Liczniki składników gracza (persist w SaveData.Pouch).</summary>
public sealed class Pouch
{
    public Dictionary<string, long> Counts { get; } = new();

    public long Count(string id) => Counts.GetValueOrDefault(id);

    public void Add(string id, long amount = 1)
    {
        if (amount <= 0) return;
        Counts[id] = Count(id) + amount;
    }

    /// <summary>Zdejmuje amount, jeśli jest — false bez zmian, gdy brakuje.</summary>
    public bool TryTake(string id, long amount = 1)
    {
        if (amount <= 0) return true;
        long have = Count(id);
        if (have < amount) return false;
        long left = have - amount;
        if (left == 0) Counts.Remove(id);
        else Counts[id] = left;
        return true;
    }
}

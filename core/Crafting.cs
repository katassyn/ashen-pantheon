using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AshenPantheon.Core;

/// <summary>Receptura kowala (kanon MyCraftingPlugin2): materiały z sakwy (+ złoto) → przedmiot lub składnik.
/// Kategorie = zakładki GUI u blacksmitha (Robert). Wynik "item" = konkretny slot rolowany LootGeneratorem;
/// "ingredient" = rafinacja materiałów.</summary>
public sealed class RecipeInput
{
    public string Ingredient { get; set; } = "";
    public int Count { get; set; } = 1;
}

public sealed class RecipeDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Zakładka GUI: armor | weapons | jewelry | materials.</summary>
    public string Category { get; set; } = "materials";
    public List<RecipeInput> Inputs { get; set; } = new();
    public long GoldCost { get; set; }

    /// <summary>item | ingredient.</summary>
    public string ResultType { get; set; } = "item";
    // ── result item ──
    public string ResultKind { get; set; } = "";     // ItemKind (np. BodyArmour)
    public string ResultRarity { get; set; } = "Rare";
    public int ResultItemLevel { get; set; } = 56;
    // ── result ingredient ──
    public string ResultIngredient { get; set; } = "";
    public int ResultCount { get; set; } = 1;
}

public sealed class RecipeFile
{
    public List<RecipeDefinition> Recipes { get; set; } = new();
}

public static class RecipeCatalog
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };
    public static readonly List<RecipeDefinition> Recipes = new();
    public static bool Loaded => Recipes.Count > 0;

    public static void Load(string json)
    {
        var file = JsonSerializer.Deserialize<RecipeFile>(json, Opts) ?? throw new ArgumentException("pusty plik receptur");
        Recipes.AddRange(file.Recipes);
    }

    public static RecipeDefinition? Find(string id) => Recipes.FirstOrDefault(r => r.Id == id);

    public static IEnumerable<string> Categories =>
        Recipes.Select(r => r.Category).Distinct();

    public static IEnumerable<RecipeDefinition> InCategory(string category) =>
        Recipes.Where(r => r.Category == category).OrderBy(r => r.Name);
}

/// <summary>Czysta logika craftingu — sprawdzenie i wykonanie (zużycie z sakwy/portfela, produkcja wyniku).
/// Warstwa Godot podpina Pouch/Wallet/plecak; tu tylko reguły (testowalne).</summary>
public static class Crafting
{
    /// <summary>Czy stać na recepturę: wszystkie materiały w sakwie + złoto.</summary>
    public static bool CanCraft(RecipeDefinition r, Pouch pouch, long gold)
    {
        if (gold < r.GoldCost) return false;
        foreach (var i in r.Inputs)
            if (pouch.Count(i.Ingredient) < i.Count) return false;
        return true;
    }

    /// <summary>Brakujące materiały (do podświetlenia w GUI).</summary>
    public static IEnumerable<RecipeInput> Missing(RecipeDefinition r, Pouch pouch) =>
        r.Inputs.Where(i => pouch.Count(i.Ingredient) < i.Count);

    /// <summary>Zdejmuje koszty z sakwy (złoto pobiera warstwa Godot). Zwraca false, gdy nie stać
    /// (nic nie zdejmuje). Po sukcesie wołający produkuje wynik przez Result().</summary>
    public static bool TakeCosts(RecipeDefinition r, Pouch pouch, long gold)
    {
        if (!CanCraft(r, pouch, gold)) return false;
        foreach (var i in r.Inputs) pouch.TryTake(i.Ingredient, i.Count);
        return true;
    }

    /// <summary>Produkt receptury: item (LootGenerator z wymuszonym slotem) lub składnik do sakwy.
    /// Zwraca (Item albo null, ingredientId, ingredientCount).</summary>
    public static (Item? Item, string Ingredient, int Count) Result(RecipeDefinition r, LootGenerator gen)
    {
        if (r.ResultType == "ingredient")
            return (null, r.ResultIngredient, Math.Max(1, r.ResultCount));

        var rarity = Enum.TryParse<Rarity>(r.ResultRarity, true, out var rr) ? rr : Rarity.Rare;
        ItemKind? kind = Enum.TryParse<ItemKind>(r.ResultKind, true, out var k) ? k : null;
        return (gen.Generate(rarity, r.ResultItemLevel, kind), "", 0);
    }
}

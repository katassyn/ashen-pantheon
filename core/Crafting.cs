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

/// <summary>Ulepszanie przedmiotu u kowala (+1..+4, tylko Rare+). Koszt rośnie z poziomem: coraz więcej
/// common+rare materiałów, a +3/+4 dodatkowo wymaga legendary essence (dowolne — dropi z bossów Q).
/// Materiały: common="upgrade_dust", rare="upgrade_shard", legendary=dowolny ingredient rarity="legendary".</summary>
public static class ItemUpgrade
{
    public const string CommonMat = "monster_soul_fragment";
    public const string RareMat = "monster_heart_fragment";

    /// <summary>Koszt dojścia do targetLevel (1..4): złoto + common + rare + legendary (dowolne).</summary>
    public static (long Gold, int Common, int Rare, int Legendary) Cost(int targetLevel) => targetLevel switch
    {
        1 => (300, 4, 1, 0),
        2 => (900, 8, 3, 0),
        3 => (2500, 14, 6, 1),
        4 => (6000, 22, 10, 2),
        _ => (0, 0, 0, 0),
    };

    /// <summary>Suma posiadanych legendary essence (dowolnego rodzaju — każdy Q ma swoje).</summary>
    public static long LegendaryOwned(Pouch pouch) =>
        IngredientCatalog.OfRarity("legendary").Sum(pouch.Count);

    public static bool CanUpgrade(Item item, Pouch pouch, long gold)
    {
        if (item == null || !item.CanBeUpgraded || item.UpgradeLevel >= Item.MaxUpgrade) return false;
        var (g, c, r, l) = Cost(item.UpgradeLevel + 1);
        return gold >= g
            && pouch.Count(CommonMat) >= c
            && pouch.Count(RareMat) >= r
            && LegendaryOwned(pouch) >= l;
    }

    /// <summary>Zdejmuje materiały (złoto pobiera warstwa Godot) i podnosi UpgradeLevel o 1.
    /// Legendary zdejmowane z dowolnych essence (najpierw te, których gracz ma najwięcej).</summary>
    public static bool Apply(Item item, Pouch pouch, long gold)
    {
        if (!CanUpgrade(item, pouch, gold)) return false;
        var (_, c, r, l) = Cost(item.UpgradeLevel + 1);
        pouch.TryTake(CommonMat, c);
        pouch.TryTake(RareMat, r);
        TakeLegendary(pouch, l);
        item.UpgradeLevel++;
        return true;
    }

    private static void TakeLegendary(Pouch pouch, int amount)
    {
        foreach (var id in IngredientCatalog.OfRarity("legendary").OrderByDescending(pouch.Count))
        {
            if (amount <= 0) break;
            long have = pouch.Count(id);
            long take = Math.Min(have, amount);
            if (take > 0 && pouch.TryTake(id, take)) amount -= (int)take;
        }
    }
}

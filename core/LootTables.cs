using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AshenPantheon.Core;

/// <summary>Tabele lootu jako dane. Wpisy ważone; typy: nothing / gold / item / table (zagnieżdżenie —
/// tabela może odwoływać się do innych tabel → rozbudowa dropów bez zmian w kodzie).</summary>
public sealed class LootTableDefinition
{
    public string Id { get; set; } = "";
    /// <summary>Ile losowań z tej tabeli przy jednym dropie.</summary>
    public int Rolls { get; set; } = 1;
    public List<LootEntry> Entries { get; set; } = new();
}

public sealed class LootEntry
{
    /// <summary>nothing | gold | item | jewel | table | ingredient</summary>
    public string Type { get; set; } = "nothing";
    public int Weight { get; set; } = 1;
    public int GoldMin { get; set; }
    public int GoldMax { get; set; }
    /// <summary>Dla type=ingredient: id z katalogu składników + ile sztuk.</summary>
    public string Ingredient { get; set; } = "";
    public int CountMin { get; set; } = 1;
    public int CountMax { get; set; } = 1;
    /// <summary>Wymuszona rzadkość itemu (puste = wagi generatora).</summary>
    public string Rarity { get; set; } = "";
    /// <summary>Id tabeli dla type=table.</summary>
    public string Table { get; set; } = "";
}

/// <summary>Wynik jednego dropu.</summary>
public sealed class LootDrop
{
    public Item? Item { get; init; }
    public long Gold { get; init; }
    /// <summary>Składnik do sakwy (id + ilość); pusty = brak.</summary>
    public string Ingredient { get; init; } = "";
    public int IngredientCount { get; init; }
}

public static class LootTables
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };
    public static readonly Dictionary<string, LootTableDefinition> Tables = new();

    public static void Load(string json)
    {
        var def = JsonSerializer.Deserialize<LootTableDefinition>(json, Opts)
            ?? throw new ArgumentException("pusta tabela lootu");
        if (string.IsNullOrEmpty(def.Id)) throw new ArgumentException("tabela bez id");
        Tables[def.Id] = def;
    }

    /// <summary>Losuje dropy z tabeli (rekurencyjnie po zagnieżdżeniach). Deterministyczne dla danego rng.
    /// itemLevel = poziom potwora/strefy — skaluje affixy dropów.</summary>
    public static List<LootDrop> Roll(string tableId, Random rng, LootGenerator gen, int itemLevel = 50, int depth = 0)
    {
        var drops = new List<LootDrop>();
        if (depth > 8 || !Tables.TryGetValue(tableId, out var table)) return drops;

        for (int i = 0; i < table.Rolls; i++)
        {
            var entry = Pick(table.Entries, rng);
            if (entry == null) continue;
            switch (entry.Type)
            {
                case "gold":
                    long amount = entry.GoldMin + rng.Next(Math.Max(1, entry.GoldMax - entry.GoldMin + 1));
                    if (amount > 0) drops.Add(new LootDrop { Gold = amount });
                    break;
                case "item":
                    var item = string.IsNullOrEmpty(entry.Rarity) || !Enum.TryParse<Rarity>(entry.Rarity, out var r)
                        ? gen.Generate(gen.RollRarity(), itemLevel)
                        : gen.Generate(r, itemLevel);
                    drops.Add(new LootDrop { Item = item });
                    break;
                case "jewel":
                    if (JewelCatalog.Jewels.Count > 0)
                        drops.Add(new LootDrop { Item = JewelCatalog.Roll(rng, itemLevel) });
                    break;
                case "ingredient":
                    if (entry.Ingredient.Length > 0)
                    {
                        int count = entry.CountMin + rng.Next(Math.Max(1, entry.CountMax - entry.CountMin + 1));
                        drops.Add(new LootDrop { Ingredient = entry.Ingredient, IngredientCount = Math.Max(1, count) });
                    }
                    break;
                case "table":
                    drops.AddRange(Roll(entry.Table, rng, gen, itemLevel, depth + 1));
                    break;
                // "nothing" → nic
            }
        }
        return drops;
    }

    private static LootEntry? Pick(List<LootEntry> entries, Random rng)
    {
        int total = entries.Sum(e => e.Weight);
        if (total <= 0) return null;
        int roll = rng.Next(total);
        foreach (var e in entries)
        {
            roll -= e.Weight;
            if (roll < 0) return e;
        }
        return null;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AshenPantheon.Core;

/// <summary>Definicja klejnotu (DsoCraft: jewels.php) — jeden stat, wartość rollowana wg ilvl.
/// Dane w data/jewels/*.json; nowy jewel = wpis w JSON.</summary>
public sealed class JewelDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Stat { get; set; } = "";
    /// <summary>Zakres CAP (ilvl 50+) — skalowany przez AffixRanges.ScaleFor.</summary>
    public float Min { get; set; }
    public float Max { get; set; }
    public string Tint { get; set; } = "#ffffff";

    public AffixStat AffixStat => Enum.TryParse<AffixStat>(Stat, out var s) ? s : AffixStat.FlatLife;
}

public sealed class JewelFile
{
    public List<JewelDefinition> Jewels { get; set; } = new();
}

public static class JewelCatalog
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };
    public static readonly Dictionary<string, JewelDefinition> Jewels = new();

    public static void Load(string json)
    {
        var file = JsonSerializer.Deserialize<JewelFile>(json, Opts) ?? throw new ArgumentException("pusty plik jeweli");
        foreach (var j in file.Jewels) Jewels[j.Id] = j;
    }

    public static JewelDefinition? Find(string id) => Jewels.GetValueOrDefault(id);

    /// <summary>Wygeneruj klejnot (wartość rollowana w skali ilvl).</summary>
    public static Item Roll(Random rng, int itemLevel)
    {
        if (Jewels.Count == 0) throw new InvalidOperationException("JewelCatalog pusty");
        var def = Jewels.Values.ElementAt(rng.Next(Jewels.Count));
        float f = AffixRanges.ScaleFor(itemLevel);
        float value = (def.Min + (float)rng.NextDouble() * (def.Max - def.Min)) * f;
        return new Item
        {
            Name = def.Name,
            Kind = ItemKind.Jewel,
            Rarity = Rarity.Magic,
            JewelId = def.Id,
            ItemLevel = itemLevel,
            Affixes = { new Affix { Stat = def.AffixStat, Value = value } },
        };
    }

    /// <summary>Walidacja jewela (walidator serwera): istnieje w katalogu, jeden affix zgodnego statu, wartość ≤ cap·f(ilvl).</summary>
    public static bool Validate(string? jewelId, IReadOnlyList<(string Stat, float Value)> affixes, int itemLevel)
    {
        if (jewelId == null || !Jewels.TryGetValue(jewelId, out var def)) return false;
        if (affixes.Count != 1) return false;
        if (!string.Equals(affixes[0].Stat, def.Stat, StringComparison.OrdinalIgnoreCase)) return false;
        float f = AffixRanges.ScaleFor(itemLevel);
        return affixes[0].Value <= def.Max * f + 0.0001f && affixes[0].Value >= 0f;
    }
}

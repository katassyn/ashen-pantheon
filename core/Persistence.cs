using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AshenPantheon.Core;

// ── DTO zapisu (server-ready: ten sam kontrakt użyje później baza/serwer) ──

public sealed class AffixDto { public string Stat { get; set; } = ""; public float Value { get; set; } }

public sealed class ItemDto
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Rarity { get; set; } = "Normal";
    public string? UniqueId { get; set; }
    public List<AffixDto> Affixes { get; set; } = new();

    /// <summary>Poziom itemu (0 w starych zapisach = traktowany jako 50).</summary>
    public int ItemLevel { get; set; }
    public int Sockets { get; set; }
    public string? JewelId { get; set; }
    /// <summary>Klejnoty w socketach (zagnieżdżone ItemDto typu Jewel).</summary>
    public List<ItemDto> Jewels { get; set; } = new();
}

public sealed class PlacedItemDto { public ItemDto Item { get; set; } = new(); public int X { get; set; } public int Y { get; set; } }

public sealed class SaveData
{
    public string Name { get; set; } = "Bezimienny";
    public string ClassId { get; set; } = "ranger";
    public int Level { get; set; } = 1;
    /// <summary>Odblokowane pasywne węzły drzewa klasy (track między skillami).</summary>
    public List<string> PassiveNodes { get; set; } = new();

    /// <summary>Questy: aktywne (questId → objectiveId → postęp) + ukończone.</summary>
    public Dictionary<string, Dictionary<string, int>> QuestActive { get; set; } = new();
    public List<string> QuestCompleted { get; set; } = new();

    /// <summary>Odkryte strefy świata (waystone fast-travel odblokowany).</summary>
    public List<string> DiscoveredZones { get; set; } = new();

    /// <summary>Endgame: najwyższy ODBLOKOWANY stopień The Final Proving (clear Qn → Qn+1).</summary>
    public int EndgameQ { get; set; } = 1;
    /// <summary>Endgame: ukończone dungeony grupowe — wpisy "dungeonId/difficultyId".</summary>
    public List<string> EndgameCleared { get; set; } = new();
    public long Xp { get; set; }
    public int AttributePoints { get; set; }
    public int SkillPoints { get; set; }
    public int SpentStr { get; set; }
    public int SpentDex { get; set; }
    public int SpentInt { get; set; }
    public long Gold { get; set; }
    public string PledgedGod { get; set; } = "None";
    public List<string> GodSkills { get; set; } = new();
    public List<string?> Loadout { get; set; } = new();
    public Dictionary<string, List<string>> TreeNodes { get; set; } = new();
    public Dictionary<string, ItemDto> Equipment { get; set; } = new();
    public List<PlacedItemDto> Bag { get; set; } = new();
    public List<PlacedItemDto> Stash { get; set; } = new();
}

/// <summary>Abstrakcja persystencji — dziś plik JSON, docelowo serwer/baza (podmiana bez ruszania gameplayu).</summary>
public interface IGameStateRepository
{
    SaveData? Load();
    void Save(SaveData data);
}

public sealed class JsonGameStateRepository : IGameStateRepository
{
    private readonly string _path;
    /// <summary>Wspólne opcje JSON dla zapisu lokalnego, klienta HTTP i serwera (PascalCase, enumy jako stringi).</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonGameStateRepository(string path) => _path = path;

    public SaveData? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(_path), Options);
        }
        catch
        {
            return null; // uszkodzony zapis → świeży start (nie wywalaj gry)
        }
    }

    public void Save(SaveData data)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_path, JsonSerializer.Serialize(data, Options));
    }
}

/// <summary>Mapowanie Item ↔ DTO. Uniki odtwarzane z katalogu po UniqueId (integralność hand-authored).</summary>
public static class ItemMapper
{
    public static ItemDto ToDto(Item item) => new()
    {
        Name = item.Name,
        Kind = item.Kind.ToString(),
        Rarity = item.Rarity.ToString(),
        UniqueId = item.UniqueId,
        Affixes = item.Affixes.Select(a => new AffixDto { Stat = a.Stat.ToString(), Value = a.Value }).ToList(),
        ItemLevel = item.ItemLevel,
        Sockets = item.Sockets,
        JewelId = item.JewelId,
        Jewels = item.SocketedJewels.Select(ToDto).ToList(),
    };

    public static Item FromDto(ItemDto dto)
    {
        if (dto.UniqueId != null)
        {
            var unique = UniqueCatalog.ById(dto.UniqueId);
            if (unique != null) return unique;
        }
        var item = new Item
        {
            Name = dto.Name,
            Kind = Enum.TryParse<ItemKind>(dto.Kind, out var k) ? k : ItemKind.Ring,
            Rarity = Enum.TryParse<Rarity>(dto.Rarity, out var r) ? r : Rarity.Normal,
            JewelId = dto.JewelId,
            Affixes = dto.Affixes
                .Where(a => Enum.TryParse<AffixStat>(a.Stat, out _))
                .Select(a => new Affix { Stat = Enum.Parse<AffixStat>(a.Stat), Value = a.Value })
                .ToList(),
            ItemLevel = dto.ItemLevel <= 0 ? 50 : dto.ItemLevel, // legacy zapisy = pełna skala
            Sockets = dto.Sockets,
        };
        foreach (var j in dto.Jewels) item.SocketedJewels.Add(FromDto(j));
        return item;
    }
}

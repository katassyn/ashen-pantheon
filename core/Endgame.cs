using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AshenPantheon.Core;

/// <summary>Endgame (kanon DsoCraft): dungeony grupowe T1-T5 (mitologiczne) w trudnościach
/// Blood/Hell/Infernal (opłata wejścia, skala HP/dmg/ilvl dropu) + solo "The Final Proving" Q1-Q10
/// (sekwencyjne odblokowanie). Klucze itemowe do tierów = przyszła faza (teraz opłata złotem).</summary>
public sealed class DungeonDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; } = 1;
    /// <summary>Strefa bestiariusza (data/zones) odpalana w arenie.</summary>
    public string Zone { get; set; } = "";
    public int LevelReq { get; set; } = 50;
    public bool Enabled { get; set; } = true;
}

public sealed class DifficultyDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float HpMult { get; set; } = 1f;
    public float DmgMult { get; set; } = 1f;
    public float XpMult { get; set; } = 1f;
    /// <summary>Poziom itemów dropu w tej trudności (nadpisuje poziom potwora).</summary>
    public int ItemLevel { get; set; } = 50;
    public long GoldFee { get; set; }
    /// <summary>Wejście wymaga ukończenia poprzedniej trudności tego dungeonu.</summary>
    public bool RequiresPrevious { get; set; }
}

public sealed class EndgameFile
{
    public List<DungeonDefinition> Dungeons { get; set; } = new();
    public List<DifficultyDefinition> Difficulties { get; set; } = new();
    /// <summary>Mapy runu Q w kolejności: M1 (mini-boss) → M2 (mini-boss) → M3 (arena głównego bossa).</summary>
    public List<string> QMaps { get; set; } = new();
    /// <summary>Powtarzalny auto-quest runu Q (gracz dostaje przy wejściu, cele = Clear kolejnych map).</summary>
    public string QQuest { get; set; } = "";
    public int QMax { get; set; } = 10;
}

public static class EndgameCatalog
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    public static readonly List<DungeonDefinition> Dungeons = new();
    public static readonly List<DifficultyDefinition> Difficulties = new();
    public static readonly List<string> QMaps = new();
    public static string QQuest { get; private set; } = "";
    public static int QMax { get; private set; } = 10;
    public static bool Loaded => Difficulties.Count > 0;

    public static void Load(string json)
    {
        var file = JsonSerializer.Deserialize<EndgameFile>(json, Opts) ?? throw new ArgumentException("pusty plik endgame");
        Dungeons.Clear();
        Dungeons.AddRange(file.Dungeons.OrderBy(d => d.Tier));
        Difficulties.Clear();
        Difficulties.AddRange(file.Difficulties);
        QMaps.Clear();
        QMaps.AddRange(file.QMaps);
        QQuest = file.QQuest;
        QMax = file.QMax;
    }

    /// <summary>Następna mapa runu Q po ukończeniu podanej (null = to była ostatnia / spoza runu).</summary>
    public static string? NextQMap(string zoneId)
    {
        int i = QMaps.IndexOf(zoneId);
        return i < 0 || i + 1 >= QMaps.Count ? null : QMaps[i + 1];
    }

    /// <summary>Numer mapy runu (1-based; 0 = spoza runu Q).</summary>
    public static int QMapIndex(string zoneId) => QMaps.IndexOf(zoneId) + 1;

    public static DungeonDefinition? Dungeon(string id) => Dungeons.FirstOrDefault(d => d.Id == id);
    public static DifficultyDefinition? Difficulty(string id) => Difficulties.FirstOrDefault(d => d.Id == id);

    /// <summary>Poprzednia trudność w kolejności pliku (Blood→Hell→Infernal); null dla pierwszej.</summary>
    public static DifficultyDefinition? Previous(string difficultyId)
    {
        int idx = Difficulties.FindIndex(d => d.Id == difficultyId);
        return idx <= 0 ? null : Difficulties[idx - 1];
    }

    /// <summary>Skala solo-wyzwania Q1-Q10 (The Final Proving): rosnące HP/dmg/XP/ilvl/opłata.</summary>
    public static (float Hp, float Dmg, float Xp, int ItemLevel, long Fee) QScale(int q)
    {
        q = Math.Clamp(q, 1, QMax);
        return (1f + 0.35f * q, 1f + 0.20f * q, 1f + 0.30f * q, 50 + 2 * q, 250L * q);
    }

    // ── identyfikatory wyzwań (w RPC podróży i zapisie clearów) ──
    // format: "q:3" (solo Q3) · "g:odyssey_of_shadows/blood" (dungeon grupowy + trudność)

    public static string GroupChallenge(string dungeonId, string difficultyId) => $"g:{dungeonId}/{difficultyId}";
    public static string QChallenge(int q) => $"q:{q}";

    public static bool TryParseQ(string challenge, out int q)
    {
        q = 0;
        return challenge.StartsWith("q:") && int.TryParse(challenge[2..], out q) && q >= 1 && q <= QMax;
    }

    public static bool TryParseGroup(string challenge, out DungeonDefinition? dungeon, out DifficultyDefinition? difficulty)
    {
        dungeon = null;
        difficulty = null;
        if (!challenge.StartsWith("g:")) return false;
        var parts = challenge[2..].Split('/');
        if (parts.Length != 2) return false;
        dungeon = Dungeon(parts[0]);
        difficulty = Difficulty(parts[1]);
        return dungeon != null && difficulty != null;
    }

    /// <summary>Wpis do SaveData.EndgameCleared ("dungeonId/difficultyId") — walidowalny.</summary>
    public static bool ValidClearedEntry(string entry)
    {
        var parts = entry.Split('/');
        return parts.Length == 2 && Dungeon(parts[0]) != null && Difficulty(parts[1]) != null;
    }
}

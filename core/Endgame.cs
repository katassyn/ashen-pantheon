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

/// <summary>Definicja runu Q: 3 mapy (M1/M2 mini-bossy → M3 boss) + powtarzalny auto-quest.
/// Mode "arena" = proceduralne pokoje; "world" = statyczne strefy z markerami (questy Q2-Q10
/// z interakcjami wymagają stałej geografii). Q bez wpisu gra runem q=1 (fallback).</summary>
public sealed class QRunDefinition
{
    public int Q { get; set; } = 1;
    public string Quest { get; set; } = "";
    public List<string> Maps { get; set; } = new();
    public string Mode { get; set; } = "arena"; // arena | world
}

/// <summary>Trudność runu Q (kanon Parallel World): Infernal(lvl 50) / Hell(65) / Bloodshed(80).
/// Mnożniki wg YML _inf/_hell/_blood (boss HP 5k/15k/50k → x1/x3/x10, dmg x1/x2/x5); opłata w Fragments
/// of Infernal Passage rośnie 10/25/50; ItemLevel dropu rośnie z trudnością.</summary>
public sealed class QDifficultyDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int LevelReq { get; set; } = 50;
    public int IpsFee { get; set; } = 10;
    public float HpMult { get; set; } = 1f;
    public float DmgMult { get; set; } = 1f;
    public float XpMult { get; set; } = 1f;
    public int ItemLevel { get; set; } = 56;
}

public sealed class EndgameFile
{
    public List<DungeonDefinition> Dungeons { get; set; } = new();
    public List<DifficultyDefinition> Difficulties { get; set; } = new();
    public List<QRunDefinition> QRuns { get; set; } = new();
    public List<QDifficultyDefinition> QDifficulties { get; set; } = new();
    public int QMax { get; set; } = 10;
}

public static class EndgameCatalog
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    public static readonly List<DungeonDefinition> Dungeons = new();
    public static readonly List<DifficultyDefinition> Difficulties = new();
    public static readonly List<QRunDefinition> QRuns = new();
    public static readonly List<QDifficultyDefinition> QDifficulties = new();
    public static int QMax { get; private set; } = 10;
    public static bool Loaded => Difficulties.Count > 0;

    public static void Load(string json)
    {
        var file = JsonSerializer.Deserialize<EndgameFile>(json, Opts) ?? throw new ArgumentException("pusty plik endgame");
        Dungeons.Clear();
        Dungeons.AddRange(file.Dungeons.OrderBy(d => d.Tier));
        Difficulties.Clear();
        Difficulties.AddRange(file.Difficulties);
        QRuns.Clear();
        QRuns.AddRange(file.QRuns.OrderBy(r => r.Q));
        QDifficulties.Clear();
        QDifficulties.AddRange(file.QDifficulties);
        QMax = file.QMax;
    }

    public static QDifficultyDefinition? QDifficulty(string id) => QDifficulties.FirstOrDefault(d => d.Id == id);
    /// <summary>Najniższa trudność (Infernal) — domyślna / wstecznie dla starych "q:N".</summary>
    public static QDifficultyDefinition? DefaultQDifficulty => QDifficulties.FirstOrDefault();

    /// <summary>Run dla stopnia Q; brak dedykowanego wpisu = run bazowy (q=1) w skali QScale(q).</summary>
    public static QRunDefinition? RunFor(int q) =>
        QRuns.FirstOrDefault(r => r.Q == q) ?? QRuns.FirstOrDefault(r => r.Q == 1);

    /// <summary>Run, do którego należy mapa (null = strefa spoza runów Q).</summary>
    public static QRunDefinition? RunOfMap(string zoneId) =>
        QRuns.FirstOrDefault(r => r.Maps.Contains(zoneId));

    /// <summary>Następna mapa w OBRĘBIE runu (null = ostatnia / spoza runu).</summary>
    public static string? NextQMap(string zoneId)
    {
        var run = RunOfMap(zoneId);
        if (run == null) return null;
        int i = run.Maps.IndexOf(zoneId);
        return i + 1 >= run.Maps.Count ? null : run.Maps[i + 1];
    }

    /// <summary>Numer mapy w runie (1-based; 0 = spoza runu Q).</summary>
    public static int QMapIndex(string zoneId) => (RunOfMap(zoneId)?.Maps.IndexOf(zoneId) ?? -1) + 1;

    public static DungeonDefinition? Dungeon(string id) => Dungeons.FirstOrDefault(d => d.Id == id);
    public static DifficultyDefinition? Difficulty(string id) => Difficulties.FirstOrDefault(d => d.Id == id);

    /// <summary>Poprzednia trudność w kolejności pliku (Blood→Hell→Infernal); null dla pierwszej.</summary>
    public static DifficultyDefinition? Previous(string difficultyId)
    {
        int idx = Difficulties.FindIndex(d => d.Id == difficultyId);
        return idx <= 0 ? null : Difficulties[idx - 1];
    }

    // ── identyfikatory wyzwań (w RPC podróży i zapisie clearów) ──
    // format: "q:3:hell" (Q3 na trudności Hell) · "g:odyssey_of_shadows/blood" (dungeon grupowy)
    // wstecznie "q:3" (bez trudności) = Infernal (najniższa)

    public static string GroupChallenge(string dungeonId, string difficultyId) => $"g:{dungeonId}/{difficultyId}";
    public static string QChallenge(int q, string difficultyId) => $"q:{q}:{difficultyId}";

    /// <summary>Parsuje wyzwanie Q: numer + trudność. "q:3:hell" → (3, Hell); "q:3" → (3, Infernal).</summary>
    public static bool TryParseQ(string challenge, out int q, out QDifficultyDefinition? difficulty)
    {
        q = 0;
        difficulty = DefaultQDifficulty;
        if (!challenge.StartsWith("q:")) return false;
        var parts = challenge[2..].Split(':');
        if (!int.TryParse(parts[0], out q) || q < 1 || q > QMax) return false;
        if (parts.Length >= 2) difficulty = QDifficulty(parts[1]) ?? DefaultQDifficulty;
        return true;
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

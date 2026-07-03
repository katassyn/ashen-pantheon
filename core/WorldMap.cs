using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AshenPantheon.Core;

/// <summary>Trwała strefa mapy świata (kampania 1–50): mob packi ze stałym respawnem co X sekund
/// (import z DsoCraft), wyjścia do innych stref. Dane w data/world/*.json.</summary>
public sealed class WorldZoneDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int LevelMin { get; set; } = 1;
    public int LevelMax { get; set; } = 10;
    public float SpawnX { get; set; }
    public float SpawnY { get; set; }
    public List<MobPackDefinition> Packs { get; set; } = new();
    public List<ZoneExitDefinition> Exits { get; set; } = new();
    /// <summary>Znaczniki questowe (cele Reach/Interact/Defend).</summary>
    public List<MarkerDefinition> Markers { get; set; } = new();
}

public sealed class MarkerDefinition
{
    public string Id { get; set; } = "";
    /// <summary>reach | interact | escort | defend</summary>
    public string Type { get; set; } = "reach";
    public float X { get; set; }
    public float Y { get; set; }
    public string Label { get; set; } = "";

    // ── escort: NPC idzie z (X,Y) do (DestX,DestY) gdy gracz blisko; śmierć = reset ──
    public float DestX { get; set; }
    public float DestY { get; set; }
    public float EscortHp { get; set; } = 120f;
    public float EscortSpeed { get; set; } = 70f;

    // ── defend: fale mobów szturmują punkt; przetrwaj/odeprzyj ──
    public int Waves { get; set; } = 3;
    public List<string> WaveMonsters { get; set; } = new();
    public float WaveInterval { get; set; } = 12f;

    // ── survive: wytrzymaj X sekund pod presją spawnów (WaveMonsters co WaveInterval) ──
    public int SurviveSeconds { get; set; } = 30;
}

/// <summary>Pack mobów: pozycja, skład (z bestiariusza), respawn co X s po wybiciu.</summary>
public sealed class MobPackDefinition
{
    public float X { get; set; }
    public float Y { get; set; }
    public List<string> Monsters { get; set; } = new();
    public float RespawnSeconds { get; set; } = 30f;
    /// <summary>Rozrzut spawnu członków packa wokół punktu.</summary>
    public float Spread { get; set; } = 70f;
    /// <summary>Zasięg aggro — pack nie goni przez pół mapy.</summary>
    public float AggroRange { get; set; } = 320f;
}

public sealed class ZoneExitDefinition
{
    public float X { get; set; }
    public float Y { get; set; }
    /// <summary>"hub"/id strefy świata; przy Scene=Arena — id strefy bestiariusza (dungeon).</summary>
    public string Target { get; set; } = "hub";
    public string Label { get; set; } = "";
    /// <summary>Opcjonalna scena docelowa (np. "res://scenes/Arena.tscn" dla dungeonu). Puste = auto.</summary>
    public string Scene { get; set; } = "";
}

public static class WorldMaps
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };
    public static readonly Dictionary<string, WorldZoneDefinition> Zones = new();

    public static void Load(string json)
    {
        var def = JsonSerializer.Deserialize<WorldZoneDefinition>(json, Opts)
            ?? throw new ArgumentException("pusta strefa świata");
        if (string.IsNullOrEmpty(def.Id)) throw new ArgumentException("strefa świata bez id");
        Zones[def.Id] = def;
    }

    public static WorldZoneDefinition Zone(string id) =>
        Zones.TryGetValue(id, out var d) ? d : throw new KeyNotFoundException($"brak strefy świata: {id}");

    /// <summary>Strefy uporządkowane wg poziomu (mapa świata / waystone).</summary>
    public static IEnumerable<WorldZoneDefinition> Ordered() =>
        Zones.Values.OrderBy(z => z.LevelMin).ThenBy(z => z.Name);
}

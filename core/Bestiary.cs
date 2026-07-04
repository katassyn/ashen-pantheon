using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AshenPantheon.Core;

/// <summary>Bestiariusz: potwory jako DANE (JSON), nie kod. Jeden generyczny Monster w Godocie
/// odgrywa definicję; nowy potwór = nowy plik w data/monsters/.</summary>
public sealed class MonsterDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float Hp { get; set; } = 60f;
    public float Speed { get; set; } = 95f;
    /// <summary>chase | keep_distance</summary>
    public string Movement { get; set; } = "chase";
    public float PreferredRange { get; set; } = 150f; // dla keep_distance
    public float Scale { get; set; } = 1f;
    public string Tint { get; set; } = "#d94d4d";
    public long Xp { get; set; } = 12;
    public string LootTable { get; set; } = "common";
    /// <summary>Poziom potwora — poziom dropionych itemów (skalowanie affixów) i przyszłe formuły.</summary>
    public int Level { get; set; } = 1;
    /// <summary>Przedmiot questowy zrzucany przy śmierci (cel Collect). Pusty = brak.</summary>
    public string QuestItem { get; set; } = "";
    /// <summary>Szansa na zrzut przedmiotu questowego (0..1).</summary>
    public float QuestItemChance { get; set; } = 0.5f;

    // obrona (typy obrażeń graczy mają znaczenie)
    public float Armour { get; set; }
    public float EvadeChance { get; set; }
    public float ResFire { get; set; }
    public float ResCold { get; set; }
    public float ResLightning { get; set; }
    public float ResChaos { get; set; }
    public float AttackInterval { get; set; } = 1.2f;
    public List<AbilityDefinition> Abilities { get; set; } = new();
    /// <summary>Fazy bossa (puste = zwykły potwór). Sortowane malejąco po HpBelow.</summary>
    public List<PhaseDefinition> Phases { get; set; } = new();

    public bool IsBoss => Phases.Count > 0;

    /// <summary>Aktywny zestaw ability + interwał dla aktualnego % HP (fazy bossów).</summary>
    public (List<AbilityDefinition> Abilities, float Interval) ActiveSet(float hpFrac)
    {
        foreach (var p in Phases.OrderBy(p => p.HpBelow))
            if (hpFrac <= p.HpBelow)
                return (p.Abilities, p.AttackInterval > 0 ? p.AttackInterval : AttackInterval);
        return (Abilities, AttackInterval);
    }
}

/// <summary>Skill potwora — "rozpiska" jako dane. Typy: melee, projectile, tele_circle, tele_cone, tele_line, summon.</summary>
public sealed class AbilityDefinition
{
    public string Type { get; set; } = "melee";
    public float Damage { get; set; } = 10f;
    /// <summary>Typ obrażeń ataku (Physical/Fire/Cold/Lightning/Chaos) — resisty gracza mają znaczenie.</summary>
    public string DamageType { get; set; } = "Physical";
    public float Cooldown { get; set; } = 0f;     // 0 = ogranicza tylko AttackInterval
    public float Windup { get; set; } = 0.35f;
    public float Reach { get; set; } = 55f;       // melee
    public float Speed { get; set; } = 320f;      // projectile
    public float Radius { get; set; } = 85f;      // tele_circle / tele_cone(długość)
    public float HalfAngleDeg { get; set; } = 34f;// tele_cone
    public float Length { get; set; } = 340f;     // tele_line
    public float HalfWidth { get; set; } = 28f;   // tele_line
    public string MonsterId { get; set; } = "";   // summon
    public int Count { get; set; } = 1;           // summon
}

public sealed class PhaseDefinition
{
    /// <summary>Faza aktywna gdy HP ≤ tego ułamka (np. 0.5 = poniżej połowy).</summary>
    public float HpBelow { get; set; } = 1f;
    public float AttackInterval { get; set; }
    public List<AbilityDefinition> Abilities { get; set; } = new();
}

/// <summary>Strefa/akt: pula potworów (wagi), boss, liczba pokoi — też dane.</summary>
public sealed class ZoneDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<SpawnWeight> Monsters { get; set; } = new();
    public string Boss { get; set; } = "";
    public int RoomsMin { get; set; } = 4;
    public int RoomsMax { get; set; } = 5;
    public int BaseSpawnCount { get; set; } = 4;
    public int SpawnCountPerRoom { get; set; } = 2;

    public string RollMonster(Random rng)
    {
        int total = Monsters.Sum(m => m.Weight);
        int roll = rng.Next(total);
        foreach (var m in Monsters)
        {
            roll -= m.Weight;
            if (roll < 0) return m.Id;
        }
        return Monsters[0].Id;
    }
}

public sealed class SpawnWeight
{
    public string Id { get; set; } = "";
    public int Weight { get; set; } = 1;
}

public static class Bestiary
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    public static readonly Dictionary<string, MonsterDefinition> Monsters = new();
    public static readonly Dictionary<string, ZoneDefinition> Zones = new();

    public static void LoadMonster(string json)
    {
        var def = JsonSerializer.Deserialize<MonsterDefinition>(json, Opts)
            ?? throw new ArgumentException("pusta definicja potwora");
        if (string.IsNullOrEmpty(def.Id)) throw new ArgumentException("potwór bez id");
        Monsters[def.Id] = def;
    }

    public static void LoadZone(string json)
    {
        var def = JsonSerializer.Deserialize<ZoneDefinition>(json, Opts)
            ?? throw new ArgumentException("pusta definicja strefy");
        if (string.IsNullOrEmpty(def.Id)) throw new ArgumentException("strefa bez id");
        Zones[def.Id] = def;
    }

    public static MonsterDefinition Monster(string id) =>
        Monsters.TryGetValue(id, out var d) ? d : throw new KeyNotFoundException($"brak potwora: {id}");

    public static ZoneDefinition Zone(string id) =>
        Zones.TryGetValue(id, out var d) ? d : throw new KeyNotFoundException($"brak strefy: {id}");
}

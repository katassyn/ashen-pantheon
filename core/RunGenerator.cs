using System;
using System.Collections.Generic;

namespace AshenPantheon.Core;

/// <summary>Plan jednego pokoju runu — konkretne spawny z puli strefy.</summary>
public sealed class RoomPlan
{
    public int Index { get; init; }
    public List<string> Spawns { get; init; } = new();
    /// <summary>Id bossa (pusty = zwykły pokój).</summary>
    public string BossId { get; init; } = "";
    public int ObstacleSeed { get; init; }
    public int ObstacleCount { get; init; }
    public float HpMult { get; init; } = 1f;
    public float DmgMult { get; init; } = 1f;
    public float XpMult { get; init; } = 1f;

    public bool Boss => BossId.Length > 0;
}

/// <summary>Proceduralny plan runu ze STREFY (data-driven pula potworów + boss).
/// Iteracja 1: sekwencja liniowa; graf pokoi — przyszła iteracja przy tilesetach.</summary>
public static class RunGenerator
{
    public static List<RoomPlan> Generate(int seed, int playerLevel, ZoneDefinition zone)
    {
        var rng = new Random(seed);
        int rooms = zone.RoomsMin + rng.Next(Math.Max(1, zone.RoomsMax - zone.RoomsMin + 1));
        var plan = new List<RoomPlan>();
        float levelScale = 1f + playerLevel * 0.03f;

        for (int i = 0; i < rooms; i++)
        {
            var spawns = new List<string>();
            int count = zone.BaseSpawnCount + i * zone.SpawnCountPerRoom + rng.Next(2);
            for (int s = 0; s < count; s++)
                spawns.Add(zone.RollMonster(rng));

            plan.Add(new RoomPlan
            {
                Index = i,
                Spawns = spawns,
                ObstacleSeed = rng.Next(),
                ObstacleCount = 3 + rng.Next(4),
                HpMult = (1f + 0.12f * i) * levelScale,
                DmgMult = (1f + 0.08f * i) * levelScale,
                XpMult = 1f + 0.15f * i,
            });
        }

        // finał: boss + eskorta
        var escort = new List<string>();
        for (int s = 0; s < zone.BaseSpawnCount; s++)
            escort.Add(zone.RollMonster(rng));

        plan.Add(new RoomPlan
        {
            Index = rooms,
            Spawns = escort,
            BossId = zone.Boss,
            ObstacleSeed = rng.Next(),
            ObstacleCount = 2 + rng.Next(3),
            HpMult = (1f + 0.12f * rooms) * levelScale,
            DmgMult = (1f + 0.08f * rooms) * levelScale,
            XpMult = 1f + 0.15f * rooms,
        });

        return plan;
    }
}

using System;
using System.Collections.Generic;

namespace AshenPantheon.Core;

/// <summary>Plan jednego pokoju runu.</summary>
public sealed class RoomPlan
{
    public int Index { get; init; }
    public int HuskCount { get; init; }
    public bool Boss { get; init; }
    public int ObstacleSeed { get; init; }
    public int ObstacleCount { get; init; }
    public float HpMult { get; init; } = 1f;
    public float DmgMult { get; init; } = 1f;
    public long XpPerHusk { get; init; }
}

/// <summary>Proceduralny plan runu: sekwencja pokoi rosnącej trudności, finał z bossem.
/// Iteracja 1 (sekwencja liniowa); docelowo graf pokoi — decyzja udokumentowana w docs/design.</summary>
public static class RunGenerator
{
    public static List<RoomPlan> Generate(int seed, int playerLevel)
    {
        var rng = new Random(seed);
        int rooms = 4 + rng.Next(2); // 4–5 zwykłych pokoi + boss
        var plan = new List<RoomPlan>();
        float levelScale = 1f + playerLevel * 0.03f;

        for (int i = 0; i < rooms; i++)
        {
            plan.Add(new RoomPlan
            {
                Index = i,
                HuskCount = 4 + i * 2 + rng.Next(2),
                Boss = false,
                ObstacleSeed = rng.Next(),
                ObstacleCount = 3 + rng.Next(4),
                HpMult = (1f + 0.12f * i) * levelScale,
                DmgMult = (1f + 0.08f * i) * levelScale,
                XpPerHusk = 12 + 3 * i + playerLevel,
            });
        }

        plan.Add(new RoomPlan
        {
            Index = rooms,
            HuskCount = 4,
            Boss = true,
            ObstacleSeed = rng.Next(),
            ObstacleCount = 2 + rng.Next(3),
            HpMult = (1f + 0.12f * rooms) * levelScale,
            DmgMult = (1f + 0.08f * rooms) * levelScale,
            XpPerHusk = 12 + 3 * rooms + playerLevel,
        });

        return plan;
    }

    public const long BossXp = 150;
}

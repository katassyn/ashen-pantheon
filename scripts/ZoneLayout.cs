using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Struktura mapy: zamiast płaskiego prostokąta — KOMNATY (wokół spawnu, packów, wyjść)
/// połączone KORYTARZAMI, otoczone KOLIZYJNYMI ŚCIANAMI (nieregularny, zamknięty kształt lochu),
/// z taktycznymi przeszkodami. Deterministyczne z seeda (co-op: identyczny layout u wszystkich).
/// Ściany + przeszkody na warstwie 4 (blokują gracza i wrogów).</summary>
public partial class ZoneLayout : Node2D
{
    public int Seed;
    public Vector2 Spawn;
    public List<Vector2> RoomCenters = new(); // spawn + packi + wyjścia + markery
    public List<Vector2> Portals = new();      // wejścia — bez przeszkód/ścian blisko

    private readonly List<(Vector2 A, Vector2 B)> _corridors = new();
    private readonly List<(Vector2 Pos, float R, float Hue)> _rooms = new();

    private const float CorridorHalfWidth = 70f;
    private const float WallStep = 52f;

    private static readonly Color FloorBase = new(0.15f, 0.13f, 0.19f);
    private static readonly Color FloorEdge = new(0.34f, 0.29f, 0.46f);
    private static readonly Color WallFill = new(0.09f, 0.08f, 0.13f);
    private static readonly Color WallEdge = new(0.26f, 0.22f, 0.34f);
    private static readonly Color Dark = new(0.045f, 0.04f, 0.07f);

    public override void _Ready()
    {
        ZIndex = -5; // pod graczami/wrogami
        var rng = new Random(Seed);

        foreach (var c in RoomCenters)
        {
            float r = 155f + (float)rng.NextDouble() * 95f;
            float hue = (float)rng.NextDouble() * 0.06f - 0.03f; // subtelna wariacja koloru per komnata
            _rooms.Add((c, r, hue));
        }

        BuildCorridors(rng);
        BuildWalls();
        SpawnObstacles(rng);
        QueueRedraw();
    }

    // ── korytarze: MST (Prim) + kilka pętli ──
    private void BuildCorridors(Random rng)
    {
        int n = _rooms.Count;
        if (n < 2) return;
        var inTree = new bool[n];
        inTree[0] = true;
        var edges = new List<(int A, int B)>();

        for (int added = 1; added < n; added++)
        {
            float best = float.MaxValue; int ba = -1, bb = -1;
            for (int i = 0; i < n; i++)
            {
                if (!inTree[i]) continue;
                for (int j = 0; j < n; j++)
                {
                    if (inTree[j]) continue;
                    float d = _rooms[i].Pos.DistanceSquaredTo(_rooms[j].Pos);
                    if (d < best) { best = d; ba = i; bb = j; }
                }
            }
            if (bb < 0) break;
            inTree[bb] = true;
            edges.Add((ba, bb));
        }

        int extra = Math.Max(1, n / 4);
        for (int e = 0; e < extra; e++)
        {
            int i = rng.Next(n), j = rng.Next(n);
            if (i != j && !edges.Contains((i, j)) && !edges.Contains((j, i))
                && _rooms[i].Pos.DistanceTo(_rooms[j].Pos) < 720f)
                edges.Add((i, j));
        }

        foreach (var (a, b) in edges) _corridors.Add((_rooms[a].Pos, _rooms[b].Pos));
    }

    // ── ściany: pierścień wokół komnaty z LUKAMI w kierunku korytarzy + boki korytarzy ──
    private void BuildWalls()
    {
        foreach (var (pos, r, _) in _rooms)
        {
            // kierunki korytarzy wychodzących z tej komnaty (luki w ścianie)
            var openDirs = _corridors
                .Where(c => c.A == pos || c.B == pos)
                .Select(c => ((c.A == pos ? c.B : c.A) - pos).Angle())
                .ToList();
            float gapHalf = Mathf.Atan2(CorridorHalfWidth + 26f, r); // szerokość luki na wejście

            for (float a = 0; a < Mathf.Tau; a += 0.30f)
            {
                if (openDirs.Any(d => Mathf.Abs(Mathf.AngleDifference(a, d)) < gapHalf)) continue;
                var wp = pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r + 6f);
                if (Portals.Any(p => p.DistanceTo(wp) < 130f)) continue;
                AddWall(wp, 30f);
            }
        }

        foreach (var (a, b) in _corridors)
        {
            var dir = (b - a).Normalized();
            var perp = new Vector2(-dir.Y, dir.X);
            float len = a.DistanceTo(b);
            for (float t = WallStep; t < len - WallStep; t += WallStep)
            {
                var mid = a + dir * t;
                AddWall(mid + perp * (CorridorHalfWidth + 6f), 24f);
                AddWall(mid - perp * (CorridorHalfWidth + 6f), 24f);
            }
        }
    }

    private void AddWall(Vector2 pos, float size)
    {
        var body = new StaticBody2D { Position = pos, CollisionLayer = 4, CollisionMask = 0 };
        body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = size } });
        body.AddChild(new WallVisual { Radius = size });
        AddChild(body);
    }

    private void SpawnObstacles(Random rng)
    {
        foreach (var (center, radius, _) in _rooms)
        {
            bool nearPortal = Portals.Any(p => p.DistanceTo(center) < 140f);
            int count = nearPortal ? 0 : rng.Next(3);
            for (int i = 0; i < count; i++)
            {
                float ang = (float)(rng.NextDouble() * Math.Tau);
                float dist = radius * (0.3f + (float)rng.NextDouble() * 0.35f);
                var pos = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
                if (Portals.Any(p => p.DistanceTo(pos) < 100f)) continue;
                AddObstacle(pos, 30f + (float)rng.NextDouble() * 26f);
            }
        }
    }

    private void AddObstacle(Vector2 pos, float size)
    {
        var body = new StaticBody2D { Position = pos, CollisionLayer = 4, CollisionMask = 0 };
        body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = size } });
        body.AddChild(new ObstacleVisual { Radius = size });
        AddChild(body);
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(-3400, -2700, 6800, 5400), Dark); // ciemność poza strukturą

        // korytarze (pod komnatami)
        foreach (var (a, b) in _corridors)
        {
            var dir = (b - a).Normalized();
            var perp = new Vector2(-dir.Y, dir.X) * CorridorHalfWidth;
            DrawColoredPolygon(new[] { a + perp, b + perp, b - perp, a - perp }, FloorBase);
        }
        // komnaty: podłoga z subtelną wariacją + obrys; pierścień "podłogowy" pod ścianą
        foreach (var (pos, r, hue) in _rooms)
        {
            var floor = new Color(FloorBase.R + hue, FloorBase.G + hue * 0.7f, FloorBase.B + hue);
            DrawCircle(pos, r, floor);
            DrawArc(pos, r, 0, Mathf.Tau, 52, FloorEdge, 3f);
            DrawArc(pos, r * 0.5f, 0, Mathf.Tau, 40, new Color(FloorEdge, 0.15f), 2f); // detal
        }
    }
}

/// <summary>Ściana — ciemny kamienny blok z obrysem.</summary>
public partial class WallVisual : Node2D
{
    public float Radius = 30f;
    public override void _Ready() { ZIndex = 1; QueueRedraw(); }
    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, Radius, new Color(0.09f, 0.08f, 0.13f));
        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 20, new Color(0.26f, 0.22f, 0.34f), 3f);
    }
}

/// <summary>Taktyczna przeszkoda (głaz/kolumna) w komnacie.</summary>
public partial class ObstacleVisual : Node2D
{
    public float Radius = 40f;
    public override void _Ready() => QueueRedraw();
    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, Radius, new Color(0.20f, 0.17f, 0.28f));
        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 24, new Color(0.36f, 0.31f, 0.48f), 3f);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>Struktura mapy: zamiast płaskiego prostokąta — KOMNATY (wokół spawnu, packów, wyjść)
/// połączone KORYTARZAMI, na ciemnym tle, z taktycznymi przeszkodami (kolumny/głazy).
/// Deterministyczne z seeda (co-op: każda maszyna generuje identyczny layout).
/// Kolizyjne przeszkody na warstwie 4 (blokują gracza i wrogów).</summary>
public partial class ZoneLayout : Node2D
{
    public int Seed;
    public Vector2 Spawn;
    public List<Vector2> RoomCenters = new(); // spawn + packi + wyjścia
    public List<Vector2> Portals = new();      // pozycje portali/spawnu — bez przeszkód w pobliżu

    private readonly List<(Vector2 A, Vector2 B)> _corridors = new();
    private readonly List<(Vector2 Pos, float R)> _rooms = new();

    private static readonly Color FloorLit = new(0.16f, 0.14f, 0.20f);
    private static readonly Color FloorEdge = new(0.30f, 0.26f, 0.40f);
    private static readonly Color Dark = new(0.05f, 0.04f, 0.08f);

    public override void _Ready()
    {
        ZIndex = -5; // pod graczami/wrogami
        var rng = new Random(Seed);

        // 1) komnaty: promień zależny od "ważności" (spawn/wyjścia większe)
        foreach (var c in RoomCenters)
        {
            float r = 150f + (float)rng.NextDouble() * 90f;
            _rooms.Add((c, r));
        }

        // 2) korytarze: minimalne drzewo rozpinające (wszystko przechodnie) + kilka pętli
        BuildCorridors(rng);

        // 3) przeszkody taktyczne w komnatach (z dala od centrów packów i portali)
        SpawnObstacles(rng);

        QueueRedraw();
    }

    /// <summary>MST po centrach komnat (Prim) — gwarantuje spójność; + 25% dodatkowych krawędzi na pętle.</summary>
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

        // pętle: kilka dodatkowych krótkich połączeń (mapa nie jest czystym drzewem = ciekawsza nawigacja)
        int extra = Math.Max(1, n / 4);
        for (int e = 0; e < extra; e++)
        {
            int i = rng.Next(n), j = rng.Next(n);
            if (i != j && !edges.Contains((i, j)) && !edges.Contains((j, i))
                && _rooms[i].Pos.DistanceTo(_rooms[j].Pos) < 700f)
                edges.Add((i, j));
        }

        foreach (var (a, b) in edges) _corridors.Add((_rooms[a].Pos, _rooms[b].Pos));
    }

    private void SpawnObstacles(Random rng)
    {
        foreach (var (center, radius) in _rooms)
        {
            // przy portalach/spawnie mniej przeszkód (nie blokuj wejść)
            bool nearPortal = Portals.Any(p => p.DistanceTo(center) < 120f);
            int count = nearPortal ? 0 : 1 + rng.Next(3);
            for (int i = 0; i < count; i++)
            {
                float ang = (float)(rng.NextDouble() * Math.Tau);
                float dist = radius * (0.35f + (float)rng.NextDouble() * 0.4f);
                var pos = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
                if (Portals.Any(p => p.DistanceTo(pos) < 90f)) continue;
                AddObstacle(pos, 34f + (float)rng.NextDouble() * 30f);
            }
        }
    }

    private void AddObstacle(Vector2 pos, float size)
    {
        var body = new StaticBody2D { Position = pos, CollisionLayer = 4, CollisionMask = 0 };
        body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = size } });
        AddChild(body);

        // wizualny głaz/kolumna (rysowany w node przeszkody)
        var rock = new ObstacleVisual { Radius = size };
        body.AddChild(rock);
    }

    public override void _Draw()
    {
        // ciemne tło strefy (duży prostokąt) — komnaty rozjaśniają
        DrawRect(new Rect2(-3000, -2400, 6000, 4800), Dark);

        // korytarze najpierw (pod komnatami) — grube jasne pasy
        foreach (var (a, b) in _corridors)
        {
            var dir = (b - a).Normalized();
            var perp = new Vector2(-dir.Y, dir.X) * 66f;
            DrawColoredPolygon(new[] { a + perp, b + perp, b - perp, a - perp }, FloorLit);
        }
        // komnaty: wypełnienie + obrys
        foreach (var (pos, r) in _rooms)
        {
            DrawCircle(pos, r, FloorLit);
            DrawArc(pos, r, 0, Mathf.Tau, 48, FloorEdge, 4f);
        }
    }
}

/// <summary>Wizual przeszkody (głaz/kolumna) — dziecko kolizyjnego StaticBody2D.</summary>
public partial class ObstacleVisual : Node2D
{
    public float Radius = 40f;

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, Radius, new Color(0.20f, 0.17f, 0.28f));
        DrawArc(Vector2.Zero, Radius, 0, Mathf.Tau, 24, new Color(0.34f, 0.30f, 0.46f), 3f);
    }
}

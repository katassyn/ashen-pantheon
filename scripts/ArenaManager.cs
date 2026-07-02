using System;
using System.Collections.Generic;
using Godot;
using AshenPantheon.Core;

/// <summary>Proceduralny run: plan pokoi z RunGenerator (core). Pokój = przeszkody + wrogowie,
/// clear → następny pokój, finał z bossem → wygrana. Śmierć/wygrana → powrót do miasta (R).</summary>
public partial class ArenaManager : Node
{
    private enum State { Starting, Fighting, RoomCleared, Won, Lost }
    private State _state = State.Starting;

    private List<RoomPlan> _plan;
    private int _room;
    private float _timer = 1.2f;

    private PackedScene _enemyScene;
    private PackedScene _bossScene;
    private PlayerController _player;
    private readonly List<Node> _obstacles = new();

    public string TopStatus { get; private set; } = "";
    public string CenterMessage { get; private set; } = "";

    public override void _Ready()
    {
        AddToGroup("arena");
        _enemyScene = GD.Load<PackedScene>("res://scenes/Enemy.tscn");
        _bossScene = GD.Load<PackedScene>("res://scenes/Boss.tscn");
        _player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        _plan = RunGenerator.Generate(seed: (int)(GD.Randi() % int.MaxValue), GameState.Progress.Level);
    }

    public void OnPlayerDied()
    {
        if (_state is State.Won or State.Lost) return;
        _state = State.Lost;
        TopStatus = "";
        CenterMessage = "ZGINĄŁEŚ\n[R] powrót do miasta";
        GameState.Save();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        switch (_state)
        {
            case State.Starting:
                TopStatus = "Przygotuj się...";
                _timer -= dt;
                if (_timer <= 0f) StartRoom(0);
                break;

            case State.Fighting:
                int alive = GetTree().GetNodesInGroup("enemies").Count;
                var room = _plan[_room];
                TopStatus = $"Pokój {_room + 1}/{_plan.Count}{(room.Boss ? " [BOSS]" : "")}   wrogów: {alive}   lvl {GameState.Progress.Level}";
                if (alive == 0)
                {
                    if (_room + 1 >= _plan.Count)
                    {
                        _state = State.Won;
                        TopStatus = "";
                        CenterMessage = "RUN UKOŃCZONY!\n[R] powrót do miasta";
                        GameState.Save();
                    }
                    else
                    {
                        _state = State.RoomCleared;
                        _timer = 1.6f;
                    }
                }
                break;

            case State.RoomCleared:
                TopStatus = "Pokój oczyszczony — kolejny nadchodzi...";
                _timer -= dt;
                if (_timer <= 0f) StartRoom(_room + 1);
                break;

            case State.Won:
            case State.Lost:
                if (Input.IsPhysicalKeyPressed(Key.R))
                {
                    GameState.Save();
                    GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
                }
                break;
        }
    }

    private void StartRoom(int index)
    {
        _room = index;
        _state = State.Fighting;
        var room = _plan[index];

        SpawnObstacles(room);

        for (int i = 0; i < room.HuskCount; i++)
        {
            var e = _enemyScene.Instantiate<Enemy>();
            ApplyPlan(e, room);
            e.Position = RandomEdgePosition();
            GetParent().AddChild(e);
        }

        if (room.Boss)
        {
            var b = _bossScene.Instantiate<Boss>();
            ApplyPlan(b, room);
            b.Position = PlayerPos() + new Vector2(0f, -360f);
            GetParent().AddChild(b);
        }
    }

    private static void ApplyPlan(EnemyBase e, RoomPlan room)
    {
        e.HpMult = room.HpMult;
        e.DmgMult = room.DmgMult;
        if (e is Enemy) e.XpReward = room.XpPerHusk;
    }

    private void SpawnObstacles(RoomPlan room)
    {
        foreach (var o in _obstacles) if (IsInstanceValid(o)) o.QueueFree();
        _obstacles.Clear();

        var rng = new Random(room.ObstacleSeed);
        for (int i = 0; i < room.ObstacleCount; i++)
        {
            float ang = (float)(rng.NextDouble() * Math.Tau);
            float dist = 160f + (float)rng.NextDouble() * 260f;
            var pos = PlayerPos() + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
            float w = 40f + rng.Next(60), h = 40f + rng.Next(60);

            // warstwa 4 = teren/przeszkody: blokuje i gracza (maska 4), i wrogów (maska 4)
            var body = new StaticBody2D { Position = pos, CollisionLayer = 4, CollisionMask = 0 };
            var shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(w, h) } };
            body.AddChild(shape);
            var rect = new ColorRect
            {
                Size = new Vector2(w, h), Position = new Vector2(-w / 2f, -h / 2f),
                Color = new Color(0.20f, 0.17f, 0.28f)
            };
            body.AddChild(rect);
            GetParent().AddChild(body);
            _obstacles.Add(body);
        }
    }

    private Vector2 PlayerPos() =>
        _player != null && IsInstanceValid(_player) ? _player.GlobalPosition : Vector2.Zero;

    private Vector2 RandomEdgePosition()
    {
        float ang = GD.Randf() * Mathf.Tau;
        float dist = (float)GD.RandRange(380.0, 520.0);
        return PlayerPos() + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
    }
}

using Godot;

/// <summary>Pętla areny: fale wrogów → oczyść → kolejna → boss → wygrana/przegrana → restart (R).</summary>
public partial class ArenaManager : Node
{
    private readonly int[] _huskPerWave = { 4, 6, 3 };
    private readonly bool[] _bossWave = { false, false, true };
    private int TotalWaves => _huskPerWave.Length;

    private enum State { Starting, Fighting, BetweenWaves, Won, Lost }
    private State _state = State.Starting;
    private int _wave;
    private float _timer = 1.2f;

    private PackedScene _enemyScene;
    private PackedScene _bossScene;
    private PlayerController _player;

    public string TopStatus { get; private set; } = "";
    public string CenterMessage { get; private set; } = "";

    public override void _Ready()
    {
        AddToGroup("arena");
        _enemyScene = GD.Load<PackedScene>("res://scenes/Enemy.tscn");
        _bossScene = GD.Load<PackedScene>("res://scenes/Boss.tscn");
        _player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
    }

    public void OnPlayerDied()
    {
        if (_state is State.Won or State.Lost) return;
        _state = State.Lost;
        TopStatus = "";
        CenterMessage = "ZGINĄŁEŚ\n[R] restart";
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        switch (_state)
        {
            case State.Starting:
                TopStatus = "Przygotuj się...";
                _timer -= dt;
                if (_timer <= 0f) StartWave(0);
                break;

            case State.Fighting:
                int alive = GetTree().GetNodesInGroup("enemies").Count;
                TopStatus = $"Fala {_wave + 1}/{TotalWaves}   wrogów: {alive}";
                if (alive == 0)
                {
                    if (_wave + 1 >= TotalWaves)
                    {
                        _state = State.Won;
                        TopStatus = "";
                        CenterMessage = "ARENA OCZYSZCZONA!\n[R] restart";
                    }
                    else
                    {
                        _state = State.BetweenWaves;
                        _timer = 1.6f;
                    }
                }
                break;

            case State.BetweenWaves:
                TopStatus = "Fala oczyszczona — kolejna nadchodzi...";
                _timer -= dt;
                if (_timer <= 0f) StartWave(_wave + 1);
                break;

            case State.Won:
            case State.Lost:
                if (Input.IsPhysicalKeyPressed(Key.R))
                    GetTree().ReloadCurrentScene();
                break;
        }
    }

    private void StartWave(int index)
    {
        _wave = index;
        _state = State.Fighting;

        for (int i = 0; i < _huskPerWave[index]; i++)
            Spawn(_enemyScene, RandomEdgePosition());

        if (_bossWave[index])
            Spawn(_bossScene, PlayerPos() + new Vector2(0f, -340f));
    }

    private void Spawn(PackedScene scene, Vector2 pos)
    {
        var n = scene.Instantiate<Node2D>();
        n.Position = pos;
        GetParent().AddChild(n);
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

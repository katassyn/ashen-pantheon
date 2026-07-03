using System.Linq;
using Godot;
using AshenPantheon.Core;

/// <summary>Escortowany NPC (host-authoritative): idzie do celu, gdy gracz jest blisko (osłona).
/// Wrogowie w promieniu zagrożenia obniżają mu HP — trzeba ich odganiać. Śmierć = reset + fail celu.</summary>
public partial class EscortNpc : Node2D
{
    public string MarkerId = "";
    public string LabelText = "";
    public Vector2 StartPos;
    public Vector2 DestPos;
    public float MaxHp = 120f;
    public float MoveSpeed = 70f;

    private const float ProtectRadius = 260f; // gracz musi być bliżej, by NPC szedł
    private const float DangerRadius = 150f;   // wrogowie w tym promieniu ranią NPC
    private const float DrainPerEnemy = 6f;    // dps na jednego wroga w pobliżu
    private const float ArriveDist = 40f;

    private float _hp;
    private bool _arrived;
    private float _respawn;

    // puppet (klient)
    private Vector2 _netPos;
    private float _netHpFrac = 1f;

    public override void _Ready()
    {
        AddToGroup("escort");
        _hp = MaxHp;
        StartPos = GlobalPosition;
        _netPos = GlobalPosition;
        ZIndex = 5;
    }

    public void ApplyNetState(Vector2 pos, float hpFrac, bool moving)
    {
        _netPos = pos;
        _netHpFrac = hpFrac;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!Net.IsServer)
        {
            GlobalPosition = GlobalPosition.Lerp(_netPos, Mathf.Min(1f, 12f * (float)delta));
            QueueRedraw();
            return;
        }

        float dt = (float)delta;

        if (_respawn > 0f)
        {
            _respawn -= dt;
            if (_respawn <= 0f) { _hp = MaxHp; GlobalPosition = StartPos; }
            Broadcast(false);
            return;
        }
        if (_arrived) { Broadcast(false); return; }

        var player = NearestPlayer();
        bool protectedNow = player != null && GlobalPosition.DistanceTo(player.GlobalPosition) <= ProtectRadius;

        // wrogowie w pobliżu ranią NPC
        int nearEnemies = EnemyBase.All(GetTree()).Count(e => e.GlobalPosition.DistanceTo(GlobalPosition) <= DangerRadius);
        if (nearEnemies > 0)
        {
            _hp -= nearEnemies * DrainPerEnemy * dt;
            if (_hp <= 0f)
            {
                _hp = 0f;
                _respawn = 4f;
                Net.BroadcastEscortFailed(MarkerId);
                FloatingText.Spawn(GetParent(), GlobalPosition, "escort down!", new Color(0.9f, 0.3f, 0.3f), 16);
                Broadcast(false);
                return;
            }
        }

        bool moving = false;
        if (protectedNow)
        {
            Vector2 to = DestPos - GlobalPosition;
            if (to.Length() <= ArriveDist)
            {
                _arrived = true;
                Net.BroadcastEscortArrived(MarkerId);
                FloatingText.Spawn(GetParent(), GlobalPosition, "escort safe!", new Color(0.4f, 0.9f, 0.5f), 16);
            }
            else
            {
                GlobalPosition += to.Normalized() * MoveSpeed * dt;
                moving = true;
            }
        }
        Broadcast(moving);
    }

    private void Broadcast(bool moving)
    {
        QueueRedraw();
        if (Net.Online && Engine.GetPhysicsFrames() % 6 == 0)
            Net.SyncEscort(GlobalPosition, _hp / MaxHp, moving);
    }

    private PlayerController NearestPlayer()
    {
        PlayerController best = null;
        float bd = float.MaxValue;
        foreach (Node n in GetTree().GetNodesInGroup("players"))
            if (n is PlayerController p && !p.Dead)
            {
                float d = GlobalPosition.DistanceTo(p.GlobalPosition);
                if (d < bd) { bd = d; best = p; }
            }
        return best;
    }

    public override void _Draw()
    {
        float frac = Net.IsServer ? _hp / MaxHp : _netHpFrac;
        DrawCircle(Vector2.Zero, 14f, new Color(0.6f, 0.85f, 1f));
        DrawCircle(Vector2.Zero, 14f, new Color(0.2f, 0.4f, 0.7f), false, 2f);
        // pasek HP
        var barPos = new Vector2(-20, -28);
        DrawRect(new Rect2(barPos, new Vector2(40, 5)), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(barPos, new Vector2(40 * Mathf.Clamp(frac, 0, 1), 5)), new Color(0.4f, 0.8f, 1f));
        DrawString(ThemeDB.FallbackFont, new Vector2(-40, -34), $"◈ {LabelText}",
            HorizontalAlignment.Left, -1, 12, new Color(0.8f, 0.9f, 1f));
    }
}

/// <summary>Punkt obrony (host-authoritative): gdy gracz blisko, ruszają fale mobów szturmujących punkt.
/// Przetrwaj/odeprzyj N fal. Mobki spawnowane przez hosta = replikowane za darmo.</summary>
public partial class DefendZone : Node2D
{
    public string MarkerId = "";
    public string LabelText = "";
    public int Waves = 3;
    public System.Collections.Generic.List<string> WaveMonsters = new();
    public float WaveInterval = 12f;

    private const float TriggerRadius = 220f;
    private const float SpawnRadius = 360f;

    private bool _active;
    private int _wave;
    private float _timer;
    private readonly System.Collections.Generic.List<EnemyBase> _current = new();

    public override void _Ready()
    {
        AddToGroup("defend");
        ZIndex = 4;
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
        if (!Net.IsServer) return;
        float dt = (float)delta;

        if (!_active)
        {
            var p = NearestPlayer();
            if (p != null && GlobalPosition.DistanceTo(p.GlobalPosition) <= TriggerRadius)
            {
                _active = true;
                _timer = 0f;
            }
            return;
        }

        if (_wave >= Waves) return; // ukończone

        _current.RemoveAll(e => !IsInstanceValid(e));
        _timer -= dt;

        // następna fala: gdy poprzednia wybita LUB minął interwał
        if (_timer <= 0f || (_wave > 0 && _current.Count == 0))
        {
            SpawnWave();
            _wave++;
            Net.BroadcastDefendWave(MarkerId);
            _timer = WaveInterval;
            if (_wave >= Waves)
                FloatingText.Spawn(GetParent(), GlobalPosition, "point held!", new Color(0.4f, 0.9f, 0.5f), 16);
        }
    }

    private void SpawnWave()
    {
        if (WaveMonsters.Count == 0) return;
        int i = 0;
        foreach (var monsterId in WaveMonsters)
        {
            var m = Monster.Create(monsterId);
            m.HomePos = GlobalPosition;        // leash do punktu obrony
            m.AggroRange = 100000f;             // szturmują aktywnie
            m.Position = GlobalPosition + Vector2.Right.Rotated(Mathf.Tau * i / Mathf.Max(1, WaveMonsters.Count)) * SpawnRadius;
            i++;
            _current.Add(m);
            GetParent().AddChild(m);
        }
    }

    private PlayerController NearestPlayer()
    {
        PlayerController best = null;
        float bd = float.MaxValue;
        foreach (Node n in GetTree().GetNodesInGroup("players"))
            if (n is PlayerController p && !p.Dead)
            {
                float d = GlobalPosition.DistanceTo(p.GlobalPosition);
                if (d < bd) { bd = d; best = p; }
            }
        return best;
    }

    public override void _Draw()
    {
        var col = _active && _wave < Waves ? new Color(0.9f, 0.4f, 0.3f) : new Color(0.5f, 0.7f, 0.9f);
        DrawArc(Vector2.Zero, TriggerRadius, 0, Mathf.Tau, 48, new Color(col.R, col.G, col.B, 0.35f), 2f);
        string status = !_active ? $"◈ {LabelText}"
            : _wave >= Waves ? $"◈ {LabelText} — held!"
            : $"◈ {LabelText} — wave {_wave}/{Waves}";
        DrawString(ThemeDB.FallbackFont, new Vector2(-70, -TriggerRadius - 8), status,
            HorizontalAlignment.Left, -1, 13, new Color(1f, 0.9f, 0.6f));
    }
}

/// <summary>Strefa przetrwania (host-authoritative): wytrzymaj X sekund pod presją spawnów.
/// Opuszczenie strefy pauzuje licznik — trzeba stać w środku.</summary>
public partial class SurviveZone : Node2D
{
    public string MarkerId = "";
    public string LabelText = "";
    public int SurviveSeconds = 30;
    public System.Collections.Generic.List<string> WaveMonsters = new();
    public float WaveInterval = 8f;

    private const float TriggerRadius = 200f;
    private const float SpawnRadius = 340f;

    private bool _done;
    private float _elapsed;
    private float _spawnTimer;
    private int _credited;

    public override void _Ready()
    {
        AddToGroup("survive");
        ZIndex = 4;
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
        if (!Net.IsServer || _done) return;
        float dt = (float)delta;

        var p = NearestPlayerInside();
        if (p == null) return; // poza strefą — licznik stoi

        _elapsed += dt;
        int whole = Mathf.FloorToInt(_elapsed);
        if (whole > _credited)
        {
            for (int s = _credited + 1; s <= whole && s <= SurviveSeconds; s++)
                Net.BroadcastSurviveSecond(MarkerId);
            _credited = Mathf.Min(whole, SurviveSeconds);
        }

        if (_elapsed >= SurviveSeconds)
        {
            _done = true;
            FloatingText.Spawn(GetParent(), GlobalPosition, "survived!", new Color(0.4f, 0.9f, 0.5f), 16);
            return;
        }

        _spawnTimer -= dt;
        if (_spawnTimer <= 0f && WaveMonsters.Count > 0)
        {
            _spawnTimer = WaveInterval;
            int i = 0;
            foreach (var monsterId in WaveMonsters)
            {
                var m = Monster.Create(monsterId);
                m.HomePos = GlobalPosition;
                m.AggroRange = 100000f;
                m.Position = GlobalPosition + Vector2.Right.Rotated(Mathf.Tau * i / Mathf.Max(1, WaveMonsters.Count)) * SpawnRadius;
                i++;
                GetParent().AddChild(m);
            }
        }
    }

    private PlayerController NearestPlayerInside()
    {
        foreach (Node n in GetTree().GetNodesInGroup("players"))
            if (n is PlayerController p && !p.Dead && GlobalPosition.DistanceTo(p.GlobalPosition) <= TriggerRadius)
                return p;
        return null;
    }

    public override void _Draw()
    {
        var col = _done ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.9f, 0.7f, 0.3f);
        DrawArc(Vector2.Zero, TriggerRadius, 0, Mathf.Tau, 48, new Color(col.R, col.G, col.B, 0.35f), 2f);
        int left = Mathf.Max(0, SurviveSeconds - Mathf.FloorToInt(_elapsed));
        string status = _done ? $"◈ {LabelText} — survived!" : $"◈ {LabelText} — {left}s (stay inside)";
        DrawString(ThemeDB.FallbackFont, new Vector2(-70, -TriggerRadius - 8), status,
            HorizontalAlignment.Left, -1, 13, new Color(1f, 0.9f, 0.6f));
    }
}

using Godot;

/// <summary>Feel ruchu gracza (warstwa czysto wizualna, bez mechanik): cień pod postacią,
/// pył kroków przy ruchu, smuga (afterimage) podczas dashu, obrót sprite'a w stronę celowania/ruchu.
/// Działa dla lokalnego gracza i puppetów; gotowe pod podmianę na prawdziwy art.</summary>
public partial class PlayerFx : Node2D
{
    private PlayerController _owner;
    private Sprite2D _sprite;

    private float _stepAccum;      // dystans do kolejnego pyłu kroku
    private float _afterimageAcc;  // timer smugi dashu
    private bool _wasDashing;

    public override void _Ready()
    {
        ZIndex = -3; // cień pod sprite'em/animacją
        _owner = GetParent<PlayerController>();
        _sprite = _owner.GetNodeOrNull<Sprite2D>("Sprite2D");
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_owner == null || !IsInstanceValid(_owner) || _owner.Dead) return;
        float dt = (float)delta;
        var vel = _owner.Velocity;
        float spd = vel.Length();

        // pył kroków: co ~46px przebytej drogi mały obłoczek przy nogach
        if (spd > 40f)
        {
            _stepAccum += spd * dt;
            if (_stepAccum >= 46f)
            {
                _stepAccum = 0f;
                SpawnDust(_owner.GlobalPosition + new Vector2((float)GD.RandRange(-8, 8), 20f), spd);
            }
        }
        else _stepAccum = 46f; // stojąc — natychmiastowy pył na starcie ruchu

        // facing: patrz w stronę celowania (lokalny) lub ruchu (puppet)
        if (_sprite != null)
        {
            float faceX = _owner.IsMultiplayerAuthority() ? _owner.AimDirection().X : vel.X;
            if (Mathf.Abs(faceX) > 6f) _sprite.FlipH = faceX < 0f;
        }

        // smuga dashu: kopie sprite'a co 0.03 s podczas dashu (+ błysk/squash na starcie)
        bool dashing = _owner.IsDashing;
        if (dashing && !_wasDashing) OnDashStart();
        if (dashing)
        {
            _afterimageAcc -= dt;
            if (_afterimageAcc <= 0f) { _afterimageAcc = 0.03f; SpawnAfterimage(); }
        }
        _wasDashing = dashing;
    }

    private void OnDashStart()
    {
        // błysk startu w kierunku dashu (mały rozbłysk pod nogami)
        SpawnDust(_owner.GlobalPosition + new Vector2(0, 18f), 600f);
    }

    private void SpawnDust(Vector2 pos, float speed)
    {
        var puff = new DustPuff { StartRadius = Mathf.Clamp(speed / 90f, 4f, 12f) };
        _owner.GetParent().AddChild(puff);
        puff.GlobalPosition = pos;
    }

    private void SpawnAfterimage()
    {
        if (_sprite == null) return;
        var ghost = new DashAfterimage
        {
            Texture = _sprite.Texture,
            GlobalPosition = _sprite.GlobalPosition,
            Scale = _sprite.Scale,
            FlipH = _sprite.FlipH,
            Rotation = _sprite.GlobalRotation,
        };
        _owner.GetParent().AddChild(ghost);
    }

    // cień: miękki ciemny owal pod postacią, osadza ją w świecie
    public override void _Draw()
    {
        DrawSetTransform(new Vector2(0, 22f), 0f, new Vector2(1f, 0.5f));
        DrawCircle(Vector2.Zero, 20f, new Color(0f, 0f, 0f, 0.28f));
    }
}

/// <summary>Obłoczek pyłu spod stóp — rośnie i zanika.</summary>
public partial class DustPuff : Node2D
{
    public float StartRadius = 6f;
    private float _t;
    private const float Life = 0.35f;

    public override void _Ready() { ZIndex = -2; QueueRedraw(); }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (_t >= Life) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float k = _t / Life;
        float r = StartRadius * (1f + k * 1.6f);
        float a = 0.22f * (1f - k);
        DrawCircle(Vector2.Zero, r, new Color(0.6f, 0.58f, 0.66f, a));
    }
}

/// <summary>Zanikająca kopia sprite'a podczas dashu (smuga ruchu).</summary>
public partial class DashAfterimage : Sprite2D
{
    private float _t;
    private const float Life = 0.22f;

    public override void _Ready()
    {
        ZIndex = -1;
        SelfModulate = new Color(0.7f, 0.85f, 1f, 0.5f);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        float k = _t / Life;
        if (k >= 1f) { QueueFree(); return; }
        SelfModulate = new Color(0.7f, 0.85f, 1f, 0.5f * (1f - k));
    }
}

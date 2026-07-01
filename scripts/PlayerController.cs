using Godot;
using AshenPantheon.Core;

public partial class PlayerController : CharacterBody2D
{
    [Export] public float Speed = 240f;

    [Export] public float DashSpeed = 780f;
    [Export] public float DashDuration = 0.15f;
    [Export] public float IFrameDuration = 0.2f;

    private CharacterSheet _sheet;
    public CharacterSheet Sheet => _sheet;
    public float MaxHealth => _sheet?.MaxLife ?? 100f;
    public float Health { get; private set; }
    private bool _dead;

    [Export] public float MaxConcentration = 100f;
    [Export] public float ConcentrationRegen = 30f;
    public float Concentration { get; private set; }

    private bool _godActive;
    public bool GodActive => _godActive;
    public string GodName => RangerKit.GodName;

    // Cooldowny skilli z CD (4–9)
    private float _rainCd, _mineCd, _hedgeCd, _dashCd, _hawkCd, _adrenalineCd;
    private float _adrenalineTime;

    // Dash
    private float _dashTimeLeft, _iFrameLeft;
    private Vector2 _dashDir;
    public bool IsInvulnerable => _iFrameLeft > 0f;

    private PackedScene _projectileScene;

    public override void _Ready()
    {
        _projectileScene = GD.Load<PackedScene>("res://scenes/Projectile.tscn");
        RecomputeSheet();
        Health = MaxHealth;
        Concentration = MaxConcentration;
    }

    private void RecomputeSheet()
    {
        _sheet = GameState.BuildSheet();
        if (Health > MaxHealth) Health = MaxHealth;
    }

    public void PickUp(Item item) => GameState.Inventory.Add(item);
    public void Refresh() => RecomputeSheet();

    /// <summary>Obrażenia od wroga — mitygowane armour/resistami (na razie traktowane jako fizyczne).</summary>
    public void TakeDamage(float amount)
    {
        if (_dead || IsInvulnerable) return;
        float mitigated = _sheet != null ? _sheet.MitigatedDamage(DamageType.Physical, amount) : amount;
        Health -= mitigated;
        if (Health <= 0f)
        {
            Health = 0f;
            _dead = true;
            Velocity = Vector2.Zero;
            if (GetTree().GetFirstNodeInGroup("arena") is ArenaManager arena)
                arena.OnPlayerDied();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_dead) return;

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left) CastBasic();
            else if (mb.ButtonIndex == MouseButton.Right) CastSpread();
        }

        if (@event is InputEventKey k && k.Pressed && !k.Echo)
        {
            switch (k.PhysicalKeycode)
            {
                case Key.Q: CastExecutor(); break;
                case Key.E: CastRain(); break;
                case Key.R: CastMine(); break;
                case Key.F: CastHedge(); break;
                case Key.Space: CastDash(); break;
                case Key.X: CastAdrenaline(); break;
                case Key.Z: CastHawk(); break;
                case Key.G: _godActive = !_godActive; break;
            }
        }
    }

    // --- Pomocnicze ---

    private Vector2 AimDirection()
    {
        Vector2 dir = GetGlobalMousePosition() - GlobalPosition;
        return dir == Vector2.Zero ? Vector2.Right : dir.Normalized();
    }

    private bool TrySpend(float cost)
    {
        if (_adrenalineTime > 0f) return true; // ∞ koncentracji podczas adrenaliny
        if (Concentration < cost) return false;
        Concentration -= cost;
        return true;
    }

    /// <summary>Podpięcie ofensywy: atk damage % z atrybutów/gearu + rzut na krytyka.</summary>
    private ResolvedSkill Offense(ResolvedSkill s)
    {
        if (_sheet == null) return s;
        s.Damage *= _sheet.AttackDamageMultiplier;
        if (GD.Randf() < _sheet.CritChance) s.Damage *= _sheet.CritMultiplier;
        return s;
    }

    private void SpawnProjectile(ResolvedSkill skill, Vector2 dir, Color tint)
    {
        var proj = _projectileScene.Instantiate<Projectile>();
        proj.Setup(skill, dir, tint);
        GetParent().AddChild(proj);
        proj.GlobalPosition = GlobalPosition + dir * 20f;
    }

    // --- Skille 1–3 ---

    private void CastBasic()
    {
        var skill = RangerKit.BasicShot(_godActive);
        if (!TrySpend(skill.ConcentrationCost)) return;
        SpawnProjectile(Offense(skill), AimDirection(), new Color(0.95f, 0.95f, 0.85f));
    }

    private void CastSpread()
    {
        var skill = RangerKit.Spread(_godActive);
        if (!TrySpend(skill.ConcentrationCost)) return;
        Offense(skill);

        int count = RangerKit.SpreadCount(_godActive);
        float baseAngle = AimDirection().Angle();
        float spread = Mathf.DegToRad(12f);
        float start = -spread * (count - 1) / 2f;
        for (int i = 0; i < count; i++)
            SpawnProjectile(skill, Vector2.Right.Rotated(baseAngle + start + spread * i), new Color(0.6f, 0.95f, 0.5f));
    }

    private void CastExecutor()
    {
        var skill = RangerKit.Executor(_godActive);
        if (!TrySpend(skill.ConcentrationCost)) return;
        SpawnProjectile(Offense(skill), AimDirection(), new Color(1f, 0.85f, 0.3f));
    }

    // --- Skille 4–9 (koszt + CD) ---

    private void CastRain()
    {
        if (_rainCd > 0f) return;
        var skill = RangerKit.Rain(_godActive);
        if (!TrySpend(skill.ConcentrationCost)) return;
        _rainCd = RangerKit.RainCd;

        var zone = new GroundZone();
        zone.Setup(Offense(skill), RangerKit.RainRadius(_godActive));
        GetParent().AddChild(zone);
        zone.GlobalPosition = GetGlobalMousePosition();
    }

    private void CastMine()
    {
        if (_mineCd > 0f) return;
        var skill = RangerKit.Mine(_godActive);
        if (!TrySpend(skill.ConcentrationCost)) return;
        _mineCd = RangerKit.MineCd;

        var mine = new Mine();
        mine.Setup(Offense(skill));
        GetParent().AddChild(mine);
        mine.GlobalPosition = GlobalPosition;
    }

    private void CastHedge()
    {
        if (_hedgeCd > 0f) return;
        var skill = RangerKit.Hedge(_godActive);
        if (!TrySpend(skill.ConcentrationCost)) return;
        _hedgeCd = RangerKit.HedgeCd;

        var hedge = new HedgeZone();
        GetParent().AddChild(hedge);
        hedge.GlobalPosition = GlobalPosition;
        hedge.Setup(Offense(skill), AimDirection(), 340f);
    }

    private void CastDash()
    {
        if (_dashCd > 0f || _dashTimeLeft > 0f) return;
        if (!TrySpend(RangerKit.DashConcentration)) return;
        _dashCd = RangerKit.DashCd;

        Vector2 dir = ReadMoveInput();
        if (dir == Vector2.Zero) dir = AimDirection();
        _dashDir = dir;
        _dashTimeLeft = DashDuration;
        _iFrameLeft = IFrameDuration;
    }

    private void CastAdrenaline()
    {
        if (_adrenalineCd > 0f) return;
        _adrenalineCd = RangerKit.AdrenalineCd;
        _adrenalineTime = RangerKit.AdrenalineDuration;
    }

    private void CastHawk()
    {
        if (_hawkCd > 0f) return;
        var skill = RangerKit.Hawk(_godActive);
        if (!TrySpend(skill.ConcentrationCost)) return;
        _hawkCd = RangerKit.HawkCd;

        var hawk = new Hawk();
        hawk.Setup(Offense(skill));
        GetParent().AddChild(hawk);
        hawk.GlobalPosition = GetGlobalMousePosition();
    }

    private static Vector2 ReadMoveInput()
    {
        Vector2 v = Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.W)) v.Y -= 1f;
        if (Input.IsPhysicalKeyPressed(Key.S)) v.Y += 1f;
        if (Input.IsPhysicalKeyPressed(Key.A)) v.X -= 1f;
        if (Input.IsPhysicalKeyPressed(Key.D)) v.X += 1f;
        return v == Vector2.Zero ? Vector2.Zero : v.Normalized();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dead) { Velocity = Vector2.Zero; return; }

        float dt = (float)delta;

        if (_rainCd > 0f) _rainCd -= dt;
        if (_mineCd > 0f) _mineCd -= dt;
        if (_hedgeCd > 0f) _hedgeCd -= dt;
        if (_dashCd > 0f) _dashCd -= dt;
        if (_hawkCd > 0f) _hawkCd -= dt;
        if (_adrenalineCd > 0f) _adrenalineCd -= dt;
        if (_iFrameLeft > 0f) _iFrameLeft -= dt;
        if (_adrenalineTime > 0f) _adrenalineTime -= dt;

        if (_adrenalineTime > 0f)
            Concentration = MaxConcentration;
        else
            Concentration = Mathf.Min(MaxConcentration, Concentration + ConcentrationRegen * dt);

        if (_dashTimeLeft > 0f)
        {
            _dashTimeLeft -= dt;
            Velocity = _dashDir * DashSpeed;
            MoveAndSlide();
            return;
        }

        float speed = Speed;
        if (_godActive) speed *= RangerKit.GodMoveSpeedBonus;
        if (_adrenalineTime > 0f) speed *= 1.4f;

        Velocity = ReadMoveInput() * speed;
        MoveAndSlide();
    }
}

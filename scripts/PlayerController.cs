using System.Collections.Generic;
using Godot;
using AshenPantheon.Core;

public partial class PlayerController : CharacterBody2D
{
    /// <summary>Lokalny gracz tej maszyny (puppety innych peerów tego nie ustawiają).</summary>
    public static PlayerController Local { get; private set; }

    [Export] public float Speed = 240f;
    [Export] public float DashSpeed = 780f;
    [Export] public float DashDuration = 0.15f;
    [Export] public float IFrameDuration = 0.2f;
    [Export] public bool CombatEnabled = true;

    private CharacterSheet _sheet;
    public CharacterSheet Sheet => _sheet;
    private readonly PlayerDefense _defense = new();
    public float MaxHealth => _sheet?.MaxLife ?? 100f;
    public float MaxEnergyShield => _sheet?.MaxEnergyShield ?? 0f;
    public float Health { get => _defense.Health; private set => _defense.Health = value; }
    public float EnergyShield => _defense.EnergyShield;
    private bool _dead;
    public bool Dead => _dead;
    /// <summary>Ułamek HP: lokalny = realny, puppet = z sieci (party frame).</summary>
    public float HealthFraction => IsMultiplayerAuthority()
        ? (MaxHealth > 0f ? Mathf.Clamp(Health / MaxHealth, 0f, 1f) : 0f)
        : Mathf.Clamp(_netHpFrac, 0f, 1f);

    public float MaxResource => GameState.Class.ResourceMax;
    public float Resource { get; private set; }

    private readonly Dictionary<string, float> _cd = new();
    public float CooldownLeft(string skillId) => _cd.GetValueOrDefault(skillId);

    private float _adrenalineTime;
    private float _adrenalineDmgBonus;
    public bool AdrenalineActive => _adrenalineTime > 0f;
    public float AdrenalineTimeLeft => Mathf.Max(0f, _adrenalineTime);

    private float _dashTimeLeft, _iFrameLeft;
    private Vector2 _dashDir;
    public bool IsInvulnerable => _iFrameLeft > 0f;

    private EnemyAnimator _animator;

    // puppet: interpolacja/ekstrapolacja stanu z sieci
    private Vector2 _netPos;
    private Vector2 _netVel;
    private float _netHpFrac = 1f;
    // krótka pauza broadcastu po spawnie — peery zmieniają scenę w różnych klatkach (mniej zgubionych pakietów)
    private float _syncWarmup = 0.5f;

    public override void _Ready()
    {
        _netPos = GlobalPosition;
        _animator = GetNodeOrNull<EnemyAnimator>("Animator");

        if (!IsMultiplayerAuthority())
        {
            // puppet innego gracza: tylko wizualia
            var cam = GetNodeOrNull<Camera2D>("Camera2D");
            if (cam != null) cam.Enabled = false;
            var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
            if (sprite != null) sprite.Modulate = new Color(0.7f, 0.9f, 1f); // odróżnij sojusznika
            return;
        }

        Local = this;
        GameState.LoadOrInit();
        RecomputeSheet();
        _defense.ResetFull(_sheet);
        Resource = MaxResource;
        Net.AnnounceName(); // social: rejestr nicków
    }

    public override void _ExitTree()
    {
        if (Local == this) Local = null;
    }

    private void RecomputeSheet()
    {
        _sheet = GameState.BuildSheet();
        if (Health > MaxHealth) Health = MaxHealth;
    }

    public void Refresh() => RecomputeSheet();

    public void Heal(float amount)
    {
        if (_dead) return;
        Health = Mathf.Min(MaxHealth, Health + amount);
    }

    public void TakeDamage(float amount, DamageType type = DamageType.Physical)
    {
        if (_dead || IsInvulnerable) return;
        float mitigated = _sheet != null ? _sheet.MitigatedDamage(type, amount) : amount;
        _defense.Absorb(mitigated, type); // ES przed HP (chaos przebija ES)
        if (mitigated > 0.5f)
            FloatingText.Spawn(GetParent(), GlobalPosition, $"-{mitigated:0}", new Color(1f, 0.35f, 0.3f), 15);
        _animator?.Flash("hit");
        if (Health <= 0f)
        {
            Health = 0f;
            SetDead(true);
            Net.NotifyPlayerDied();
            // mapa świata: brak wipe'u — samo-odrodzenie u wejścia strefy po 3 s
            if (GetTree().GetFirstNodeInGroup("arena") is WorldZoneManager)
                _worldRespawn = 3f;
        }
    }

    private float _worldRespawn;

    /// <summary>Odrodzenie (po oczyszczeniu pokoju przez drużynę).</summary>
    public void Revive(float healthFraction)
    {
        if (!_dead) return;
        SetDead(false);
        Health = MaxHealth * healthFraction;
    }

    private void SetDead(bool dead)
    {
        _dead = dead;
        Velocity = Vector2.Zero;
        CollisionLayer = dead ? 0u : 1u; // trup nie blokuje wrogów/sojuszników
        var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite != null) sprite.Modulate = dead
            ? new Color(0.4f, 0.4f, 0.4f, 0.6f)
            : (IsMultiplayerAuthority() ? Colors.White : new Color(0.7f, 0.9f, 1f));
    }

    private void PayHealth(float amount) => Health = Mathf.Max(1f, Health - amount);

    // ── budowa skilla: SkillResolver (dane) — spec → patch boga → drzewko → broń/arkusz ──

    public ResolvedSkill BuildSkill(string skillId)
    {
        var spec = GameState.ClassSpec.Skill(skillId);
        if (spec == null) return null;

        GodSpec god = GameState.GodSkills.Contains(skillId) ? GameData.God(GameState.PledgedGod) : null;
        var ctx = new CasterContext
        {
            AttackDamageMultiplier = _sheet?.AttackDamageMultiplier ?? 1f,
            HitChance = _sheet?.HitChance ?? 100f,
            WeaponDamage = _sheet?.WeaponDamage ?? 0f,
            AttackSpeed = _sheet?.AttackSpeed ?? 1f,
            CastSpeed = _sheet?.CastSpeed ?? 1f,
            CasterPeer = Net.MyId,
        };
        var s = SkillResolver.Resolve(spec, god, GameState.Trees, ctx);

        if (GameState.HasUniqueEffect(UniqueEffect.Overcharge)) s.CostMult *= 1.2f;
        if (skillId == "dash" && GameState.HasUniqueEffect(UniqueEffect.SwiftDash)) s.CdMult *= 0.6f;
        if (s.Damage > 0f && _adrenalineTime > 0f && _adrenalineDmgBonus > 0f)
            s.Damage *= 1f + _adrenalineDmgBonus;
        return s;
    }

    /// <summary>Rzut na krytyka + unik MarkOnHit (celność/unik liczy CombatResolver per trafienie).</summary>
    private ResolvedSkill Offense(ResolvedSkill s)
    {
        if (_sheet != null && GD.Randf() < _sheet.CritChance)
        {
            s.Damage *= _sheet.CritMultiplier;
            s.IsCrit = true;
        }
        if (GameState.HasUniqueEffect(UniqueEffect.MarkOnHit) && !s.AppliesMark)
        {
            s.AppliesMark = true;
            s.MarkDuration = Mathf.Max(s.MarkDuration, 3f);
        }
        return s;
    }

    private bool TryPay(ResolvedSkill s)
    {
        float cost = s.ConcentrationCost * s.CostMult;
        if (s.VariantTag == "dash_blood") { PayHealth(8f); return true; }
        if (_adrenalineTime > 0f || cost <= 0f) return true;

        if (Resource >= cost) { Resource -= cost; return true; }

        float hpPerPoint = GameData.God(GameState.PledgedGod)?.BloodCostHpPerPoint ?? 0f;
        if (hpPerPoint > 0f)
        {
            float missing = cost - Resource;
            Resource = 0f;
            PayHealth(missing * hpPerPoint);
            return true;
        }
        return false;
    }

    // ── input (tylko lokalny gracz) ──

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsMultiplayerAuthority() || _dead || !CombatEnabled) return;

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left) CastSlot(0);
            else if (mb.ButtonIndex == MouseButton.Right) CastSlot(1);
        }

        if (@event is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (Keybinds.Matches(k, "slot_q")) CastSlot(2);
            else if (Keybinds.Matches(k, "slot_e")) CastSlot(3);
            else if (Keybinds.Matches(k, "slot_r")) CastSlot(4);
        }
    }

    // globalny lock rzucania — atk/cast speed realnie steruje tempem skilli
    private float _castLock;

    private void CastSlot(int slot)
    {
        string skillId = GameState.Loadout.Slots[slot];
        if (skillId == null) return;

        var info = GameState.Class.Skill(skillId);
        if (info == null) return;
        // skill odblokowuje się z poziomem postaci
        if ((GameState.ClassSpec.Skill(skillId)?.RequiredLevel ?? 1) > GameState.Progress.Level) return;
        if (_castLock > 0f) return;
        if (_cd.GetValueOrDefault(skillId) > 0f) return;
        if (skillId == "dash" && _dashTimeLeft > 0f) return;

        var s = BuildSkill(skillId);
        if (s == null || !TryPay(s)) return;

        Execute(skillId, s);

        _castLock = s.CastTime;
        float cd = info.Cooldown * s.CdMult;
        if (cd > 0f) _cd[skillId] = cd;
    }

    private void Execute(string skillId, ResolvedSkill s)
    {
        switch (skillId)
        {
            case "basic": FireProjectiles(Offense(s), new Color(0.95f, 0.95f, 0.85f)); break;
            case "spread": FireSpread(s); break;
            case "exec": FireProjectiles(Offense(s), new Color(1f, 0.85f, 0.3f)); break;
            case "rain":
                Net.SpawnEffect("rain", Offense(s), new Color(0.5f, 0.8f, 1f), GetGlobalMousePosition(), Vector2.Zero, 120f * s.AoeMult);
                break;
            case "mine": CastMine(s); break;
            case "hedge": CastHedge(s); break;
            case "dash": CastDash(s); break;
            case "adrenaline": CastAdrenaline(s); break;
            case "hawk": CastHawk(s); break;
        }
    }

    private Vector2 AimDirection()
    {
        Vector2 dir = GetGlobalMousePosition() - GlobalPosition;
        return dir == Vector2.Zero ? Vector2.Right : dir.Normalized();
    }

    private void FireProjectiles(ResolvedSkill s, Color tint)
    {
        Vector2 dir = AimDirection();
        Net.SpawnEffect("proj", s, tint, GlobalPosition + dir * 20f, dir);
        for (int i = 0; i < s.ExtraProjectiles && s.Id != "spread"; i++)
            Net.SpawnEffect("proj", s, tint, GlobalPosition + dir * 20f, dir.Rotated(Mathf.DegToRad(6f * (i + 1))));
    }

    private void FireSpread(ResolvedSkill s)
    {
        Offense(s);
        int count = 3 + s.ExtraProjectiles;
        float baseAngle = AimDirection().Angle();
        float spreadRad = Mathf.DegToRad(12f);
        float start = -spreadRad * (count - 1) / 2f;
        var tint = new Color(0.6f, 0.95f, 0.5f);
        for (int i = 0; i < count; i++)
        {
            var dir = Vector2.Right.Rotated(baseAngle + start + spreadRad * i);
            Net.SpawnEffect("proj", s, tint, GlobalPosition + dir * 20f, dir);
        }
    }

    private void CastMine(ResolvedSkill s)
    {
        Offense(s);
        int mines = 1 + s.ExtraProjectiles;
        for (int i = 0; i < mines; i++)
            Net.SpawnEffect("mine", s, Colors.White, GlobalPosition + (i == 0 ? Vector2.Zero : new Vector2(46f * i, 0f)), Vector2.Zero);
    }

    private void CastHedge(ResolvedSkill s)
    {
        Offense(s);
        if (s.VariantTag == "hedge_bomb")
        {
            s.Explodes = true;
            Vector2 dir = AimDirection();
            Net.SpawnEffect("proj", s, new Color(0.5f, 0.9f, 0.4f), GlobalPosition + dir * 20f, dir);
            return;
        }
        Net.SpawnEffect("hedge", s, Colors.White, GlobalPosition, AimDirection(), 340f * s.AoeMult);
    }

    private void CastDash(ResolvedSkill s)
    {
        Vector2 dir = ReadMoveInput();
        if (dir == Vector2.Zero) dir = AimDirection();
        _dashDir = dir;
        _dashTimeLeft = DashDuration * s.AoeMult;
        _iFrameLeft = IFrameDuration * s.DurationMult;

        if (s.VariantTag == "dash_trail")
        {
            var trail = SkillResolver.Resolve(GameState.ClassSpec.Skill("hedge"), null, null, new CasterContext { CasterPeer = Net.MyId });
            trail.Damage = 6f;
            trail.CasterPeer = Net.MyId;
            Net.SpawnEffect("hedge", trail, Colors.White, GlobalPosition, dir, DashSpeed * DashDuration * s.AoeMult);
        }
    }

    private void CastAdrenaline(ResolvedSkill s)
    {
        _adrenalineTime = 5f * s.DurationMult;
        _adrenalineDmgBonus = s.Damage;
        if (s.VariantTag == "adrenaline_blood") Heal(30f);
    }

    private void CastHawk(ResolvedSkill s)
    {
        Offense(s);
        if (s.VariantTag == "hawk_pets")
        {
            for (int i = 0; i < 3; i++)
            {
                var petSkill = s;
                Net.SpawnEffect("pet", new ResolvedSkill { Id = "pet", Damage = s.Damage * 0.45f, Shape = SkillShape.SingleTarget, CasterPeer = Net.MyId },
                    Colors.White, GlobalPosition + Vector2.Right.Rotated(Mathf.Tau * i / 3f) * 40f, Vector2.Zero);
            }
            return;
        }
        Net.SpawnEffect("hawk", s, Colors.White, GetGlobalMousePosition(), Vector2.Zero, s.VariantTag == "hawk_all" ? 1f : 0f);
    }

    private static Vector2 ReadMoveInput()
    {
        Vector2 v = Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Keybinds.Get("move_up"))) v.Y -= 1f;
        if (Input.IsPhysicalKeyPressed(Keybinds.Get("move_down"))) v.Y += 1f;
        if (Input.IsPhysicalKeyPressed(Keybinds.Get("move_left"))) v.X -= 1f;
        if (Input.IsPhysicalKeyPressed(Keybinds.Get("move_right"))) v.X += 1f;
        return v == Vector2.Zero ? Vector2.Zero : v.Normalized();
    }

    // ── sync stanu gracza (pozycja + prędkość do ekstrapolacji + HP/śmierć dla pasków sojuszników) ──

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void NetState(Vector2 pos, Vector2 vel, float hpFrac, bool dead)
    {
        _netPos = pos;
        _netVel = vel;
        _netHpFrac = hpFrac;
        if (dead != _dead) SetDead(dead);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (IsMultiplayerAuthority()) return; // własne HP jest w HUD — pasek tylko nad sojusznikami
        var size = new Vector2(40f, 5f);
        var pos = new Vector2(-20f, -34f);
        DrawRect(new Rect2(pos, size), new Color(0f, 0f, 0f, 0.7f));
        DrawRect(new Rect2(pos, new Vector2(size.X * Mathf.Clamp(_netHpFrac, 0f, 1f), size.Y)),
            new Color(0.25f, 0.8f, 0.35f));
        if (_dead)
            DrawString(ThemeDB.FallbackFont, new Vector2(-28f, -40f), "POKONANY",
                HorizontalAlignment.Left, -1, 10, new Color(1f, 0.4f, 0.4f));
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (!IsMultiplayerAuthority())
        {
            // ekstrapolacja z prędkości + łagodna korekta do ostatniej znanej pozycji
            GlobalPosition += _netVel * dt;
            GlobalPosition = GlobalPosition.Lerp(_netPos, Mathf.Min(1f, 8f * dt));
            return;
        }

        // broadcast stanu też po śmierci (sojusznicy widzą zgon i pasek HP)
        if (_syncWarmup > 0f) _syncWarmup -= dt;
        else if (Net.Online && Engine.GetPhysicsFrames() % 2 == 0)
            Rpc(MethodName.NetState, GlobalPosition, Velocity,
                MaxHealth > 0f ? Health / MaxHealth : 0f, _dead);

        if (_dead)
        {
            Velocity = Vector2.Zero;
            if (_worldRespawn > 0f)
            {
                _worldRespawn -= dt;
                if (_worldRespawn <= 0f && GetTree().GetFirstNodeInGroup("arena") is WorldZoneManager zone)
                {
                    GlobalPosition = zone.SpawnPoint;
                    Revive(0.5f);
                }
            }
            return;
        }

        foreach (var key in new List<string>(_cd.Keys))
        {
            _cd[key] -= dt;
            if (_cd[key] <= 0f) _cd.Remove(key);
        }
        if (_iFrameLeft > 0f) _iFrameLeft -= dt;
        if (_castLock > 0f) _castLock -= dt;
        if (_adrenalineTime > 0f) _adrenalineTime -= dt;

        if (_adrenalineTime > 0f)
            Resource = MaxResource;
        else
            Resource = Mathf.Min(MaxResource, Resource + GameState.Class.ResourceRegen * dt);

        if (_sheet != null) _defense.Tick(dt, _sheet); // ES recharge po 3 s + life regen

        if (_dashTimeLeft > 0f)
        {
            _dashTimeLeft -= dt;
            Velocity = _dashDir * DashSpeed;
            MoveAndSlide();
        }
        else
        {
            float speed = Speed;
            speed *= GameData.God(GameState.PledgedGod)?.MoveSpeedMult ?? 1f;
            if (_adrenalineTime > 0f) speed *= 1.4f;

            // feel ruchu: przyspieszenie/hamowanie zamiast natychmiastowej prędkości
            Vector2 target = ReadMoveInput() * speed;
            float accel = target == Vector2.Zero ? 2600f : 1900f;
            Velocity = Velocity.MoveToward(target, accel * dt);
            MoveAndSlide();

            _animator?.Play(Velocity.LengthSquared() > 100f ? "walk" : "idle");
        }
    }
}

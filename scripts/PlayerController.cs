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
    public float MaxHealth => _sheet?.MaxLife ?? 100f;
    public float Health { get; private set; }
    private bool _dead;
    public bool Dead => _dead;

    public float MaxResource => GameState.Class.ResourceMax;
    public float Resource { get; private set; }

    private readonly Dictionary<string, float> _cd = new();
    public float CooldownLeft(string skillId) => _cd.GetValueOrDefault(skillId);

    private float _adrenalineTime;
    private float _adrenalineDmgBonus;
    public bool AdrenalineActive => _adrenalineTime > 0f;

    private float _dashTimeLeft, _iFrameLeft;
    private Vector2 _dashDir;
    public bool IsInvulnerable => _iFrameLeft > 0f;

    // puppet: cel interpolacji pozycji z sieci
    private Vector2 _netPos;

    public override void _Ready()
    {
        _netPos = GlobalPosition;

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
        Health = MaxHealth;
        Resource = MaxResource;
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
            Net.NotifyPlayerDied();
        }
    }

    private void PayHealth(float amount) => Health = Mathf.Max(1f, Health - amount);

    // ── budowa skilla (baza/bóg → drzewko → uniki → pasywki) ──

    public ResolvedSkill BuildSkill(string skillId)
    {
        GodId god = GameState.GodSkills.Contains(skillId) ? GameState.PledgedGod : GodId.None;
        var s = RangerKit.Get(skillId, god);
        GameState.Trees.ApplyTo(skillId, s);
        s.CasterPeer = Net.MyId;

        if (GameState.HasUniqueEffect(UniqueEffect.Overcharge)) s.CostMult *= 1.2f;
        if (skillId == "dash" && GameState.HasUniqueEffect(UniqueEffect.SwiftDash)) s.CdMult *= 0.6f;

        if (s.Damage > 0f && skillId != "adrenaline")
        {
            if (GameState.PledgedGod == GodId.Blood) s.Damage *= Gods.BloodDamageBonus;
            if (_adrenalineTime > 0f && _adrenalineDmgBonus > 0f) s.Damage *= 1f + _adrenalineDmgBonus;
        }
        return s;
    }

    private ResolvedSkill Offense(ResolvedSkill s)
    {
        if (_sheet != null)
        {
            s.Damage *= _sheet.AttackDamageMultiplier;
            if (GD.Randf() < _sheet.CritChance) s.Damage *= _sheet.CritMultiplier;
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

        if (GameState.PledgedGod == GodId.Blood)
        {
            float missing = cost - Resource;
            Resource = 0f;
            PayHealth(missing * Gods.BloodHpPerConcentration);
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
            switch (k.PhysicalKeycode)
            {
                case Key.Q: CastSlot(2); break;
                case Key.E: CastSlot(3); break;
                case Key.R: CastSlot(4); break;
            }
        }
    }

    private void CastSlot(int slot)
    {
        string skillId = GameState.Loadout.Slots[slot];
        if (skillId == null) return;

        var info = GameState.Class.Skill(skillId);
        if (info == null) return;
        if (_cd.GetValueOrDefault(skillId) > 0f) return;
        if (skillId == "dash" && _dashTimeLeft > 0f) return;

        var s = BuildSkill(skillId);
        if (!TryPay(s)) return;

        Execute(skillId, s);

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
        int count = RangerKit.SpreadCount(s);
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
            var trail = RangerKit.Get("hedge", GodId.None);
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
        if (Input.IsPhysicalKeyPressed(Key.W)) v.Y -= 1f;
        if (Input.IsPhysicalKeyPressed(Key.S)) v.Y += 1f;
        if (Input.IsPhysicalKeyPressed(Key.A)) v.X -= 1f;
        if (Input.IsPhysicalKeyPressed(Key.D)) v.X += 1f;
        return v == Vector2.Zero ? Vector2.Zero : v.Normalized();
    }

    // ── sync pozycji ──

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void NetState(Vector2 pos)
    {
        _netPos = pos;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (!IsMultiplayerAuthority())
        {
            GlobalPosition = GlobalPosition.Lerp(_netPos, Mathf.Min(1f, 14f * dt));
            return;
        }

        if (_dead) { Velocity = Vector2.Zero; return; }

        foreach (var key in new List<string>(_cd.Keys))
        {
            _cd[key] -= dt;
            if (_cd[key] <= 0f) _cd.Remove(key);
        }
        if (_iFrameLeft > 0f) _iFrameLeft -= dt;
        if (_adrenalineTime > 0f) _adrenalineTime -= dt;

        if (_adrenalineTime > 0f)
            Resource = MaxResource;
        else
            Resource = Mathf.Min(MaxResource, Resource + GameState.Class.ResourceRegen * dt);

        if (_dashTimeLeft > 0f)
        {
            _dashTimeLeft -= dt;
            Velocity = _dashDir * DashSpeed;
            MoveAndSlide();
        }
        else
        {
            float speed = Speed;
            if (GameState.PledgedGod == GodId.Wilds) speed *= Gods.WildsMoveSpeedBonus;
            if (_adrenalineTime > 0f) speed *= 1.4f;

            Velocity = ReadMoveInput() * speed;
            MoveAndSlide();
        }

        if (Net.Online && Engine.GetPhysicsFrames() % 3 == 0)
            Rpc(MethodName.NetState, GlobalPosition);
    }
}

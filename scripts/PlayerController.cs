using System.Collections.Generic;
using Godot;
using AshenPantheon.Core;

public partial class PlayerController : CharacterBody2D
{
    [Export] public float Speed = 240f;
    [Export] public float DashSpeed = 780f;
    [Export] public float DashDuration = 0.15f;
    [Export] public float IFrameDuration = 0.2f;
    /// <summary>W hubie skille są wyłączone (E/klawisze służą interakcji).</summary>
    [Export] public bool CombatEnabled = true;

    private CharacterSheet _sheet;
    public CharacterSheet Sheet => _sheet;
    public float MaxHealth => _sheet?.MaxLife ?? 100f;
    public float Health { get; private set; }
    private bool _dead;

    public float MaxResource => GameState.Class.ResourceMax;
    public float Resource { get; private set; }

    // Cooldowny per skill id
    private readonly Dictionary<string, float> _cd = new();
    public float CooldownLeft(string skillId) => _cd.GetValueOrDefault(skillId);

    private float _adrenalineTime;
    private float _adrenalineDmgBonus;
    public bool AdrenalineActive => _adrenalineTime > 0f;

    private float _dashTimeLeft, _iFrameLeft;
    private Vector2 _dashDir;
    public bool IsInvulnerable => _iFrameLeft > 0f;

    private PackedScene _projectileScene;

    public override void _Ready()
    {
        _projectileScene = GD.Load<PackedScene>("res://scenes/Projectile.tscn");
        GameState.LoadOrInit();
        RecomputeSheet();
        Health = MaxHealth;
        Resource = MaxResource;
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

    public void PickUp(Item item)
    {
        if (!GameState.Bag.TryAutoPlace(item))
            GD.Print("Plecak pełny!");
        else
            GameState.Save();
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
            if (GetTree().GetFirstNodeInGroup("arena") is ArenaManager arena)
                arena.OnPlayerDied();
        }
    }

    /// <summary>Bezpośrednia utrata HP (koszty krwi Vharosa) — bez mitygacji, nie zabija poniżej 1 HP.</summary>
    private void PayHealth(float amount)
    {
        Health = Mathf.Max(1f, Health - amount);
    }

    // ── Budowa skilla: baza/bóg → drzewko → uniki → pasywki ──

    public ResolvedSkill BuildSkill(string skillId)
    {
        GodId god = GameState.GodSkills.Contains(skillId) ? GameState.PledgedGod : GodId.None;
        var s = RangerKit.Get(skillId, god);
        GameState.Trees.ApplyTo(skillId, s);

        if (GameState.HasUniqueEffect(UniqueEffect.Overcharge)) s.CostMult *= 1.2f;
        if (skillId == "dash" && GameState.HasUniqueEffect(UniqueEffect.SwiftDash)) s.CdMult *= 0.6f;

        if (s.Damage > 0f && skillId != "adrenaline")
        {
            if (GameState.PledgedGod == GodId.Blood) s.Damage *= Gods.BloodDamageBonus;
            if (_adrenalineTime > 0f && _adrenalineDmgBonus > 0f) s.Damage *= 1f + _adrenalineDmgBonus;
        }
        return s;
    }

    /// <summary>Ofensywa z arkusza: atk%, krytyk, unik MarkOnHit.</summary>
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

        // Vharos: brakującą koncentrację płacisz zdrowiem
        if (GameState.PledgedGod == GodId.Blood)
        {
            float missing = cost - Resource;
            Resource = 0f;
            PayHealth(missing * Gods.BloodHpPerConcentration);
            return true;
        }
        return false;
    }

    // ── Input: sloty loadoutu ──

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_dead || !CombatEnabled) return;

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
            case "basic": CastProjectile(Offense(s), new Color(0.95f, 0.95f, 0.85f)); break;
            case "spread": CastSpread(s); break;
            case "exec": CastProjectile(Offense(s), new Color(1f, 0.85f, 0.3f)); break;
            case "rain": CastRain(s); break;
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

    private void CastProjectile(ResolvedSkill s, Color tint, Vector2? dirOverride = null)
    {
        Vector2 dir = dirOverride ?? AimDirection();
        var proj = _projectileScene.Instantiate<Projectile>();
        proj.Setup(s, dir, tint);
        GetParent().AddChild(proj);
        proj.GlobalPosition = GlobalPosition + dir * 20f;

        // dodatkowe pociski z drzewka (basic_twin itd.) — lekki rozrzut
        for (int i = 0; i < s.ExtraProjectiles && s.Id != "spread"; i++)
        {
            var extra = _projectileScene.Instantiate<Projectile>();
            extra.Setup(s, dir.Rotated(Mathf.DegToRad(6f * (i + 1))), tint);
            GetParent().AddChild(extra);
            extra.GlobalPosition = GlobalPosition + dir * 20f;
        }
    }

    private void CastSpread(ResolvedSkill s)
    {
        Offense(s);
        int count = RangerKit.SpreadCount(s);
        float baseAngle = AimDirection().Angle();
        float spreadRad = Mathf.DegToRad(12f);
        float start = -spreadRad * (count - 1) / 2f;
        for (int i = 0; i < count; i++)
            CastProjectileRaw(s, Vector2.Right.Rotated(baseAngle + start + spreadRad * i), new Color(0.6f, 0.95f, 0.5f));
    }

    private void CastProjectileRaw(ResolvedSkill s, Vector2 dir, Color tint)
    {
        var proj = _projectileScene.Instantiate<Projectile>();
        proj.Setup(s, dir, tint);
        GetParent().AddChild(proj);
        proj.GlobalPosition = GlobalPosition + dir * 20f;
    }

    private void CastRain(ResolvedSkill s)
    {
        var zone = new GroundZone();
        zone.Setup(Offense(s), 120f * s.AoeMult);
        GetParent().AddChild(zone);
        zone.GlobalPosition = GetGlobalMousePosition();
    }

    private void CastMine(ResolvedSkill s)
    {
        Offense(s);
        int mines = 1 + s.ExtraProjectiles;
        for (int i = 0; i < mines; i++)
        {
            var mine = new Mine();
            mine.Setup(s);
            GetParent().AddChild(mine);
            mine.GlobalPosition = GlobalPosition + (i == 0 ? Vector2.Zero : new Vector2(46f * i, 0f));
        }
    }

    private void CastHedge(ResolvedSkill s)
    {
        Offense(s);
        if (s.VariantTag == "hedge_bomb")
        {
            // Dzikie Ostępy: latająca bomba kolców — eksploduje trucizną, bez CD
            s.Explodes = true;
            CastProjectileRaw(s, AimDirection(), new Color(0.5f, 0.9f, 0.4f));
            return;
        }
        var hedge = new HedgeZone();
        GetParent().AddChild(hedge);
        hedge.GlobalPosition = GlobalPosition;
        hedge.Setup(s, AimDirection(), 340f * s.AoeMult);
    }

    private void CastDash(ResolvedSkill s)
    {
        Vector2 dir = ReadMoveInput();
        if (dir == Vector2.Zero) dir = AimDirection();
        _dashDir = dir;
        _dashTimeLeft = DashDuration * s.AoeMult; // dash_far wydłuża sus
        _iFrameLeft = IFrameDuration * s.DurationMult;

        if (s.VariantTag == "dash_trail")
        {
            // Dzikie Ostępy: kolczasty ślad za dashem
            var trail = RangerKit.Get("hedge", GodId.None);
            trail.Damage = 6f;
            var hedge = new HedgeZone();
            GetParent().AddChild(hedge);
            hedge.GlobalPosition = GlobalPosition;
            hedge.Setup(trail, dir, DashSpeed * DashDuration * s.AoeMult);
        }
    }

    private void CastAdrenaline(ResolvedSkill s)
    {
        _adrenalineTime = 5f * s.DurationMult;
        _adrenalineDmgBonus = s.Damage; // węzeł adr_power zapisuje bonus w Damage
        if (s.VariantTag == "adrenaline_blood") Heal(30f);
    }

    private void CastHawk(ResolvedSkill s)
    {
        Offense(s);
        if (s.VariantTag == "hawk_pets")
        {
            // Dzikie Ostępy: 3 WIELKIE jastrzębie-pety walczące u boku
            for (int i = 0; i < 3; i++)
            {
                var pet = new Pet();
                pet.Damage = s.Damage * 0.45f;
                GetParent().AddChild(pet);
                pet.GlobalPosition = GlobalPosition + Vector2.Right.Rotated(Mathf.Tau * i / 3f) * 40f;
            }
            return;
        }
        var hawk = new Hawk();
        hawk.Setup(s, s.VariantTag == "hawk_all");
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

        foreach (var key in new List<string>(_cd.Keys))
        {
            _cd[key] -= dt;
            if (_cd[key] <= 0f) _cd.Remove(key);
        }
        if (_iFrameLeft > 0f) _iFrameLeft -= dt;
        if (_adrenalineTime > 0f) _adrenalineTime -= dt;

        if (_adrenalineTime > 0f)
            Resource = MaxResource; // nielimitowana koncentracja
        else
            Resource = Mathf.Min(MaxResource, Resource + GameState.Class.ResourceRegen * dt);

        if (_dashTimeLeft > 0f)
        {
            _dashTimeLeft -= dt;
            Velocity = _dashDir * DashSpeed;
            MoveAndSlide();
            return;
        }

        float speed = Speed;
        if (GameState.PledgedGod == GodId.Wilds) speed *= Gods.WildsMoveSpeedBonus;
        if (_adrenalineTime > 0f) speed *= 1.4f;

        Velocity = ReadMoveInput() * speed;
        MoveAndSlide();
    }
}

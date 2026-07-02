using System.Collections.Generic;
using Godot;
using AshenPantheon.Core;

/// <summary>Generyczny potwór data-driven: odgrywa MonsterDefinition z bestiariusza
/// (ruch, rotacja ability z cooldownami, fazy bossów). Nowy potwór = nowy JSON, zero kodu.</summary>
public partial class Monster : EnemyBase
{
    public string MonsterId = "husk";

    private MonsterDefinition _def;
    private float _atkTimer;
    private int _rotation;
    private readonly Dictionary<AbilityDefinition, float> _lastUse = new();

    // melee windup
    private AbilityDefinition _pendingMelee;
    private float _windupLeft;

    protected override Color BaseTint => Color.FromString(_def?.Tint ?? "#d94d4d", new Color(0.85f, 0.3f, 0.3f));
    public override string ReplicationId => MonsterId;
    protected override string LootTableId => _def?.LootTable ?? "common";

    public static Monster Create(string monsterId)
    {
        var scene = GD.Load<PackedScene>("res://scenes/Monster.tscn");
        var m = scene.Instantiate<Monster>();
        m.MonsterId = monsterId;
        return m;
    }

    public override void _Ready()
    {
        _def = Bestiary.Monster(MonsterId);
        MaxHealth = _def.Hp;
        XpReward = (long)(_def.Xp * XpMult);
        ContactRange = 0f; // obrażenia wyłącznie przez ability (dane)
        HpBarWidth = 40f * _def.Scale;
        HpBarY = -34f * _def.Scale;

        base._Ready();

        // obrona z danych bestiariusza — typy obrażeń gracza mają znaczenie
        Combatant.Armour = _def.Armour;
        Combatant.EvadeChance = _def.EvadeChance;
        Combatant.ResFire = _def.ResFire;
        Combatant.ResCold = _def.ResCold;
        Combatant.ResLightning = _def.ResLightning;
        Combatant.ResChaos = _def.ResChaos;

        var sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (sprite != null) sprite.Scale = new Vector2(0.22f, 0.22f) * _def.Scale;
        var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (shape?.Shape is CircleShape2D circle) circle.Radius = 16f * _def.Scale;
        Animator?.Rebuild(0.22f * _def.Scale); // animacje w skali potwora (boss nie jest ściskany)

        _atkTimer = _def.AttackInterval;
        UpdateTint();
    }

    protected override void Behavior(float dt, Vector2 toPlayer, float dist)
    {
        float slow = IsChilled ? 0.5f : 1f;

        // dokończ windup melee (atak do uniknięcia — gracz może odejść)
        if (_pendingMelee != null)
        {
            Velocity = Vector2.Zero;
            _windupLeft -= dt;
            if (_windupLeft <= 0f)
            {
                Animator?.Play("attack");
                if (dist <= _pendingMelee.Reach && CurrentTarget != null)
                    Net.DamagePlayer(CurrentTarget, _pendingMelee.Damage * DmgMult, System.Enum.TryParse<DamageType>(_pendingMelee.DamageType, true, out var mt) ? mt : DamageType.Physical);
                _pendingMelee = null;
            }
            return;
        }

        // ruch wg definicji
        var (abilities, interval) = _def.ActiveSet(HpFrac);
        bool wantMelee = abilities.Exists(a => a.Type == "melee");
        float stopAt = _def.Movement == "keep_distance" ? _def.PreferredRange
            : wantMelee ? 44f : 30f;

        if (dist > stopAt + 15f)
        {
            Velocity = toPlayer.Normalized() * _def.Speed * slow;
            MoveAndSlide();
            Animator?.Play("walk");
        }
        else
        {
            Velocity = Vector2.Zero;
            Animator?.Play("idle");
        }

        // rotacja ability
        _atkTimer -= dt * slow;
        if (_atkTimer > 0f || abilities.Count == 0) return;
        _atkTimer = interval;

        float now = Time.GetTicksMsec() / 1000f;
        for (int tryN = 0; tryN < abilities.Count; tryN++)
        {
            var ability = abilities[_rotation % abilities.Count];
            _rotation++;
            if (ability.Cooldown > 0f && _lastUse.TryGetValue(ability, out float last) && now - last < ability.Cooldown)
                continue;
            _lastUse[ability] = now;
            ExecuteAbility(ability, toPlayer, dist);
            return;
        }
    }

    private void ExecuteAbility(AbilityDefinition a, Vector2 toPlayer, float dist)
    {
        float dmg = a.Damage * DmgMult;
        var dmgType = System.Enum.TryParse<DamageType>(a.DamageType, true, out var t) ? t : DamageType.Physical;
        switch (a.Type)
        {
            case "melee":
                if (dist > a.Reach * 1.4f) { _atkTimer = 0.1f; return; } // za daleko — podejdź
                _pendingMelee = a;
                _windupLeft = a.Windup;
                Animator?.Play("windup");
                break;

            case "projectile":
                Animator?.Play("windup");
                Net.SpawnEnemyProjectile(GlobalPosition + toPlayer.Normalized() * 20f,
                    toPlayer.Normalized(), a.Speed, dmg, dmgType);
                break;

            case "tele_circle":
                Net.SpawnTelegraph((int)TelegraphShape.Circle, a.Radius, 0f, 0f, dmg,
                    CurrentTarget?.GlobalPosition ?? GlobalPosition, 0f, dmgType);
                break;

            case "tele_cone":
                Net.SpawnTelegraph((int)TelegraphShape.Cone, a.Radius, a.HalfAngleDeg, 0f, dmg,
                    GlobalPosition, toPlayer.Angle(), dmgType);
                break;

            case "tele_line":
                Net.SpawnTelegraph((int)TelegraphShape.Line, a.Length, 0f, a.HalfWidth, dmg,
                    GlobalPosition, toPlayer.Angle(), dmgType);
                break;

            case "summon":
                for (int i = 0; i < a.Count; i++)
                {
                    var minion = Create(a.MonsterId);
                    minion.HpMult = HpMult * 0.8f;
                    minion.DmgMult = DmgMult;
                    minion.Position = GlobalPosition + Vector2.Right.Rotated(GD.Randf() * Mathf.Tau) * 70f;
                    GetParent().AddChild(minion);
                }
                break;
        }
    }
}

using Godot;
using AshenPantheon.Core;

/// <summary>Baza wrogów. Multiplayer: prawdziwa logika (AI/HP/DoT) żyje TYLKO u hosta;
/// na klientach wróg jest puppetem (pozycja/HP/status z sieci). Loot rolowany per-gracz.</summary>
public abstract partial class EnemyBase : CharacterBody2D, IHittable
{
    [Export] public float MaxHealth = 60f;
    [Export] public float ContactDamage = 12f;
    [Export] public float ContactCooldown = 0.8f;
    [Export] public float ContactRange = 42f;
    [Export] public float HpBarWidth = 40f;
    [Export] public float HpBarY = -34f;

    public float HpMult = 1f;
    public float DmgMult = 1f;
    public long XpReward = 12;
    public float LootChance = 0.45f;

    public long NetId;
    public bool Puppet;

    protected Combatant Combatant;
    protected Sprite2D Sprite;
    protected EnemyAnimator Animator;
    protected PlayerController CurrentTarget;

    private float _contactCd;
    private bool _dying;
    private Vector2 _netPos;
    private bool _netMoving;

    protected bool IsChilled => Combatant.IsChilled;
    public bool IsMarked => Combatant != null && Combatant.IsMarked;
    public float HpFrac => Combatant == null ? 1f : Mathf.Clamp(Combatant.Health / Combatant.MaxHealth, 0f, 1f);
    public StatusType ActiveStatus => Combatant?.ActiveStatus ?? StatusType.None;
    public bool Moving => Velocity.LengthSquared() > 1f || _netMoving;
    protected abstract Color BaseTint { get; }

    private static readonly LootGenerator Loot = new();

    public override void _Ready()
    {
        Combatant = new Combatant { MaxHealth = MaxHealth * HpMult, Health = MaxHealth * HpMult };
        ContactDamage *= DmgMult;
        Sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        Animator = GetNodeOrNull<EnemyAnimator>("Animator");
        AddToGroup("hittable");
        AddToGroup("enemies");
        _netPos = GlobalPosition;
        UpdateTint();
        QueueRedraw();

        if (!Puppet && Net.Online) Net.RegisterEnemy(this);
    }

    /// <summary>Najbliższy żywy gracz (co-op: wrogowie wybierają cel spośród wszystkich).</summary>
    protected PlayerController NearestPlayer()
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

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_dying) { Velocity = Vector2.Zero; return; }

        if (Puppet)
        {
            GlobalPosition = GlobalPosition.Lerp(_netPos, Mathf.Min(1f, 14f * dt));
            Animator?.Play(_netMoving ? "walk" : "idle");
            return;
        }

        if (_contactCd > 0f) _contactCd -= dt;

        if (Combatant.StatusTimeLeft > 0f)
        {
            Combatant.StatusTimeLeft -= dt;
            float dps = Combatant.ActiveStatus switch
            {
                StatusType.Burn => 8f, StatusType.Poison => 6f, StatusType.Bleed => 7f, _ => 0f
            };
            if (dps > 0f) Combatant.Health -= dps * dt;
            if (Combatant.StatusTimeLeft <= 0f) { Combatant.ActiveStatus = StatusType.None; UpdateTint(); }
            QueueRedraw();
            if (Combatant.IsDead) { Die(); return; }
        }

        if (Combatant.MarkTimeLeft > 0f)
        {
            Combatant.MarkTimeLeft -= dt;
            if (Combatant.MarkTimeLeft <= 0f) Combatant.Marked = false;
            QueueRedraw();
        }

        if (Combatant.IsStunned)
        {
            Combatant.StunTimeLeft -= dt;
            Velocity = Vector2.Zero;
            Animator?.Play("idle");
            if (Net.Online && Engine.GetPhysicsFrames() % 6 == 0) Net.SyncEnemy(this);
            return;
        }

        CurrentTarget = NearestPlayer();
        if (CurrentTarget != null)
        {
            Vector2 toPlayer = CurrentTarget.GlobalPosition - GlobalPosition;
            Behavior(dt, toPlayer, toPlayer.Length());

            if (ContactRange > 0f && toPlayer.Length() <= ContactRange && _contactCd <= 0f)
            {
                _contactCd = ContactCooldown;
                Net.DamagePlayer(CurrentTarget, ContactDamage);
            }
        }

        if (Net.Online && Engine.GetPhysicsFrames() % 6 == 0) Net.SyncEnemy(this);
    }

    protected abstract void Behavior(float dt, Vector2 toPlayer, float dist);

    public static System.Collections.Generic.IEnumerable<EnemyBase> All(SceneTree tree)
    {
        foreach (Node n in tree.GetNodesInGroup("enemies"))
            if (n is EnemyBase e && !e._dying) yield return e;
    }

    public void ReceiveHit(ResolvedSkill skill)
    {
        if (_dying) return;
        if (Puppet)
        {
            // klient: tylko wizualny feedback — HP/statusy przyjdą z serwera
            Animator?.Flash("hit");
            return;
        }

        CombatResolver.ApplyHit(skill, Combatant);
        if (skill.HealOnHit > 0f)
            Net.HealCaster(skill.CasterPeer, skill.HealOnHit);
        UpdateTint();
        QueueRedraw();
        if (Combatant.IsDead) Die();
        else Animator?.Flash("hit");
    }

    /// <summary>Puppet: stan z serwera.</summary>
    public void ApplyNetState(Vector2 pos, float hpFrac, int status, bool marked, bool moving)
    {
        _netPos = pos;
        _netMoving = moving;
        Combatant.Health = hpFrac * Combatant.MaxHealth;
        var st = (StatusType)status;
        if (st != Combatant.ActiveStatus)
        {
            Combatant.ActiveStatus = st;
            UpdateTint();
        }
        Combatant.StatusTimeLeft = st == StatusType.None ? 0f : 1f;
        Combatant.Marked = marked;
        Combatant.MarkTimeLeft = marked ? 1f : 0f;
        QueueRedraw();
    }

    protected virtual bool DropsLoot => true;

    protected void Die()
    {
        if (_dying) return;
        _dying = true;
        RemoveFromGroup("enemies");
        RemoveFromGroup("hittable");

        Net.GrantXpAll(XpReward);

        if (DropsLoot)
            foreach (int peer in Net.AllPeers())
                if (GD.Randf() < LootChance)
                    Net.GivePickup(peer, Loot.Generate(), GlobalPosition);

        if (Net.Online) Net.DespawnEnemy(this, died: true);
        PlayDeathAndFree();
    }

    /// <summary>Puppet: śmierć zreplikowana z serwera.</summary>
    public void RemoteDie()
    {
        if (_dying) return;
        _dying = true;
        RemoveFromGroup("enemies");
        RemoveFromGroup("hittable");
        PlayDeathAndFree();
    }

    private void PlayDeathAndFree()
    {
        var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (shape != null) shape.SetDeferred("disabled", true);
        if (Animator != null) Animator.PlayDeath(() => QueueFree());
        else QueueFree();
    }

    protected void UpdateTint()
    {
        if (Sprite == null) return;
        Sprite.Modulate = Combatant.ActiveStatus switch
        {
            StatusType.Burn => new Color(1f, 0.4f, 0.2f),
            StatusType.Chill => new Color(0.4f, 0.7f, 1f),
            StatusType.Poison => new Color(0.5f, 0.9f, 0.35f),
            StatusType.Bleed => new Color(0.85f, 0.15f, 0.25f),
            _ => BaseTint
        };
    }

    public override void _Draw()
    {
        if (Combatant == null) return;
        float frac = HpFrac;
        var size = new Vector2(HpBarWidth, 6f);
        var pos = new Vector2(-HpBarWidth / 2f, HpBarY);
        DrawRect(new Rect2(pos, size), new Color(0f, 0f, 0f, 0.7f));
        DrawRect(new Rect2(pos, new Vector2(size.X * frac, size.Y)), new Color(0.9f, 0.2f, 0.2f));

        if (Combatant.IsMarked)
        {
            float y = HpBarY - 8f;
            var tri = new Vector2[] { new(-6f, y), new(6f, y), new(0f, y + 8f) };
            DrawColoredPolygon(tri, new Color(1f, 0.85f, 0.2f));
        }
    }
}

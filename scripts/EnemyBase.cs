using Godot;
using AshenPantheon.Core;

/// <summary>Wspólna baza wrogów: HP, statusy (DoT-y), Mark, Stun, pasek HP, kontaktowe obrażenia, XP i loot.
/// Podklasy implementują Behavior(). Szablon pod kolejne potwory.</summary>
public abstract partial class EnemyBase : CharacterBody2D, IHittable
{
    [Export] public float MaxHealth = 60f;
    [Export] public float ContactDamage = 12f;
    [Export] public float ContactCooldown = 0.8f;
    [Export] public float ContactRange = 42f;
    [Export] public float HpBarWidth = 40f;
    [Export] public float HpBarY = -34f;

    /// <summary>Skalowanie z planu pokoju (proceduralne runy).</summary>
    public float HpMult = 1f;
    public float DmgMult = 1f;
    public long XpReward = 12;
    public float LootChance = 0.45f;

    protected Combatant Combatant;
    protected PlayerController Player;
    protected Sprite2D Sprite;
    protected EnemyAnimator Animator;

    private float _contactCd;
    private bool _dying;

    protected bool IsChilled => Combatant.IsChilled;
    public bool IsMarked => Combatant != null && Combatant.IsMarked;
    protected abstract Color BaseTint { get; }

    private static readonly LootGenerator Loot = new();

    public override void _Ready()
    {
        Combatant = new Combatant { MaxHealth = MaxHealth * HpMult, Health = MaxHealth * HpMult };
        ContactDamage *= DmgMult;
        Player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        Sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        Animator = GetNodeOrNull<EnemyAnimator>("Animator");
        AddToGroup("hittable");
        AddToGroup("enemies");
        UpdateTint();
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dying) { Velocity = Vector2.Zero; return; }

        float dt = (float)delta;
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

        if (Player == null || !IsInstanceValid(Player)) return;

        if (Combatant.IsStunned)
        {
            Combatant.StunTimeLeft -= dt;
            Velocity = Vector2.Zero;
            Animator?.Play("idle");
            return;
        }

        Vector2 toPlayer = Player.GlobalPosition - GlobalPosition;
        Behavior(dt, toPlayer, toPlayer.Length());

        // kontaktowe obrażenia — wspólne (podklasa może nadpisać przez ContactRange=0)
        if (ContactRange > 0f && toPlayer.Length() <= ContactRange && _contactCd <= 0f)
        {
            _contactCd = ContactCooldown;
            Player.TakeDamage(ContactDamage);
        }
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
        CombatResolver.ApplyHit(skill, Combatant);
        if (skill.HealOnHit > 0f && Player != null && IsInstanceValid(Player))
            Player.Heal(skill.HealOnHit);
        UpdateTint();
        QueueRedraw();
        if (Combatant.IsDead) Die();
        else Animator?.Flash("hit");
    }

    protected virtual bool DropsLoot => true;

    protected void Die()
    {
        if (_dying) return;
        _dying = true;
        RemoveFromGroup("enemies");
        RemoveFromGroup("hittable");
        GameState.Progress.GainXp(XpReward);

        if (DropsLoot && GD.Randf() < LootChance)
            ItemPickup.Spawn(GetParent(), GlobalPosition, Loot.Generate());

        if (Animator != null)
        {
            var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
            if (shape != null) shape.SetDeferred("disabled", true);
            Animator.PlayDeath(() => QueueFree());
        }
        else
        {
            QueueFree();
        }
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
        float frac = Mathf.Clamp(Combatant.Health / Combatant.MaxHealth, 0f, 1f);
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

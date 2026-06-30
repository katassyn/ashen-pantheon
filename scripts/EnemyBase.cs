using Godot;
using AshenPantheon.Core;

/// <summary>Wspólna baza wrogów: HP, statusy, pasek HP, trafialność skillami, solidna kolizja i obrażenia kontaktowe.
/// Podklasy implementują tylko swoje zachowanie (ruch/atak) w Behavior().</summary>
public abstract partial class EnemyBase : CharacterBody2D, IHittable
{
    [Export] public float MaxHealth = 60f;
    [Export] public float ContactDamage = 12f;
    [Export] public float ContactCooldown = 0.8f;
    [Export] public float ContactRange = 42f;
    [Export] public float HpBarWidth = 40f;
    [Export] public float HpBarY = -34f;

    protected Combatant Combatant;
    protected PlayerController Player;
    protected Sprite2D Sprite;

    private float _contactCd;

    protected bool IsChilled => Combatant.IsChilled;
    protected abstract Color BaseTint { get; }

    public override void _Ready()
    {
        Combatant = new Combatant { MaxHealth = MaxHealth, Health = MaxHealth };
        Player = GetTree().GetFirstNodeInGroup("player") as PlayerController;
        Sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        AddToGroup("hittable");
        AddToGroup("enemies");
        UpdateTint();
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_contactCd > 0f) _contactCd -= dt;

        if (Combatant.StatusTimeLeft > 0f)
        {
            Combatant.StatusTimeLeft -= dt;
            if (Combatant.ActiveStatus == StatusType.Burn) Combatant.Health -= 8f * dt;
            if (Combatant.StatusTimeLeft <= 0f) { Combatant.ActiveStatus = StatusType.None; UpdateTint(); }
            QueueRedraw();
            if (Combatant.IsDead) { QueueFree(); return; }
        }

        if (Player == null || !IsInstanceValid(Player)) return;

        Vector2 toPlayer = Player.GlobalPosition - GlobalPosition;
        Behavior(dt, toPlayer, toPlayer.Length());

        // Obrażenia kontaktowe — wspólne dla każdego wroga (też boss)
        if (toPlayer.Length() <= ContactRange && _contactCd <= 0f)
        {
            _contactCd = ContactCooldown;
            Player.TakeDamage(ContactDamage);
        }
    }

    /// <summary>Ruch/atak danego typu wroga. Wywoływane co klatkę fizyki.</summary>
    protected abstract void Behavior(float dt, Vector2 toPlayer, float dist);

    public void ReceiveHit(ResolvedSkill skill)
    {
        CombatResolver.ApplyHit(skill, Combatant);
        UpdateTint();
        QueueRedraw();
        if (Combatant.IsDead) QueueFree();
    }

    protected void UpdateTint()
    {
        if (Sprite == null) return;
        Sprite.Modulate = Combatant.ActiveStatus switch
        {
            StatusType.Burn => new Color(1f, 0.4f, 0.2f),
            StatusType.Chill => new Color(0.4f, 0.7f, 1f),
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
    }
}

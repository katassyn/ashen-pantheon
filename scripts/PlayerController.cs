using Godot;
using AshenPantheon.Core;

public partial class PlayerController : CharacterBody2D
{
    [Export] public float Speed = 240f;

    private CharacterSheet _sheet;
    public CharacterSheet Sheet => _sheet;
    public float MaxHealth => _sheet?.MaxLife ?? 100f;
    public float Health { get; private set; }
    private bool _dead;

    [Export] public float MaxConcentration = 100f;
    [Export] public float ConcentrationRegen = 30f; // /s — mała pula, szybki regen
    public float Concentration { get; private set; }

    private bool _godActive;
    public bool GodActive => _godActive;
    public string GodName => RangerKit.GodName;

    private PackedScene _projectileScene;

    // i-frames zostają jako hook (adrenalina/dash-skill później); teraz brak
    public bool IsInvulnerable => false;

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

    /// <summary>Podniesienie itemu: leci do plecaka (zakładasz ręcznie w panelu I).</summary>
    public void PickUp(Item item)
    {
        GameState.Inventory.Add(item);
    }

    /// <summary>Wołane przez panel po zmianie ekwipunku — przelicza postać.</summary>
    public void Refresh() => RecomputeSheet();

    public void TakeDamage(float amount)
    {
        if (_dead || IsInvulnerable) return;
        Health -= amount;
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
            if (k.PhysicalKeycode == Key.Q) CastExecutor();
            else if (k.PhysicalKeycode == Key.G) _godActive = !_godActive;
        }
    }

    private Vector2 AimDirection()
    {
        Vector2 dir = GetGlobalMousePosition() - GlobalPosition;
        return dir == Vector2.Zero ? Vector2.Right : dir.Normalized();
    }

    private bool TrySpend(float cost)
    {
        if (Concentration < cost) return false;
        Concentration -= cost;
        return true;
    }

    private void SpawnProjectile(ResolvedSkill skill, Vector2 dir, Color tint)
    {
        var proj = _projectileScene.Instantiate<Projectile>();
        proj.Setup(skill, dir, tint);
        GetParent().AddChild(proj);
        proj.GlobalPosition = GlobalPosition + dir * 20f;
    }

    private void CastBasic()
    {
        var skill = RangerKit.BasicShot(_godActive);
        if (!TrySpend(skill.ConcentrationCost)) return;
        SpawnProjectile(skill, AimDirection(), new Color(0.95f, 0.95f, 0.85f));
    }

    private void CastSpread()
    {
        var skill = RangerKit.Spread(_godActive);
        if (!TrySpend(skill.ConcentrationCost)) return;

        int count = RangerKit.SpreadCount(_godActive);
        float baseAngle = AimDirection().Angle();
        float spread = Mathf.DegToRad(12f);
        float start = -spread * (count - 1) / 2f;
        for (int i = 0; i < count; i++)
        {
            var dir = Vector2.Right.Rotated(baseAngle + start + spread * i);
            SpawnProjectile(skill, dir, new Color(0.6f, 0.95f, 0.5f));
        }
    }

    private void CastExecutor()
    {
        var skill = RangerKit.Executor(_godActive);
        if (!TrySpend(skill.ConcentrationCost)) return;
        SpawnProjectile(skill, AimDirection(), new Color(1f, 0.85f, 0.3f));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dead) { Velocity = Vector2.Zero; return; }

        float dt = (float)delta;
        Concentration = Mathf.Min(MaxConcentration, Concentration + ConcentrationRegen * dt);

        float speed = Speed * (_godActive ? RangerKit.GodMoveSpeedBonus : 1f);
        Velocity = ReadMoveInput() * speed;
        MoveAndSlide();
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
}

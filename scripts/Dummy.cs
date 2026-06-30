using Godot;
using AshenPantheon.Core;

public partial class Dummy : Area2D
{
    private readonly Combatant _combatant = new() { MaxHealth = 200f, Health = 200f };

    public override void _Process(double delta)
    {
        // tykanie statusów (Burn DoT / wygasanie Chill)
        if (_combatant.StatusTimeLeft > 0f)
        {
            _combatant.StatusTimeLeft -= (float)delta;
            if (_combatant.ActiveStatus == StatusType.Burn)
                _combatant.Health -= 8f * (float)delta;
            if (_combatant.StatusTimeLeft <= 0f)
                _combatant.ActiveStatus = StatusType.None;
        }
    }

    /// <summary>Wywoływane przez gracza przy trafieniu.</summary>
    public void ReceiveHit(ResolvedSkill skill)
    {
        CombatResolver.ApplyHit(skill, _combatant);
        GD.Print($"Dummy HP: {_combatant.Health:0} | status: {_combatant.ActiveStatus} | chill?: {_combatant.IsChilled}");
        Modulate = _combatant.ActiveStatus switch
        {
            StatusType.Burn => new Color(1f, 0.4f, 0.2f),
            StatusType.Chill => new Color(0.4f, 0.7f, 1f),
            _ => Colors.White
        };
        if (_combatant.IsDead)
            QueueFree();
    }
}

namespace AshenPantheon.Core;

public sealed class Combatant
{
    public required float MaxHealth { get; init; }
    public float Health { get; set; }
    public StatusType ActiveStatus { get; set; } = StatusType.None;
    public float StatusTimeLeft { get; set; }
    /// <summary>Dps aktywnego DoT-a (ustawiany przez skill z danych).</summary>
    public float StatusDps { get; set; }
    /// <summary>Szansa uniku celu 0..1 (celność atakującego vs unik w CombatResolver).</summary>
    public float EvadeChance { get; set; }

    // Ranger: Oznaczenie
    public bool Marked { get; set; }
    public float MarkTimeLeft { get; set; }

    // Ogłuszenie
    public float StunTimeLeft { get; set; }

    public bool IsDead => Health <= 0f;
    public bool IsChilled => ActiveStatus == StatusType.Chill && StatusTimeLeft > 0f;
    public bool IsMarked => Marked && MarkTimeLeft > 0f;
    public bool IsStunned => StunTimeLeft > 0f;
}

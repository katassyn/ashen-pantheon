namespace AshenPantheon.Core;

public sealed class Combatant
{
    public required float MaxHealth { get; init; }
    public float Health { get; set; }
    public StatusType ActiveStatus { get; set; } = StatusType.None;
    public float StatusTimeLeft { get; set; }

    // Ranger: Oznaczenie
    public bool Marked { get; set; }
    public float MarkTimeLeft { get; set; }

    public bool IsDead => Health <= 0f;
    public bool IsChilled => ActiveStatus == StatusType.Chill && StatusTimeLeft > 0f;
    public bool IsMarked => Marked && MarkTimeLeft > 0f;
}

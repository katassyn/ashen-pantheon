namespace AshenPantheon.Core;

/// <summary>Bogowie panteonu. Gracz pledguje jednego; per-skill wybiera wersję bazową lub boga.</summary>
public enum GodId { None, Wilds, Blood }

public static class Gods
{
    public const float WildsMoveSpeedBonus = 1.15f;   // pasywka Dzikich Ostępów
    public const float BloodDamageBonus = 1.15f;      // pasywka Vharosa
    public const float BloodHpPerConcentration = 0.75f; // brakującą koncentrację płacisz HP (Vharos)

    public static string Name(GodId g) => g switch
    {
        GodId.Wilds => "Dzikie Ostępy",
        GodId.Blood => "Vharos, Bóg Krwi",
        _ => "—"
    };

    public static string Passive(GodId g) => g switch
    {
        GodId.Wilds => "+15% szybkości ruchu · oznaczenia trwają dłużej",
        GodId.Blood => "+15% obrażeń · brakującą koncentrację płacisz zdrowiem",
        _ => "brak boga — wszystkie skille bazowe"
    };

    public static readonly GodId[] All = { GodId.None, GodId.Wilds, GodId.Blood };
}

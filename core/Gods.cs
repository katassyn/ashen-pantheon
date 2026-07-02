namespace AshenPantheon.Core;

/// <summary>Bogowie panteonu — dane w GameData.GodSpecs (data/gods/*.json); tu tylko enum wyboru + fasada.</summary>
public enum GodId { None, Wilds, Blood }

public static class Gods
{
    public static string Name(GodId g) => GameData.God(g)?.Name ?? "—";

    public static string Passive(GodId g) =>
        GameData.God(g)?.Passive ?? "brak boga — wszystkie skille bazowe";

    public static readonly GodId[] All = { GodId.None, GodId.Wilds, GodId.Blood };
}

using AshenPantheon.Core;

/// <summary>Trwały stan postaci na czas sesji gry — przeżywa zmianę sceny (miasto ↔ arena).</summary>
public static class GameState
{
    public static Attributes BaseAttributes = new() { Strength = 12, Dexterity = 15, Intelligence = 5 };
    public static Equipment Equipment = new();
    public static Inventory Inventory = new();
    public static int Level = 1;

    /// <summary>Id skilli, dla których gracz wybrał wariant boga (reszta gra bazowo). Wybór per-skill w panelu K.</summary>
    public static System.Collections.Generic.HashSet<string> GodSkills = new();

    public static CharacterSheet BuildSheet()
    {
        var sheet = Equipment.BuildSheet(BaseAttributes, Level);
        sheet.BaseLife = 80f;
        sheet.BaseMana = 50f;
        return sheet;
    }
}

namespace AshenPantheon.Core;

/// <summary>Trzy główne atrybuty (2 punkty na poziom). Za każdy punkt:
/// INT +2 many i +1% ES · DEX +2 evasion i +1% hit rate · Siła +2 HP i +1% attack damage.</summary>
public sealed class Attributes
{
    public int Intelligence { get; set; }
    public int Dexterity { get; set; }
    public int Strength { get; set; }
}

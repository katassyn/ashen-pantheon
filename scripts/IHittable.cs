using AshenPantheon.Core;

/// <summary>Cokolwiek, co skille gracza mogą trafić (manekin, wróg, boss).</summary>
public interface IHittable
{
    void ReceiveHit(ResolvedSkill skill);
}

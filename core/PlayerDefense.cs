using System;

namespace AshenPantheon.Core;

/// <summary>Warstwa obronna gracza: Energy Shield absorbuje PRZED HP (chaos omija ES — jak w PoE),
/// recharge ES po 3 s bez obrażeń, life regen. Czysta logika = testowalna.</summary>
public sealed class PlayerDefense
{
    public const float EsRechargeDelay = 3f;
    public const float EsRechargePerSecondFrac = 0.25f; // 25% max ES / s

    public float Health { get; set; }
    public float EnergyShield { get; set; }
    private float _esDelay;

    public void ResetFull(CharacterSheet sheet)
    {
        Health = sheet.MaxLife;
        EnergyShield = sheet.MaxEnergyShield;
        _esDelay = 0f;
    }

    /// <summary>Przyjmuje JUŻ zmitygowane obrażenia. Zwraca ile realnie zeszło z HP (0 = ES przyjął wszystko).</summary>
    public float Absorb(float mitigated, DamageType type)
    {
        if (mitigated <= 0f) return 0f;
        _esDelay = EsRechargeDelay;

        float toHp = mitigated;
        if (type != DamageType.Chaos && EnergyShield > 0f) // chaos przebija ES
        {
            float absorbed = MathF.Min(EnergyShield, toHp);
            EnergyShield -= absorbed;
            toHp -= absorbed;
        }
        Health -= toHp;
        return toHp;
    }

    /// <summary>Tyknięcie: recharge ES po opóźnieniu + life regen. Clampuje do maksów z arkusza.</summary>
    public void Tick(float dt, CharacterSheet sheet)
    {
        float rechargeTime = dt;
        if (_esDelay > 0f)
        {
            float consumed = MathF.Min(_esDelay, dt);
            _esDelay -= consumed;
            rechargeTime = dt - consumed; // resztka ticka ładuje ES
        }
        if (rechargeTime > 0f && EnergyShield < sheet.MaxEnergyShield)
            EnergyShield = MathF.Min(sheet.MaxEnergyShield, EnergyShield + sheet.MaxEnergyShield * EsRechargePerSecondFrac * rechargeTime);

        if (sheet.LifeRegen > 0f && Health < sheet.MaxLife && Health > 0f)
            Health = MathF.Min(sheet.MaxLife, Health + sheet.LifeRegen * dt);

        Health = MathF.Min(Health, sheet.MaxLife);
        EnergyShield = MathF.Min(EnergyShield, sheet.MaxEnergyShield);
    }
}

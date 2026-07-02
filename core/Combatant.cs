using System.Collections.Generic;
using System.Linq;

namespace AshenPantheon.Core;

/// <summary>Instancja statusu — wiele statusów może działać RÓWNOCZEŚNIE (Burn+Chill+Bleed...).</summary>
public sealed class StatusInstance
{
    public StatusType Type { get; init; }
    public float TimeLeft { get; set; }
    public float Dps { get; set; }
}

public sealed class Combatant
{
    public required float MaxHealth { get; init; }
    public float Health { get; set; }

    /// <summary>Aktywne statusy (multi-status: ten sam typ odświeża czas i bierze mocniejszy dps).</summary>
    public List<StatusInstance> Statuses { get; } = new();

    // Obrona celu (wrogowie z danych bestiariusza; gracz ma własny arkusz)
    public float EvadeChance { get; set; }
    public float Armour { get; set; }
    public float ResFire { get; set; }
    public float ResCold { get; set; }
    public float ResLightning { get; set; }
    public float ResChaos { get; set; }

    // Ranger: Oznaczenie
    public bool Marked { get; set; }
    public float MarkTimeLeft { get; set; }

    // Ogłuszenie
    public float StunTimeLeft { get; set; }

    public bool IsDead => Health <= 0f;
    public bool IsChilled => Has(StatusType.Chill);
    public bool IsMarked => Marked && MarkTimeLeft > 0f;
    public bool IsStunned => StunTimeLeft > 0f;

    public bool Has(StatusType type) => Statuses.Any(s => s.Type == type && s.TimeLeft > 0f);

    /// <summary>Bitmaska aktywnych statusów (sync sieciowy / UI).</summary>
    public int StatusMask()
    {
        int mask = 0;
        foreach (var s in Statuses) mask |= 1 << (int)s.Type;
        return mask;
    }

    public void ApplyStatus(StatusType type, float duration, float dps)
    {
        if (type == StatusType.None || duration <= 0f) return;
        var existing = Statuses.FirstOrDefault(s => s.Type == type);
        if (existing != null)
        {
            existing.TimeLeft = System.MathF.Max(existing.TimeLeft, duration);
            existing.Dps = System.MathF.Max(existing.Dps, dps);
        }
        else
        {
            Statuses.Add(new StatusInstance { Type = type, TimeLeft = duration, Dps = dps });
        }
    }

    /// <summary>Tyknięcie statusów/oznaczenia/stuna (logika w core = testowalna). Zwraca true przy zmianie zestawu statusów.</summary>
    public bool Tick(float dt)
    {
        bool changed = false;
        for (int i = Statuses.Count - 1; i >= 0; i--)
        {
            var s = Statuses[i];
            s.TimeLeft -= dt;
            if (s.Dps > 0f) Health -= s.Dps * dt;
            if (s.TimeLeft <= 0f)
            {
                Statuses.RemoveAt(i);
                changed = true;
            }
        }

        if (MarkTimeLeft > 0f)
        {
            MarkTimeLeft -= dt;
            if (MarkTimeLeft <= 0f) { Marked = false; changed = true; }
        }

        if (StunTimeLeft > 0f) StunTimeLeft -= dt;
        return changed;
    }

    /// <summary>Mitygacja obrażeń po stronie celu (armour dla fizycznych, resisty dla żywiołów/chaosu).</summary>
    public float Mitigate(DamageType type, float raw)
    {
        if (type == DamageType.Physical)
        {
            if (Armour <= 0f || raw <= 0f) return raw;
            float reduction = System.MathF.Min(Armour / (Armour + 10f * raw), 0.9f);
            return raw * (1f - reduction);
        }
        float res = type switch
        {
            DamageType.Fire => ResFire, DamageType.Cold => ResCold,
            DamageType.Lightning => ResLightning, DamageType.Chaos => ResChaos, _ => 0f
        };
        return raw * (1f - System.Math.Clamp(res, 0f, 75f) / 100f);
    }
}

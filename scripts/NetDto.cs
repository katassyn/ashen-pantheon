using System.Text.Json;
using AshenPantheon.Core;
using Godot;

/// <summary>Serializacja ResolvedSkill (+ tint) do przesyłu RPC. Kontrakt lekki, JSON — wystarczający na LAN/co-op.</summary>
public sealed class SkillDto
{
    public string Id { get; set; } = "";
    public float Damage { get; set; }
    public int Shape { get; set; }
    public int OnHitStatus { get; set; }
    public float StatusDuration { get; set; }
    public bool Explodes { get; set; }
    public bool Pierces { get; set; }
    public bool PierceMarkedOnly { get; set; }
    public bool AppliesMark { get; set; }
    public float MarkDuration { get; set; }
    public float MarkedMultiplier { get; set; } = 1f;
    public float StunDuration { get; set; }
    public float HealOnHit { get; set; }
    public float AoeMult { get; set; } = 1f;
    public float DurationMult { get; set; } = 1f;
    public string VariantTag { get; set; }
    public int CasterPeer { get; set; } = 1;
    public float StatusDps { get; set; }
    public float HitChance { get; set; } = 100f;
    public int DamageType { get; set; }
    public bool IsCrit { get; set; }
    public float Tr { get; set; } = 1f;
    public float Tg { get; set; } = 1f;
    public float Tb { get; set; } = 1f;

    public static string Pack(ResolvedSkill s, Color tint) => JsonSerializer.Serialize(new SkillDto
    {
        Id = s.Id, Damage = s.Damage, Shape = (int)s.Shape,
        OnHitStatus = (int)s.OnHitStatus, StatusDuration = s.StatusDuration,
        Explodes = s.Explodes, Pierces = s.Pierces, PierceMarkedOnly = s.PierceMarkedOnly,
        AppliesMark = s.AppliesMark, MarkDuration = s.MarkDuration, MarkedMultiplier = s.MarkedMultiplier,
        StunDuration = s.StunDuration, HealOnHit = s.HealOnHit,
        AoeMult = s.AoeMult, DurationMult = s.DurationMult,
        VariantTag = s.VariantTag, CasterPeer = s.CasterPeer,
        StatusDps = s.StatusDps, HitChance = s.HitChance, DamageType = (int)s.DamageType, IsCrit = s.IsCrit,
        Tr = tint.R, Tg = tint.G, Tb = tint.B,
    });

    public static (ResolvedSkill Skill, Color Tint) Unpack(string json)
    {
        var d = JsonSerializer.Deserialize<SkillDto>(json) ?? new SkillDto();
        var s = new ResolvedSkill
        {
            Id = d.Id, Damage = d.Damage, Shape = (SkillShape)d.Shape,
            OnHitStatus = (StatusType)d.OnHitStatus, StatusDuration = d.StatusDuration,
            Explodes = d.Explodes, Pierces = d.Pierces, PierceMarkedOnly = d.PierceMarkedOnly,
            AppliesMark = d.AppliesMark, MarkDuration = d.MarkDuration, MarkedMultiplier = d.MarkedMultiplier,
            StunDuration = d.StunDuration, HealOnHit = d.HealOnHit,
            AoeMult = d.AoeMult, DurationMult = d.DurationMult,
            VariantTag = d.VariantTag, CasterPeer = d.CasterPeer,
            StatusDps = d.StatusDps, HitChance = d.HitChance, DamageType = (DamageType)d.DamageType, IsCrit = d.IsCrit,
        };
        return (s, new Color(d.Tr, d.Tg, d.Tb));
    }
}

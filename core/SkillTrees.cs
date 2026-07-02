using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenPantheon.Core;

/// <summary>Węzeł drzewka ulepszeń skilla. Węzły z tym samym ExclusiveGroup wykluczają się nawzajem.</summary>
public sealed class SkillNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public string? ExclusiveGroup { get; init; }
    public Action<ResolvedSkill>? Apply { get; init; }
}

/// <summary>Pełne drzewka wszystkich 9 skilli Rangera. Koszt węzła = 1 punkt skilla.</summary>
public static class RangerTrees
{
    public static readonly Dictionary<string, SkillNode[]> BySkill = new()
    {
        ["basic"] = new[]
        {
            new SkillNode { Id = "basic_dmg", Name = "Ostre groty", Description = "+30% obrażeń", Apply = s => s.Damage *= 1.3f },
            new SkillNode { Id = "basic_pierce", Name = "Przeszycie", Description = "Strzał przebija wrogów", ExclusiveGroup = "b1", Apply = s => s.Pierces = true },
            new SkillNode { Id = "basic_twin", Name = "Podwójny strzał", Description = "+1 pocisk", ExclusiveGroup = "b1", Apply = s => s.ExtraProjectiles += 1 },
            new SkillNode { Id = "basic_mark", Name = "Głębokie piętno", Description = "Oznaczenie trwa +2s", Apply = s => s.MarkDuration += 2f },
        },
        ["spread"] = new[]
        {
            new SkillNode { Id = "spread_extra", Name = "Szerszy wachlarz", Description = "+1 pocisk", ExclusiveGroup = "s1", Apply = s => s.ExtraProjectiles += 1 },
            new SkillNode { Id = "spread_focus", Name = "Skupiony rozrzut", Description = "+25% obrażeń", ExclusiveGroup = "s1", Apply = s => s.Damage *= 1.25f },
            new SkillNode { Id = "spread_cheap", Name = "Ekonomia ruchu", Description = "Koszt −30%", Apply = s => s.CostMult *= 0.7f },
            new SkillNode { Id = "spread_dmg", Name = "Ciężkie strzały", Description = "+15% obrażeń", Apply = s => s.Damage *= 1.15f },
        },
        ["exec"] = new[]
        {
            new SkillNode { Id = "exec_dmg", Name = "Wyrok", Description = "+20% obrażeń", Apply = s => s.Damage *= 1.2f },
            new SkillNode { Id = "exec_mult", Name = "Egzekucja totalna", Description = "×2.5 na oznaczonych (zamiast ×2)", ExclusiveGroup = "e1", Apply = s => s.MarkedMultiplier = Math.Max(s.MarkedMultiplier, 2.5f) },
            new SkillNode { Id = "exec_cheap", Name = "Zimna precyzja", Description = "Koszt −40%", ExclusiveGroup = "e1", Apply = s => s.CostMult *= 0.6f },
        },
        ["rain"] = new[]
        {
            new SkillNode { Id = "rain_area", Name = "Nawałnica", Description = "+40% obszaru", ExclusiveGroup = "r1", Apply = s => s.AoeMult *= 1.4f },
            new SkillNode { Id = "rain_dmg", Name = "Ciężki grad", Description = "+40% obrażeń", ExclusiveGroup = "r1", Apply = s => s.Damage *= 1.4f },
            new SkillNode { Id = "rain_cd", Name = "Sprawne dłonie", Description = "CD −30%", Apply = s => s.CdMult *= 0.7f },
            new SkillNode { Id = "rain_slow", Name = "Grzęzawisko", Description = "Spowolnienie trwa +50%", Apply = s => s.DurationMult *= 1.5f },
        },
        ["mine"] = new[]
        {
            new SkillNode { Id = "mine_stun", Name = "Ogłuszający ładunek", Description = "Stun +0.6s", Apply = s => s.StunDuration += 0.6f },
            new SkillNode { Id = "mine_twin", Name = "Podwójny ładunek", Description = "Stawiasz 2 miny", ExclusiveGroup = "m1", Apply = s => s.ExtraProjectiles += 1 },
            new SkillNode { Id = "mine_dmg", Name = "Silniejszy proch", Description = "+50% obrażeń", ExclusiveGroup = "m1", Apply = s => s.Damage *= 1.5f },
            new SkillNode { Id = "mine_cheap", Name = "Oszczędny zapalnik", Description = "Koszt −30%", Apply = s => s.CostMult *= 0.7f },
        },
        ["hedge"] = new[]
        {
            new SkillNode { Id = "hedge_long", Name = "Długa przesieka", Description = "+40% długości", Apply = s => s.AoeMult *= 1.4f },
            new SkillNode { Id = "hedge_poison", Name = "Zatrute kolce", Description = "Nakłada truciznę", ExclusiveGroup = "h1", Apply = s => { s.OnHitStatus = StatusType.Poison; s.StatusDuration = Math.Max(s.StatusDuration, 3f); } },
            new SkillNode { Id = "hedge_wide", Name = "Gęstwina", Description = "+50% szerokości", ExclusiveGroup = "h1", Apply = s => s.DurationMult *= 1.5f },
            new SkillNode { Id = "hedge_cd", Name = "Szybki siew", Description = "CD −25%", Apply = s => s.CdMult *= 0.75f },
        },
        ["dash"] = new[]
        {
            new SkillNode { Id = "dash_cd", Name = "Refleks", Description = "CD −30%", Apply = s => s.CdMult *= 0.7f },
            new SkillNode { Id = "dash_far", Name = "Długi sus", Description = "+35% dystansu", ExclusiveGroup = "d1", Apply = s => s.AoeMult *= 1.35f },
            new SkillNode { Id = "dash_free", Name = "Lekkość", Description = "Dash nic nie kosztuje", ExclusiveGroup = "d1", Apply = s => s.CostMult = 0f },
            new SkillNode { Id = "dash_iframe", Name = "Cień łowcy", Description = "Nietykalność +0.1s", Apply = s => s.DurationMult *= 1.5f },
        },
        ["adrenaline"] = new[]
        {
            new SkillNode { Id = "adr_time", Name = "Długi zryw", Description = "Czas trwania +2s", Apply = s => s.DurationMult *= 1.4f },
            new SkillNode { Id = "adr_cd", Name = "Szybka regeneracja", Description = "CD −30%", ExclusiveGroup = "a1", Apply = s => s.CdMult *= 0.7f },
            new SkillNode { Id = "adr_power", Name = "Bojowy szał", Description = "+25% obrażeń w trakcie", ExclusiveGroup = "a1", Apply = s => s.Damage = 0.25f }, // Damage tu = bonus dmg% podczas trwania
        },
        ["hawk"] = new[]
        {
            new SkillNode { Id = "hawk_dmg", Name = "Ostre szpony", Description = "+30% obrażeń", Apply = s => s.Damage *= 1.3f },
            new SkillNode { Id = "hawk_stun", Name = "Druzgocące uderzenie", Description = "Stun +1s", ExclusiveGroup = "hk1", Apply = s => s.StunDuration += 1f },
            new SkillNode { Id = "hawk_cd", Name = "Wierny towarzysz", Description = "CD −30%", ExclusiveGroup = "hk1", Apply = s => s.CdMult *= 0.7f },
            new SkillNode { Id = "hawk_mark", Name = "Oko łowcy", Description = "Oznaczenie z jastrzębia trwa 8s", Apply = s => s.MarkDuration = Math.Max(s.MarkDuration, 8f) },
        },
    };

    public static SkillNode? Find(string skillId, string nodeId) =>
        BySkill.TryGetValue(skillId, out var nodes) ? nodes.FirstOrDefault(n => n.Id == nodeId) : null;
}

/// <summary>Stan alokacji drzewek gracza (co odblokowano). Egzekwuje wykluczające się gałęzie.</summary>
public sealed class SkillTreeState
{
    public Dictionary<string, HashSet<string>> Allocated { get; } = new();

    public int SpentPoints => Allocated.Values.Sum(v => v.Count);

    public bool IsAllocated(string skillId, string nodeId) =>
        Allocated.TryGetValue(skillId, out var set) && set.Contains(nodeId);

    public bool CanAllocate(string skillId, string nodeId)
    {
        var node = RangerTrees.Find(skillId, nodeId);
        if (node is null || IsAllocated(skillId, nodeId)) return false;
        if (node.ExclusiveGroup is null) return true;
        // konflikt: inny węzeł z tej samej grupy już wzięty
        return !RangerTrees.BySkill[skillId]
            .Where(n => n.ExclusiveGroup == node.ExclusiveGroup && n.Id != nodeId)
            .Any(n => IsAllocated(skillId, n.Id));
    }

    public bool Allocate(string skillId, string nodeId)
    {
        if (!CanAllocate(skillId, nodeId)) return false;
        if (!Allocated.TryGetValue(skillId, out var set)) Allocated[skillId] = set = new();
        set.Add(nodeId);
        return true;
    }

    /// <summary>Nakłada efekty odblokowanych węzłów danego skilla na ResolvedSkill.</summary>
    public void ApplyTo(string skillId, ResolvedSkill skill)
    {
        if (!Allocated.TryGetValue(skillId, out var set)) return;
        foreach (var nodeId in set)
            RangerTrees.Find(skillId, nodeId)?.Apply?.Invoke(skill);
    }

    /// <summary>Pełny reset (respec). Zwraca liczbę zwróconych punktów.</summary>
    public int ResetAll()
    {
        int refunded = SpentPoints;
        Allocated.Clear();
        return refunded;
    }
}

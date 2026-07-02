using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenPantheon.Core;

/// <summary>Walidacja zapisu postaci po stronie serwera (ochrona ekonomii przed przyszłym AH):
/// itemy muszą spełniać reguły generatora, uniki istnieć w katalogu, punkty zgadzać się z poziomem.</summary>
public static class SaveValidator
{
    public static (bool Ok, string? Error) Validate(SaveData data)
    {
        if (data.Level is < 1 or > PlayerProgress.LevelCap) return (false, "poziom poza zakresem");
        if (data.Xp < 0 || data.Gold < 0) return (false, "ujemne XP/złoto");
        if (data.AttributePoints < 0 || data.SkillPoints < 0) return (false, "ujemne punkty");
        if (data.SpentStr < 0 || data.SpentDex < 0 || data.SpentInt < 0) return (false, "ujemne atrybuty");

        // punkty zdobyte = 2/lvl (atrybuty) i 1/lvl (skille) od poziomu 2
        int earnedAttr = PlayerProgress.AttributePointsPerLevel * (data.Level - 1);
        int earnedSkill = PlayerProgress.SkillPointsPerLevel * (data.Level - 1);
        if (data.AttributePoints + data.SpentStr + data.SpentDex + data.SpentInt > earnedAttr)
            return (false, "za dużo punktów atrybutów względem poziomu");
        int allocatedNodes = data.TreeNodes.Values.Sum(v => v.Count);
        if (data.SkillPoints + allocatedNodes > earnedSkill)
            return (false, "za dużo punktów skilli względem poziomu");

        foreach (var (skillId, nodes) in data.TreeNodes)
            foreach (var n in nodes)
                if (RangerTrees.Find(skillId, n) == null)
                    return (false, $"nieistniejący węzeł drzewka: {skillId}/{n}");

        foreach (var id in data.Loadout)
            if (id != null && RangerKit.Class.Skill(id) == null)
                return (false, $"nieznany skill w loadout: {id}");

        foreach (var dto in AllItems(data))
        {
            var (ok, err) = ValidateItem(dto);
            if (!ok) return (false, err);
        }

        return (true, null);
    }

    private static IEnumerable<ItemDto> AllItems(SaveData data)
    {
        foreach (var kv in data.Equipment) yield return kv.Value;
        foreach (var p in data.Bag) yield return p.Item;
        foreach (var p in data.Stash) yield return p.Item;
    }

    public static (bool Ok, string? Error) ValidateItem(ItemDto dto)
    {
        if (!Enum.TryParse<ItemKind>(dto.Kind, out _)) return (false, $"nieznany typ itemu: {dto.Kind}");
        if (!Enum.TryParse<Rarity>(dto.Rarity, out var rarity)) return (false, $"nieznana rzadkość: {dto.Rarity}");

        // hand-authored tiery muszą pochodzić z katalogu (dane itemu i tak odtwarzamy z katalogu po UniqueId)
        if (rarity is Rarity.Legendary or Rarity.Unique or Rarity.Mythic)
        {
            if (dto.UniqueId == null || UniqueCatalog.ById(dto.UniqueId) == null)
                return (false, $"item {dto.Name}: tier {rarity} bez ważnego UniqueId");
            return (true, null);
        }

        if (dto.UniqueId != null && UniqueCatalog.ById(dto.UniqueId) == null)
            return (false, $"item {dto.Name}: fałszywy UniqueId");

        int maxAffixes = rarity switch { Rarity.Normal => 0, Rarity.Magic => 2, Rarity.Rare => 4, _ => 0 };
        if (dto.Affixes.Count > maxAffixes)
            return (false, $"item {dto.Name}: za dużo affixów ({dto.Affixes.Count}) dla {rarity}");

        foreach (var a in dto.Affixes)
        {
            if (!Enum.TryParse<AffixStat>(a.Stat, out var stat))
                return (false, $"item {dto.Name}: nieznany affix {a.Stat}");
            if (!AffixRanges.InRange(stat, a.Value))
                return (false, $"item {dto.Name}: affix {a.Stat}={a.Value} poza zakresem generatora");
        }

        return (true, null);
    }
}

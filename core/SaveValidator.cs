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
        int spentOnNodes = GameData.Loaded
            ? data.TreeNodes.Sum(kv => kv.Value.Sum(id => GameData.FindNode(kv.Key, id)?.Cost ?? 1))
            : data.TreeNodes.Values.Sum(v => v.Count);
        int spentOnPassives = data.PassiveNodes.Sum(id => ClassTree.Find(data.ClassId, id)?.Cost ?? 1);
        if (data.SkillPoints + spentOnNodes + spentOnPassives > earnedSkill)
            return (false, "za dużo punktów skilli względem poziomu");

        if (ClassTree.Trees.Count > 0)
            foreach (var id in data.PassiveNodes)
            {
                var node = ClassTree.Find(data.ClassId, id);
                if (node == null || node.Type != "passive")
                    return (false, $"nieistniejąca pasywka drzewa klasy: {id}");
                if (node.RequiredLevel > data.Level)
                    return (false, $"pasywka {id} wymaga poziomu {node.RequiredLevel}");
            }

        // walidacja względem katalogów danych (serwer/testy ładują data/; brak danych → pomiń te checki)
        if (GameData.Loaded)
        {
            foreach (var (skillId, nodes) in data.TreeNodes)
                foreach (var n in nodes)
                {
                    var node = GameData.FindNode(skillId, n);
                    if (node == null)
                        return (false, $"nieistniejący węzeł drzewka: {skillId}/{n}");
                    if (node.RequiredLevel > data.Level)
                        return (false, $"węzeł {skillId}/{n} wymaga poziomu {node.RequiredLevel}");
                }

            var anyClassHas = (string id) => GameData.Classes.Values.Any(c => c.Skill(id) != null);
            foreach (var id in data.Loadout)
                if (id != null && !anyClassHas(id))
                    return (false, $"nieznany skill w loadout: {id}");
        }

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
        if (!Enum.TryParse<ItemKind>(dto.Kind, out var kind)) return (false, $"nieznany typ itemu: {dto.Kind}");
        if (!Enum.TryParse<Rarity>(dto.Rarity, out var rarity)) return (false, $"nieznana rzadkość: {dto.Rarity}");

        int ilvl = dto.ItemLevel <= 0 ? 50 : dto.ItemLevel; // legacy zapisy bez ilvl = pełna skala
        if (ilvl > 100) return (false, $"item {dto.Name}: ilvl {ilvl} poza zakresem");

        // klejnot: jeden affix zgodny z katalogiem (gdy katalog załadowany — serwer/testy TAK)
        if (kind == ItemKind.Jewel)
        {
            if (JewelCatalog.Jewels.Count == 0) return (true, null); // katalog niedostępny → pomiń (lokalne edge-case'y)
            bool okJewel = JewelCatalog.Validate(dto.JewelId,
                dto.Affixes.Select(a => (a.Stat, a.Value)).ToList(), ilvl);
            return okJewel ? (true, null) : (false, $"jewel {dto.Name}: niezgodny z katalogiem");
        }

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
        // implicity broni (WeaponDamage/WeaponAttackSpeed) nie liczą się do limitu affixów
        int rolled = dto.Affixes.Count(a => a.Stat is not ("WeaponDamage" or "WeaponAttackSpeed"));
        if (rolled > maxAffixes)
            return (false, $"item {dto.Name}: za dużo affixów ({rolled}) dla {rarity}");

        foreach (var a in dto.Affixes)
        {
            if (!Enum.TryParse<AffixStat>(a.Stat, out var stat))
                return (false, $"item {dto.Name}: nieznany affix {a.Stat}");
            if (!AffixRanges.InRange(stat, a.Value, ilvl))
                return (false, $"item {dto.Name}: affix {a.Stat}={a.Value} poza zakresem dla ilvl {ilvl}");
        }

        // sockety + klejnoty w środku
        if (dto.Sockets < 0 || dto.Sockets > Item.MaxSocketsFor(kind))
            return (false, $"item {dto.Name}: {dto.Sockets} socketów > cap dla {kind}");
        if (dto.Jewels.Count > dto.Sockets)
            return (false, $"item {dto.Name}: więcej jeweli ({dto.Jewels.Count}) niż socketów ({dto.Sockets})");
        foreach (var j in dto.Jewels)
        {
            if (!string.Equals(j.Kind, "Jewel", StringComparison.OrdinalIgnoreCase))
                return (false, $"item {dto.Name}: w sockecie nie-jewel");
            var (jok, jerr) = ValidateItem(j);
            if (!jok) return (false, jerr);
        }

        return (true, null);
    }
}

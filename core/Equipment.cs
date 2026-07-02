using System.Collections.Generic;

namespace AshenPantheon.Core;

/// <summary>Założony ekwipunek: item per slot. Buduje CharacterSheet z bazowych atrybutów + affixów gearu.</summary>
public sealed class Equipment
{
    private readonly Dictionary<EquipmentSlot, Item> _slots = new();

    public Item? Get(EquipmentSlot slot) => _slots.GetValueOrDefault(slot);
    public bool IsEmpty(EquipmentSlot slot) => !_slots.ContainsKey(slot);

    /// <summary>Czy item danego rodzaju może wejść w slot (z regułą 2H/off-hand).</summary>
    public bool CanEquip(Item item, EquipmentSlot slot)
    {
        bool validSlot = System.Array.IndexOf(Item.SlotsFor(item.Kind), slot) >= 0;
        if (!validSlot) return false;

        // Off-hand tylko przy broni 1-ręcznej (albo pustym slocie broni)
        if (slot == EquipmentSlot.OffHand)
        {
            var weapon = Get(EquipmentSlot.Weapon);
            if (weapon != null && weapon.Kind == ItemKind.TwoHandWeapon) return false;
        }
        return true;
    }

    /// <summary>Zakłada item do slotu. Zwraca zdjęte itemy (wyparty ze slotu + ew. off-hand przy 2H).</summary>
    public List<Item> Equip(Item item, EquipmentSlot slot)
    {
        var removed = new List<Item>();
        if (!CanEquip(item, slot)) return removed;

        if (_slots.TryGetValue(slot, out var prev)) { removed.Add(prev); _slots.Remove(slot); }

        // Broń dwuręczna wypiera off-hand
        if (slot == EquipmentSlot.Weapon && item.Kind == ItemKind.TwoHandWeapon
            && _slots.TryGetValue(EquipmentSlot.OffHand, out var off))
        {
            removed.Add(off);
            _slots.Remove(EquipmentSlot.OffHand);
        }

        _slots[slot] = item;
        return removed;
    }

    /// <summary>Zakłada item w pierwszy wolny (lub pierwszy dozwolony) slot. Zwraca zdjęte itemy, albo null gdy się nie da.</summary>
    public List<Item>? EquipAuto(Item item)
    {
        foreach (var slot in Item.SlotsFor(item.Kind))
            if (CanEquip(item, slot) && IsEmpty(slot))
                return Equip(item, slot);

        foreach (var slot in Item.SlotsFor(item.Kind))
            if (CanEquip(item, slot))
                return Equip(item, slot);

        return null;
    }

    public Item? Unequip(EquipmentSlot slot)
    {
        if (_slots.TryGetValue(slot, out var item)) { _slots.Remove(slot); return item; }
        return null;
    }

    public IEnumerable<Item> EquippedItems() => _slots.Values;

    /// <summary>Buduje arkusz postaci: bazowe atrybuty + affixy ze wszystkich założonych itemów.</summary>
    public CharacterSheet BuildSheet(Attributes baseAttributes, int level)
    {
        var attr = new Attributes
        {
            Strength = baseAttributes.Strength,
            Dexterity = baseAttributes.Dexterity,
            Intelligence = baseAttributes.Intelligence
        };
        var res = new Resistances();

        float flatLife = 0, flatMana = 0, flatES = 0, flatArmour = 0, flatEvasion = 0, flatHit = 0;
        float incAtk = 0, lifeRegen = 0, manaRegen = 0, critC = 0, critM = 0, atkSpd = 0, castSpd = 0;
        float weaponDmg = 0;

        foreach (var item in EquippedItems())
            foreach (var a in item.Affixes)
                switch (a.Stat)
                {
                    case AffixStat.FlatLife: flatLife += a.Value; break;
                    case AffixStat.FlatMana: flatMana += a.Value; break;
                    case AffixStat.FlatEnergyShield: flatES += a.Value; break;
                    case AffixStat.FlatArmour: flatArmour += a.Value; break;
                    case AffixStat.FlatEvasion: flatEvasion += a.Value; break;
                    case AffixStat.FlatHitChance: flatHit += a.Value; break;
                    case AffixStat.Strength: attr.Strength += (int)a.Value; break;
                    case AffixStat.Dexterity: attr.Dexterity += (int)a.Value; break;
                    case AffixStat.Intelligence: attr.Intelligence += (int)a.Value; break;
                    case AffixStat.IncreasedAttackDamage: incAtk += a.Value; break;
                    case AffixStat.FireResist: res.Fire += a.Value; break;
                    case AffixStat.ColdResist: res.Cold += a.Value; break;
                    case AffixStat.LightningResist: res.Lightning += a.Value; break;
                    case AffixStat.ChaosResist: res.Chaos += a.Value; break;
                    case AffixStat.LifeRegen: lifeRegen += a.Value; break;
                    case AffixStat.ManaRegen: manaRegen += a.Value; break;
                    case AffixStat.CritChance: critC += a.Value; break;
                    case AffixStat.CritMultiplier: critM += a.Value; break;
                    case AffixStat.AttackSpeed: atkSpd += a.Value; break;
                    case AffixStat.CastSpeed: castSpd += a.Value; break;
                    case AffixStat.WeaponDamage: weaponDmg += a.Value; break;
                    case AffixStat.WeaponAttackSpeed: atkSpd += a.Value; break;
                }

        var sheet = new CharacterSheet
        {
            Level = level,
            Attributes = attr,
            Resistances = res,
            FlatLife = flatLife,
            FlatMana = flatMana,
            FlatEnergyShield = flatES,
            FlatArmour = flatArmour,
            FlatEvasion = flatEvasion,
            FlatHitChance = flatHit,
            IncreasedAttackDamage = incAtk,
            LifeRegen = lifeRegen,
            ManaRegen = manaRegen
        };
        sheet.CritChance += critC;
        sheet.CritMultiplier += critM;
        sheet.AttackSpeed += atkSpd;
        sheet.CastSpeed += castSpd;
        sheet.WeaponDamage = weaponDmg;
        return sheet;
    }
}

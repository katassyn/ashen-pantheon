using System;
using System.Collections.Generic;
using System.Linq;
using AshenPantheon.Core;

/// <summary>Trwały stan postaci. Server-ready: całość serializuje się przez IGameStateRepository
/// (dziś lokalny JSON, docelowo serwer/baza — podmiana repo bez ruszania gameplayu).</summary>
public static class GameState
{
    // Klasa postaci — architektura pod N klas: definicje w data/classes/*.json
    public static string ClassId = "ranger";
    private static ClassDefinition _classDef;
    public static ClassDefinition Class => _classDef ??= GameData.Class(ClassId).ToDefinition();
    public static ClassSpec ClassSpec => GameData.Class(ClassId);

    // Startowe atrybuty klasy + punkty wydane przez gracza (osobno — respec zwraca tylko wydane)
    public static readonly Attributes ClassBase = new() { Strength = 12, Dexterity = 15, Intelligence = 5 };
    public static Attributes Spent = new();

    public static PlayerProgress Progress = new();
    public static Wallet Wallet = new();
    public static Equipment Equipment = new();
    public static GridInventory Bag = new(12, 6);
    public static GridInventory Stash = new(12, 8);
    public static SkillTreeState Trees = new();
    public static Loadout Loadout = new();

    public static string CharacterName = "Bezimienny";
    public static QuestLog Quests = new();
    /// <summary>Odkryte strefy świata (waystone fast-travel).</summary>
    public static System.Collections.Generic.HashSet<string> DiscoveredZones = new();

    // ── endgame (The Final Proving Q1-Q10 + dungeony grupowe) ──
    public static int EndgameQ = 1;
    public static System.Collections.Generic.HashSet<string> EndgameCleared = new();

    /// <summary>Kampania ukończona = wejście do endgame (finał = pokonanie Nefertari).</summary>
    public static bool CampaignCompleted => Quests.IsCompleted("desert_03");

    /// <summary>Zaliczenie wyzwania endgame ("q:N" lub "g:dungeon/diff") — odblokowuje następny stopień.</summary>
    public static void MarkEndgameCleared(string challenge)
    {
        if (string.IsNullOrEmpty(challenge)) return;
        if (EndgameCatalog.TryParseQ(challenge, out int q))
            EndgameQ = System.Math.Max(EndgameQ, System.Math.Min(q + 1, EndgameCatalog.QMax));
        else if (challenge.StartsWith("g:"))
            EndgameCleared.Add(challenge[2..]);
        Save();
    }

    /// <summary>Oznacz strefę jako odkrytą (wejście przez portal). Zwraca true jeśli nowa.</summary>
    public static bool DiscoverZone(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId) || !DiscoveredZones.Add(zoneId)) return false;
        Save();
        return true;
    }
    /// <summary>Kupione pasywki z GŁÓWNEGO drzewa klasy (track między skillami).</summary>
    public static System.Collections.Generic.HashSet<string> PassiveNodes = new();

    public static GodId PledgedGod = GodId.None;
    public static HashSet<string> GodSkills = new();

    public static IGameStateRepository? Repository;
    private static bool _loaded;

    public static CharacterSheet BuildSheet()
    {
        var total = new Attributes
        {
            Strength = ClassBase.Strength + Spent.Strength,
            Dexterity = ClassBase.Dexterity + Spent.Dexterity,
            Intelligence = ClassBase.Intelligence + Spent.Intelligence,
        };
        // pasywki z drzewa klasy wchodzą tym samym pipeline co affixy gearu
        var sheet = Equipment.BuildSheet(total, Progress.Level, ClassTree.PassiveAffixes(ClassId, PassiveNodes));
        sheet.BaseLife = 80f;
        sheet.BaseMana = 50f;
        return sheet;
    }

    /// <summary>Świeża postać (kreator w menu głównym).</summary>
    public static void NewCharacter(string name, string classId, IGameStateRepository repo)
    {
        Repository = repo;
        _loaded = true;
        CharacterName = string.IsNullOrWhiteSpace(name) ? "Bezimienny" : name.Trim();
        ClassId = classId;
        _classDef = null;
        Progress = new PlayerProgress();
        Spent = new Attributes();
        Wallet = new Wallet();
        Equipment = new Equipment();
        Bag = new GridInventory(12, 6);
        Stash = new GridInventory(12, 8);
        Trees = new SkillTreeState();
        PassiveNodes = new();
        Quests = new QuestLog();
        DiscoveredZones = new();
        GodSkills = new();
        PledgedGod = GodId.None;
        Loadout = new Loadout();
        EnsureDefaultLoadout();

        // starter kit: bez broni skille biją śladowo — gra ma być grywalna od 1. minuty
        Equipment.Equip(new Item
        {
            Name = "Worn Bow", Kind = ItemKind.TwoHandWeapon, Rarity = Rarity.Normal, ItemLevel = 1,
            Affixes = { new Affix { Stat = AffixStat.WeaponDamage, Value = 8f } },
        }, EquipmentSlot.Weapon);

        Save();
    }

    /// <summary>Po awansie: świeżo odblokowane skille wskakują w wolne sloty paska (onboarding).</summary>
    public static void AutoAssignUnlockedSkills()
    {
        foreach (var spec in ClassSpec.Skills)
        {
            if (spec.RequiredLevel > Progress.Level) continue;
            if (Loadout.SlotOf(spec.Id) != null) continue;
            int free = -1;
            for (int i = 0; i < Loadout.SlotCount; i++)
                if (Loadout.Slots[i] == null) { free = i; break; }
            if (free < 0) return;
            Loadout.Assign(free, spec.Id);
            Net.SendChatLocal($"New skill unlocked: {spec.Name} — assigned to [{Keybinds.SlotKeyName(free)}]");
        }
        Save();
    }

    /// <summary>Efekty mechaniczne uników z założonego gearu.</summary>
    public static bool HasUniqueEffect(UniqueEffect effect) =>
        Equipment.EquippedItems().Any(i => i.Effect == effect);

    public static void EnsureDefaultLoadout()
    {
        if (Loadout.Slots.Any(s => s != null)) return;
        Loadout.Assign(0, "basic");
        Loadout.Assign(1, "spread");
        Loadout.Assign(2, "exec");
        Loadout.Assign(3, "rain");
        Loadout.Assign(4, "dash");
    }

    // ── Persystencja ──

    public static void LoadOrInit()
    {
        if (_loaded) return;
        _loaded = true;

        var data = Repository?.Load();
        if (data == null) { EnsureDefaultLoadout(); return; }
        Apply(data);
    }

    /// <summary>Przełączenie repozytorium (logowanie/wylogowanie). Jeśli nowe repo jest puste
    /// (świeże konto) — wypycha OBECNĄ postać (migracja lokalnego zapisu na serwer).</summary>
    public static void SwitchRepository(IGameStateRepository repo)
    {
        Repository = repo;
        var data = repo.Load();
        if (data == null) { Save(); return; }
        Apply(data);
    }

    private static void Apply(SaveData data)
    {
        CharacterName = data.Name;
        Quests = new QuestLog();
        foreach (var (qid, prog) in data.QuestActive)
        {
            Quests.Active[qid] = new System.Collections.Generic.Dictionary<string, int>(prog);
        }
        foreach (var id in data.QuestCompleted) Quests.Completed.Add(id);
        DiscoveredZones = new System.Collections.Generic.HashSet<string>(data.DiscoveredZones);
        EndgameQ = System.Math.Max(1, data.EndgameQ);
        EndgameCleared = new System.Collections.Generic.HashSet<string>(data.EndgameCleared);
        ClassId = string.IsNullOrEmpty(data.ClassId) ? "ranger" : data.ClassId;
        _classDef = null;
        PassiveNodes = new System.Collections.Generic.HashSet<string>(data.PassiveNodes);
        Progress = new PlayerProgress { Level = data.Level, Xp = data.Xp, AttributePoints = data.AttributePoints, SkillPoints = data.SkillPoints };
        Spent = new Attributes { Strength = data.SpentStr, Dexterity = data.SpentDex, Intelligence = data.SpentInt };
        Wallet = new Wallet { Gold = data.Gold };
        PledgedGod = Enum.TryParse<GodId>(data.PledgedGod, out var g) ? g : GodId.None;
        GodSkills = new HashSet<string>(data.GodSkills);

        Loadout = new Loadout();
        for (int i = 0; i < Loadout.SlotCount && i < data.Loadout.Count; i++)
            Loadout.Assign(i, data.Loadout[i]);
        EnsureDefaultLoadout();

        Trees = new SkillTreeState();
        foreach (var (skillId, nodes) in data.TreeNodes)
            foreach (var n in nodes)
                Trees.Allocate(skillId, n);

        Equipment = new Equipment();
        foreach (var (slotName, dto) in data.Equipment)
            if (Enum.TryParse<EquipmentSlot>(slotName, out var slot))
                Equipment.Equip(ItemMapper.FromDto(dto), slot);

        Bag = new GridInventory(12, 6);
        foreach (var p in data.Bag)
        {
            var item = ItemMapper.FromDto(p.Item);
            if (!Bag.PlaceAt(item, p.X, p.Y)) Bag.TryAutoPlace(item);
        }

        Stash = new GridInventory(12, 8);
        foreach (var p in data.Stash)
        {
            var item = ItemMapper.FromDto(p.Item);
            if (!Stash.PlaceAt(item, p.X, p.Y)) Stash.TryAutoPlace(item);
        }
    }

    public static void Save()
    {
        if (Repository == null) return;
        var data = new SaveData
        {
            Level = Progress.Level, Xp = Progress.Xp,
            AttributePoints = Progress.AttributePoints, SkillPoints = Progress.SkillPoints,
            SpentStr = Spent.Strength, SpentDex = Spent.Dexterity, SpentInt = Spent.Intelligence,
            Gold = Wallet.Gold,
            Name = CharacterName,
            ClassId = ClassId,
            PassiveNodes = PassiveNodes.ToList(),
            QuestActive = Quests.Active.ToDictionary(kv => kv.Key, kv => new System.Collections.Generic.Dictionary<string, int>(kv.Value)),
            QuestCompleted = Quests.Completed.ToList(),
            DiscoveredZones = DiscoveredZones.ToList(),
            EndgameQ = EndgameQ,
            EndgameCleared = EndgameCleared.ToList(),
            PledgedGod = PledgedGod.ToString(),
            GodSkills = GodSkills.ToList(),
            Loadout = Loadout.Slots.ToList(),
            TreeNodes = Trees.Allocated.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()),
            Equipment = SlotDump(),
            Bag = Bag.Placed.Select(p => new PlacedItemDto { Item = ItemMapper.ToDto(p.Item), X = p.X, Y = p.Y }).ToList(),
            Stash = Stash.Placed.Select(p => new PlacedItemDto { Item = ItemMapper.ToDto(p.Item), X = p.X, Y = p.Y }).ToList(),
        };
        Repository.Save(data);
    }

    private static Dictionary<string, ItemDto> SlotDump()
    {
        var result = new Dictionary<string, ItemDto>();
        foreach (EquipmentSlot slot in Enum.GetValues<EquipmentSlot>())
        {
            var item = Equipment.Get(slot);
            if (item != null) result[slot.ToString()] = ItemMapper.ToDto(item);
        }
        return result;
    }
}

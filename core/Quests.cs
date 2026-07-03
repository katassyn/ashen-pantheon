using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace AshenPantheon.Core;

/// <summary>Typy celów questów — WZBOGACONE względem DsoCraft (tam ograniczał Minecraft):
/// klasyczne TALK/KILL/COLLECT/REACH/CLEAR + nowe ESCORT/DEFEND/SURVIVE/INTERACT.</summary>
public enum ObjectiveType
{
    Talk,      // porozmawiaj z NPC (target = npcId)
    Kill,      // zabij X potworów (target = monsterId lub "*" w strefie)
    Collect,   // zbierz X przedmiotów questowych (dropią z targetu)
    Reach,     // dotrzyj do znacznika w strefie (target = markerId)
    Clear,     // ukończ dungeon/run (target = zoneId)
    Escort,    // doprowadź NPC żywego do celu (target = npcId; Escort ginie = reset celu)
    Defend,    // obroń obiekt/NPC przez N fal (target = markerId, amount = fale)
    Survive,   // przeżyj X sekund w strefie zdarzenia (amount = sekundy)
    Interact,  // użyj obiektu w świecie (dźwignia/ołtarz; target = markerId)
}

public sealed class ObjectiveDefinition
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "Kill";
    public string Target { get; set; } = "";
    public int Amount { get; set; } = 1;
    public string Description { get; set; } = "";

    public ObjectiveType Kind => Enum.TryParse<ObjectiveType>(Type, true, out var t) ? t : ObjectiveType.Kill;
}

public sealed class QuestDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int RequiredLevel { get; set; } = 1;
    public string QuestGiver { get; set; } = "";
    public string TurnIn { get; set; } = "";
    public List<string> Prerequisites { get; set; } = new();
    public List<ObjectiveDefinition> Objectives { get; set; } = new();
    public List<string> DialogueStart { get; set; } = new();
    public List<string> DialogueCompletion { get; set; } = new();
    public long RewardXp { get; set; }
    public long RewardGold { get; set; }
    public string NextQuest { get; set; } = "";
    /// <summary>Strefa mapy świata, w której toczy się quest (tracker/markery).</summary>
    public string Zone { get; set; } = "";
}

public sealed class QuestFile
{
    public List<QuestDefinition> Quests { get; set; } = new();
}

public static class QuestCatalog
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };
    public static readonly Dictionary<string, QuestDefinition> Quests = new();

    public static void Load(string json)
    {
        var file = JsonSerializer.Deserialize<QuestFile>(json, Opts) ?? throw new ArgumentException("pusty plik questów");
        foreach (var q in file.Quests) Quests[q.Id] = q;
    }

    public static QuestDefinition? Find(string id) => Quests.GetValueOrDefault(id);
}

/// <summary>Dziennik questów gracza: aktywne (z postępem per cel), ukończone, łańcuchy odblokowań.
/// Czysta logika — zdarzenia świata (kill/collect/reach/...) wpadają przez metody OnX.</summary>
public sealed class QuestLog
{
    /// <summary>questId → (objectiveId → postęp).</summary>
    public Dictionary<string, Dictionary<string, int>> Active { get; } = new();
    public HashSet<string> Completed { get; } = new();

    public bool IsActive(string questId) => Active.ContainsKey(questId);
    public bool IsCompleted(string questId) => Completed.Contains(questId);

    /// <summary>Czy quest jest dostępny do wzięcia (poziom + prerequisites + nie wzięty/ukończony).</summary>
    public bool CanAccept(QuestDefinition q, int playerLevel) =>
        !IsActive(q.Id) && !IsCompleted(q.Id)
        && q.RequiredLevel <= playerLevel
        && q.Prerequisites.All(Completed.Contains);

    public bool Accept(QuestDefinition q, int playerLevel)
    {
        if (!CanAccept(q, playerLevel)) return false;
        Active[q.Id] = q.Objectives.ToDictionary(o => o.Id, _ => 0);
        return true;
    }

    public int Progress(string questId, string objectiveId) =>
        Active.TryGetValue(questId, out var map) ? map.GetValueOrDefault(objectiveId) : 0;

    public bool ObjectiveDone(QuestDefinition q, ObjectiveDefinition o) =>
        Progress(q.Id, o.Id) >= o.Amount;

    /// <summary>Wszystkie cele spełnione → gotowy do oddania u TurnIn NPC.</summary>
    public bool ReadyToTurnIn(QuestDefinition q) =>
        IsActive(q.Id) && q.Objectives.All(o => ObjectiveDone(q, o));

    /// <summary>Oddanie questa. Zwraca definicję następnego w łańcuchu (albo null).</summary>
    public QuestDefinition? TurnIn(QuestDefinition q)
    {
        if (!ReadyToTurnIn(q)) return null;
        Active.Remove(q.Id);
        Completed.Add(q.Id);
        return string.IsNullOrEmpty(q.NextQuest) ? null : QuestCatalog.Find(q.NextQuest);
    }

    // ── zdarzenia świata → postęp celów (zwracają true, jeśli coś się zmieniło) ──

    public bool OnKill(string monsterId) => Bump(ObjectiveType.Kill, o => o.Target == monsterId || o.Target == "*");
    public bool OnCollect(string itemId, int count = 1) => Bump(ObjectiveType.Collect, o => o.Target == itemId, count);
    public bool OnTalk(string npcId) => Bump(ObjectiveType.Talk, o => o.Target == npcId);
    public bool OnReach(string markerId) => BumpOnce(ObjectiveType.Reach, markerId);
    public bool OnInteract(string markerId) => BumpOnce(ObjectiveType.Interact, markerId);
    public bool OnClear(string zoneId) => Bump(ObjectiveType.Clear, o => o.Target == zoneId);
    public bool OnEscortArrived(string npcId) => BumpOnce(ObjectiveType.Escort, npcId);
    public bool OnDefendWave(string markerId) => Bump(ObjectiveType.Defend, o => o.Target == markerId);
    public bool OnSurviveSeconds(string markerId, int seconds) => Bump(ObjectiveType.Survive, o => o.Target == markerId, seconds);

    /// <summary>Escort padł → cel eskorty resetuje się do zera.</summary>
    public void OnEscortFailed(string npcId)
    {
        foreach (var (questId, prog) in Active)
        {
            var q = QuestCatalog.Find(questId);
            if (q == null) continue;
            foreach (var o in q.Objectives.Where(o => o.Kind == ObjectiveType.Escort && o.Target == npcId))
                prog[o.Id] = 0;
        }
    }

    private bool BumpOnce(ObjectiveType kind, string target) =>
        Bump(kind, o => o.Target == target, bumpToMax: true);

    private bool Bump(ObjectiveType kind, Func<ObjectiveDefinition, bool> match, int count = 1, bool bumpToMax = false)
    {
        bool changed = false;
        foreach (var (questId, prog) in Active)
        {
            var q = QuestCatalog.Find(questId);
            if (q == null) continue;
            foreach (var o in q.Objectives.Where(o => o.Kind == kind && match(o)))
            {
                int cur = prog.GetValueOrDefault(o.Id);
                if (cur >= o.Amount) continue;
                prog[o.Id] = bumpToMax ? o.Amount : Math.Min(o.Amount, cur + count);
                changed = true;
            }
        }
        return changed;
    }
}

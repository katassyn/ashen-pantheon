using Godot;
using AshenPantheon.Core;

/// <summary>Ładuje content data-driven (res://data/**.json) do katalogów core. Wołane raz przy starcie.</summary>
public static class DataLoader
{
    private static bool _done;

    public static void LoadAll()
    {
        if (_done) return;
        _done = true;

        foreach (var json in ReadDir("res://data/classes")) GameData.LoadClass(json);
        foreach (var json in ReadDir("res://data/gods")) GameData.LoadGod(json);
        foreach (var json in ReadDir("res://data/trees")) GameData.LoadTrees(json);
        foreach (var json in ReadDir("res://data/loot")) LootTables.Load(json);
        foreach (var json in ReadDir("res://data/monsters")) Bestiary.LoadMonster(json);
        foreach (var json in ReadDir("res://data/zones")) Bestiary.LoadZone(json);
        foreach (var json in ReadDir("res://data/world")) WorldMaps.Load(json);

        GD.Print($"[data] potwory: {Bestiary.Monsters.Count} · strefy: {Bestiary.Zones.Count} · mapy świata: {WorldMaps.Zones.Count} · tabele lootu: {LootTables.Tables.Count}");
    }

    private static System.Collections.Generic.IEnumerable<string> ReadDir(string dirPath)
    {
        using var dir = DirAccess.Open(dirPath);
        if (dir == null)
        {
            GD.PushError($"[data] brak katalogu: {dirPath}");
            yield break;
        }
        foreach (string file in dir.GetFiles())
            if (file.EndsWith(".json"))
                yield return FileAccess.GetFileAsString($"{dirPath}/{file}");
    }
}

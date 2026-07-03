using System;
using System.IO;
using AshenPantheon.Core;

/// <summary>Ładuje PRAWDZIWE data/ repo do katalogów core (testy integracyjne buildów na realnych JSON-ach).</summary>
public static class TestData
{
    private static bool _loaded;
    private static readonly object Gate = new();

    public static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_loaded) return;
            string data = FindDataDir();
            GameData.LoadFromDirectory(data);
            foreach (var f in SafeFiles(Path.Combine(data, "world"))) WorldMaps.Load(File.ReadAllText(f));
            foreach (var f in SafeFiles(Path.Combine(data, "monsters"))) Bestiary.LoadMonster(File.ReadAllText(f));
            foreach (var f in SafeFiles(Path.Combine(data, "zones"))) Bestiary.LoadZone(File.ReadAllText(f));
            foreach (var f in SafeFiles(Path.Combine(data, "loot"))) LootTables.Load(File.ReadAllText(f));
            _loaded = true;
        }
    }

    private static string[] SafeFiles(string dir) =>
        Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json") : Array.Empty<string>();

    private static string FindDataDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "data", "classes");
            if (Directory.Exists(candidate)) return Path.Combine(dir.FullName, "data");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("nie znaleziono katalogu data/ w drzewie repo");
    }
}

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
            GameData.LoadFromDirectory(FindDataDir());
            _loaded = true;
        }
    }

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

using System.Collections.Generic;
using System.Text.Json;
using Godot;

/// <summary>Konfigurowalne klawisze paneli/interakcji + trwałe ustawienia (rozdzielczość/głośność/fullscreen)
/// zapisywane do user://settings.json. Ruch (WASD) i sloty skilli zostają na razie stałe.</summary>
public static class Keybinds
{
    private const string Path = "user://settings.json";

    /// <summary>Akcje rebindowalne w kolejności wyświetlania: id → (etykieta, domyślny klawisz).</summary>
    public static readonly (string id, string label, Key def)[] Actions =
    {
        ("move_up", "Move Up", Key.W),
        ("move_down", "Move Down", Key.S),
        ("move_left", "Move Left", Key.A),
        ("move_right", "Move Right", Key.D),
        ("slot_q", "Skill 3", Key.Q),
        ("slot_e", "Skill 4", Key.E),
        ("slot_r", "Skill 5", Key.R),
        ("stats", "Stats", Key.C),
        ("inventory", "Inventory", Key.I),
        ("skills", "Skills / Talents", Key.K),
        ("journal", "Quest Journal", Key.J),
        ("map", "World Map", Key.M),
        ("social", "Friends / Guild", Key.O),
        ("interact", "Interact", Key.E),
    };

    private static readonly Dictionary<string, Key> _keys = new();

    // ── ustawienia ──
    public static int ResolutionIndex { get; set; } = 2; // 1920x1080
    public static float Volume { get; set; } = 1f;
    public static bool Fullscreen { get; set; }

    private sealed class SettingsData
    {
        public Dictionary<string, int> Keys { get; set; } = new();
        public int ResolutionIndex { get; set; } = 2;
        public float Volume { get; set; } = 1f;
        public bool Fullscreen { get; set; }
    }

    static Keybinds() { foreach (var a in Actions) _keys[a.id] = a.def; }

    public static Key Get(string action) => _keys.TryGetValue(action, out var k) ? k : Key.None;

    public static string KeyName(string action) => OS.GetKeycodeString(Get(action));

    public static bool Matches(InputEventKey e, string action) => e.PhysicalKeycode == Get(action);

    /// <summary>Etykieta klawisza slotu paska skilli (0=LMB,1=RMB,2..4=rebindowalne).</summary>
    public static string SlotKeyName(int slot) => slot switch
    {
        0 => "LMB", 1 => "RMB",
        2 => KeyName("slot_q"), 3 => KeyName("slot_e"), 4 => KeyName("slot_r"),
        _ => "?"
    };

    public static void Rebind(string action, Key key)
    {
        if (!_keys.ContainsKey(action)) return;
        // zdejmij konflikt (ten sam klawisz na innej akcji)
        foreach (var a in Actions) if (a.id != action && _keys[a.id] == key) _keys[a.id] = Key.None;
        _keys[action] = key;
        Save();
    }

    public static void ResetDefaults()
    {
        foreach (var a in Actions) _keys[a.id] = a.def;
        Save();
    }

    public static void Load()
    {
        try
        {
            if (!FileAccess.FileExists(Path)) return;
            using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
            var data = JsonSerializer.Deserialize<SettingsData>(f.GetAsText());
            if (data == null) return;
            foreach (var a in Actions)
                if (data.Keys.TryGetValue(a.id, out int k)) _keys[a.id] = (Key)k;
            ResolutionIndex = data.ResolutionIndex;
            Volume = data.Volume;
            Fullscreen = data.Fullscreen;
        }
        catch { /* uszkodzony plik → domyślne */ }
    }

    public static void Save()
    {
        var data = new SettingsData { ResolutionIndex = ResolutionIndex, Volume = Volume, Fullscreen = Fullscreen };
        foreach (var a in Actions) data.Keys[a.id] = (int)_keys[a.id];
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        f?.StoreString(JsonSerializer.Serialize(data));
    }

    /// <summary>Zastosuj ustawienia audio/wideo (przy starcie i po zmianie).</summary>
    public static void ApplyVideoAudio()
    {
        AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(Volume));
        if (Fullscreen) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
    }
}

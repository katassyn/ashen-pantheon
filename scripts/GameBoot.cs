using System.Linq;
using Godot;
using AshenPantheon.Core;

/// <summary>Autoload: repozytorium zapisu + auto-host/join z cmdline (testy dwóch instancji lokalnie).</summary>
public partial class GameBoot : Node
{
    public override void _Ready()
    {
        DataLoader.LoadAll(); // bestiariusz/strefy/tabele lootu przed czymkolwiek
        Keybinds.Load();      // klawisze + ustawienia audio/wideo
        Keybinds.ApplyVideoAudio();

        // GUI zawsze 1:1 w pikselach (stretch wyłączony) + czytelny bazowy font + min. rozmiar okna
        ThemeDB.FallbackFontSize = 17;
        DisplayServer.WindowSetMinSize(new Vector2I(1280, 720));

        var args = OS.GetCmdlineUserArgs();
        bool join = args.Contains("--join");

        // druga instancja na tym samym PC (--join) dostaje osobny plik zapisu, żeby nie nadpisywać hosta
        string file = join ? "/save_guest.json" : "/save.json";
        string path = OS.GetUserDataDir() + file;
        GameState.Repository = new JsonGameStateRepository(path);
        GameState.LoadOrInit();
        GD.Print($"[boot] save: {path}");

        if (args.Contains("--host") || join) CallDeferred(nameof(SkipMenu)); // testy: pomiń menu główne
        if (args.Contains("--host")) CallDeferred(nameof(AutoHost));
        else if (join) CallDeferred(nameof(AutoJoin));

        if (args.Contains("--autorun"))
        {
            // test: host sam wchodzi do areny po 8 s (smoke-test replikacji co-op)
            var t = new Timer { WaitTime = 8.0, OneShot = true, Autostart = true };
            t.Timeout += () =>
            {
                if (Net.IsServer && GetTree().CurrentScene?.Name == "Hub")
                    Net.TravelAll("res://scenes/Arena.tscn", 12345);
            };
            AddChild(t);
        }
    }

    private void SkipMenu()
    {
        if (GetTree().CurrentScene?.Name == "MainMenu")
            GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
    }

    private void AutoHost()
    {
        Net.HostGame();
        GD.Print("[boot] auto-host");
    }

    private void AutoJoin()
    {
        Net.JoinGame("127.0.0.1");
        GD.Print("[boot] auto-join 127.0.0.1");
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            GameState.Save();
            if (GameState.Repository is HttpGameStateRepository http)
                http.FlushBlocking(); // dopchnij ostatni zapis na serwer przed wyjściem
            GetTree().Quit();
        }
    }
}

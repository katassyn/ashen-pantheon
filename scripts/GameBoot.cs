using System.Linq;
using Godot;
using AshenPantheon.Core;

/// <summary>Autoload: repozytorium zapisu + auto-host/join z cmdline (testy dwóch instancji lokalnie).</summary>
public partial class GameBoot : Node
{
    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        bool join = args.Contains("--join");

        // druga instancja na tym samym PC (--join) dostaje osobny plik zapisu, żeby nie nadpisywać hosta
        string file = join ? "/save_guest.json" : "/save.json";
        string path = OS.GetUserDataDir() + file;
        GameState.Repository = new JsonGameStateRepository(path);
        GameState.LoadOrInit();
        GD.Print($"[boot] save: {path}");

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
            GetTree().Quit();
        }
    }
}

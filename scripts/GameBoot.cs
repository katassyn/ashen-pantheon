using Godot;
using AshenPantheon.Core;

/// <summary>Autoload: podpina repozytorium zapisu (user://save.json), ładuje stan, zapisuje przy zamknięciu.</summary>
public partial class GameBoot : Node
{
    public override void _Ready()
    {
        string path = OS.GetUserDataDir() + "/save.json";
        GameState.Repository = new JsonGameStateRepository(path);
        GameState.LoadOrInit();
        GD.Print($"[boot] save: {path}");
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

using Godot;

/// <summary>Kontener graczy w scenie: spawnuje lokalnego gracza + puppety pozostałych peerów.
/// Nazwa node'a gracza = peer id (identyczne ścieżki RPC). Po zmianie sesji (join zmienia NASZE id!)
/// przebudowuje wszystkich graczy od zera.</summary>
public partial class NetPlayers : Node2D
{
    [Export] public bool CombatEnabled = true;

    private PackedScene _playerScene;

    public override void _Ready()
    {
        _playerScene = GD.Load<PackedScene>("res://scenes/Player.tscn");
        Rebuild();
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Net.SessionChanged += OnSessionChanged;
    }

    public override void _ExitTree()
    {
        Multiplayer.PeerConnected -= OnPeerConnected;
        Multiplayer.PeerDisconnected -= OnPeerDisconnected;
        Net.SessionChanged -= OnSessionChanged;
    }

    private void OnSessionChanged() => CallDeferred(nameof(Rebuild));

    private void OnPeerConnected(long peer) => SpawnFor((int)peer);

    private void OnPeerDisconnected(long peer) =>
        GetNodeOrNull(peer.ToString())?.QueueFree();

    private void Rebuild()
    {
        foreach (Node c in GetChildren())
        {
            c.Name = c.Name + "_old";
            c.QueueFree();
        }
        SpawnFor(Net.MyId);
        foreach (int peer in Multiplayer.GetPeers()) SpawnFor(peer);
    }

    private void SpawnFor(int peer)
    {
        if (GetNodeOrNull(peer.ToString()) != null) return;
        var p = _playerScene.Instantiate<PlayerController>();
        p.Name = peer.ToString();
        p.SetMultiplayerAuthority(peer);
        p.CombatEnabled = CombatEnabled;
        p.Position = new Vector2(60f * GetChildCount(), 40f * GetChildCount());
        AddChild(p);
        GD.Print($"[net] spawn gracza: peer {peer}{(peer == Net.MyId ? " (lokalny)" : " (puppet)")}");
    }
}

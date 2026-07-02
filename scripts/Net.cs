using System.Collections.Generic;
using Godot;
using AshenPantheon.Core;

/// <summary>Autoload sieci (co-op host-authoritative, do 4 graczy).
/// Solo = OfflineMultiplayerPeer (te same ścieżki kodu, RPC CallLocal działa lokalnie).
/// Host = autorytet walki: wrogowie żyją u hosta, klienci dostają stan; efekty skilli odpalają się
/// wszędzie (wizualnie), ale obrażenia liczy tylko serwer; loot rolowany per-gracz.</summary>
public partial class Net : Node
{
    public static Net I { get; private set; }
    public const int DefaultPort = 7777;
    public const int MaxClients = 3; // host + 3 = 4 graczy

    public static int RunSeed;
    /// <summary>Id strefy mapy świata dla WorldZone.tscn (ustawiane przy podróży grupowej).</summary>
    public static string TravelZoneId = "";
    public static string Status { get; private set; } = "offline (solo)";

    public static bool Online => I != null && I.Multiplayer.MultiplayerPeer is ENetMultiplayerPeer;
    public static bool IsServer => I == null || I.Multiplayer.IsServer();
    public static int MyId => I?.Multiplayer.GetUniqueId() ?? 1;

    private static readonly Dictionary<long, EnemyBase> EnemiesById = new();
    private static long _nextEnemyId = 1;

    /// <summary>Zmiana sesji (host/join/rozłączenie) — NetPlayers przebudowuje wtedy graczy (id peera się zmienia!).</summary>
    public static event System.Action SessionChanged;

    public override void _Ready()
    {
        I = this;
        EnsureOffline();
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += _ => { };
        Multiplayer.ConnectedToServer += () => { Status = "połączono z hostem"; SessionChanged?.Invoke(); };
        Multiplayer.ConnectionFailed += () => { Status = "połączenie nieudane"; EnsureOffline(); SessionChanged?.Invoke(); };
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    private void EnsureOffline()
    {
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
    }

    public static IEnumerable<int> AllPeers()
    {
        yield return MyId;
        if (I == null) yield break;
        foreach (int p in I.Multiplayer.GetPeers()) yield return p;
    }

    public static int PlayerCount()
    {
        int n = 1;
        if (I != null) n += I.Multiplayer.GetPeers().Length;
        return n;
    }

    // ── lobby ──

    public static bool HostGame()
    {
        var peer = new ENetMultiplayerPeer();
        if (peer.CreateServer(DefaultPort, MaxClients) != Error.Ok) { Status = "nie można otworzyć portu"; return false; }
        I.Multiplayer.MultiplayerPeer = peer;
        Status = $"HOST (port {DefaultPort})";
        return true;
    }

    public static bool JoinGame(string ip)
    {
        var peer = new ENetMultiplayerPeer();
        if (peer.CreateClient(ip, DefaultPort) != Error.Ok) { Status = "błędny adres"; return false; }
        I.Multiplayer.MultiplayerPeer = peer;
        Status = $"łączenie z {ip}...";
        return true;
    }

    public static void Leave()
    {
        I.Multiplayer.MultiplayerPeer?.Close();
        I.EnsureOffline();
        EnemiesById.Clear();
        Status = "offline (solo)";
        SessionChanged?.Invoke();
        if (I.GetTree().CurrentScene?.Name != "Hub")
            I.GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
    }

    private void OnPeerConnected(long peer)
    {
        GD.Print($"[net] peer connected: {peer} (ja: {MyId})");
        // dołączanie tylko gdy host stoi w mieście — w trakcie runu odsyłamy
        if (Multiplayer.IsServer() && GetTree().CurrentScene?.Name != "Hub")
            RpcId(peer, MethodName.RpcKick, "Host jest w trakcie runu — spróbuj za chwilę.");
    }

    private void OnServerDisconnected()
    {
        Status = "host się rozłączył";
        EnsureOffline();
        EnemiesById.Clear();
        SessionChanged?.Invoke();
        GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcKick(string reason)
    {
        Status = reason;
        Leave();
    }

    // ── podróż grupowa ──

    public static void TravelAll(string scenePath, int seed, string zoneId = "")
    {
        I.Rpc(MethodName.RpcTravel, scenePath, seed, zoneId);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcTravel(string scenePath, int seed, string zoneId)
    {
        GameState.Save();
        RunSeed = seed;
        TravelZoneId = zoneId;
        EnemiesById.Clear();
        GetTree().ChangeSceneToFile(scenePath);
    }

    // ── replikacja wrogów (serwer → klienci) ──

    public static void RegisterEnemy(EnemyBase e)
    {
        e.NetId = _nextEnemyId++;
        EnemiesById[e.NetId] = e;
        I.Rpc(MethodName.RpcSpawnEnemy, e.NetId, e.ReplicationId, e.GlobalPosition, e.HpMult, e.DmgMult);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcSpawnEnemy(long netId, string monsterId, Vector2 pos, float hpMult, float dmgMult)
    {
        GD.Print($"[net] puppet wroga #{netId} ({monsterId})");
        var e = Monster.Create(monsterId);
        e.Puppet = true;
        e.NetId = netId;
        e.HpMult = hpMult;
        e.DmgMult = dmgMult;
        e.Position = pos;
        EnemiesById[netId] = e;
        GetTree().CurrentScene?.AddChild(e);
    }

    /// <summary>Pocisk wroga: replikowany wszędzie, każda maszyna trafia tylko SWOJEGO gracza.</summary>
    public static void SpawnEnemyProjectile(Vector2 pos, Vector2 dir, float speed, float damage, DamageType type = DamageType.Physical)
    {
        I.Rpc(MethodName.RpcEnemyProjectile, pos, dir, speed, damage, (int)type);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcEnemyProjectile(Vector2 pos, Vector2 dir, float speed, float damage, int type)
    {
        var scene = GetTree().CurrentScene;
        if (scene == null) return;
        var proj = new EnemyProjectile { Direction = dir, Speed = speed, Damage = damage, DamageType = (DamageType)type };
        scene.AddChild(proj);
        proj.GlobalPosition = pos;
    }

    public static void SyncEnemy(EnemyBase e)
    {
        I.Rpc(MethodName.RpcEnemyState, e.NetId, e.GlobalPosition,
            e.HpFrac, e.StatusMask, e.IsMarked, e.Moving);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void RpcEnemyState(long netId, Vector2 pos, float hpFrac, int status, bool marked, bool moving)
    {
        if (EnemiesById.TryGetValue(netId, out var e) && IsInstanceValid(e))
            e.ApplyNetState(pos, hpFrac, status, marked, moving);
    }

    public static void DespawnEnemy(EnemyBase e, bool died)
    {
        EnemiesById.Remove(e.NetId);
        I.Rpc(MethodName.RpcDespawnEnemy, e.NetId, died);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcDespawnEnemy(long netId, bool died)
    {
        if (EnemiesById.TryGetValue(netId, out var e) && IsInstanceValid(e))
        {
            EnemiesById.Remove(netId);
            if (died) e.RemoteDie();
            else e.QueueFree();
        }
    }

    // ── efekty skilli (rzucający → wszyscy; obrażenia liczy tylko serwer) ──

    public static void SpawnEffect(string kind, ResolvedSkill skill, Color tint, Vector2 pos, Vector2 dir, float p1 = 0f)
    {
        I.Rpc(MethodName.RpcSpawnEffect, kind, SkillDto.Pack(skill, tint), pos, dir, p1);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcSpawnEffect(string kind, string skillJson, Vector2 pos, Vector2 dir, float p1)
    {
        var scene = GetTree().CurrentScene;
        if (scene == null) return;
        var (skill, tint) = SkillDto.Unpack(skillJson);

        switch (kind)
        {
            case "proj":
            {
                var proj = GD.Load<PackedScene>("res://scenes/Projectile.tscn").Instantiate<Projectile>();
                proj.Setup(skill, dir, tint);
                scene.AddChild(proj);
                proj.GlobalPosition = pos;
                break;
            }
            case "rain":
            {
                var zone = new GroundZone();
                zone.Setup(skill, p1);
                scene.AddChild(zone);
                zone.GlobalPosition = pos;
                break;
            }
            case "mine":
            {
                var mine = new Mine();
                mine.Setup(skill);
                scene.AddChild(mine);
                mine.GlobalPosition = pos;
                break;
            }
            case "hedge":
            {
                var hedge = new HedgeZone();
                scene.AddChild(hedge);
                hedge.GlobalPosition = pos;
                hedge.Setup(skill, dir, p1);
                break;
            }
            case "hawk":
            {
                var hawk = new Hawk();
                hawk.Setup(skill, p1 > 0.5f);
                scene.AddChild(hawk);
                hawk.GlobalPosition = pos;
                break;
            }
            case "pet":
            {
                var pet = new Pet { Damage = skill.Damage, CasterPeer = skill.CasterPeer };
                scene.AddChild(pet);
                pet.GlobalPosition = pos;
                break;
            }
        }
    }

    /// <summary>Telegraf bossa: każda maszyna symuluje go dla SWOJEGO gracza (obrażenia lokalne).</summary>
    public static void SpawnTelegraph(int shape, float radius, float halfAngleDeg, float halfWidth, float damage, Vector2 pos, float rot, DamageType type = DamageType.Physical)
    {
        I.Rpc(MethodName.RpcSpawnTelegraph, shape, radius, halfAngleDeg, halfWidth, damage, pos, rot, (int)type);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcSpawnTelegraph(int shape, float radius, float halfAngleDeg, float halfWidth, float damage, Vector2 pos, float rot, int type)
    {
        var scene = GetTree().CurrentScene;
        if (scene == null) return;
        var tg = new Telegraph
        {
            Shape = (TelegraphShape)shape, Radius = radius,
            HalfAngleDeg = halfAngleDeg, HalfWidth = halfWidth, Damage = damage,
            DamageType = (DamageType)type,
        };
        scene.AddChild(tg);
        tg.GlobalPosition = pos;
        tg.Rotation = rot;
    }

    // ── gracze: obrażenia / heale / śmierć / XP / loot ──

    public static void DamagePlayer(PlayerController target, float amount, DamageType type = DamageType.Physical)
    {
        int owner = target.GetMultiplayerAuthority();
        if (owner == MyId) target.TakeDamage(amount, type);
        else I.RpcId(owner, MethodName.RpcDamageLocal, amount, (int)type);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcDamageLocal(float amount, int type) =>
        PlayerController.Local?.TakeDamage(amount, (DamageType)type);

    public static void HealCaster(int casterPeer, float amount)
    {
        if (casterPeer == MyId) PlayerController.Local?.Heal(amount);
        else I.RpcId(casterPeer, MethodName.RpcHealLocal, amount);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcHealLocal(float amount) => PlayerController.Local?.Heal(amount);

    /// <summary>Odrodzenie poległych po oczyszczeniu pokoju (każda maszyna wskrzesza swojego gracza).</summary>
    public static void ReviveAll(float healthFraction) => I.Rpc(MethodName.RpcRevive, healthFraction);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcRevive(float healthFraction) => PlayerController.Local?.Revive(healthFraction);

    public static void GrantXpAll(long amount) => I.Rpc(MethodName.RpcGrantXp, amount);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcGrantXp(long amount)
    {
        GameState.Progress.GainXp(amount);
    }

    /// <summary>Loot instancjonowany: pickup istnieje tylko na maszynie obdarowanego gracza.</summary>
    public static void GivePickup(int peer, Item item, Vector2 pos)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(ItemMapper.ToDto(item));
        if (peer == MyId) I.SpawnPickupLocal(json, pos);
        else I.RpcId(peer, MethodName.RpcSpawnPickup, json, pos);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcSpawnPickup(string itemJson, Vector2 pos) => SpawnPickupLocal(itemJson, pos);

    private void SpawnPickupLocal(string itemJson, Vector2 pos)
    {
        var dto = System.Text.Json.JsonSerializer.Deserialize<ItemDto>(itemJson);
        if (dto == null || GetTree().CurrentScene == null) return;
        ItemPickup.Spawn(GetTree().CurrentScene, pos, ItemMapper.FromDto(dto));
    }

    /// <summary>Złoto instancjonowane per-gracz (jak itemy).</summary>
    public static void GiveGold(int peer, long amount, Vector2 pos)
    {
        if (peer == MyId) I.SpawnGoldLocal(amount, pos);
        else I.RpcId(peer, MethodName.RpcSpawnGold, amount, pos);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcSpawnGold(long amount, Vector2 pos) => SpawnGoldLocal(amount, pos);

    private void SpawnGoldLocal(long amount, Vector2 pos)
    {
        var scene = GetTree().CurrentScene;
        if (scene == null) return;
        var gold = new GoldPickup { Amount = amount };
        scene.AddChild(gold);
        gold.GlobalPosition = pos;
    }

    public static void NotifyPlayerDied()
    {
        if (IsServer) I.RpcPlayerDied((long)MyId);
        else I.RpcId(1, MethodName.RpcPlayerDied, (long)MyId);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcPlayerDied(long peer)
    {
        if (!Multiplayer.IsServer()) return;
        if (GetTree().GetFirstNodeInGroup("arena") is ArenaManager arena)
            arena.PlayerDied((int)peer);
    }

    // ── status areny (serwer → wszyscy) ──

    public static void BroadcastArenaStatus(string top, string center)
    {
        I.Rpc(MethodName.RpcArenaStatus, top, center);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcArenaStatus(string top, string center)
    {
        if (GetTree().GetFirstNodeInGroup("arena") is ArenaManager arena)
            arena.SetStatusRemote(top, center);
    }

    public static void StartRoomAll(int index) => I.Rpc(MethodName.RpcStartRoom, index);

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcStartRoom(int index)
    {
        if (GetTree().GetFirstNodeInGroup("arena") is ArenaManager arena)
            arena.OnRoomStartedRemote(index);
    }
}

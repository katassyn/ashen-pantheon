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
        Multiplayer.ConnectedToServer += () => { Status = "connected to host"; SessionChanged?.Invoke(); };
        Multiplayer.ConnectionFailed += () => { Status = "connection failed"; EnsureOffline(); SessionChanged?.Invoke(); };
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
        if (peer.CreateServer(DefaultPort, MaxClients) != Error.Ok) { Status = "cannot open port"; return false; }
        I.Multiplayer.MultiplayerPeer = peer;
        Status = $"HOST (port {DefaultPort})";
        return true;
    }

    public static bool JoinGame(string ip)
    {
        var peer = new ENetMultiplayerPeer();
        if (peer.CreateClient(ip, DefaultPort) != Error.Ok) { Status = "invalid address"; return false; }
        I.Multiplayer.MultiplayerPeer = peer;
        Status = $"connecting to {ip}...";
        return true;
    }

    public static void Leave(bool goHub = true)
    {
        I.Multiplayer.MultiplayerPeer?.Close();
        I.EnsureOffline();
        EnemiesById.Clear();
        Status = "offline (solo)";
        SessionChanged?.Invoke();
        if (goHub && I.GetTree().CurrentScene?.Name != "Hub")
            I.GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
    }

    private void OnPeerConnected(long peer)
    {
        GD.Print($"[net] peer connected: {peer} (ja: {MyId})");
        AnnounceName(); // nowy peer (i my) poznajemy nawzajem nicki
        // dołączanie tylko gdy host stoi w mieście — w trakcie runu odsyłamy
        if (Multiplayer.IsServer() && GetTree().CurrentScene?.Name != "Hub")
            RpcId(peer, MethodName.RpcKick, "Host is mid-run — try again in a moment.");
    }

    // ── nicki graczy (social: party frame, czat, handel) ──

    public static readonly Dictionary<long, string> PlayerNames = new();

    public static string NameOf(long peer) =>
        PlayerNames.TryGetValue(peer, out var n) ? n : $"Player #{peer}";

    public static void AnnounceName()
    {
        PlayerNames[MyId] = GameState.CharacterName;
        if (Online) I.Rpc(MethodName.RpcAnnounceName, MyId, GameState.CharacterName);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcAnnounceName(long peer, string name) => PlayerNames[peer] = name;

    // ── czat co-op ──

    public static event System.Action<string> ChatReceived;

    public static void SendChat(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        if (text.Length > 200) text = text[..200];
        if (Online) I.Rpc(MethodName.RpcChat, MyId, text);
        else ChatReceived?.Invoke($"{GameState.CharacterName}: {text}");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcChat(long peer, string text) => ChatReceived?.Invoke($"{NameOf(peer)}: {text}");

    /// <summary>Komunikat tylko lokalny (toasty systemowe w logu czatu).</summary>
    public static void SendChatLocal(string text) => ChatReceived?.Invoke($"» {text}");

    // ── handel gracz-gracz (P2P; escrow lokalny + dwustronny confirm) ──
    // Uwaga: pełne anti-dupe wymaga meta-serwera; tu lobby prywatne (zaufani gracze).

    public static event System.Action<long> TradeRequested;        // fromPeer
    public static event System.Action<long> TradeAccepted;         // partner
    public static event System.Action<long> TradeDeclined;         // partner
    public static event System.Action<string, long> TradeOffer;    // partner offer: itemsJson, gold
    public static event System.Action<bool> TradePartnerConfirm;
    public static event System.Action TradeCancelled;

    public static void TradeRequest(long partner) => I.RpcId(partner, MethodName.RpcTradeReq, MyId);
    public static void TradeAccept(long partner) => I.RpcId(partner, MethodName.RpcTradeAcc, MyId);
    public static void TradeDecline(long partner) => I.RpcId(partner, MethodName.RpcTradeDec, MyId);
    public static void TradeSendOffer(long partner, string itemsJson, long gold) => I.RpcId(partner, MethodName.RpcTradeOffer, itemsJson, gold);
    public static void TradeSendConfirm(long partner, bool on) => I.RpcId(partner, MethodName.RpcTradeConfirm, on);
    public static void TradeSendCancel(long partner) => I.RpcId(partner, MethodName.RpcTradeCancel);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcTradeReq(long from) => TradeRequested?.Invoke(from);
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcTradeAcc(long from) => TradeAccepted?.Invoke(from);
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcTradeDec(long from) => TradeDeclined?.Invoke(from);
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcTradeOffer(string itemsJson, long gold) => TradeOffer?.Invoke(itemsJson, gold);
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcTradeConfirm(bool on) => TradePartnerConfirm?.Invoke(on);
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcTradeCancel() => TradeCancelled?.Invoke();

    private void OnServerDisconnected()
    {
        Status = "host disconnected";
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
    // ── questy: zdarzenia świata liczone u KAŻDEGO gracza (party-share jak w DSO) ──

    public static void BroadcastQuestKill(string monsterId)
    {
        if (Online) I.Rpc(MethodName.RpcQuestKill, monsterId);
        else QuestKillLocal(monsterId);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcQuestKill(string monsterId) => QuestKillLocal(monsterId);

    private static void QuestKillLocal(string monsterId)
    {
        if (GameState.Quests.OnKill(monsterId)) GameState.Save();
    }

    public static void BroadcastQuestCollect(string itemId)
    {
        if (Online) I.Rpc(MethodName.RpcQuestCollect, itemId);
        else QuestCollectLocal(itemId);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcQuestCollect(string itemId) => QuestCollectLocal(itemId);

    private static void QuestCollectLocal(string itemId)
    {
        if (GameState.Quests.OnCollect(itemId)) GameState.Save();
    }

    public static void BroadcastQuestClear(string zoneId)
    {
        if (Online) I.Rpc(MethodName.RpcQuestClear, zoneId);
        else QuestClearLocal(zoneId);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcQuestClear(string zoneId) => QuestClearLocal(zoneId);

    private static void QuestClearLocal(string zoneId)
    {
        if (GameState.Quests.OnClear(zoneId)) GameState.Save();
    }

    public static void BroadcastEscortArrived(string markerId)
    {
        if (Online) I.Rpc(MethodName.RpcEscortArrived, markerId);
        else { if (GameState.Quests.OnEscortArrived(markerId)) GameState.Save(); }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcEscortArrived(string markerId)
    {
        if (GameState.Quests.OnEscortArrived(markerId)) GameState.Save();
    }

    public static void BroadcastEscortFailed(string markerId)
    {
        if (Online) I.Rpc(MethodName.RpcEscortFailed, markerId);
        else GameState.Quests.OnEscortFailed(markerId);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcEscortFailed(string markerId) => GameState.Quests.OnEscortFailed(markerId);

    public static void BroadcastDefendWave(string markerId)
    {
        if (Online) I.Rpc(MethodName.RpcDefendWave, markerId);
        else { if (GameState.Quests.OnDefendWave(markerId)) GameState.Save(); }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcDefendWave(string markerId)
    {
        if (GameState.Quests.OnDefendWave(markerId)) GameState.Save();
    }

    public static void BroadcastSurviveSecond(string markerId)
    {
        if (Online) I.Rpc(MethodName.RpcSurviveSecond, markerId);
        else GameState.Quests.OnSurviveSeconds(markerId, 1);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcSurviveSecond(string markerId) => GameState.Quests.OnSurviveSeconds(markerId, 1);

    /// <summary>Pozycja+HP eskortowanego NPC (host → klienci; best-effort).</summary>
    public static void SyncEscort(Vector2 pos, float hpFrac, bool moving)
    {
        if (Online) I.Rpc(MethodName.RpcEscortState, pos, hpFrac, moving);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void RpcEscortState(Vector2 pos, float hpFrac, bool moving)
    {
        foreach (Node n in GetTree().GetNodesInGroup("escort"))
            if (n is EscortNpc e) e.ApplyNetState(pos, hpFrac, moving);
    }

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

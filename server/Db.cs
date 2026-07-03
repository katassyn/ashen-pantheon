using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace AshenPantheon.Server;

/// <summary>Warstwa danych meta-serwera. SQLite na start (zero-instalacji);
/// migracja na Postgres = podmiana tej klasy, kontrakt endpointów zostaje.</summary>
public sealed class Db
{
    private readonly string _cs;

    public Db(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _cs = $"Data Source={path}";
        using var c = Open();
        Exec(c, """
            CREATE TABLE IF NOT EXISTS accounts(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL UNIQUE COLLATE NOCASE,
                pass_hash TEXT NOT NULL,
                pass_salt TEXT NOT NULL,
                created_at TEXT NOT NULL DEFAULT (datetime('now')));
            CREATE TABLE IF NOT EXISTS tokens(
                token TEXT PRIMARY KEY,
                account_id INTEGER NOT NULL,
                expires_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS characters(
                account_id INTEGER PRIMARY KEY,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL DEFAULT (datetime('now')));
            CREATE TABLE IF NOT EXISTS friend_requests(
                from_id INTEGER NOT NULL, to_id INTEGER NOT NULL,
                PRIMARY KEY(from_id, to_id));
            CREATE TABLE IF NOT EXISTS friendships(
                a_id INTEGER NOT NULL, b_id INTEGER NOT NULL,
                PRIMARY KEY(a_id, b_id));
            CREATE TABLE IF NOT EXISTS guilds(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE COLLATE NOCASE,
                leader_id INTEGER NOT NULL,
                created_at TEXT NOT NULL DEFAULT (datetime('now')));
            CREATE TABLE IF NOT EXISTS guild_members(
                account_id INTEGER PRIMARY KEY,
                guild_id INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS guild_invites(
                guild_id INTEGER NOT NULL, account_id INTEGER NOT NULL,
                PRIMARY KEY(guild_id, account_id));
            """);
    }

    // ── pomocnicze ──

    private long? IdByName(SqliteConnection c, string username)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id FROM accounts WHERE username=$u";
        cmd.Parameters.AddWithValue("$u", username);
        return cmd.ExecuteScalar() is long id ? id : null;
    }

    private static List<string> Names(SqliteConnection c, string sql, params (string, object)[] args)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v);
        var list = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    // ── znajomi (wzajemni, z zaproszeniami) ──

    public (bool Ok, string Error) SendFriendRequest(long fromId, string toName)
    {
        using var c = Open();
        var toId = IdByName(c, toName);
        if (toId == null) return (false, "no such player");
        if (toId == fromId) return (false, "that's you");
        using var exists = c.CreateCommand();
        exists.CommandText = "SELECT 1 FROM friendships WHERE a_id=$a AND b_id=$b";
        exists.Parameters.AddWithValue("$a", fromId); exists.Parameters.AddWithValue("$b", toId.Value);
        if (exists.ExecuteScalar() != null) return (false, "already friends");
        Exec(c, "INSERT OR IGNORE INTO friend_requests(from_id,to_id) VALUES($f,$t)", ("$f", fromId), ("$t", toId.Value));
        return (true, "");
    }

    public (List<string> Friends, List<string> Requests) FriendsView(long meId)
    {
        using var c = Open();
        var friends = Names(c, "SELECT username FROM accounts JOIN friendships ON b_id=id WHERE a_id=$m ORDER BY username", ("$m", meId));
        var requests = Names(c, "SELECT username FROM accounts JOIN friend_requests ON from_id=id WHERE to_id=$m ORDER BY username", ("$m", meId));
        return (friends, requests);
    }

    public (bool Ok, string Error) AcceptFriend(long meId, string fromName)
    {
        using var c = Open();
        var fromId = IdByName(c, fromName);
        if (fromId == null) return (false, "no such player");
        using var req = c.CreateCommand();
        req.CommandText = "SELECT 1 FROM friend_requests WHERE from_id=$f AND to_id=$m";
        req.Parameters.AddWithValue("$f", fromId.Value); req.Parameters.AddWithValue("$m", meId);
        if (req.ExecuteScalar() == null) return (false, "no pending request");
        Exec(c, "DELETE FROM friend_requests WHERE from_id=$f AND to_id=$m", ("$f", fromId.Value), ("$m", meId));
        Exec(c, "INSERT OR IGNORE INTO friendships(a_id,b_id) VALUES($a,$b),($b,$a)", ("$a", meId), ("$b", fromId.Value));
        return (true, "");
    }

    public void RemoveFriend(long meId, string otherName)
    {
        using var c = Open();
        var other = IdByName(c, otherName);
        if (other == null) return;
        Exec(c, "DELETE FROM friendships WHERE (a_id=$m AND b_id=$o) OR (a_id=$o AND b_id=$m)", ("$m", meId), ("$o", other.Value));
        Exec(c, "DELETE FROM friend_requests WHERE (from_id=$m AND to_id=$o) OR (from_id=$o AND to_id=$m)", ("$m", meId), ("$o", other.Value));
    }

    // ── guildie (1 na konto; lider) ──

    private long? GuildIdOf(SqliteConnection c, long accountId)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT guild_id FROM guild_members WHERE account_id=$a";
        cmd.Parameters.AddWithValue("$a", accountId);
        return cmd.ExecuteScalar() is long g ? g : null;
    }

    public (bool Ok, string Error) CreateGuild(long leaderId, string name)
    {
        if (name.Length is < 3 or > 24) return (false, "name 3-24 chars");
        using var c = Open();
        if (GuildIdOf(c, leaderId) != null) return (false, "you're already in a guild");
        try
        {
            Exec(c, "INSERT INTO guilds(name, leader_id) VALUES($n,$l)", ("$n", name), ("$l", leaderId));
            using var idc = c.CreateCommand();
            idc.CommandText = "SELECT last_insert_rowid()";
            long gid = (long)idc.ExecuteScalar()!;
            Exec(c, "INSERT INTO guild_members(account_id, guild_id) VALUES($a,$g)", ("$a", leaderId), ("$g", gid));
            return (true, "");
        }
        catch (SqliteException) { return (false, "name taken"); }
    }

    public (bool Ok, string Error) InviteToGuild(long inviterId, string targetName)
    {
        using var c = Open();
        var gid = GuildIdOf(c, inviterId);
        if (gid == null) return (false, "you're not in a guild");
        var targetId = IdByName(c, targetName);
        if (targetId == null) return (false, "no such player");
        if (GuildIdOf(c, targetId.Value) != null) return (false, "player already in a guild");
        Exec(c, "INSERT OR IGNORE INTO guild_invites(guild_id, account_id) VALUES($g,$a)", ("$g", gid.Value), ("$a", targetId.Value));
        return (true, "");
    }

    public (bool Ok, string Error) AcceptGuildInvite(long meId, long guildId)
    {
        using var c = Open();
        if (GuildIdOf(c, meId) != null) return (false, "you're already in a guild");
        using var inv = c.CreateCommand();
        inv.CommandText = "SELECT 1 FROM guild_invites WHERE guild_id=$g AND account_id=$a";
        inv.Parameters.AddWithValue("$g", guildId); inv.Parameters.AddWithValue("$a", meId);
        if (inv.ExecuteScalar() == null) return (false, "no invite");
        Exec(c, "INSERT INTO guild_members(account_id, guild_id) VALUES($a,$g)", ("$a", meId), ("$g", guildId));
        Exec(c, "DELETE FROM guild_invites WHERE account_id=$a", ("$a", meId));
        return (true, "");
    }

    public void LeaveGuild(long meId)
    {
        using var c = Open();
        var gid = GuildIdOf(c, meId);
        if (gid == null) return;
        using var lq = c.CreateCommand();
        lq.CommandText = "SELECT leader_id FROM guilds WHERE id=$g";
        lq.Parameters.AddWithValue("$g", gid.Value);
        bool isLeader = lq.ExecuteScalar() is long l && l == meId;
        if (isLeader) // lider odchodzi → rozwiązanie gildii
        {
            Exec(c, "DELETE FROM guild_members WHERE guild_id=$g", ("$g", gid.Value));
            Exec(c, "DELETE FROM guild_invites WHERE guild_id=$g", ("$g", gid.Value));
            Exec(c, "DELETE FROM guilds WHERE id=$g", ("$g", gid.Value));
        }
        else Exec(c, "DELETE FROM guild_members WHERE account_id=$a", ("$a", meId));
    }

    public (string? Name, bool IsLeader, List<(string Name, bool Leader)> Members, List<(long Id, string Name)> Invites) GuildView(long meId)
    {
        using var c = Open();
        var gid = GuildIdOf(c, meId);
        var invites = new List<(long, string)>();
        using (var iv = c.CreateCommand())
        {
            iv.CommandText = "SELECT g.id, g.name FROM guild_invites gi JOIN guilds g ON g.id=gi.guild_id WHERE gi.account_id=$a";
            iv.Parameters.AddWithValue("$a", meId);
            using var r = iv.ExecuteReader();
            while (r.Read()) invites.Add((r.GetInt64(0), r.GetString(1)));
        }
        if (gid == null) return (null, false, new(), invites);

        string gname; long leaderId;
        using (var gq = c.CreateCommand())
        {
            gq.CommandText = "SELECT name, leader_id FROM guilds WHERE id=$g";
            gq.Parameters.AddWithValue("$g", gid.Value);
            using var r = gq.ExecuteReader();
            r.Read();
            gname = r.GetString(0); leaderId = r.GetInt64(1);
        }
        var members = new List<(string, bool)>();
        using (var mq = c.CreateCommand())
        {
            mq.CommandText = "SELECT a.id, a.username FROM guild_members m JOIN accounts a ON a.id=m.account_id WHERE m.guild_id=$g ORDER BY a.username";
            mq.Parameters.AddWithValue("$g", gid.Value);
            using var r = mq.ExecuteReader();
            while (r.Read()) members.Add((r.GetString(1), r.GetInt64(0) == leaderId));
        }
        return (gname, leaderId == meId, members, invites);
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_cs);
        c.Open();
        return c;
    }

    private static void Exec(SqliteConnection c, string sql, params (string, object)[] args)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v);
        cmd.ExecuteNonQuery();
    }

    // ── hasła: PBKDF2 ──

    private static (string Hash, string Salt) HashPassword(string password, byte[]? salt = null)
    {
        salt ??= RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToHexString(hash), Convert.ToHexString(salt));
    }

    public (bool Ok, string Error) Register(string username, string password)
    {
        if (username.Length is < 3 or > 24) return (false, "nazwa 3–24 znaki");
        if (password.Length < 6) return (false, "hasło min 6 znaków");
        var (hash, salt) = HashPassword(password);
        using var c = Open();
        try
        {
            Exec(c, "INSERT INTO accounts(username, pass_hash, pass_salt) VALUES($u,$h,$s)",
                ("$u", username), ("$h", hash), ("$s", salt));
            return (true, "");
        }
        catch (SqliteException)
        {
            return (false, "nazwa zajęta");
        }
    }

    public long? Authenticate(string username, string password)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, pass_hash, pass_salt FROM accounts WHERE username=$u";
        cmd.Parameters.AddWithValue("$u", username);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        long id = r.GetInt64(0);
        var (hash, _) = HashPassword(password, Convert.FromHexString(r.GetString(2)));
        return string.Equals(hash, r.GetString(1), StringComparison.OrdinalIgnoreCase) ? id : null;
    }

    public string IssueToken(long accountId)
    {
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        using var c = Open();
        Exec(c, "INSERT INTO tokens(token, account_id, expires_at) VALUES($t,$a,datetime('now','+7 days'))",
            ("$t", token), ("$a", accountId));
        return token;
    }

    public long? ResolveToken(string token)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT account_id FROM tokens WHERE token=$t AND expires_at > datetime('now')";
        cmd.Parameters.AddWithValue("$t", token);
        var result = cmd.ExecuteScalar();
        return result == null ? null : (long)result;
    }

    public string? LoadCharacter(long accountId)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json FROM characters WHERE account_id=$a";
        cmd.Parameters.AddWithValue("$a", accountId);
        return cmd.ExecuteScalar() as string;
    }

    public void SaveCharacter(long accountId, string json)
    {
        using var c = Open();
        Exec(c, """
            INSERT INTO characters(account_id, json, updated_at) VALUES($a,$j,datetime('now'))
            ON CONFLICT(account_id) DO UPDATE SET json=$j, updated_at=datetime('now')
            """, ("$a", accountId), ("$j", json));
    }
}

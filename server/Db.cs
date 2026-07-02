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
            """);
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

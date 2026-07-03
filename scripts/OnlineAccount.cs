using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AshenPantheon.Core;

/// <summary>Sesja konta online (meta-serwer). Gra działa też w pełni offline (lokalny JSON).</summary>
public static class AccountSession
{
    public static string ServerUrl = "http://127.0.0.1:8080";
    public static string? Token;
    public static string? Username;
    public static bool LoggedIn => Token != null;
}

/// <summary>Klient HTTP meta-serwera: rejestracja/logowanie (kontrakt: POST /auth/*, GET/PUT /character).</summary>
public static class AccountClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };

    /// <summary>Blokujące (wywoływane z przycisków UI; Task.Run → bez deadlocku na main thread).</summary>
    public static (bool Ok, string Message) RegisterOrLogin(string endpoint, string username, string password)
    {
        try
        {
            return Task.Run(async () =>
            {
                var body = new StringContent(
                    JsonSerializer.Serialize(new { Username = username, Password = password }),
                    Encoding.UTF8, "application/json");
                var resp = await Http.PostAsync($"{AccountSession.ServerUrl}{endpoint}", body);
                string text = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    return (false, $"error: {(int)resp.StatusCode} {Short(text)}");

                using var doc = JsonDocument.Parse(text);
                AccountSession.Token = doc.RootElement.GetProperty("Token").GetString();
                AccountSession.Username = username;
                return (true, $"zalogowano: {username}");
            }).Result;
        }
        catch (Exception e)
        {
            return (false, $"server unavailable ({Short(e.InnerException?.Message ?? e.Message)})");
        }
    }

    private static string Short(string s) => s.Length > 60 ? s[..60] : s;

    // ── social: znajomi + guildie (wymaga zalogowania online) ──

    private static HttpRequestMessage Authed(HttpMethod m, string path, object? body = null)
    {
        var req = new HttpRequestMessage(m, $"{AccountSession.ServerUrl}{path}");
        req.Headers.Add("Authorization", $"Bearer {AccountSession.Token}");
        if (body != null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return req;
    }

    /// <summary>POST z ciałem {Username} lub {GuildId}; zwraca (ok, komunikat błędu/"" ).</summary>
    public static (bool Ok, string Error) Post(string path, object body)
    {
        if (!AccountSession.LoggedIn) return (false, "not logged in");
        try
        {
            return Task.Run(async () =>
            {
                var resp = await Http.SendAsync(Authed(HttpMethod.Post, path, body));
                if (resp.IsSuccessStatusCode) return (true, "");
                string text = await resp.Content.ReadAsStringAsync();
                string err = text;
                try { using var d = JsonDocument.Parse(text); err = d.RootElement.GetProperty("Error").GetString() ?? text; } catch { }
                return (false, err);
            }).Result;
        }
        catch (Exception e) { return (false, $"server unavailable ({Short(e.Message)})"); }
    }

    /// <summary>GET zwracający surowy JSON (parsowany przez panel).</summary>
    public static string? GetJson(string path)
    {
        if (!AccountSession.LoggedIn) return null;
        try
        {
            return Task.Run(async () =>
            {
                var resp = await Http.SendAsync(Authed(HttpMethod.Get, path));
                return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync() : null;
            }).Result;
        }
        catch { return null; }
    }
}

/// <summary>Repozytorium postaci na meta-serwerze. Load blokujący (moment logowania);
/// Save asynchroniczny last-wins (nie zamraża gry); FlushBlocking przy zamknięciu.</summary>
public sealed class HttpGameStateRepository : IGameStateRepository
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
    private SaveData? _pending;
    private int _pushing;

    private HttpRequestMessage Req(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, $"{AccountSession.ServerUrl}{path}");
        req.Headers.Add("Authorization", $"Bearer {AccountSession.Token}");
        return req;
    }

    public SaveData? Load()
    {
        return Task.Run(async () =>
        {
            var resp = await Http.SendAsync(Req(HttpMethod.Get, "/character"));
            if (!resp.IsSuccessStatusCode) return null;
            string json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SaveData>(json, JsonGameStateRepository.Options);
        }).Result;
    }

    public void Save(SaveData data)
    {
        _pending = data; // last-wins
        if (Interlocked.CompareExchange(ref _pushing, 1, 0) != 0) return;
        Task.Run(async () =>
        {
            try
            {
                while (_pending is { } toSend)
                {
                    _pending = null;
                    await PushAsync(toSend);
                }
            }
            catch { /* brak sieci → kolejny Save spróbuje ponownie */ }
            finally { Interlocked.Exchange(ref _pushing, 0); }
        });
    }

    /// <summary>Blokujący zapis przy zamykaniu gry (żeby ostatni stan nie przepadł).</summary>
    public void FlushBlocking()
    {
        var data = _pending;
        if (data == null) return;
        _pending = null;
        try { Task.Run(() => PushAsync(data)).Wait(TimeSpan.FromSeconds(3)); } catch { }
    }

    private async Task PushAsync(SaveData data)
    {
        var req = Req(HttpMethod.Put, "/character");
        req.Content = new StringContent(
            JsonSerializer.Serialize(data, JsonGameStateRepository.Options),
            Encoding.UTF8, "application/json");
        await Http.SendAsync(req);
    }
}

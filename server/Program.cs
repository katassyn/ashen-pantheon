using System.Text.Json;
using AshenPantheon.Core;
using AshenPantheon.Server;

// ── Ashen Pantheon meta-serwer: konta + postacie (kontrakt SaveData z core/) ──
// Uruchomienie: dotnet run --project server  (port 8080; --urls http://0.0.0.0:8080)

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = null; // PascalCase — zgodnie z klientem/zapisem lokalnym
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

var app = builder.Build();
var db = new Db(Path.Combine(AppContext.BaseDirectory, "db", "ashen.db"));

// katalogi buildów (klasy/drzewka) — walidator zapisu zna dozwolone skille i węzły
GameData.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "data"));

long? Auth(HttpRequest req)
{
    string? h = req.Headers.Authorization.FirstOrDefault();
    if (h == null || !h.StartsWith("Bearer ")) return null;
    return db.ResolveToken(h["Bearer ".Length..].Trim());
}

app.MapGet("/health", () => Results.Text("ok"));

app.MapPost("/auth/register", (Creds creds) =>
{
    var (ok, error) = db.Register(creds.Username, creds.Password);
    if (!ok) return Results.BadRequest(new { Error = error });
    long id = db.Authenticate(creds.Username, creds.Password)!.Value;
    return Results.Ok(new { Token = db.IssueToken(id) });
});

app.MapPost("/auth/login", (Creds creds) =>
{
    long? id = db.Authenticate(creds.Username, creds.Password);
    if (id == null) return Results.Unauthorized();
    return Results.Ok(new { Token = db.IssueToken(id.Value) });
});

app.MapGet("/character", (HttpRequest req) =>
{
    long? account = Auth(req);
    if (account == null) return Results.Unauthorized();
    string? json = db.LoadCharacter(account.Value);
    return json == null ? Results.NotFound() : Results.Text(json, "application/json");
});

app.MapPut("/character", async (HttpRequest req) =>
{
    long? account = Auth(req);
    if (account == null) return Results.Unauthorized();

    string body = await new StreamReader(req.Body).ReadToEndAsync();
    SaveData? data;
    try
    {
        data = JsonSerializer.Deserialize<SaveData>(body, JsonGameStateRepository.Options);
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { Error = "niepoprawny JSON" });
    }
    if (data == null) return Results.BadRequest(new { Error = "pusty zapis" });

    // ochrona ekonomii: te same reguły co generator lootu (core/SaveValidator)
    var (ok, error) = SaveValidator.Validate(data);
    if (!ok) return Results.BadRequest(new { Error = error });

    db.SaveCharacter(account.Value, JsonSerializer.Serialize(data, JsonGameStateRepository.Options));
    return Results.Ok();
});

// ── znajomi ──

app.MapGet("/friends", (HttpRequest req) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var (friends, requests) = db.FriendsView(me.Value);
    return Results.Json(new { Friends = friends, Requests = requests });
});

app.MapPost("/friends/request", (HttpRequest req, NameBody b) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var (ok, err) = db.SendFriendRequest(me.Value, b.Username);
    return ok ? Results.Ok() : Results.BadRequest(new { Error = err });
});

app.MapPost("/friends/accept", (HttpRequest req, NameBody b) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var (ok, err) = db.AcceptFriend(me.Value, b.Username);
    return ok ? Results.Ok() : Results.BadRequest(new { Error = err });
});

app.MapPost("/friends/remove", (HttpRequest req, NameBody b) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    db.RemoveFriend(me.Value, b.Username);
    return Results.Ok();
});

// ── guildie ──

app.MapGet("/guild", (HttpRequest req) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var v = db.GuildView(me.Value);
    return Results.Json(new
    {
        Guild = v.Name == null ? null : new
        {
            v.Name, v.IsLeader,
            Members = v.Members.Select(m => new { m.Name, m.Leader }),
        },
        Invites = v.Invites.Select(i => new { i.Id, i.Name }),
    });
});

app.MapPost("/guild/create", (HttpRequest req, NameBody b) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var (ok, err) = db.CreateGuild(me.Value, b.Username);
    return ok ? Results.Ok() : Results.BadRequest(new { Error = err });
});

app.MapPost("/guild/invite", (HttpRequest req, NameBody b) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var (ok, err) = db.InviteToGuild(me.Value, b.Username);
    return ok ? Results.Ok() : Results.BadRequest(new { Error = err });
});

app.MapPost("/guild/accept", (HttpRequest req, GuildBody b) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var (ok, err) = db.AcceptGuildInvite(me.Value, b.GuildId);
    return ok ? Results.Ok() : Results.BadRequest(new { Error = err });
});

app.MapPost("/guild/leave", (HttpRequest req) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    db.LeaveGuild(me.Value);
    return Results.Ok();
});

app.Run("http://0.0.0.0:8080");

record Creds(string Username, string Password);
record NameBody(string Username);
record GuildBody(long GuildId);

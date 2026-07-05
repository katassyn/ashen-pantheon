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
    long? id = db.ResolveToken(h["Bearer ".Length..].Trim());
    if (id != null) db.Touch(id.Value); // presence za darmo przy każdym żądaniu
    return id;
}

// walidacja itemu z załącznika/ogłoszenia — te same reguły co zapis postaci (anty-fabrykacja)
(bool Ok, string Error) CheckItemJson(string? itemJson)
{
    if (string.IsNullOrEmpty(itemJson)) return (true, "");
    ItemDto? dto;
    try { dto = JsonSerializer.Deserialize<ItemDto>(itemJson, JsonGameStateRepository.Options); }
    catch (JsonException) { return (false, "malformed item"); }
    if (dto == null) return (false, "malformed item");
    var (ok, err) = SaveValidator.ValidateItem(dto);
    return ok ? (true, "") : (false, err ?? "invalid item");
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
    return Results.Json(new
    {
        Friends = friends.Select(f => new { f.Name, f.Online }), // krotki nie serializują się same
        Requests = requests,
    });
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

// ── presence (heartbeat dla bezczynnych klientów; Auth i tak robi Touch) ──

app.MapPost("/presence/ping", (HttpRequest req) =>
    Auth(req) == null ? Results.Unauthorized() : Results.Ok());

// ── poczta ──

app.MapGet("/mail", (HttpRequest req) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var inbox = db.Inbox(me.Value);
    return Results.Json(new
    {
        Mail = inbox.Select(m => new { m.Id, m.From, m.Body, m.Gold, m.HasItem, m.Claimed, m.Created }),
    });
});

app.MapPost("/mail/send", (HttpRequest req, MailBody b) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var (iok, ierr) = CheckItemJson(b.ItemJson);
    if (!iok) return Results.BadRequest(new { Error = ierr });
    var (ok, err) = db.SendMail(me.Value, b.To ?? "", b.Body ?? "", b.Gold, b.ItemJson);
    return ok ? Results.Ok() : Results.BadRequest(new { Error = err });
});

app.MapPost("/mail/claim", (HttpRequest req, IdBody b) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var (ok, err, gold, item) = db.ClaimMail(me.Value, b.Id);
    return ok ? Results.Json(new { Gold = gold, ItemJson = item }) : Results.BadRequest(new { Error = err });
});

app.MapPost("/mail/delete", (HttpRequest req, IdBody b) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var (ok, err) = db.DeleteMail(me.Value, b.Id);
    return ok ? Results.Ok() : Results.BadRequest(new { Error = err });
});

// ── globalny rynek (cross-lobby AH): escrow w ogłoszeniu, wpływy pocztą ──

app.MapGet("/market", (HttpRequest req) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var listings = db.MarketActive();
    return Results.Json(new
    {
        Me = me.Value,
        Listings = listings.Select(l => new { l.Id, l.SellerId, l.Seller, l.ItemJson, l.Price }),
    });
});

app.MapPost("/market/list", (HttpRequest req, MarketBody b) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    if (string.IsNullOrEmpty(b.ItemJson)) return Results.BadRequest(new { Error = "no item" });
    var (iok, ierr) = CheckItemJson(b.ItemJson);
    if (!iok) return Results.BadRequest(new { Error = ierr });
    string name = db.UsernameOf(me.Value) ?? "?";
    var (ok, err, id) = db.MarketList(me.Value, name, b.ItemJson, b.Price);
    return ok ? Results.Json(new { Id = id }) : Results.BadRequest(new { Error = err });
});

app.MapPost("/market/buy", (HttpRequest req, IdBody b) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    string name = db.UsernameOf(me.Value) ?? "?";
    var (ok, err, item, price) = db.MarketBuy(me.Value, name, b.Id);
    return ok ? Results.Json(new { ItemJson = item, Price = price }) : Results.BadRequest(new { Error = err });
});

app.MapPost("/market/cancel", (HttpRequest req, IdBody b) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var (ok, err, item) = db.MarketCancel(me.Value, b.Id);
    return ok ? Results.Json(new { ItemJson = item }) : Results.BadRequest(new { Error = err });
});

// ── czat gildii (poll) ──

app.MapGet("/guild/chat", (HttpRequest req, long sinceId) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var msgs = db.GuildChatSince(me.Value, sinceId);
    return Results.Json(new { Messages = msgs.Select(m => new { m.Id, m.From, m.Text }) });
});

app.MapPost("/guild/chat", (HttpRequest req, TextBody b) =>
{
    long? me = Auth(req);
    if (me == null) return Results.Unauthorized();
    var (ok, err) = db.GuildChatPost(me.Value, b.Text ?? "");
    return ok ? Results.Ok() : Results.BadRequest(new { Error = err });
});

app.Run("http://0.0.0.0:8080");

record Creds(string Username, string Password);
record NameBody(string Username);
record GuildBody(long GuildId);
record MailBody(string? To, string? Body, long Gold, string? ItemJson);
record IdBody(long Id);
record MarketBody(string? ItemJson, long Price);
record TextBody(string? Text);

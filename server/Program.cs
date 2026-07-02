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

app.Run("http://0.0.0.0:8080");

record Creds(string Username, string Password);

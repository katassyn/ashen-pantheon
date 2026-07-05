# Faza 4 — meta-serwer (konta + postacie online): notatki implementacyjne

Data: 2026-07-02. Realizacja fazy 4 dawnej roadmapy (zastąpionej przez `docs/ROADMAP.md`).

## Architektura

- **Serwer:** `server/` — ASP.NET Core minimal API (net10.0) + **SQLite** (`Microsoft.Data.Sqlite`, zero-instalacji). Migracja na Postgres = podmiana klasy `Db`, kontrakt endpointów zostaje. Referuje `core/` — **ten sam kod reguł gry po obu stronach** (payoff czystej architektury).
- **Endpoints:** `POST /auth/register`, `POST /auth/login` → token (7 dni, w DB); `GET/PUT /character` (Bearer); `GET /health`. Kontrakt danych = `SaveData` z core (PascalCase, enumy stringami — wspólne `JsonGameStateRepository.Options`).
- **Hasła:** PBKDF2 (SHA-256, 100k iteracji, sól per konto). Tokeny: 24B losowe, wygasają po 7 dniach.
- **Jedna postać per konto** na teraz (kontrakt łatwo rozszerzyć o sloty postaci).

## Ochrona ekonomii (fundament pod AH)

`core/SaveValidator` — serwer odrzuca zapis, jeśli:
- affix ma wartość spoza zakresów **`AffixRanges`** (wspólne źródło prawdy z `LootGenerator` — generator losuje wyłącznie w tych granicach, więc każdy legalny item przechodzi),
- item tieru Legendary/Unique/Mythic nie ma ważnego `UniqueId` z katalogu (hand-authored przechodzą tylko z katalogu),
- liczba affixów przekracza limit rzadkości (N=0, M≤2, R≤4),
- punkty atrybutów/skilli przekraczają pulę z poziomu (2/lvl i 1/lvl),
- węzły drzewek / skille loadoutu nie istnieją.

Testy: `tests/SaveValidatorTests.cs` (m.in. 50 wygenerowanych itemów przechodzi; FlatLife=9999 odrzucone).

## Klient (Godot)

- **`HttpGameStateRepository : IGameStateRepository`** — wpina się w istniejącą abstrakcję. `Load()` blokujący (moment logowania, `Task.Run(...).Result` — bez deadlocku na main thread); `Save()` **asynchroniczny last-wins** (nie zamraża gry, brak sieci → ponowi przy kolejnym zapisie); `FlushBlocking()` przy zamykaniu okna.
- **`LoginPanel`** w hubie (pod panelem co-op): adres serwera + konto + hasło, Rejestruj/Zaloguj/Wyloguj. **Gra działa w pełni offline bez logowania** (lokalny JSON jak dotąd).
- **Migracja:** logowanie na świeże konto z pustym charakterem → obecna lokalna postać jest **wypychana na serwer** (`GameState.SwitchRepository`). Wylogowanie → powrót na zapis lokalny.

## Uruchamianie serwera

```
dotnet run --project server        # http://0.0.0.0:8080, baza w server/bin/.../data/ashen.db
```
Smoke (wykonany automatycznie): health → register → PUT postaci (200) → GET (round-trip) → PUT z oszukanym affixem (400 + powód) → bez tokena (401).

## Świadome ograniczenia

- Token/limit prób logowania bez rate-limitu; HTTP bez TLS — **deployment za reverse-proxy (Caddy/nginx) z HTTPS** przed publiczną betą.
- Walidator nie powstrzyma zdeterminowanego cheatera w co-op PvE (host-auth trust) — chroni integralność danych pod przyszły AH; pełny server-auth combat = Etap C roadmapy.
- Brak odzyskiwania hasła / e-maili — beta-friendly minimalizm.

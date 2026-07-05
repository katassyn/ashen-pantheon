# Ashen Pantheon

Pixelowy, top-down **loot ARPG/MMO** w klimacie dark-fantasy. Kilka klas + panteon bogów, którzy
przekształcają działanie skilli i definiują buildy. Trudna, skilowa walka. Solo/duo focus, party do 4
(instancja u gracza), wspólny meta-serwer (konta, ekonomia, social).

**Silnik:** Godot 4.7 (.NET) + C# · **IDE:** Rider · **Serwer:** ASP.NET Core + SQLite

## Status (2026-07-05)

**Kampania 1-50 kompletna i grywalna; warstwa MMO działa lokalnie i online.**

- **Walka:** typy obrażeń w obie strony, multi-status (Burn/Chill/Poison/Bleed), ES/armour/resisty,
  telegrafy, bossowie fazowi + boss bar, death recap (kto/ile/czym) + respawn na klik (bez kary).
- **Build system:** Ranger (9 skilli + drzewka ulepszeń + drzewo klasowe), 2 bogów z wariantem
  każdego skilla, itemizacja z ilvl-scalingiem, sockety, uniki, loadout 5 slotów, pełny rebind.
- **Kampania:** 11 stref (hub→Swerdfield→…→Great Desert→grobowiec Nefertari), 27-questowy łańcuch,
  9 typów celów (Kill/Escort/Defend/Survive…), interaktywne dialogi (Accept/Decline/Complete),
  nagrody itemowe, wskaźniki !/? i strzałka do celu.
- **MMO w lobby (max 4):** co-op host-authoritative (solo = te same ścieżki kodu), party frames,
  nameplaty z menu PPM (Trade/Whisper/Guild/Inspect), czat, handel P2P z escrow, mapa świata + waystony.
- **Miasto:** vendor (Sell/Buy/Buyback), skrytka, blok AH, NPC questowi.
- **Online realm (meta-serwer):** konta + postać na serwerze z walidatorem anty-cheat, znajomi
  z presence, guildie + guild chat (/g), **poczta z załącznikami**, **globalny rynek AH**
  (wpływy ze sprzedaży przychodzą pocztą).
- Logika w `core/` bez zależności od Godota — **98 testów xUnit** 🟢 · content w `data/*.json`
  (nowy mob/quest/strefa/bóg = plik JSON, zero kodu).

## Uruchomienie

- Gra: otwórz projekt w Godot 4.7 mono i graj (menu → realm lokalny → postać).
- Meta-serwer (opcjonalny): `dotnet run --project server` → w menu wybierz realm „Online",
  zarejestruj konto. Bez serwera gra działa w pełni offline.
- Druga instancja do testów co-op: `Godot...exe --path . -- --join` (host: przycisk w menu pauzy).

## Sterowanie (domyślne, wszystko rebindowalne w ESC)

WASD ruch · mysz celowanie · **LPM/PPM/Q/E/R** sloty skilli · **Space** flaska HP ·
**C** staty · **I** ekwipunek · **K** skille · **J** dziennik · **M** mapa świata · **TAB** duża minimapa ·
**O** social (znajomi/gildia/poczta) · **E** interakcja · **Enter** czat (`/g` = gildia) · **ESC** pauza

## Dokumentacja

- **PLAN (jedyny aktualny):** [`docs/ROADMAP.md`](docs/ROADMAP.md)
- Architektura co-op: [`docs/design/phase3-coop-notes.md`](docs/design/phase3-coop-notes.md)
- Architektura meta-serwera: [`docs/design/phase4-meta-server-notes.md`](docs/design/phase4-meta-server-notes.md)
- Wizja importu z DsoCraft: [`docs/design/dsocraft-import-vision.md`](docs/design/dsocraft-import-vision.md)
- Kampania/endgame (referencja): [`docs/design/campaign-extraction.md`](docs/design/campaign-extraction.md)
- Kit Rangera: [`docs/design/ranger-class.md`](docs/design/ranger-class.md) ·
  Staty i EQ: [`docs/design/stats-and-equipment.md`](docs/design/stats-and-equipment.md)
- Zadania graficzne (tablet): [`docs/design/art-tasks-tablet.md`](docs/design/art-tasks-tablet.md)

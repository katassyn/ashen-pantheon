# Faza 2 — notatki implementacyjne (decyzje pozostawione wykonawcy)

Data: 2026-07-02. Realizacja briefu `fable5-next-phase-brief.md`. Poniżej decyzje projektowe podjęte tam, gdzie brief dawał swobodę.

## Drugi bóg: Vharos, Bóg Krwi

Gameplay-changer (nie żywiołowy): **ofiara i wampiryzm**.
- **Pasywka:** +15% obrażeń; gdy brakuje koncentracji, **różnicę płacisz zdrowiem** (0.75 HP za 1 pkt; nigdy poniżej 1 HP). Zmienia zarządzanie zasobem w zarządzanie własnym HP.
- Warianty (wszystkie 9): basic/spread dokładają **Bleed**; egzekutor ×2.5 na oznaczonych + lifesteal; deszcz → **krwawy deszcz leczy gracza w środku**; mina bez stuna ale +50% dmg + lifesteal; przesieka **drenuje** (leczy za każde tyknięcie na wrogu); dash płacony HP z krótszym CD; adrenalina leczy 30 HP; jastrząb uderza **wszystkich oznaczonych** naraz z lifestealem.

Naprawione braki Dzikich Ostępów (4–9): deszcz większy+dłuższy slow, mina truje, **przesieka = latająca bomba kolców bez CD** (z designu właściciela), dash zostawia kolczasty ślad, adrenalina +50% czasu, **jastrząb = 3 wielkie pety** (z designu właściciela).

## Model wyboru boga

`GameState.PledgedGod` (None/Wilds/Blood) + per-skill zbiór `GodSkills` (skill w wersji boga czy bazowej). Pasywka działa od pledge'a. Zmiana pledge'a zachowuje zaznaczenia per-skill.

## Krzywa XP / poziomy

`XpToNext(level) = 40·level^1.5 + 60`, cap **100** (progi kar resistów 50/75/100). Level-up: **+2 pkt atrybutów, +1 pkt skilli**. XP za huska rośnie z głębokością pokoju i poziomem gracza; boss = 150.

## Rzadkość

Wagi dropu: Normal 40% / Magic 35% / Rare 18% / Legendary 4% / Unique 2.5% / Mythic 0.5%. Affixy: N=0, M=1–2, R=3–4. L/U/M **wyłącznie z UniqueCatalog** (6 hand-authored itemów; efekty mechaniczne: `SwiftDash` — dash CD ×0.6, `Overcharge` — +30% dmg ale koszty ×1.2, `MarkOnHit` — każde trafienie oznacza). Szansa dropu: husk 45%, boss 100%.

## Tetris inventory

Plecak 12×6, skrytka 12×8 (jedna zakładka). Rozmiary itemów wzorem PoE (zbroja 2×3, 2H 2×4, pierścień 1×1...). Drag&drop w siatce + na sloty EQ; PPM = szybkie założenie.

## Proceduralne runy — iteracja 1

`RunGenerator` (core, seedowany): 4–5 pokoi + pokój bossa; skalowanie HP/DMG/XP z indeksem pokoju i poziomem gracza; przeszkody (StaticBody2D) z seeda pokoju. Świadomie **sekwencja liniowa** zamiast grafu pokoi — graf/gałęzie to następna iteracja, gdy będzie tileset i prawdziwe layouty. Czyszczenie pokoju → następny spawnuje się wokół gracza.

## Animacje (szablon-Husk)

`EnemyAnimator` — prawdziwy `AnimationPlayer` z animacjami **generowanymi w kodzie** (idle/walk/windup/attack/hit/death, tory na scale/rotation/modulate sprite'a). Podmiana na art = podmiana animacji w bibliotece, **zero zmian w kodzie sterującym**. Husk dostał telegrafowany atak (windup 0.35 s → cios możliwy do uniknięcia) zamiast auto-kontaktu; stany: Chase/Windup/Recover.

## Persystencja

`IGameStateRepository` + `JsonGameStateRepository` (core, czysty .NET) → `user://save.json` (ścieżkę daje autoload `GameBoot`). Zapis przy: pickup/equip/sell/allocate/pledge/level-up/koniec runu/zamknięcie okna. Uniki serializowane po `UniqueId` i odtwarzane z katalogu (integralność). Ten sam kontrakt `SaveData` użyje przyszły serwer.

## Architektura pod N klas

`ClassDefinition` (id, nazwa, zasób+max+regen, lista `SkillInfo`) — `GameState.Class` wskazuje aktywną klasę; HUD/panele/loadout czytają z niej. Kolejna klasa = nowa definicja + własny zestaw `Get(skillId, god)` i dispatch w kontrolerze (do wydzielenia w `ISkillCaster`, gdy powstanie druga klasa — YAGNI teraz).

## Świadome skróty (do przyszłych iteracji)

- Vendor/stash: listy klikane (pełny tetris-drag tylko w plecaku).
- Atak/cast speed z arkusza jeszcze nie wpływa na tempo skilli (brak autoataku).
- Hit chance/evasion gracza vs wrogów nieużywane w obie strony (wrogowie nie mają arkusza).
- Boss bez animatora stanowego (telegrafy pełnią tę rolę); dostanie go przy prawdziwym arcie.

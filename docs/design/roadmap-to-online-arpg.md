# Roadmap: od prototypu do online ARPG (Hero Siege-like, 4 graczy/instancja)

Data: 2026-07-02 · Autor: plan techniczny (senior game dev perspective)
Cel końcowy: **ARPG z co-op do 4 graczy na instancję** (miasto = prywatne lobby gracza, znajomi dołączają), trwałe postacie na koncie online, handel/AH, sezony — model Hero Siege.

---

## Zasada przewodnia kolejności

**Sieć przed contentem.** Dorabianie multiplayera do gotowej gry oznacza przepisanie każdego systemu drugi raz. Nasz `core/` (logika bez Godota, deterministyczne obliczenia, kontrakt `SaveData`) był projektowany pod ten moment — spłacamy tę inwestycję teraz, póki contentu jest mało i koszt synchronizacji jest najniższy. Content (potwory, akty, bossy) wchodzi PO sieci, od razu budowany jako "network-aware".

Druga zasada: **uczciwość co do serwera autorytatywnego.** Pełny server-authoritative combat (jak PoE) to dla solo-deva miesiące pracy i koszty infrastruktury. Plan zakłada podejście etapowe:
- **Etap A (teraz):** host-authoritative co-op — gracz-gospodarz jest autorytetem walki (jak Hero Siege w praktyce). Zero kosztów serwerów gry.
- **Etap B (meta-serwer):** konta/postacie/AH na lekkim serwerze HTTP + baza — to jest ta część, gdzie oszustwa naprawdę bolą (ekonomia), i ją da się zabezpieczyć tanio (walidacja itemów po stronie serwera meta).
- **Etap C (opcjonalny, po sukcesie):** migracja walki na dedykowane serwery — możliwa, bo walka liczy się w `core/`, który odpali się na serwerze bez zmian.

---

## FAZA 3 — Co-op fundament (4 graczy, host-authoritative) 🎯 NAJWIĘKSZE RYZYKO — ROBIMY NAJPIERW

**Cel:** dwóch+ graczy w jednym mieście i jednym runie; host liczy walkę; loot per-gracz.

Zakres:
1. **Scaffolding:** Godot High-Level Multiplayer (ENet). Tryby: host (create lobby) / join (IP/kod). Miasto gracza = jego instancja; goście dołączają do jego huba.
2. **Synchronizacja graczy:** pozycje/animacje przez `MultiplayerSynchronizer`; skille wywoływane RPC → **host wykonuje logikę** (`core/` u hosta), wyniki (HP wrogów, statusy, śmierci) replikowane w dół.
3. **Wrogowie u hosta:** AI/spawny/plan runu tylko na hoście; klienci dostają stan. `RunGenerator` seed od hosta.
4. **Loot instancjonowany per-gracz** (jak PoE/Hero Siege — każdy widzi swoje dropy; zero kradzieży). Drop roll u hosta, przypisany do gracza, widoczny tylko dla niego.
5. **Przejścia scen w grupie:** portal głosuje/teleportuje całą drużynę; powrót do miasta hosta.
6. **Rozłączenia:** gość znika bez psucia runu; host kończy sesję dla wszystkich (komunikat).
7. **Skalowanie trudności od liczby graczy** (HP wrogów ×, drop ×).

Poza zakresem fazy: czat, Steam lobbies (dołączanie po IP/kodzie wystarczy do testów), przewidywanie ruchu (rollback niepotrzebny przy PvE).

**Definition of Done:** ty + drugi klient na LAN/localhost przechodzicie pełny run (miasto → portal → pokoje → boss → powrót), obaj widzicie spójny stan walki, każdy zbiera swój loot, XP/gold nalicza się obu.

**Ryzyka:** największa niewiadoma projektu — dlatego pierwsza. Jeśli syncing okaże się bagnem, lepiej wiedzieć teraz niż po zbudowaniu 5 aktów contentu.

---

## FAZA 4 — Meta-serwer: konta i postacie online

**Cel:** postać przestaje żyć w lokalnym pliku — żyje na koncie, jak w prawdziwym online ARPG.

Zakres:
1. **Backend:** ASP.NET Core minimal API + PostgreSQL (jeden język z resztą projektu; tanio hostowalne: fly.io/hetzner). Endpoints: rejestracja/login (token), get/save postaci (istniejący kontrakt `SaveData` — dlatego go tak projektowaliśmy), lista postaci.
2. **Klient:** `IGameStateRepository` dostaje implementację `HttpGameStateRepository` — podmiana bez ruszania gameplayu (do tego była abstrakcja). Tryb offline zostaje (lokalny JSON) jako fallback/dev.
3. **Ochrona ekonomii (minimum sensowne):** item ma podpis pochodzenia (id + seed rolla) nadawany przy zapisie; serwer odrzuca itemy niespełniające reguł generatora (wartości poza zakresami afiksów, nieistniejące uniki). Nie zatrzyma zdeterminowanego cheatera w co-op PvE, ale chroni przyszły AH przed śmieciami.
4. **Ekran logowania + wybór postaci** (tu naturalnie wchodzi też **wybór klasy** — slot na przyszłego Warriora/Mage'a).

**DoD:** logujesz się z dwóch komputerów na to samo konto, postać z całym EQ/drzewkami/progresem jest tam i tam; zapis po każdym runie.

---

## FAZA 5 — Content framework + gra właściwa

**Cel:** z "jednego wzorcowego Huska" robi się prawdziwa gra z aktami i bossami — content jako DANE, nie kod.

Zakres:
1. **Bestiariusz data-driven:** definicja potwora = zasób/JSON (staty, prędkość, zachowanie z listy: melee-chaser / ranged / charger / summoner, telegrafy, tinty, XP, loot). Husk-szablon staje się silnikiem; ty sztancujesz potwory bez kodu.
2. **Bossy mitologiczne** (twój stary design: greccy/nordyccy/egipscy/japońscy/słowiańscy): framework faz bossa (progi HP → zmiana zestawu ataków), 1 pełny boss jako wzorzec.
3. **Struktura świata:** akty/strefy (tematyczne pule potworów + tileset-placeholder), progresja poziomów stref, **endgame**: mapy z modyfikatorami (jak Hero Siege wormholes / PoE maps) — to jest pętla retencji po kampanii.
4. **Druga klasa** (architektura czeka): Warrior albo Mage — inny zasób, 9 skilli, 2 bogów wariantowo. Waliduje, że system klas naprawdę jest generyczny.
5. **Trudności** (Normal/Nightmare/Hell...) z mnożnikami i lepszym lootem.
6. Dociągnięcie skrótów fazy 2: atk/cast speed w pacing skilli, arkusz statów wrogów (hit/evasion w obie strony), animator na bossie.

**DoD:** 2 akty × ~4 typy potworów + boss aktu, endgame-mapy działają w co-op, druga klasa grywalna.

---

## FAZA 6 — Art & audio pass (podmiana placeholderów)

**Cel:** gra wygląda i brzmi jak gra. Wchodzisz ty z tabletem.

Zakres:
1. **Pipeline pixel-art:** ustalenie rozdzielczości bazowej sprite'ów (np. 32×32/48×48), paleta "ashen" (mamy kierunek z brainstormu), szablon spritesheetu pod `EnemyAnimator` (stany już zdefiniowane — art wchodzi 1:1).
2. Postać gracza (+ per-klasa), Husk i rodzina potworów, boss, VFX skilli (pociski/strefy/telegrafy), **tileset** do proceduralnych pokoi (wtedy generator przechodzi na room-templates → iteracja 2: graf pokoi).
3. Ikony itemów per typ + ramki rzadkości; UI skin (paski, panele, sloty).
4. **Audio:** SFX skilli/trafień/dropów (nawet z paczek CC0 na start), ambient/muzyka miasta i walki.
5. Delikatny juice zgodny z twoim gustem (bez screenshake'ów — floating damage numbers?, subtelny hit-stop do decyzji).

**DoD:** zero ikonek Godota na ekranie; sesja nagrywalna na trailer bez wstydu.

---

## FAZA 7 — Handel, social, sezony

**Cel:** warstwa "MMO-lite", dla której ludzie zostają.

Zakres:
1. **AH gracz-gracz** na meta-serwerze (wystaw/licytuj/kup; podatek złota jako gold-sink), na kontrakcie itemów z fazy 4.
2. **Social:** lista znajomych, zaproszenia do party (jeszcze bez Steama — po kodzie), czat w party.
3. **Rankingi/leaderboardy** (głębokość endgame, czas bossów) — tanie w budowie, mocne w retencji.
4. **Architektura sezonów:** flaga ligi na postaci, reset sezonowy, modyfikatory sezonu — projektujemy teraz, uruchamiamy po premierze.

**DoD:** dwóch graczy handluje przez AH bez kontaktu bezpośredniego; ranking widoczny w mieście.

---

## FAZA 8 — Steam & beta publiczna

**Cel:** gra w rękach obcych ludzi.

Zakres:
1. **Steamworks:** lobbies/zaproszenia przez Steam (zastępuje kod/IP), rich presence, achievements, cloud save jako backup meta.
2. Strona sklepu + trailer + demo (pierwszy akt solo?).
3. **Closed beta** (klucze dla społeczności — budujesz ją devlogami jak planowałeś przy DsoCraft: "building in public").
4. Telemetria minimalna (gdzie giną, gdzie odpadają), pętla balansu.
5. Onboarding: tutorial pierwszych 10 minut.

**DoD:** obcy człowiek instaluje ze Steama, gra co-op z tobą, jego postać przeżywa restart — bez twojej pomocy przez Discord.

---

## Bramki decyzyjne (gdzie świadomie przystajesz)

| Po fazie | Pytanie decyzyjne |
|---|---|
| 3 | Czy co-op czuje się dobrze na realnym łączu (nie LAN)? Jeśli latency boli → budżet na prediction/interpolację ZANIM powstanie content. |
| 4 | Koszty serwera meta akceptowalne? (powinny być ~groszowe do bety) |
| 5 | Czy endgame-pętla wciąga CIEBIE na >10h? Jeśli nie — iteruj tu, nie idź w art. |
| 6→8 | Werdykt rynkowy: demo/beta decyduje czy Etap C (dedykowane serwery walki) w ogóle powstanie. |

## Szacunek proporcji wysiłku (solo + AI, bardzo zgrubnie)

Faza 3: ★★★★ (największe ryzyko techniczne) · Faza 4: ★★ · Faza 5: ★★★★ (najwięcej roboty, ale przyjemnej) · Faza 6: ★★★ (zależy od twojego tempa rysowania) · Faza 7: ★★ · Faza 8: ★★

## Co świadomie POZA planem (żeby nie umrzeć)

- PvP (inna gra, inne problemy netcode)
- Cross-play/mobile, konsole
- Pełny server-authoritative combat przed walidacją rynkową (Etap C tylko po sukcesie bety)
- Handel real-money, mikropłatności (model: Steam premium + ew. skromny Patreon — bez zmian)

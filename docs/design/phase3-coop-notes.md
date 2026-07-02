# Faza 3 — co-op host-authoritative: notatki implementacyjne

Data: 2026-07-02. Realizacja fazy 3 z `roadmap-to-online-arpg.md`.

## Architektura

- **Jeden code-path solo/multi:** `OfflineMultiplayerPeer` od startu — solo gra jest „hostem bez gości". Zero rozgałęzień if-online w logice gry.
- **Host = autorytet walki:** wrogowie (AI, HP, DoT, stun, mark) istnieją tylko u hosta. Klienci mają **puppety** (interpolowana pozycja + ekstrapolacja z prędkości, HP/status/mark sync ~10 Hz, spawn/despawn reliable).
- **Efekty skilli:** rzucający wysyła RPC `SpawnEffect` (CallLocal) → efekt powstaje **na każdej maszynie** (wizualnie identyczny, deterministyczny lot pocisku), ale `EnemyBase.ReceiveHit` na kliencie robi tylko flash — **obrażenia liczy wyłącznie serwer**. Build gracza (drzewka/bóg/gear) zserializowany w `SkillDto` — host nie musi znać konfiguracji klienta (świadomy trust co-op PvE; walidacja ekonomii = faza 4 meta-serwer).
- **Lifesteal/heale:** hit serwerowy → `HealCaster(peer)` RPC do właściciela. Krwawy deszcz: każda maszyna leczy SWOJEGO gracza w strefie.
- **Telegrafy:** replikowane parametrycznie; każda maszyna sprawdza kolizję tylko ze SWOIM `PlayerController.Local` (obrażenia gracza są client-side jak reszta jego HP).
- **Loot instancjonowany:** serwer roluje drop OSOBNO per peer; pickup istnieje wyłącznie na maszynie odbiorcy. XP wspólne (pełne dla każdego).
- **Podróż grupowa:** host wchodzi w portal → `TravelAll(scena, seed)`; wspólny seed → `RunGenerator` daje identyczny plan (przeszkody deterministyczne lokalnie, bez syncu). Logika pokoi tylko u hosta; klienci dostają `StartRoom(index)` + statusy tekstowe.
- **Gracze:** node per peer (nazwa = peer id → identyczne ścieżki RPC). **`SessionChanged` → pełny rebuild graczy** — kluczowe, bo join ZMIENIA własne id klienta (pierwszy złapany bug fazy).
- **Śmierć/respawn:** trup nie blokuje kolizji, nadaje stan sojusznikom (szary + „POKONANY"); **polegli wstają z 50% HP po oczyszczeniu pokoju** (Hero Siege-style); wipe całej drużyny = przegrana; run kończy **R u hosta**.
- **Skalowanie co-op:** HP wrogów ×(1+0.6·(n−1)), DMG ×(1+0.25·(n−1)).
- **Dołączanie w trakcie runu:** kick z komunikatem (dołączanie tylko do hosta w mieście).

## Testowanie

- Flagi cmdline: `--host` (auto-host), `--join` (auto-join 127.0.0.1 + **osobny zapis `save_guest.json`**, żeby dwie instancje na jednym PC nie nadpisywały postaci), `--autorun` (host sam wchodzi w portal po 8 s).
- Zautomatyzowany smoke: dwie instancje headless → join → wspólny hub → podróż grupowa → replikacja wrogów do klienta. **PASS** (log: `[net] puppet wroga #1..4` u klienta, zero SCRIPT ERROR).
- Test ręczny na jednym PC: instancja 1 = F5 + „Hostuj grę"; instancja 2 = `Godot...exe --path . -- --join`.

## Znane ograniczenia (świadome)

- Jedna zgubiona paczka unreliable przy zmianie sceny („Invalid packet received" u hosta) — pakiet w locie trafia w okno przejścia; silnik odrzuca bezpiecznie, kosmetyczne.
- Brak predykcji/rollbacku — PvE nie wymaga; przy złym feel na realnym łączu (bramka decyzyjna) dołożymy bufor interpolacji.
- Rejoin w trakcie runu niewspierany (kick); powrót możliwy gdy host w mieście.
- Trust klienta co do liczb obrażeń (host-auth model) — ekonomia zabezpieczana w fazie 4 na meta-serwerze, pełny server-auth = Etap C roadmapy.

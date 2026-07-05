# Ashen Pantheon — ROADMAP (jedyny aktualny plan)

Data: 2026-07-05. Ten dokument ZASTĘPUJE wcześniejsze plany (`roadmap-to-online-arpg.md`,
`itemization-2-plan.md`, briefy faz — usunięte). Żywa dokumentacja architektury zostaje:
`design/phase3-coop-notes.md` (netcode), `design/phase4-meta-server-notes.md` (serwer),
`design/dsocraft-import-vision.md` (katalog systemów do importu), `design/campaign-extraction.md`
(referencja endgame 51-100), `design/art-tasks-tablet.md` (zadania graficzne właściciela).

---

## STAN GRY (co już działa — skrót)

- **Core:** czysta logika w `core/` (98 testów xUnit), content w JSON (`data/`), Godot tylko renderuje.
- **Walka v3:** typy obrażeń w obie strony, multi-status (Burn/Chill/Poison/Bleed + kropki nad HP),
  ES/armour/resisty/evade, telegrafy, bossowie fazowi + boss bar, death recap + respawn na klik (bez kary).
- **Build system:** Ranger 9 skilli + drzewka ulepszeń + drzewo klasowe z pasywkami; 2 bogów
  (warianty per skill); ekwipunek 11 slotów, ilvl-scaling affixów, sockety+jewele (placeholder),
  uniki; loadout 5 slotów; rebind wszystkiego.
- **Kampania 1-50 KOMPLETNA:** 11 stref + boss aktu (Nefertari), 27-questowy łańcuch,
  cele Talk/Kill/Collect/Reach/Clear/Escort/Defend/Survive/Interact, interaktywne dialogi
  (Accept/Decline/Complete), Abandon, wskaźniki !/?, nagrody itemowe, strzałka do celu.
- **MMO w lobby (max 4):** co-op host-authoritative, party frames, nameplaty (PPM = Trade/Whisper/
  Guild/Inspect), czat, handel P2P z escrow, AH-blok, mapa świata+waystony, minimapa, world map ❗.
- **Miasto:** vendor Sell/Buy/Buyback, stash, AH, NPC questowi.
- **Online realm (meta-serwer ASP.NET+SQLite):** konta+tokeny, postać na serwerze z walidatorem
  anty-cheat, friends+presence (kropki online), guildie, POCZTA z załącznikami, GLOBALNY rynek AH
  (wpływy pocztą), guild chat (/g, poll). Wszystko curl-E2E-zweryfikowane.
- **GUI:** pełnoekranowe panele bez poziomych suwaków (doktryna UiKit.Window + VScroll), UI 1:1 px.

---

## ZASADY KOLEJNOŚCI (senior-dev)

1. **Walidacja przed rozbudową** — nie budujemy endgame'u na niesprawdzonym balansie pętli 1-50.
2. **Retencja przed głębią** — endgame (powód, by grać dalej) przed systemami buildów (jak grać ciekawiej).
3. **Systemy przed artem** — VFX/art podmieniamy raz, na stabilnych systemach (rewamp "stożków" świadomie czeka).
4. **Infra produkcyjna przed publiczną betą** — TLS/rate-limit/backup dopiero gdy ktoś obcy ma się zalogować.
5. **Decyzje właściciela oznaczone [GREENLIGHT]** — nie startujemy bez nich.

---

## FAZA A — Balans i feel pętli 1-50 (TERAZ; wejście = playtest #3 właściciela)

Cel: kampania 1-50 grywalna "od deski do deski" bez zgrzytów. Wyjście: właściciel przechodzi
akt 1-2 bez uwag blokujących.

- [ ] **Playtest #3** (właściciel) → lista zgrzytów; fixy w trybie "playtest batch" jak dotąd.
- [ ] **Balans TTK:** przejście symulacyjne dmg mobów vs HP/mitygacja gracza per strefa (1-50)
      i dmg skilli vs HP mobów; tabela w teście core (żeby balans nie regresował).
- [ ] **Ekonomia złota:** ceny vendora/AH vs drop złota per akt (teraz "na oko").
- [ ] **Juice pass (tani, bez artu):** hit-stop 2-3 klatki, screen-shake przy ciosach bossa,
      flash przy level-up, dźwięki placeholder (CC0) na hit/skill/level/loot.
- [ ] **Onboarding pierwszych 10 minut:** hint "WASD/LMB" przy starcie, wyróżnienie pierwszego
      questa, tooltip pierwszego dropu.
- [ ] **Death recap tuning** po feedbacku (czy 12 s okna wystarcza itd.).

## FAZA B — Endgame MVP [GREENLIGHT właściciela]

Cel: powód do grania po 50. Źródło designu: `design/campaign-extraction.md` (DsoCraft T1-T5,
Q1-Q10, Demon Tower) — wzorce, nie kalka.

- [ ] **Hub endgame** (osobna mapa-miasto po ukończeniu kampanii; wejście z hubu).
- [ ] **Tiered dungeons (grupowe):** Blood/Hell/Infernal — te same mapy, skalowane HP/dmg/ilvl
      dropu + klucze wejściowe (drop z kampanii/endgame; opłata = sink złota).
- [ ] **Q1-Q10 (solo scaling):** jeden dungeon, 10 stopni trudności, progresja odblokowań.
- [ ] **Loot endgame:** ilvl 50+ (skala affixów już to wspiera — `AffixRanges.ScaleFor` przyjmuje >50),
      osobne loot-table per tier, pity/duplicate-protection na unikach (do decyzji).
- [ ] **Demon Tower** (piętra, klucze) — po sprawdzeniu się tierów.

## FAZA C — Systemy głębi buildów (kolejność wg wpływu na build)

- [ ] **Questy lvl 50 + wybór BOGA-PATRONA** [DESIGN z właścicielem]: łańcuch questów per bóg,
      na końcu przysięga; **zmiana patrona możliwa za wysoką cenę** (decyzja 2026-07-05).
      Framework questów już to udźwignie (RequiredLevel/prereqs/łańcuchy).
- [ ] **Jewels → SPECJALNY SLOT** (decyzja: jewele NIE do socketów): nowy slot w Equipment,
      migracja placeholdera; **gemy z kopalni → sockety** (nowy typ itemu + kopalnia jako źródło).
- [ ] **Runy + Słowa Runiczne** (DsoCraft): sloty run co 5 lvl od 50, mix 9 run, nazwane komba.
- [ ] **Boss Souls** (trinkety z bossów).
- [ ] **Crafting / biologist / graveKeeper** (wiedza o rodzinach mobów) — dalszy horyzont.

## FAZA D — Żywy świat (retencja dzienna; wymaga B)

- [ ] Daily questy (endgame hub).
- [ ] World chesty / eventy strefowe (bloodChest, lockpick).
- [ ] Eventy sezonowe kalendarzowe (full moon, x-mas) — BEZ sezonów-resetów (decyzja: NIE ma seasonów).
- [ ] Pety (kosmetyczne/utility) — niski priorytet.

## FAZA E — Art & audio pass (równolegle z B/C, wchodzi gdy assety gotowe)

- [ ] Podmiana sprite'ów wg `design/art-tasks-tablet.md` (P0: ranger/husk/spitter/warden + tileset;
      EnemyAnimator gotowy na podmianę 1:1).
- [ ] UI kit (9-slice, sloty rzadkości, ikony skilli/itemów).
- [ ] **VFX revamp skilli** ("stożki, strzały" — obiecany rewamp; systemy są stabilne, można).
- [ ] Muzyka + SFX docelowe.

## FAZA F — Online hardening (przed publicznym testem z obcymi)

- [ ] TLS (reverse proxy: Caddy/nginx) + rate limiting + backup SQLite (albo migracja Postgres — Db.cs wymienne).
- [ ] Walidacja ZŁOTA i progresji w SaveValidator (dziś złoto bez limitu — znany trust-gap).
- [ ] Pełny anti-dupe handlu/poczty (dziś escrow client-side; docelowo transfery przez serwer).
- [ ] Multi-slot postaci na koncie online.
- [ ] Websocket/SignalR: push guild chatu, presence, powiadomienia poczty (zamiast pollingu).
- [ ] Server-authoritative combat = dopiero PO walidacji rynkowej (świadoma decyzja, bez zmian).

## FAZA G — Steam & beta

- [ ] Buildy eksportowe (Windows), ikona, capsule art.
- [ ] Zamknięta beta znajomych (klucz serwera online).
- [ ] Strona Steam + wishlist; demo?

---

## DŁUG TECHNICZNY / znane sprawy (nie blokują, pilnować)

- Kosmetyczny dropped packet 1× przy zmianie sceny w co-op (udokumentowane, nieszkodliwe).
- Trade/mail/market: escrow client-side — pełna ochrona wymaga transferów po stronie serwera (Faza F).
- Guild chat = poll 8 s (websocket w F).
- `_02/_03` questy mają nagrody itemowe wg prostej reguły — po balansie ekonomii (A) przejrzeć.
- Stale `AshenPantheon.Server` proces potrafi zablokować build DLL (Stop-Process przed buildem).

## ZASADY PRACY (bez zmian)

- Logika → `core/` + test; content → `data/*.json`; Godot renderuje.
- Cały content i UI gry PO ANGIELSKU (komentarze kodu mogą być PL).
- Weryfikacja przed commitem: build + testy + headless 4 sceny + co-op smoke (+ curl E2E przy serwerze).
- Panele UI: `UiKit.Window` + `UiKit.VScroll` (pełny ekran, zero poziomych suwaków).
- Eventy statyczne `Net.*`: TYLKO nazwane handlery, odpinane w `_ExitTree`.

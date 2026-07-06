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

## FAZA B — Endgame MVP [GREENLIGHT 2026-07-05 — W TOKU]

Cel: powód do grania po 50. Źródło designu: `design/campaign-extraction.md` (kanon DsoCraft:
dungeony T1-T5 mitologiczne, trudności Blood/Hell/Infernal, epilog Q1-Q10).

- [x] **Pantheon Gate** w mieście (blok, E; odblokowany po pokonaniu Nefertari) → panel endgame.
- [x] **Dungeony grupowe T1-T5** (katalog `data/endgame/dungeons.json`, kanon nazw: Odyssey of
      Shadows / Poseidon's Underwater Temple / Mount Olympus / Daedalus' Eternal Labyrinth /
      Fields of Immortal Souls) × trudności **Blood/Hell/Infernal** (HP/dmg/XP/ilvl dropu,
      opłata wejścia = sink złota, wyższa trudność wymaga ukończenia niższej; unlock całej
      drużyny przez RPC). T1 grywalny (moby epilogu + boss Commander Emberwing 3 fazy);
      T2-T5 "coming soon" (dane są, brakuje contentu stref).
- [x] **The Final Proving Q1-Q10** (solo): skala formułą w core (HP/dmg/XP/ilvl/fee), clear Qn
      odblokowuje Qn+1, persist w SaveData + walidator anty-fałszywym odblokowaniom.
- [ ] **Dokończenie Q3-Q10** (zarysy + rostery YML `q?_inf.yml`, flow z QuestData.java; kolejność
      wg złożoności: Q4/Q5 → Q6 → Q8 → Q3 → Q10 → Q9 → Q7).
- [ ] **Loot endgame — skala ilvl >50**: `AffixRanges.ScaleFor` plateau'uje na 50 — rozszerzyć
      krzywą (dotyka walidatora — osobny batch, przed trudnościami Hell/Blood).

## PLAN PO Q (decyzje właściciela 2026-07-05 — trzy rundy pytań; KOLEJNOŚĆ OBOWIĄZUJE)

### B1. Wejściówki + nagrody Q (od razu po Q10)
- [ ] **Fragments of Infernal Passage** — waluta wejściowa Q (kanon: Inf 10 / Hell 25 / Blood 50);
      drop z endgame + daily; zastępuje opłatę złotem w Q.
- [ ] **Klucze T1-T5** ([T?] Mythological Dungeon Key) do dungeonów grupowych.
- [ ] **EliteLootbox** — nagroda ukończenia Q jak w oryginale (skrzynka, tabela per Q/trudność).
- [ ] **Trudności Q: Infernal (lvl 50) / Hell (65) / Bloodshed (80) per Q** — z YML `_hell/_blood`
      (zastępuje sztuczną skalę QScale 1→10; sekwencja odblokowań Q1→Q10 per trudność —
      założenie do potwierdzenia przy wdrożeniu). Hell/Blood grywalne po levelingu 51+ (C2).

### B2. Żywy świat (po wejściówkach, PRZED systemami buildów)
- [ ] **Daily questy** (dailyQuestPlugin; źródło fragmentów/kluczy).
- [ ] **Eventy kalendarzowe** (eventPlugin, full_moon/x_mas z YML) — BEZ sezonów-resetów.
- [ ] **World chesty**: bloodChest (wyzwanie przy skrzyni) + lockpick (minigra) — mapy kampanii i Q.

### C. Systemy buildów (kolejność potwierdzona)
- [ ] **C1. GOD-QUESTY lvl 50** [DESIGN z właścicielem — PIERWSZY system]: łańcuch questów wyboru
      boga-patrona, zmiana za wysoką cenę. **Ascendancje z MyExperiencePlugin NIE wchodzą —
      bogowie pełnią tę rolę** (patron = duży power-spike).
- [ ] **C2. Leveling 51-100**: 5 stref farmowych z YML (Brigavik 51-60, Tetaconetl 61-70,
      Telepolos 70-80, Tywil 81-90, OceanBones 91-100) + questy farmowe (campaign-extraction:
      "Heart of Darkness"…). Odblokowuje Hell (65) i Bloodshed (80).
- [ ] **C3. Tiery T1-T5 wg YML** (WSZYSTKIE, też T1 — obecny roster T1 to MVP do wymiany na
      `odyssey_shadows.yml`; potem poseidon_isle_of_mist_t2, mount_olympus_t3,
      daedalus_eternal_labyrinth_t4, fields_of_immortal_souls_t5).
- [ ] **C4. Boss Souls** (TrinketsPlugin): **mega-rare drop WYŁĄCZNIE z Q-Bloodshed (kanon,
      bez źródeł przejściowych)** — wymaga C2. Akcesoria + Augmenter.
- [ ] **C5. Runy + Słowa Runiczne + ŁOWISKO**: **runy są stricte połączone z fishingiem**
      (FishingPlugin + fishing.yml) — łowisko wchodzi razem z runami jako ich źródło;
      sloty run co 5 lvl od 50, mix 9, nazwane komba.
- [ ] **C6. Gemy + Kopalnia + Jewel-slot**: kopalnia instancjonowana ze STAMINĄ (mineSystemPlugin)
      jako źródło gemów do socketów; jewele przeniesione do SPECJALNEGO SLOTU.

### D. Po systemach
- [ ] **Demon Tower** (piętra, klucze per piętro, fale+bossy; demon_tower_I-IV.yml) — po C.
- [ ] **Life-skills — WSZYSTKIE wchodzą** (decyzja): crafting + alchemia (MyCraftingPlugin2),
      biolog + graveKeeper (wiedza o rodzinach mobów → buffy), pety (petplugin, 31 petów),
      farming / bees (fishing już w C5). Kolejność do ustalenia przy D.
- [ ] Hub endgame jako osobna mapa (kosmetyka — po art passie).

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

# Brief dla Fable 5 — Ashen Pantheon, kolejna faza

> Ten dokument jest samodzielnym promptem/briefem. Wklej go w całości jako pierwszą wiadomość do Fable 5.

---

## ROLA

Act as a senior game developer and technical architect specializing in Godot 4 (C#) ARPG development. Pracujesz w realnym, istniejącym repozytorium gry — nie zaczynasz od zera. Twoim zadaniem jest **rozbudować istniejący, działający prototyp** o kolejny, duży zestaw mechanik, zachowując dyscyplinę inżynierską, która już obowiązuje w tym projekcie (patrz "Sposób pracy" niżej).

**Zanim zaczniesz kodować:** przeczytaj repo. W szczególności:
- `README.md` — status projektu
- `docs/design/stats-and-equipment.md` — statystyki i sloty EQ (transkrypcja notatek)
- `docs/design/ranger-class.md` — pełny kit klasy Ranger (zasób, mechanika Oznaczenia, 9 skilli, model bogów)
- `docs/design/build-system-worksheet.md` — kontekst historyczny systemu buildów
- `docs/superpowers/specs/2026-06-30-ashen-pantheon-m0-combat-slice-design.md` — pierwotny spec walki
- `core/*.cs` i `scripts/*.cs` — rzeczywisty stan kodu (poniżej masz streszczenie, ale kod jest źródłem prawdy)

---

## 1. Wizja gry (kontekst)

**Ashen Pantheon** — pixelowy, top-down **loot ARPG** w klimacie dark-fantasy. Inspiracje: Hero Siege (rdzeń walki), Path of Exile (głębia statystyk/itemizacji), Hades (hook bogów).

- **Silnik:** Godot 4.7 (.NET/mono) + C# (.NET 8). IDE: Rider.
- **Repo:** `C:\Users\mastu\Desktop\Maks\ashen-pantheon` (GitHub: `katassyn/ashen-pantheon`, branch `main`).
- **Struktura kodu:** `core/AshenPantheon.Core` — czysta biblioteka logiki, **bez zależności od Godota**, testowalna xUnit (obecnie 22 testy, wszystkie zielone). Projekt gry (`ashen.csproj`, korzeń repo) referuje `core/` i tylko "ożywia" jego wyniki na węzłach Godota (ruch, sprite'y, kolizje, UI). **Ta zasada jest nienaruszalna** — cała logika gry (obliczenia, staty, walka, itemy) ma żyć w `core/`, warstwa Godota jest "głupia" i tylko renderuje/wywołuje.
- **Sterowanie:** twin-stick — WASD ruch, mysz celowanie (niezależne, żeby kiting działał).
- **Tożsamość:** trudna, skilowa walka — trudność z telegrafów, pozycjonowania, timingu, nie z losowości.
- **Hook różnicujący:** kilka klas (bazowe kity skilli) + **panteon bogów**, którzy **przekształcają działanie skilli** i tym definiują build. Bóg dostarcza **alternatywną wersję KAŻDEGO skilla** (choćby subtelną) + **pasywkę/błogosławieństwo**. Gracz wybiera **per-skill**: wersja bazowa czy boga — bóg nigdy nie psuje builda, bo niepasujący wariant można zignorować.
- **Struktura:** miasto-hub (instancja, prywatne lobby) → portal → run (arena/dungeon) → powrót do hubu. Solo/duo focus, docelowo party do 4 (**nie teraz** — patrz sekcja "Poza zakresem").
- **Model biznesowy:** Steam premium + darmowe bety na feedback. Poza zakresem tej fazy.

---

## 2. Aktualny stan implementacji (co już działa)

### `core/` (czyste, testowane C#)
- **Damage.cs:** enumy `SkillTag`, `SkillShape` (SingleTarget/Cone/Nova/Projectile/Line), `StatusType` (Burn/Chill), `DamageType` (Physical/Fire/Cold/Lightning/Chaos).
- **Attributes.cs:** INT/DEX/Siła.
- **Resistances.cs:** Fire/Cold/Lightning cap 75%, Chaos cap 60%, kara −20% do wszystkich na poziomach 50/75/100.
- **CharacterSheet.cs:** staty pochodne z atrybutów (Str→HP+atk%, Dex→evasion+hit%, Int→mana+ES%), Armour→redukcja fizyczna, Evasion→szansa uniku (diminishing), Crit chance/multi, atk/cast speed, `MitigatedDamage(type, raw)`.
- **Item.cs / Equipment.cs:** 11 slotów EQ (Helmet, Shoulders, BodyArmour, Gloves, Boots, Belt, Amulet, Ring×2, Weapon, OffHand), affixy→staty, reguła: broń 2H usuwa OffHand. `Equipment.BuildSheet()` składa `CharacterSheet` z bazowych atrybutów + affixów gearu.
- **Inventory.cs:** **prosta lista** (jeszcze nie tetris — patrz zadania niżej).
- **SkillDefinition / ResolvedSkill / God / GodModifierSystem:** skill = dane + hooki; bóg = lista modyfikatorów aplikowanych na `ResolvedSkill`.
- **Combatant / CombatResolver:** HP, statusy (Burn DoT, Chill+bonus dmg), **Oznaczenie (Mark)** — oznaczeni biorą `MarkedMultiplier`× obrażeń, **Stun**.

### Warstwa Godota (`scripts/`, `scenes/`)
- **PlayerController.cs:** ruch WASD + cel myszką, dash, **9 skilli Rangera na SZTYWNYCH klawiszach** (LPM=Strzał, PPM=Rozbryzg, Q=Egzekutor, E=Deszcz, R=Mina, F=Przesieka, Spacja=Dash, X=Adrenalina, Z=Jastrząb). Obrażenia mnożone przez `AttackDamageMultiplier` + rzut na krytyk z arkusza. Obrażenia przychodzące mitygowane przez `MitigatedDamage(Physical, ...)`.
- **RangerKit.cs:** dane 9 skilli Rangera + **JEDEN bóg „Dzikie Ostępy"**. ⚠️ **WAŻNE — niedokończone:** tylko skille 1–3 (basic/spread/exec) mają realny wariant boga (przebicie, +2 pociski, mocniejszy dmg). Skille 4–9 (`Rain`, `Mine`, `Hedge`, `Hawk`) **przyjmują parametr `bool god`, ale go ignorują** (poza drobnym `RainRadius`). To złamanie zasady „każdy skill dostaje wariant, choćby subtelny" — **musisz to naprawić jako część zadań niżej**.
- **EnemyBase.cs (abstrakcyjna):** HP, statusy, Mark, Stun, pasek HP, obrażenia kontaktowe, drop lootu przy śmierci. `Enemy` (Husk — goni i bije) i `Boss` (Ashen Warden — telegrafowane ataki: slam/sweep/charge) dziedziczą.
- **Telegraph.cs:** system ostrzeżeń ataków (krąg/stożek/linia) z narastaniem i uderzeniem.
- **Projectile / GroundZone / Mine / HedgeZone / Hawk:** implementacje efektów skilli.
- **ArenaManager.cs:** 3 fale Husków + finałowa fala z Bossem, win/lose, restart.
- **LootFactory / ItemPickup:** losowy item z 1–2 affixami dropi z każdego wroga (**każdy dropi wszystko** — pod sprzedaż), leci do plecaka po wejściu.
- **GameState.cs:** statyczny, trwały (w pamięci procesu, **nie na dysku**) stan: atrybuty bazowe, Equipment, Inventory, wybory boga per-skill (`GodSkills: HashSet<string>`).
- **Panele UI (prowizoryczne, osobne):** `CharacterPanel` (klawisz **I** — 11 slotów + plecak, klik zakłada/zdejmuje), `StatsPanel` (klawisz **C** — pełny odczyt arkusza), `SkillPanel` (klawisz **K** — lista 9 skilli z przełącznikiem baza/bóg per pozycja, bez drag&drop).
- **Sceny:** `Main.tscn` (hub — portal, placeholdery Skrytki/AH), `Arena.tscn` (walka), reszta 1:1 z węzłami.
- **Grafika:** wszystko to placeholder — przefarbowana ikonka Godota (`icon.svg`). Zero prawdziwych animacji, zero rozróżnialnych ikon itemów.

---

## 3. Decyzje projektowe na tę fazę (ustalone z właścicielem projektu — wiążące)

1. **Persystencja / serwer:** gra docelowo będzie online z serwerem i bazą danych (jak każda gra online, real player-to-player trading itd.). **W TEJ FAZIE:** nie buduj prawdziwego serwera. Zaprojektuj kod tak, by był **server-ready** — logika w `core/` zostaje frameworkowo-agnostyczna (już jest), a persystencja stanu gracza ma iść przez **abstrakcję** (np. `IGameStateRepository` z metodami zapisu/odczytu), z implementacją **lokalną (plik JSON, np. w `user://`)** na teraz. Docelowa implementacja sieciowa/DB-owa podmieni się bez ruszania gameplayu. Stack serwera (jeśli/gdy powstanie) — **wybierz sam, co uznasz za najlepsze** (masz swobodę; C#/.NET backend + relacyjna baza jest naturalnym wyborem, bo cały projekt jest już w C#, ale to nie jest wymóg na TERAZ).
2. **Model walki w multiplayer:** docelowo **serwer autorytatywny** (bije wroga, liczy obrażenia — istotne pod przyszły handel/anti-cheat). Nie implementuj sieci teraz, ale **utrzymuj dyscyplinę**: cała matematyka walki ma żyć w `core/` (już tak jest) tak, by w przyszłości serwer mógł uruchomić tę samą bibliotekę autorytatywnie, bez przepisywania.
3. **Zakres tej fazy — WSZYSTKO co dotyczy MECHANIK gry, NIE contentu wszerz.** Nie rób 10 wariantów tego samego wroga. Zrób **jednego (Husk) naprawdę dobrze** — pełny, wielostanowy system animacji (patrz pkt 9) — jako **szablon**, który później posłuży do sztancowania kolejnych potworów. Cel: **jedna, w pełni działająca pętla gry** (EQ, skille, loot, walka, progresja) z dopracowanymi placeholderami (ikonki itd.), wzorowana na tym, jak to realnie wygląda w PoE.
4. **Loadout skilli:** dedykowane menu. Gracz ma na dole ekranu **pasek skilli z literowymi/klawiszowymi oznaczeniami** i **przenosi (drag&drop) skille z UI drzewka skilli na pasek**, by móc ich używać. To **zastępuje** dzisiejsze sztywne przypisanie klawiszy w `PlayerController.cs`. Zgodnie z `docs/design/ranger-class.md`: **5 aktywnych slotów** wybieranych z puli 9 skilli Rangera (dash — skill 7 — też konkuruje o slot, nie jest darmowym freebie).
5. **Areny:** przejście na **proceduralne** generowanie poziomów runu (zamiast dzisiejszej jednej ręcznej `Arena.tscn` + fal). Sugerowany kształt: room-based generator (grafy pokoi połączonych, czyszczone sekwencyjnie — jak Hades/Binding of Isaac) pasujący do istniejącej pętli hub→portal→run→hub. Ty decydujesz o dokładnej implementacji.
6. **Drzewka skilli:** **pełne drzewko dla WSZYSTKICH 9 skilli Rangera** (nie tylko przykładowe 1–2). Każde drzewko: mini-struktura węzłów (3–5), część rozgałęzień **wykluczająca się** (np. „+1 pocisk" ALBO „przebicie" ALBO „krwawienie" — wybierasz jedno). Punkty z levelowania (patrz pkt 8).
7. **Drugi bóg:** zaprojektuj i zaimplementuj **jednego, dobrze zaprojektowanego drugiego boga** dla Rangera (masz pełną swobodę co do tematu/nazwy — ma być gameplay-changer jak „Dzikie Ostępy", niekoniecznie żywiołowy). **Musi mieć wariant dla WSZYSTKICH 9 skilli** (choćby subtelny) + własną pasywkę. Przy okazji **napraw** brakujące warianty „Dzikich Ostępów" dla skilli 4–9 (patrz sekcja 2, ⚠️).
8. **Progresja / XP / levelowanie:** **zaimplementuj teraz.** Obecnie `GameState.Level` jest zahardkodowany na 1 i używany tylko do kary resistów. Potrzeba: zabójstwa wrogów dają XP → level up → **+2 punkty atrybutów** do wydania (zgodnie z `stats-and-equipment.md`) + punkty do drzewek skilli. Krzywa XP i cap poziomu — Twoja decyzja projektowa (rozważ ~100 jako cap, bo kary resistów są zaprojektowane pod progi 50/75/100).
9. **Ascendancje:** **usunięte z koncepcji.** Stary pomysł (9 ascendancy wybieranych na 50 lvl) **zastępuje system bogów** — nie buduj równoległego systemu ascendancy.
10. **Respec:** reset wydanych punktów (atrybuty i/lub drzewka skilli) możliwy **za walutę (złoto)**. Koszt/skalowanie — Twoja decyzja.
11. **Rzadkość itemów:** wprowadź pełny system tierów: **Normal / Magic / Rare / Legendary / Unique / Mythic**. Normal/Magic/Rare to rosnąca liczba losowych affixów (Ty ustalasz progresję, np. Normal=0, Magic=1-2, Rare=3-4) + kolor/oznaczenie wizualne. **Legendary/Unique/Mythic powinny być itemami projektowanymi ręcznie** (fixed, nazwane, z unikalnymi efektami mechanicznymi — nie tylko „więcej losowych affixów"), analogicznie do unique'ów w PoE/Diablo. Zaprojektuj strukturę danych rozróżniającą losowe affixy (proceduralne) od hand-authored uników.
12. **Ekwipunek/plecak — tetris jak w PoE.** `Inventory.cs` (obecnie płaska lista) wymaga przebudowy: każdy item ma kształt/rozmiar w komórkach (np. 1×1, 1×2, 2×2...), siatka plecaka ma stały rozmiar, trzeba sprawdzać czy item się mieści (i obsłużyć UI drag&drop w siatce). To znacząca zmiana modelu — zaprojektuj to solidnie w `core/`.
13. **Skrytka (stash):** **jedna zakładka** na teraz (nie multi-tab jak premium stash w PoE).
14. **Ekonomia / AH:** na tę fazę **sprzedaż do NPC za złoto wystarczy**. Prawdziwy gracz-do-gracza dom aukcyjny poczeka na serwer. Zaprojektuj system waluty (Gold) i wektor sprzedaży (NPC vendor) tak, by dało się później dołożyć prawdziwy AH bez przepisywania ekonomii od zera.
15. **Druga klasa:** nie projektuj jej contentowo teraz, ale **architektura ma być gotowa pod N klas** — nie zaszywaj Rangera na sztywno tam, gdzie powinien być polimorfizm/dane per-klasa (zasób inny niż Koncentracja dla innych klas, inny kit skilli itd.).
16. **Animacje:** dla „jednego dopracowanego wroga jako szablonu" (patrz pkt 3) zbuduj **prawdziwy system animacji stanowej** — `AnimationPlayer`/`AnimatedSprite2D` ze stanami: idle / walk / attack / hit / death (i telegraf ataku, skoro Husk już ma kontaktowy atak — rozważ dodanie mu prostego telegrafu, spójnie z bossem). Wizualnie może to nadal być placeholder-kształt — liczy się, że **maszyna stanów animacji działa poprawnie i jest gotowa na podmianę grafiki 1:1** bez zmian w kodzie.

---

## 4. Poza zakresem tej fazy (świadomie NIE rób)

- Prawdziwy serwer sieciowy / netcode / multiplayer (party do 4) — tylko architektura "server-ready", nie implementacja sieci.
- Prawdziwy dom aukcyjny gracz-gracz (wystarczy sprzedaż do NPC).
- Druga w pełni zaprojektowana klasa (tylko architektura pod N klas).
- Prawdziwy art / dźwięk — zostają placeholdery (poza systemem animacji, który ma być realny mechanicznie).
- Wiele wariantów tego samego wroga / wiele bossów — jeden Husk jako dopracowany szablon wystarczy.
- Ascendancje (zastąpione przez bogów — nie buduj równolegle).

---

## 5. Sposób pracy w tym repo (obowiązujący standard)

- **`core/` zawsze czyste** — żadnych typów Godota, testowalne xUnit. Każda nowa mechanika liczona w `core/`, warstwa Godota tylko renderuje wynik.
- **TDD tam gdzie to logika:** pisz testy xUnit dla nowych systemów w `core/` (wzorem istniejących 22 testów) — czerwone→zielone.
- **Waliduj zmiany Godota headless** przed uznaniem za gotowe: `dotnet build ashen.csproj`, potem uruchomienie silnika bez okna i sprawdzenie logów pod kątem błędów, np.:
  ```
  "<ścieżka do Godot>_console.exe" --headless --path . --import
  "<ścieżka do Godot>_console.exe" --headless --path . res://scenes/Arena.tscn --quit-after 240
  ```
  i sprawdzenie, że output nie zawiera `SCRIPT ERROR`/`NullReference`/`Cannot`/`.cs:` (błędy kompilacji/runtime). Ścieżka do silnika: `C:\Users\mastu\Desktop\Maks\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe`.
- **Commituj często, małymi krokami**, z opisowymi komunikatami (wzorem historii gita w tym repo).
- **Dokumentuj decyzje projektowe** w `docs/design/` (analogicznie do `ranger-class.md`, `stats-and-equipment.md`) — zwłaszcza dla systemów, które mają dużo swobody decyzyjnej pozostawionej Tobie (drugi bóg, tiery rzadkości, krzywa XP, generator proceduralny).
- **Duży zakres = rozbij na fazy.** To jest tydzień(-ie) pracy, nie jedna sesja. Zanim zaczniesz kodować, rozpisz to jako plan/kolejność (np. najpierw XP/leveling + drzewka skilli, bo od nich zależy sens loadoutu i respec; potem loadout UI; równolegle/potem tetris inventory + rarity; procedural areny i drugi bóg mogą iść niezależnie). Możesz przeorganizować kolejność, jeśli masz lepsze uzasadnienie inżynierskie.
- **Testuj grywalność na koniec każdego większego kroku** — właściciel projektu robi duże testy sesyjne, więc każdy kawałek powinien dać się realnie odpalić i sprawdzić (F5 w Godocie), nie tylko przejść testy jednostkowe.

---

## 6. Krótkie podsumowanie „co zrobić" (checklist wysokopoziomowy)

- [ ] Persystencja: `IGameStateRepository` + implementacja lokalna JSON, spinana z `GameState`.
- [ ] XP/Leveling: zabijanie → XP → level up → punkty atrybutów + punkty skilli.
- [ ] Pełne drzewka ulepszeń dla wszystkich 9 skilli Rangera (z wykluczającymi się gałęziami).
- [ ] Loadout UI: pasek 5 slotów na dole ekranu, drag&drop z widoku drzewek/skilli; usunięcie sztywnych klawiszy z `PlayerController`.
- [ ] Naprawa niedokończonych wariantów boga dla skilli 4–9 + pełny **drugi bóg** (wariant dla wszystkich 9 + pasywka).
- [ ] Respec za złoto (atrybuty i/lub drzewka skilli).
- [ ] System rzadkości: Normal/Magic/Rare/Legendary/Unique/Mythic, w tym hand-authored uniki/legendaries/mythics.
- [ ] Tetris inventory (core model + UI grid) zamiast płaskiej listy.
- [ ] Ekonomia: waluta Gold + sprzedaż do NPC.
- [ ] Procedural generation aren/poziomów runu (zamiast jednej statycznej `Arena.tscn`).
- [ ] Pełny system animacji stanowej na Husku jako dopracowany szablon (idle/walk/attack/hit/death).
- [ ] Rozróżnialne placeholder-ikonki itemów per `ItemKind` (+ oznaczenie rarity kolorem/ramką).
- [ ] Architektura otwarta pod N klas (bez twardego zaszycia Rangera tam, gdzie powinien być polimorfizm).

Powodzenia. To duży, ale ekscytujący kawałek gry — zbuduj go solidnie, warstwami, z testami i walidacją na każdym kroku.

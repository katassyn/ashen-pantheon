# Ashen Pantheon — Spec projektowy

**Working title:** Ashen Pantheon *(placeholder — do zmiany przed publikacją, by uniknąć kolizji nazw)*
**Data:** 2026-06-30
**Status:** Zatwierdzony do M0
**Zakres tego dokumentu:** Milestone **M0 — Combat Vertical Slice**. Reszta (M1–M4) jest tu tylko jako kontekst-roadmapa; każdy kamień dostanie własny spec.

---

## 1. Wizja (kontekst całości)

Pixelowy, top-down **loot ARPG** w klimacie dark-fantasy ("ashen pantheon"). Inspiracje: Hero Siege (rdzeń), Path of Exile (głębia buildów), Hades (boski hook).

| Filar | Decyzja |
|---|---|
| Silnik | **Godot 4 + C#** (wykorzystuje znajomość C# autora; wbudowany multiplayer API pod party do 4) |
| Perspektywa | Top-down, pixel art |
| Sterowanie | **Click-to-move** (LPM = ruch myszką) + skille na **Q/W/E/R/T** + **dash na spacji** (z krótkimi i-frames) |
| Tożsamość | **Trudna, skilowa gra.** Trudność wynika z czytania telegrafów, pozycjonowania i timingu dasha — nie z losowości |
| **Hook (differentiator)** | Kilka **klas** (bazowe zestawy skilli) + wielu **bogów**, którzy **przekształcają działanie skilli**. To bogowie definiują build, nie sam skill |
| Struktura | Pętla **run → hub → run**. Focus solo/duo, skaluje do **party 4** |
| Art direction | "Ashen moody" — stłumiona popielata/stalowa/fioletowa paleta + jaskrawo świecące telegrafy ataków (czytelność ponad wszystko) |
| Model biznesowy | Steam premium (płatna). Darmowe bety dla feedbacku. Ewentualnie skromny Patreon (extra klasy) później. **Poza zakresem implementacji teraz.** |

### Dlaczego ten hook jest sprytny pod solo-dev
Content mnoży się przez **kombinacje** (klasy × bogowie × skille), a nie przez ręczne robienie dziesiątek klas. Mała liczba dobrze zaprojektowanych elementów daje dużą re-grywalność.

---

## 2. Roadmapa (kontekst)

- **M0 — Vertical slice walki** ← *jedyny zakres tego speca.* Rdzeń walki + hook bogów. Solo only. Pytanie: „czy to jest fajne?"
- **M1 — Pętla gry:** hub-miasto (instancja, NPC, stash), 1 generowana strefa, loot + ekwipunek.
- **M2 — Głębia:** drzewko/poziomy, panteon 8–12 bogów, 2-ga klasa, więcej skilli.
- **M3 — Co-op:** party do 4 przez Godot multiplayer API.
- **M4 — Content + polish:** więcej stref/bossów, audio, UX, strona + demo na Steam.

**Zasada:** nie ruszamy M1, dopóki M0 nie udowodni, że walka i hook bogów dają frajdę.

---

## 3. M0 — Combat Vertical Slice (właściwy spec)

### 3.1 Cel
Grywalny prototyp jednej areny, w którym gracz jedną klasą walczy z kilkoma wrogami i mini-bossem, oraz **może wybrać jednego z dwóch bogów, co wyraźnie zmienia działanie jego skilli**. Bez hubu, lootu, menu, save'a, multiplayera.

### 3.2 Kryteria sukcesu (czy M0 się udało)
M0 jest udane, jeśli po zagraniu odpowiedź na **wszystkie** brzmi „tak":
1. **Czy walka jest satysfakcjonująca?** — ruch, dash i skille reagują dobrze; trafienia mają „punch" (feedback wizualny + chwilowy hit-stop).
2. **Czy jest trudna, ale uczciwa?** — śmierć zawsze czujesz jako swój błąd (zignorowany telegraf, zły dash), nie jako pecha.
3. **Czy hook bogów działa?** — przełączenie boga A → B sprawia, że ta sama klasa **gra się wyraźnie inaczej** (inne decyzje, inny rytm), nie tylko „inny kolor efektu".
4. **Czy chce się zagrać jeszcze raz?** — po śmierci chcesz spróbować innego boga / lepiej.

### 3.3 W zakresie M0
- 1 klasa z bazowym zestawem skilli.
- 4 aktywne skille na Q/W/E/R + dash na spacji.
- 2 bogów, którzy przekształcają te skille (konkretne modyfikatory w 3.7).
- 1 arena (ręcznie zrobiona, zamknięta).
- Wrogowie: 2–3 typy + 1 mini-boss z telegrafowanymi atakami.
- Życie gracza, obrażenia, śmierć + szybki restart areny.
- Ekran wyboru boga przed wejściem na arenę (minimalny, 2 przyciski).
- Programmer-art / proste placeholder sprite'y w palecie „ashen" są OK na tym etapie. Telegrafy muszą być czytelne.

### 3.4 Poza zakresem M0 (świadomie odcięte)
Hub, miasto, NPC, sklepy, stash, loot, ekwipunek, drzewko pasywek, poziomy/expy, generowanie proceduralne, multiplayer, menu główne, zapis gry, dźwięk (poza ew. paroma placeholderami), więcej niż 2 bogów, więcej niż 1 klasa, balans liczbowy „na produkcję".

### 3.5 Architektura — jednostki o jednej odpowiedzialności

Projektujemy w izolowanych modułach (każdy: co robi / jak używać / od czego zależy):

- **InputController** — czyta mysz/klawiaturę, zamienia na intencje (cel ruchu, użycie skilla X, dash). Nie wie nic o walce.
- **MovementController** — click-to-move: prowadzi postać do punktu, obsługuje dash (impuls + i-frames). Zależy od: intencji z Input.
- **SkillSystem** — definiuje skille **jako dane** (zasięg, kształt, bazowe obrażenia, cooldown, tagi typu `melee`/`projectile`/`aoe`/`fire`). Wykonuje skill. Nie zna konkretnych bogów.
- **GodModifierSystem** ⭐ — **najważniejszy i najbardziej ryzykowny moduł.** Bóg to zestaw modyfikatorów/transformacji nakładanych na skille przez **hooki** (np. `onHit`, `onCast`, `modifyShape`, `addStatusEffect`). Skille są „głupie i otwarte na rozszerzenia", bogowie wstrzykują zachowanie. Patrz 3.6.
- **StatusEffectSystem** — DoT/slow/stun itd. (Burn, Chill...), współdzielony przez skille i bogów.
- **EnemyController + TelegraphSystem** — AI wrogów i bossa; **telegraf** = wyraźny kształt na ziemi (krąg/stożek/linia) z czasem ostrzeżenia przed trafieniem. Czytelność = priorytet.
- **CombatResolver** — liczy obrażenia, aplikuje statusy, zarządza HP, śmiercią, hit-stopem i feedbackiem trafienia.
- **Encounter/Arena** — spawnuje fale wrogów + bossa, wykrywa „clear" i „death", restartuje.

**Test izolacji:** dodanie nowego boga = nowy plik z modyfikatorami, **bez dotykania** kodu skilli. Dodanie skilla = nowe dane, bez dotykania kodu bogów. Jeśli tak nie jest — granice modułów są złe.

### 3.6 ⭐ Kluczowa architektura: jak bóg przekształca skill

To serce gry, więc projektujemy to świadomie od początku (M0 udowadnia, że to działa):

- **Skill** = dane + zestaw **punktów rozszerzeń (hooks)**: `OnCast`, `OnHit(target)`, `ModifyProjectile`, `ModifyShape`, `OnKill`. Skill sam w sobie robi rzecz bazową (np. Bolt leci i zadaje obrażenia).
- **Bóg** = lista **modyfikatorów**, z których każdy podpina się pod hooki skilli i pod ich **tagi**. Przykłady mechanizmu:
  - modyfikator „dołóż status X przy trafieniu" → podpina się pod `OnHit` wszystkich skilli z tagiem `fire`.
  - modyfikator „zmień kształt z pojedynczego celu na stożek" → nadpisuje `ModifyShape` dla skilla z tagiem `melee`.
  - modyfikator „przy zabiciu wybuchnij" → `OnKill`.
- Dzięki temu **ten sam skill pod różnymi bogami robi co innego**, a kod skilla się nie zmienia. To jest differentiator i fundament re-grywalności — dlatego musi być czysty już w M0, na 2 bogach, zanim dorzucimy 12.

### 3.7 Konkretny content M0 (do iteracji w trakcie)

**Klasa: „Acolyte"** (uniwersalny wojownik-rzucający), zestaw bazowy:
- **Q — Strike:** szybki melee cios w kierunku kursora (broń główna, niski cooldown).
- **W — Bolt:** pocisk dystansowy.
- **E — Ward:** krótka nova wokół postaci (odpycha/zadaje obrażenia) — skill „obronny/przestrzenny".
- **R — Invocation:** mocniejszy skill z dłuższym cooldownem (np. uderzenie obszarowe).
- **Spacja — Dash:** szybki unik z ~0.2s i-frames i cooldownem (nie da się spamować).

**Bóg A — Pyr, Bóg Żaru (ogień/agresja):**
- Wszystkie skille z tagiem dmg aplikują **Burn** (DoT).
- Strike zyskuje **łukowy AoE** (z pojedynczego celu → stożek).
- Bolt **eksploduje** przy trafieniu.
- *Fantazja: agresywny, „podpalasz wszystko i sprzątasz DoT-ami", nagradza wchodzenie w tłum.*

**Bóg B — Vael, Bóg Głębi (mróz/kontrola):**
- Skille aplikują **Chill** (spowolnienie); na schłodzonych wrogach większe obrażenia.
- Bolt **przebija** wrogów w linii.
- Ward zamienia się w **mrożącą nową** (krótki freeze).
- *Fantazja: kontrolujesz pole walki, kitujesz, rozbijasz spowolnione grupy. Inny rytm niż Pyr.*

> Te same 4 skille, dwa zupełnie różne style gry — to jest dowód, że hook działa.

**Wrogowie:**
- **Husk** — melee swarmer, idzie prosto na gracza. Uczy pozycjonowania i Ward/nova.
- **Spitter** — dystansowy, telegrafowany pocisk. Zmusza do unikania i zamykania dystansu.
- **Mini-boss: „Ashen Warden"** — 3 telegrafowane ataki: (1) slam = krąg na ziemi, (2) sweep = stożek, (3) charge = linia. Test timingu dasha i czytania telegrafów. To on weryfikuje pillar „trudne, ale uczciwe".

### 3.8 Trudność (jak czynimy to skilowym, nie frustrującym)
- Każdy groźny atak ma **czytelny telegraf** z uczciwym oknem reakcji.
- Dash daje **i-frames**, ale ma **cooldown** → trzeba go używać mądrze, nie odruchowo.
- Wrogowie biją mocno (mało „gąbek na obrażenia", szybkie ale karzące starcia).
- Śmierć → natychmiastowy restart areny (pętla „jeszcze raz" bez tarcia).

### 3.9 Testowanie M0
- **Feel / playtest:** sam autor gra wielokrotnie; checklist 3.2 jako bramka.
- **GodModifierSystem (testy jednostkowe C#):** skill + modyfikator boga daje oczekiwane zachowanie (np. Bolt+Pyr → ma flagę `explodes` i aplikuje Burn; Bolt+Vael → `pierces` i aplikuje Chill). To krytyczny moduł, więc pokryty testami, by dało się dodawać bogów bez regresji.
- **CombatResolver (testy jednostkowe):** obrażenia, statusy, śmierć liczą się poprawnie.
- Moduły walki rozdzielone od renderu na tyle, by logikę dało się testować bez uruchamiania pełnej sceny.

---

## 4. Otwarte kwestie (do M1+, nie blokują M0)
- Dokładny model lootu i rzadkości (M1).
- Czy strefy są generowane proceduralnie czy ręczne + warianty (M1).
- Pełny panteon: ilu bogów, czy łączysz kilku naraz (M2).
- Netcode authority model dla co-opu (M3).
- Finalna nazwa gry i identyfikacja wizualna (przed M4/Steam).

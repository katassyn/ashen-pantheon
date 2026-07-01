# Ranger — kit klasy (projekt)

Pierwsza klasa do implementacji. Łowca dystansowy (DEX), zasób = **Koncentracja**.

## Zasób: Koncentracja (Concentration)

- Jak mana, ale **mniejsza pula + szybszy regen**.
- Rytm gry: darmowy/tani spam podtrzymuje presję; drogie skille (np. nr 3) to **bursty**, między którymi regen musi nadążyć. Zarządzanie koncentracją = skill expression.

## Mechanika rdzenia: Oznaczenie (Mark)

Sygnaturowy debuff Rangera.
- **Nakładają:** skill **1** (basic), skill **5** (mina — wszystkim w promieniu), skill **9** (jastrząb).
- **Efekt:** oznaczeni wrogowie otrzymują **zwiększone obrażenia od skilli Rangera**. Dodatkowo bramkuje specjalne interakcje (patrz skille 3 i 9).

## Filozofia kitu

Każda klasa ma **kilka spamowalnych skilli DPS (bez CD)** — wybierasz swój „main spam" pod build. Reszta ma CD. Dla Rangera spamowalne = **1, 2, 3**; CD = **4, 5, 6, 7, 8, 9**.

---

## Skille

### 1. [BASIC] Strzał — starter (lvl 1)
- **Koszt:** 0 koncentracji. **CD:** brak. Spamowalny.
- **NIE przebija.** Pojedynczy cel.
- **Nakłada Oznaczenie.**
- Startowy skill, którym gracz wbija lvl 2.
- *Przyszłe rozwinięcie (drzewko później):* np. przebija wszystkich → masowe oznaczanie.

### 2. Strzała rozbryzgowa
- **Koszt:** koncentracja. **CD:** brak.
- Średni zasięg, **wide (rozbryzg)** → przez szerokość **niższe skalowanie dmg** niż inne skille.
- Oznaczeni obrywają więcej (jak każdy skill Rangera).

### 3. [MAIN single-target DPS] — egzekutor oznaczonych
- **Koszt:** duży. **CD:** brak.
- Na celu **NIEoznaczonym:** nie przebija, zadaje **X**.
- Na celach **oznaczonych:** **przebija wszystkich oznaczonych** i zadaje **2X**.

### 4. Deszcz Strzał (AoE)
- **Koszt + CD.** **Wolniejszy cast** niż inne skille.
- Celujesz obszar → okrągły AoE, spadają strzały.
- Strzały **zostają na ziemi** przez czas, nakładając **slow** wchodzącym.

### 5. Mina
- **Koszt + CD.**
- Stawiasz pod sobą. Wejście wroga → **wybuch AoE**: obrażenia + **stun X s** + **Oznaczenie** wszystkim w promieniu.

### 6. Kolczasta przesieka
- **Koszt + CD.**
- Wystrzeliwujesz rozciągający się po mapie **żywopłot kolców** (zadaje dmg w miarę rozciągania).
- Zostaje **X s**, **mocno spowalnia** przechodzących wrogów, a wchodzącym nakłada **trujący DoT**.

### 7. [MAIN mobility] Dash
- **Koszt + CD.** Czysty odskok/unik — **bez** buffa prędkości.
- Zastępuje usuniętego generycznego dasha (spacja skasowana — mobilność jest teraz skillem w loadoucie).

### 8. Adrenalina
- **Koszt: brak. CD: długi.**
- Na **X s**: duży **movement speed** + **attack/cast speed** + **NIELIMITOWANA koncentracja**.
- Okno mocy / „go crazy".

### 9. Jastrząb
- **Koszt + CD.**
- Przyzywasz jastrzębia; po chwili atakuje: **duży dmg + stun + Oznaczenie**.
- Jeśli cel był **już oznaczony** → **3× obrażeń** i **2× dłuższy stun**.

---

## Loadout

- Pasek: **5 aktywnych slotów** (LPM / PPM / Q / E / R). Wybierasz 5 z puli.
- **Brak stałego dasha na spacji** — mobilność (skill 7) to wybór do loadoutu.

---

## System bogów (doprecyzowany model)

**Bóg dostarcza ALTERNATYWNE wersje skilli.** Gracz **per-skill** wybiera: wersja **BAZOWA** albo wersja **BOGA**.
→ Dzięki temu, jeśli zmiana boga jest nieprzydatna dla danego buildu, gracz i tak może grać bazową wersję tego skilla. Bóg nigdy nie „psuje" — tylko dokłada opcje.

### Przykład: Bóg Dzikich Ostępów (m.in.)
- **Skill 9 (Jastrząb):** zamiast jednego → przyzywasz **3 WIELKIE jastrzębie** jako trwałe **pety-miniony** walczące w zwarciu u twojego boku.
- **Skill 6 (Przesieka):** teraz leci i **znika za sobą**; po trafieniu wroga **eksploduje kolcami dookoła** (trucizna + dmg); **traci CD**; **nie spowalnia**.

> Każdy bóg zmienia działanie wielu skilli — trzeba przy każdym przemyśleć bazę vs wersję boga.

### Otwarte pytania (do decyzji)
1. Czy każdy bóg definiuje wariant dla **wszystkich 9** skilli, czy tylko dla **wybranych** (reszta zostaje bazowa)?
2. Wybór „baza vs bóg" per-skill — robimy w UI loadoutu (przy układaniu paska)?
3. Ilu bogów na start dla Rangera (2–3 do prototypu, docelowo więcej)?

---

## Wpływ na architekturę (`core/`)

Dotychczasowy model „bóg = globalne modyfikatory po tagach" **ewoluuje**:
- Skill = **bazowa definicja** + zestaw **alternatywnych wariantów** dostarczanych przez bogów.
- God = mapa `skillId → wariant`.
- Loadout gracza = per-slot wybór skilla **i** wersji (bazowa / danego boga).
- Nowe mechaniki do modelu: Koncentracja (zasób), Oznaczenie (mark + interakcje), pety/miniony, obszary trwałe na ziemi (slow/DoT), stun.

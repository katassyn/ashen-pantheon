# Zadania graficzne dla Maksa (tablet) — plan na kolejne iteracje

Data: 2026-07-03. Wszystko wpina się 1:1 w istniejące systemy (animator podmienia klatki bez zmian kodu,
ikony to ścieżki w danych). **Format ogólny: PNG z przezroczystością, pixel-art, paleta ciemna
(dark fantasy, popiół/żar — szarości + akcent pomarańczu/fioletu).** Nazwy plików podane przy zadaniach —
wrzucaj do `art/` w repo, ja podłączam.

## P0 — najpierw (odblokowuje "prawdziwy" wygląd gry)

### 1. Bohater: Ranger — sprite sheet
Stany zgodne z animatorem (podmiana 1:1): **idle (2–4 klatki), walk (4–6), attack/strzał (3–4), hit (1–2), death (4–6)**.
- Rozmiar klatki: **32×32 px** (lub 48×48, jeśli potrzebujesz detalu — zdecyduj raz i trzymajmy się tego dla WSZYSTKICH postaci)
- Kierunki: minimum **prawo** (lustrzane odbicie zrobi silnik); docelowo góra/dół, jeśli się wciągniesz
- Plik: `art/player/ranger_{stan}.png` (klatki poziomo obok siebie)

### 2. Potwory bazowe (szablon = Husk)
Te same stany co gracz (idle/walk/windup/attack/hit/death). Windup = zamach przed atakiem (czytelny telegraf!).
- **Husk** 32×32 — zgarbiony popielny nieumarły, `art/monsters/husk_{stan}.png`
- **Spitter** 32×32 — pluje z dystansu (fiolet/chaos), `art/monsters/spitter_{stan}.png`
- **Ashen Warden** 64×64 (boss, scale 2×) — popielny strażnik, ogień, `art/monsters/warden_{stan}.png`

### 3. Tileset strefy: Popielate Obrzeża + miasto
- Kafle **32×32**: ziemia (2–3 warianty), ścieżka, skały/przeszkody (kolizyjne), dekoracje (martwe drzewa, popiół, kości)
- Miasto-hub: bruk, mury, stragan/vendor, skrytka (skrzynia), portal (2–3 klatki animacji), tablica ogłoszeń (przyszłe AH)
- Plik: `art/tiles/ashen_outskirts.png`, `art/tiles/hub.png` (siatka 32×32)

## P1 — UI kit (naprawi "plastikowość" interfejsu)

### 4. Ramki i panele UI
- Panel tła (9-slice: rogi + krawędzie, np. 48×48 z 16px marginesem) — ciemny, kamień/metal
- Ramka slotu itemu **40×40** + warianty kolorów rzadkości: szary/niebieski/żółty/pomarańczowy/czerwony/fioletowy (Normal→Mythic)
- Paski: HP (czerwony), zasób (niebieski), ES (jasnoniebieski), XP (złoty) — końcówki + wypełnienie
- Przycisk normal/hover/pressed
- Plik: `art/ui/panel.png`, `art/ui/slot_{rarity}.png`, `art/ui/bar_{typ}.png`

### 5. Ikony 9 skilli Rangera (+ gwiazdka wariantu boga)
**32×32** ikony: Strzał, Rozbryzg, Egzekutor, Deszcz strzał, Mina, Przesieka, Dash, Adrenalina, Jastrząb.
Osobno mała nakładka "✦ bóg" (róg ikony). Plik: `art/skills/ranger_{id}.png` (id z data/classes/ranger.json).

### 6. Ikony itemów po typie
**32×32** (mieszczą się w slocie 40×40): hełm, naramienniki, zbroja, rękawice, buty, pas, amulet, pierścień,
broń 1H, broń 2H (łuk!), offhand (kołczan). Po 1 bazowej wersji — rzadkość robi ramka.
Plik: `art/items/{kind}.png`.

## P2 — świat i klimat

### 7. Ekran menu głównego
Grafika tła **1920×1080** (może być malowana, nie pixel-art!): panteon w popiołach, sylwetki bogów. + logo "ASHEN PANTHEON".

### 8. Portrety bogów
**Dzikie Ostępy** (natura, jastrząb, zieleń) i **Vharos, Bóg Krwi** (czerwień, ofiara) — po 1 portrecie ~128×128
do panelu wyboru + małe ikonki 24×24 przy skillach.

### 9. VFX kit (proste, czytelne)
Pocisk strzały, pocisk plwociny, wybuch miny (3–4 klatki), krąg deszczu strzał, kolce przesieki, jastrząb,
zloty błysk krytyka, zielona chmurka trucizny, płomień podpalenia. 16×16 / 32×32.

## P3 — projektowanie na kartce/tablecie (koncepty, nie assety)

### 10. Koncept mapy świata 1–50
Rozrysuj **graf stref kampanii**: 8–12 stref od Obrzeży do finału aktu, co ~5 poziomów; gdzie miasto,
gdzie bossy aktów, gdzie wejście do endgame. To zasili data/world/*.json (ja przepiszę na JSON-y).

### 11. Koncepty potworów per strefa
Po 2–3 zwykłe + 1 elitka + boss na strefę (szkice + jedna linijka mechaniki: "leczy innych", "szarżuje po linii",
"dzieli się po śmierci"). Bestiariusz przyjmie wszystko — typy ability już są danymi.

### 12. Koncepty klas: Dragonknight i Spellweaver
Sylwetka, broń, zasób, fantazja klasowa, 9 skilli hasłowo + jak bogowie je przekręcają. Architektura gotowa —
wdrożenie = JSON-y.

---

## Zasady techniczne (żeby wszystko wskoczyło bez poprawek)
1. **Jedna skala**: 32×32 dla ludzi/potworów (boss 64×64). Nie mieszaj rozmiarów klatek w jednym sheecie.
2. Klatki animacji **poziomo, równy odstęp**, tło przezroczyste.
3. Pixel-art rysuj 1:1 (bez skalowania w programie) — silnik powiększa sam, ostro (Nearest per-sprite).
4. Kolizje zostają jak są — grafika nie zmienia hitboxów.
5. Wrzucaj nawet niedokończone do `art/` i commituj — podłączam od razu, zobaczysz w grze.

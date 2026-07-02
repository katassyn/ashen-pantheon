# Ashen Pantheon

Pixelowy, top-down **loot ARPG** w klimacie dark-fantasy. Kilka klas + panteon bogów, którzy przekształcają działanie skilli i definiują buildy. Trudna, skilowa walka. Solo/duo focus, party do 4.

**Silnik:** Godot 4.7 (.NET) + C# · **IDE:** Rider

## Status

Wczesny development — **pełna pętla mechanik gry działa** (faza 2 ukończona):

- **Klasa Ranger** (architektura pod N klas): zasób Koncentracja, mechanika Oznaczenia, **9 skilli**, każdy z **pełnym drzewkiem ulepszeń** (wykluczające się gałęzie, punkty z poziomów)
- **Loadout**: pasek 5 slotów (LPM/PPM/Q/E/R), skille **przeciągane z panelu na pasek** (drag&drop)
- **Bogowie**: pledge + wybór **per-skill** baza/bóg. Dwóch pełnych bogów (wariant każdego z 9 skilli + pasywka): **Dzikie Ostępy** (3 jastrzębie-pety, bomba kolców bez CD, ślad za dashem...) i **Vharos, Bóg Krwi** (koszty HP, lifesteal, krwawy deszcz...)
- **Progresja**: XP z zabójstw → poziomy (cap 100) → punkty atrybutów (+2) i skilli (+1); **respec za złoto**
- **Itemizacja**: rzadkości Normal→Mythic, hand-authored uniki z efektami mechanicznymi, **plecak-tetris jak PoE** (drag&drop, tooltips), 11 slotów EQ, staty z PDF-ów (armour/evasion/ES/resisty z karami 50/75/100)
- **Ekonomia**: złoto + sprzedaż do NPC; **skrytka** w mieście
- **Runy proceduralne**: seedowany plan pokoi (4–5 + boss), przeszkody, skalowanie trudności/XP
- **Husk = wzorcowy wróg-szablon**: maszyna stanów + prawdziwy `AnimationPlayer` (idle/walk/windup/attack/hit/death), telegrafowany atak
- **Persystencja server-ready**: `IGameStateRepository` → lokalny JSON (`user://save.json`); zapis automatyczny
- Logika w `core/` bez zależności od Godota — **41 testów xUnit** 🟢

## Sterowanie

WASD ruch · mysz celowanie · **LPM/PPM/Q/E/R** = sloty paska · **C** staty · **I** ekwipunek · **K** skille+drzewka · **E** interakcja w mieście

## Dokumentacja

- Brief fazy 2: [`docs/design/fable5-next-phase-brief.md`](docs/design/fable5-next-phase-brief.md)
- Decyzje fazy 2: [`docs/design/phase2-implementation-notes.md`](docs/design/phase2-implementation-notes.md)
- Kit Rangera: [`docs/design/ranger-class.md`](docs/design/ranger-class.md)
- Staty i EQ: [`docs/design/stats-and-equipment.md`](docs/design/stats-and-equipment.md)

## Roadmapa

- ~~M0 — rdzeń walki~~ ✅ · ~~M1 — hub + loot~~ ✅ · ~~M2 — głębia (progresja, drzewka, bogowie, itemizacja)~~ ✅
- **Dalej:** prawdziwy art (podmiana placeholderów 1:1), balans/juice, druga klasa, serwer + co-op (party do 4), AH gracz-gracz → demo na Steam

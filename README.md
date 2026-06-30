# Ashen Pantheon

Pixelowy, top-down **loot ARPG** w klimacie dark-fantasy. Kilka klas + panteon bogów, którzy przekształcają działanie skilli i definiują buildy. Trudna, skilowa walka. Solo/duo focus, party do 4.

**Silnik:** Godot 4 + C# · **IDE:** Rider

## Status

Wczesny development.

**✅ M0 rdzeń walki — grywalny prototyp:**
- Sterowanie twin-stick: WASD ruch + celowanie myszą, dash z i-frames
- Skille widoczne i fizyczne: Strike (łuk, LPM) + Bolt (pocisk, PPM), celowane w kursor
- **Hook bogów** end-to-end: ten sam skill inaczej pod Pyrem (ogień: DoT/eksplozja/stożek) vs Vaelem (mróz: spowolnienie/przebicie)
- Wrogowie z AI (Husk: goni, bije) + **mini-boss „Ashen Warden"** z telegrafowanymi atakami (slam/sweep/charge)
- Życie gracza, obrażenia, śmierć → restart areny; paski HP, HUD
- Logika (skille/bogowie/walka) wydzielona do `core/`, pokryta testami (7 🟢)

**▶️ Zostało do domknięcia M0:** pętla areny (fale → clear → win/lose), juice (hit-stop, błyski, screen-shake), strojenie balansu/czytelności. Potem **M1** (hub + loot).

## Dokumentacja

- Spec M0: [`docs/superpowers/specs/2026-06-30-ashen-pantheon-m0-combat-slice-design.md`](docs/superpowers/specs/2026-06-30-ashen-pantheon-m0-combat-slice-design.md)
- Plan M0 Part A: [`docs/superpowers/plans/2026-06-30-ashen-pantheon-m0-part-a-combat-core.md`](docs/superpowers/plans/2026-06-30-ashen-pantheon-m0-part-a-combat-core.md)

## Roadmapa

- **M0** — Vertical slice walki (1 klasa, 4 skille + dash, 2 bogów modyfikujących skille, arena + mini-boss) ← *tu jesteśmy*
- **M1** — Pętla gry: hub-miasto + loot
- **M2** — Głębia: progresja, panteon, druga klasa
- **M3** — Co-op (party do 4)
- **M4** — Content + polish → demo na Steam

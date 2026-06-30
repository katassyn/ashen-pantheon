# Ashen Pantheon

Pixelowy, top-down **loot ARPG** w klimacie dark-fantasy. Kilka klas + panteon bogów, którzy przekształcają działanie skilli i definiują buildy. Trudna, skilowa walka. Solo/duo focus, party do 4.

**Silnik:** Godot 4 + C# · **IDE:** Rider

## Status

Wczesny development.

**✅ M0 Part A ukończone** — grywalny rdzeń: ruch click-to-move + dash z i-frames, 2 skille (Q/Strike, W/Bolt), przełączanie boga (1/Pyr, 2/Vael), treningowy manekin z paskiem HP + statusami (Burn/Chill), HUD. Logika walki i system bogów wydzielone do `core/` i pokryte testami (7 🟢). Hook „bóg przekształca skill" udowodniony end-to-end.

**▶️ Następne: M0 Part B** — realni wrogowie z AI, mini-boss z telegrafami, pętla areny (spawn → clear → śmierć → restart), juice.

## Dokumentacja

- Spec M0: [`docs/superpowers/specs/2026-06-30-ashen-pantheon-m0-combat-slice-design.md`](docs/superpowers/specs/2026-06-30-ashen-pantheon-m0-combat-slice-design.md)
- Plan M0 Part A: [`docs/superpowers/plans/2026-06-30-ashen-pantheon-m0-part-a-combat-core.md`](docs/superpowers/plans/2026-06-30-ashen-pantheon-m0-part-a-combat-core.md)

## Roadmapa

- **M0** — Vertical slice walki (1 klasa, 4 skille + dash, 2 bogów modyfikujących skille, arena + mini-boss) ← *tu jesteśmy*
- **M1** — Pętla gry: hub-miasto + loot
- **M2** — Głębia: progresja, panteon, druga klasa
- **M3** — Co-op (party do 4)
- **M4** — Content + polish → demo na Steam

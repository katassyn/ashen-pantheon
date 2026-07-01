# Statystyki i ekwipunek (planowane) — transkrypcja z notatek

Źródło: `reference/statystyki.pdf`, `reference/ekwipunek.pdf` (odręczne szkice Maksa). System w duchu Path of Exile.

## Sloty ekwipunku

11 slotów (postać w środku, podgląd):

| Zbroja | Biżuteria/Inne | Broń |
|---|---|---|
| Helmet | Belt | Weapon |
| Shoulders | Necklace (amulet) | Off-hand |
| Body Armour | Ring ×2 | |
| Gloves | | |
| Boots | | |

- Bronie **jednoręczne i dwuręczne**.
- **Off-hand tylko dla broni 1-ręcznej** (2H zajmuje oba sloty).

## Atrybuty główne (2 punkty na poziom do wydania)

| Atrybut | Za każdy punkt |
|---|---|
| **Inteligencja** | +2 mana, +1% Energy Shield |
| **Dexterity** | +2 evasion rating, +1% hit rate |
| **Siła** | +2 HP, +1% attack damage |

## Statystyki podstawowe

- **HP** — życie
- **Life regen** — życie/sekundę
- **Energy Shield (ES)** — bufor „przed" HP; po **3 s bez otrzymania obrażeń** zaczyna się odnawiać
- **Evasion rating** — przeliczane na szansę uniku; **diminishing returns** (im więcej, tym trudniej podnieść)
- **Hit rate** — przeliczane na szansę trafienia wroga atakiem (celność)
- **Armour** — przeliczane na **% redukcji obrażeń FIZYCZNYCH**
- **Mana** (+ mana regen) — wydawana na spelle
- **Attack speed / Cast speed** — liczba ataków / castów na sekundę
- **Crit** — (dopisane przez Maksa: szansa + mnożnik krytyka)

## Odporności (resisty)

- **Fire / Cold / Lightning** — max **75%**
- **Chaos** — max **60%** (obrażenia „poza żywiołami": trucizna, klątwy, spowolnienia itp.)
- **Kary progowe:** na poziomach **50 / 75 / 100** gracz dostaje **−20% do wszystkich resistów** (i chaos) — jak kary za akty w PoE. Wymusza szukanie resistów na itemach.

## Typy obrażeń

Physical · Fire · Cold · Lightning · Chaos.

---

> Uwaga implementacyjna: to wyląduje w `core/` jako czysty model (Attributes → derived stats → Combatant rozszerzony o ES/resisty/armour/evasion/crit). Liczby/formuły stroimy później; teraz ważna jest lista i zależności.

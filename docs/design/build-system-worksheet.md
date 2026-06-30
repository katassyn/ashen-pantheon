# Zadanie projektowe: System buildów (klasa × skille × bogowie)

> To jest **serce gry** i pierwszy system do zaprojektowania — definiuje go nasz hook: „kilka klas + wielu bogów, którzy przekształcają skille". Kod (więcej skilli, więcej bogów) czeka na tę treść.

Cel: rozrysować na tablecie **jedną klasę bazową + 5–6 bogów + matrycę**, jak każdy bóg zmienia każdy skill.

---

## Część 1 — Klasa bazowa (na start projektujemy 1)

Wypełnij:
- **Nazwa klasy + fantazja** (1 zdanie — kim jest, czym walczy)
- **4 skille** (sloty Q/W/E/R). Dla każdego:
  - Nazwa
  - **Co robi bazowo** (bez boga): kształt — `pojedynczy cel / stożek / nova / pocisk / linia`; zasięg; melee czy dystans
  - **Tag(i) mechaniczne** (to po nich bogowie „łapią" skill): np. `obrażenia`, `pocisk`, `melee`, `obszar`, `ruch`
  - **Rola**: `dmg / kontrola / mobilność / obrona`

> Ważne: skille bazowo są „neutralne" (bez żywiołu). Żywioł i twist dokłada bóg.

---

## Część 2 — Bogowie (zaprojektuj 5–6)

Dla każdego boga:
- **Nazwa + domena** (ogień, mróz, krew, burza, cień, ziemia, światło...)
- **Fantazja / styl gry** (1 zdanie)
- **REGUŁY** — co robi skillom wg tagów (to jest sedno; bóg = zestaw reguł). Przykłady:
  - „Każdy skill z tagiem `obrażenia` → nakłada Burn (DoT)"
  - „Każdy `pocisk` → eksploduje na trafieniu"
  - „Każdy `melee` → zyskuje stożek (AoE)"
  - „Trafienie w `obszar` → leczy cię za % zadanych obrażeń"
- **Jaki build tworzy** (np. „DoT/AoE agresja", „kontrola + kiting", „lifesteal/sustain", „glass-cannon burst")

> Dobry test: czy każdy bóg sprawia, że ta sama klasa **gra się inaczej**? Jeśli dwóch bogów daje to samo uczucie — jeden jest zbędny.

---

## Część 3 — Matryca (główny rysunek)

Tabela **4 skille (wiersze) × bogowie (kolumny)**. W każdej komórce wpisz/narysuj, jak ten skill wygląda pod tym bogiem (1–2 słowa albo mała ikonka):

|              | Bóg A (ogień) | Bóg B (mróz) | Bóg C (krew) | ... |
|--------------|---------------|--------------|--------------|-----|
| Skill Q      |               |              |              |     |
| Skill W      |               |              |              |     |
| Skill E      |               |              |              |     |
| Skill R      |               |              |              |     |

To od razu pokaże, czy kombinacje są **różnorodne i ciekawe**, czy się powtarzają.

---

## Czego NIE musisz teraz robić
- Liczb/balansu (obrażenia, cooldowny) — to stroję w kodzie
- Drzewka pasywek / ascendancy (to później)
- Finalnych nazw — placeholdery OK

## Deliverable
Kartka/plik (zdjęcie szkicu wystarczy) z: klasą + 4 skillami, listą 5–6 bogów z regułami, i matrycą. To wrzucę 1:1 jako dane do `core/` (SkillDefinition + modyfikatory bogów).

---

### Bonus (jak masz vibe na rysowanie, nie obowiązkowe)
Concept ashen-pixel **3 postaci** do podmiany placeholderów: Gracz, Husk (wróg), Ashen Warden (boss). Sylwetka + paleta popiołów/stali/fioletu. Nawet zgrubny szkic kierunku pomoże.

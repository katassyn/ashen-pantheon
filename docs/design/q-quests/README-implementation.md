# Q1-Q10 (Parallel World) — plan przełożenia na Ashen Pantheon

Źródła (KOMPLET — niczego nie wymyślamy):
- Zarysy questów: `q2 quest.md` … `q10 quest.md` (ten katalog).
- **ROSTERY MOBÓW per Q: `C:\Users\mastu\Downloads\Serwer\Serwer\serwer_dsocraft_zapis\plugins\MythicMobs\mobs\q?_inf.yml`**
  (staty Health/Damage/MovementSpeed/Skills = referencja balansu; wersje _hell/_blood = wyższe trudności).
- **Flow Q1: `C:\Users\mastu\IdeaProjects\MyDungeonTeleportPlugin`** (quests/QuestData.java — etapy, cele, warpy;
  Q1: Forgotten Circle → 25+25 flamecultów → Dragonknight → Grimmor; menu trudności Inf lvl 50 / Hell 65 /
  Bloodshed 80 + opłata "Fragments of Infernal Passage" — przyszła waluta kluczowa).

Zasady od właściciela:
- Zarysy pisane pod ograniczenia Minecrafta — powtarzalność map/questów rozwiązujemy po swojemu.
- Koordynaty portali NIEISTOTNE (MC) — u nas exity stref.
- **Nazwy mobów LEKKO ZMIENIONE (DMCA — oryginały z Drakensang Online).** Tabela niżej.
- Schemat każdego Q: 3 mapy (M1/M2 mini-bossy → M3 arena bossa) + powtarzalny auto-quest (już działa).

## Mapowanie mechanik MC → Ashen Pantheon

| Minecraft | U nas |
|---|---|
| warp q?_m?_trudność | strefa world (`data/world/q?_m?.json`) + exit → kolejna mapa (challenge niesiony przez portal) |
| interakcja z blokiem (grzyb/kocioł/dźwignia/posąg/ołtarz) | marker `interact` (QuestMarkerNode, E) |
| niefizyczny item z moba (licznik % / progresja / gwarancja) | questItem z **pity** (gwarancja po N zabiciach) |
| mini-boss nietykalny przed warunkiem | **damage-gate**: mob `gatedByObjective` → IMMUNE dopóki cel questa niezaliczony |
| obszar trucizny (śmierć po 3 s) | marker `hazard` (+ `requiresObjective` = antidotum daje odporność) |
| fale przy ołtarzu (Q7) | marker `defend` (DefendZone — już jest) |
| trudności inf/hell/blood per Q | skala Q1-Q10 (QScale); trudności zostają w dungeonach grupowych |

## Tabela nazw (DMCA-safe; id → nazwa EN)

| Q | Oryginał (DSO) | U nas (id / nazwa) |
|---|---|---|
| Q1 | grimmag | `grimmor_the_risen` / Grimmor the Risen (nazwa Z PLUGINU właściciela) |
| Q1 | raazghul_the_corruptor | `raazgor_corrupter` / Raazgor the Corrupter (nazwa Z YML właściciela) |
| Q1 | perral_world_dragonknight | `parallel_dragonknight` / Parallel Realm Dragonknight |
| Q2 | xerib_the_hunchback | `xarib_hunchback` / Xarib the Hunchback |
| Q2 | archus_the_mad | `arkhus_the_mad` / Arkhus the Mad |
| Q2 | arachna_scourge_of_duria | `arachnia_scourge` / Arachnia, Scourge of Dural |
| Q3 | parallel_world_evil_miller | `evil_miller` / The Evil Miller |
| Q3 | the_bloody_arrow | `crimson_arrow` / The Crimson Arrow |
| Q3 | undead_king_heredur | `undead_king_haradur` / Undead King Haradur |
| Q4 | ulgar_the_master_butcher | `ulgor_butcher` / Ulgor the Master Butcher |
| Q4 | bearach_champion_of_wilds | `bharok_champion` / Bharok, Champion of the Wilds |
| Q5 | old_jabbax_shaman | `old_jabbok_shaman` / Old Jabbok Shaman |
| Q5 | khalys_leader_of_cultists | `khaliss_cultist_leader` / Khaliss, Leader of Cultists |
| Q6 | mortis(_death_knight/phase3) | `morthys*` / Morthys… (Herald of the Grave) |
| Q6 | murot_high_priest | `murok_high_priest` / Murok the High Priest |
| Q7 | b1000_combat_mechanoid | `b900_mechanoid` / B-900 Combat Mechanoid |
| Q7 | iron_creeper_gate_guard | `iron_colossus_guard` / Iron Colossus Gate Guard |
| Q7 | commander_embersword | `commander_cindersword` / Commander Cindersword |
| Q7 | herald_of_anderworld | `herald_of_the_void` / Herald of the Void |
| Q8 | sigrismarr_priest_of_fjalnir | `sigrimar_priest` / Sigrimar, Priest of Fjolnir |
| Q9 | ebicarus | `ebikaros` / Ebikaros |
| Q9 | asterion / medusa | zostają (mitologia grecka, public domain) |
| Q10 | melas_the_swift_footed | `melax_swift_footed` / Melax the Swift-Footed |
| Q10 | armed_khaross | `armed_kharos` / Armed Kharos |
| Q10 | akheilos | zostaje (mitologia) |
| Q10 | parallel_world_gorga | `gorgatha` / Gorgatha |

Trash bez nazw w zarysach (fallen_warrior, cursed_farmer, slain_assassin…) = nazwy generyczne, zostają.

## Kolejność implementacji

1. **Silnik (batch "q-engine")**: pity-collect, damage-gate, hazard, cel Interact z wildcardem (`q2_mushroom_*`)
   i bramką kolejności (`after`), QRuns w katalogu endgame (per-Q: quest+mapy+tryb arena/world),
   mnożniki Q w strefach world, portal niesie challenge.
2. **Q2 komplet** (wzorzec world-mode): grzyby→kocioł→trucizna→Xarib → Arkhus → Arachnia.
3. Q4, Q5, Q6 (czyste kill/collect — szybkie), Q8 (collect+interact).
4. Q3 (dmg-gate + pity progresywny), Q10 (sekwencja skrzynia→piedestał), Q9 (losowane 4/8 posągów), Q7 (defend-fale).
5. Q bez własnego wpisu w QRuns gra tymczasowo mapami The Final Proving (ciągłość działania).

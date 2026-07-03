# Kampania 1–50 + endgame — wyciąg z configów DsoCraft (kanoniczne źródło importu)

Źródło: `C:\Users\mastu\IdeaProjects\campaignQuestSystem\src\main\resources\quests.yml` (1117 linii, 33 questy)
+ `groupDungeonPlugin/config.yml` + `demonTowerPlugin/config.yml`. Klasy/bogowie NIE stąd — zostają jak ustalone.
Brakuje tylko: **Serwer.rar (MythicMobs)** → statystyki/skille mobów; do czasu dostarczenia staty balansuję sam.

## Strefy kampanii (mapa świata 1–50) — 10 stref co 5 poziomów

| # | Strefa | Poziomy | Questy | Klimat/moby (z targetów questów) |
|---|--------|---------|--------|-----------------------------------|
| 1 | **Swerdfield** | 1–5 | 3 | nieumarli: Undead Villager, Undead Knight |
| 2 | **Catacombs** | 6–10 | 2 | krypty: talking_skeleton, mean_souls, soul_collector |
| 3 | **Silfmoor** | 11–15 | 2 | bagna: bog_beast, leechbeast, shade_crawler |
| 4 | **Teganswall** | 16–20 | 1 | mury: longhelm_evil_guard, death_knight_of_the_watch |
| 5 | **Eternal Watch** | 21–25 | 1 | straż wieczności: protector_of_life_and_death |
| 6 | **Mystra** | 26–30 | 1 | satyry: horned_satyr_warrior/general, drunken_cyclops |
| 7 | **Temple Sector** | 31–35 | 1 | gorgony: gorgon_soldier/heretic, serpent_fighter |
| 8 | **Stalgard** | 36–40 | 1 | mróz/nordyk: cold_skeleton_warrior, frost_paw, frost_catcher, norse_gnome |
| 9 | **Nahuatlan** | 41–45 | 1 | toltekowie/dżungla: obstinate_toltec_warrior/shaman, kenphrox_scarabeus |
| 10 | **Great Desert** | 46–50 | 1 | pustynia: nefertari_priest/snooper, arisen_warrior |

NPC-e kampanii: **Amuun** (mistyk, quest-giver #1), **Guildmaster**, **Robert The Blacksmith's Peon**, Chef (side questy).
Pierwsze questy (wzorzec łańcucha): „The Mystic's Summons" → „The Fallen Knights" → ... (prerequisites + next_quest).
Typy celów w danych: **TALK / KILL / COLLECT** (+ dungeon-clear w epilogu). Nagrody: exp + money (+item).

## Epilog: dungeony Q1–Q10 (od poziomu 50)

Questy epilogu: „The Final Proving" → **„The First Seal" … „The Fifth Seal"** (Q1–Q5) → **„God of Death"**,
„Beyond Death", „Harbinger of Fire" (fire_demon, commander_emberwing), „Frozen Faith", „Gorgon Queen".
Moby epilogu: demon_warden, furious_fiend, soulless_dragon_warrior, dragon_berserk_thug, black_furred_berserk/mauler, t1000 😄.

## Endgame 51–100 (mapy zaawansowane, po 1 quescie farmowym)

„Heart of Darkness" · „Glacial Frontier" · „Jungle Warriors" · „Shadow Empire" · „Corrupted Forest" · „The Final Frontier".

## Dungeony grupowe (tiery T1–T5, mitologiczne, na klucze)

| Tier | Nazwa | Klucz |
|------|-------|-------|
| T1 | The Odyssey of Shadows | [T1] Mythological Dungeon Key |
| T2 | Poseidon's Underwater Temple | [T2] |
| T3 | Mount Olympus | [T3] |
| T4 | Daedalus' Eternal Labyrinth | [T4] |
| T5 | Fields of Immortal Souls | [T5] |

(Trudności Blood/Hell/Infernal z groupDungeonPlugin — wymogi poziomu/party + opłaty wejścia.)

## Demon Tower
Wieża pięter: wymogi poziomu per piętro, klucze per piętro, odblokowanie od piętra 3, fale+bossy (demonTowerPlugin).

## Plan przełożenia na Ashen Pantheon
1. **data/world/**: 10 stref kampanii (nazwy jak wyżej, levelMin/Max, mob packi z mobów strefy) — bestiariusz dostanie
   nowe potwory per strefa (staty: moje do czasu MythicMobs z Serwer.rar).
2. **Quest framework** (core + data/quests/*.json): typy TALK/KILL/COLLECT/CLEAR, prerequisites, next_quest,
   nagrody exp/gold/item, dialogi — struktura 1:1 z quests.yml.
3. **Epilog/endgame**: Q1–Q10 jako solo-dungeony (instancje jak arena), T1–T5 grupowe z kluczami, Demon Tower — po kampanii.
4. Nazwy traktujemy jako kanon DsoCraft (user może później zmienić w danych).

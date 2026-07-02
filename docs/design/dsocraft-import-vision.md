# Wizja: import mechanik DsoCraft → Ashen Pantheon

Data: 2026-07-02. Źródła: strona `github.com/katassyn/stronDsocraft` + ~35 pluginów w `C:\Users\mastu\IdeaProjects`.
DsoCraft = MMO RPG (inspiracja Drakensang) zbudowany przez Maksa na Minecrafcie. Ashen Pantheon jest jego
pełnoprawną realizacją jako samodzielna gra — pluginy to sprawdzone projekty mechanik do przenoszenia.

## KOREKTY względem wcześniejszej roadmapy (wiążące)

1. **BEZ sezonów.** Model MMO live-service: ciągłe update'y contentu — lepszy gear, nowe mechaniki, eventy.
   (Sekcja "sezony" z fazy 7 roadmapy — skreślona; architektura eventów zamiast lig.)
2. **Struktura świata:**
   - **Kampania/fabuła na MAPIE ŚWIATA do poziomu 50** — trwałe lokacje (nie instancjonowane runy),
     questy fabularne prowadzą przez strefy.
   - **Po 50: lokacja endgame'owa** (hub) i stamtąd **specjalne dungeony do farmienia na różnych
     poziomach trudności** (wzór: solo Q1–Q10 + grupowe Blood/Hell/Infernal + Demon Tower z piętrami).
3. **Mob packi na mapach świata: stały respawn co X czasu** (import z DsoCraft) — pack ma pozycję,
   skład (z bestiariusza) i timer odrodzenia; farmisz obszary jak w Drakensang/Metin2.

## Systemy DsoCraft do importu (katalog → priorytety)

### Rdzeń progresji (wysoki priorytet)
| Plugin/system | Co robi | Notatki importu |
|---|---|---|
| **MyExperiencePlugin** | XP, cap 100, party XP-share, klasy: **Ranger / Dragonknight / Spellweaver**, ascendancje na lvl 20 (po 3 na klasę, np. Beastmaster/Shadowstalker/Earthwarden), alchemia | Nazwy 2 kolejnych klas! Ascendancje w AP zastępują bogowie — ale progi/pacing do przejęcia |
| **campaignQuestSystem / mainQuestPlugin** | Kampania fabularna: storyline, NPC, tracking ukończeń dungeonów | Fundament fabuły 1–50 na mapie świata |
| **QuestSystemNew / dailyQuestPlugin** | Questy daily/weekly/monthly | Retencja MMO |
| **groupDungeonPlugin** | Dungeony grupowe: trudności **Blood/Hell/Infernal**, wymogi poziomu/party, opłaty wejścia, portale | Wzór endgame'owych dungeonów AP |
| **demonTowerPlugin** | Wieża: fale, bossy, progresja pięter | Drugi typ endgame (nasza obecna arena ≈ proto) |
| **MyDungeonTeleportPlugin** | Solo dungeony Q1–Q10 | Tierowane solo farmienie |

### Itemizacja i buildy (wysoki)
| System | Co robi | Notatki |
|---|---|---|
| **Runy** (strona: runes.php) | Sloty run co 5 lvl od 50 (do 9), miks dowolny. Runy m.in.: Uruz (+dmg), Algiz (DR po hicie), Shield (bariera <30% HP), Thurisaz (odbicie), Wunjo (luck), Laguz (lifesteal), Gebo (overheal-bariera), Ehwaz (sprint guard), Berkano (cleanse) | Idealne pod nasz data-driven core |
| **Runic Words** (runicwords.php) | Nazwane kombinacje run (Runic Tether, Surgical Sever, Blessing Theft, Hunter's Mark, ...) | "Słowa" = bonusy za konkretne zestawy — głębia buildów |
| **Jewele/sockety** (jewels.php) | Gemy w socketach: Emberfang (+dmg), Windstep (+speed), Whirlwind (+atk speed), Heartroot (+HP), Stonehide (+def), Lasting Healing | Sockety na itemach → nowy wymiar affixów |
| **TrinketsPlugin + bossouls.php** | Akcesoria + **Boss Souls**: dusze bossów (Grimmag/Arachna/Heredur/Bearach...) z efektami na moby i graczy | Trofea z bossów jako sloty trinket |
| **MyCraftingPlugin2 / IngredientPouchPlugin / ScientistPlugin / biologPlugin** | Crafting, sakwa składników, research (biolog — wiedza o rodzinach mobów) | Crafting endgame |
| **graveKeeper** | Statystyki zabójstw rodzin mobów → bonusy (knowledge system) | Świetny long-term grind |

### Ekonomia i światowość (średni)
| System | Notatki |
|---|---|
| **auctionHousePlugin** | Mamy fundament (faza 4 walidacja) — AH gracz-gracz na meta-serwerze |
| **bloodChestPlugin / lockpickChestPlugin** | Skrzynie świata: krwawe skrzynie, wytrychy — loot-eventy na mapach |
| **mineSystemPlugin** | Instancjonowana kopalnia: sfery ze schematów, custom rudy, mobki, waluty |
| **FishingPlugin / farmingPlugin / beesPlugin** | Life-skille (łowienie/farmy/pszczoły) — spokojny content |
| **petplugin (31 petów)** | Pety bojowe/utility — mamy proto (jastrzębie Dzikich Ostępów) |
| **eventPlugin / DailyRewardsPlugin / BroadcastPlugin** | Eventy live-service, daily login, ogłoszenia |
| **PlayerDataPlugin (MySQL)** | Odpowiednik naszego meta-serwera ✓ już mamy |
| **MainMenuPlugin / MyShopPlugin / storageplugin / MyTeleportPlugin** | UI/QoL: menu, sklepy NPC ✓, magazyn ✓ (stash), teleporty między strefami |

## Przełożenie na architekturę Ashen Pantheon

**Dwa typy map (koegzystują):**
1. **Mapy świata (kampania 1–50):** trwałe sceny-strefy połączone przejściami; **MobPack** = {pozycja, skład z bestiariusza, respawn co X s}; questy fabularne (kill/collect/reach/dungeon-clear); NPC.
   W multi: mapa hostowana jak dziś (instancja hosta, drużyna wspólnie).
2. **Dungeony endgame (po 50):** instancjonowane runy — nasz obecny system aren/RunGenerator jest już
   prototypem tego; dokładamy **tiery trudności** (Blood/Hell/Infernal — mnożniki+lepsze tabele lootu),
   **wymogi wejścia** (poziom/opłata), typy: solo Q-line, grupowe, wieża piętrowa.

**Kolejność importu (propozycja):**
1. **Mapy świata + mob packi + przejścia stref** (zmiana strukturalna — rdzeń "MMO feel")
2. **Questy kampanii** (prosty framework: zabij/zbierz/dotrzyj/ukończ + nagrody + łańcuchy)
3. **Endgame: tiery dungeonów** (Blood/Hell/Infernal na obecnych runach + wymogi wejścia)
4. **Runy + Runic Words** (data-driven jak bestiariusz)
5. **Sockety + jewele**, potem **Boss Souls/trinkety**
6. Daily/weekly questy, eventy, skrzynie świata, kopalnia, pety, life-skille…

Każdy import = nasz standardowy cykl: model w `core/` (testy) → dane JSON → warstwa Godot → walidacja headless.

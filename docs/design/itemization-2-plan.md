# Orchestration plan: Itemization 2.0 (i dalej)

Data: 2026-07-04. Stan: kampania 1-50 ✅, systemy MMO (mapa/party/czat/handel/AH-blok/friends/guildie online) ✅,
opcje/rebind ✅. 84 testy zielone.

## Dlaczego ta faza (decyzja architektoniczna)

**Dziura systemowa:** LootGenerator rolluje IDENTYCZNE wartości affixów na poziomie 1 i 50 — kampania ma
progresję poziomów, ale itemizacja NIE. To psuje pętlę ARPG po ~10 poziomie i blokuje sensowną ekonomię.
**Kolejność ma znaczenie:** globalny (cross-lobby) AH budujemy dopiero PO domknięciu modelu itemu
(ilvl + sockety), żeby kontrakt rynku nie churnował.

## Faza bieżąca: Itemization 2.0

| Batch | Zakres | Status |
|---|---|---|
| A | **Item Level (ilvl)**: Item.ItemLevel, MonsterDefinition.Level, skalowanie AffixRanges `f(ilvl)=0.2+0.8·min(1, ilvl/50)`, LootGenerator/LootTables niosą ilvl, cena vendora skaluje, dto/mapper, walidator ilvl-aware (legacy=50) | → |
| B | **Sockets & Jewels** (import DsoCraft): ItemKind.Jewel + katalog data/jewels.json (Emberfang/Windstep/Whirlwind/Heartroot/Stonehide/Lasting Healing), sloty per kind+ilvl (2H/body:3, 1H/helm:2, reszta zbroi:1), BuildSheet wlicza jewele, NOWY affix MoveSpeed (Windstep) → sheet → prędkość gracza, walidator (capy, legalność) | ⏳ |
| C | Drop jeweli (loot tables `"jewel"`), socketing UI (drag jewel→założony item; permanentne po włożeniu — D2 style), poziomy potworów w JSON | ⏳ |
| D | Testy + e2e walidator + smoke + commit + docs | ⏳ |

## Kolejka następnych faz (w tej kolejności, każda za zgodą właściciela)

1. **Online economy**: cross-lobby AH na meta-serwerze (księga w DB, escrow serwerowy, kontrakt = ItemDto
   z ilvl/socketami) + poczta (offline delivery sprzedaży/zwrotów) + presence znajomych.
2. **Endgame** (czeka na greenlight właściciela): hub endgame, solo Q1–Q10, grupowe T1–T5 na kluczach
   (Blood/Hell/Infernal = mnożniki + lepsze tabele+ilvl), Demon Tower.
3. **Runes + Runic Words** (DsoCraft, poziom 50+; naturalnie po endgame): sloty run co 5 lvl od 50, miks do 9,
   nazwane komba — na naszym języku efektów.
4. **Boss Souls / trinkety** + graveKeeper (wiedza o rodzinach mobów).

## Zasady niezmienne
- Logika w core (xUnit), Godot renderuje. Content = dane. Cała gra po angielsku. Walidator serwera = ta sama
  biblioteka core. Subskrypcje statycznych eventów Net wyłącznie nazwanymi handlerami (odpinane w _ExitTree).

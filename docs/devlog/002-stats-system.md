---
tags:
  - devlog
  - systems
  - stats
---

# DEVLOG #002 — The Stats System Is Done

*April 20, 2026 · by Katassyn*

---

Three days in. First system locked.

I spent the last few days on something that sounds boring but matters more than almost anything else in an ARPG — **the stats system**. HP, damage, resistances, evasion, the math that decides whether you live or die when a boss does something mean. Everything else in the game rests on these numbers. If the stats are wrong, nothing else works.

Here's what I landed on.

## 2 Points Per Level — STR / DEX / INT

Every level you get **2 attribute points**. Spend them wherever. Strength, Dexterity, or Intelligence.

| Attribute | What It Does |
|---|---|
| STR | +7 HP, +1% phys damage, scales Block Strength |
| DEX | +2 Evasion Rating, +1% Hit Rate |
| INT | +2 Mana, +5 Energy Shield |

The important part: **attributes are not where your power comes from**. Items and the skill tree are. Attributes give you a baseline and gate what gear you can equip. This is a deliberate choice — I don't want people feeling like they need to min-max a spreadsheet to get their build working. Your build comes from skills and items. Attributes support that.

## Defense Is A Choice

I'm giving players multiple defensive layers and letting them pick which to invest in:

- **HP** — straightforward life total.
- **Energy Shield** — absorbs damage before HP, recharges fully after 5 seconds of no damage. Any hit resets the timer. INT-based.
- **Evasion** — dodge chance, hard-capped at 75%. DEX-based. Diminishing returns so you can't freely stack it to the cap.
- **Armour** — PoE-style percentage physical reduction. Strong vs small hits, weak vs big ones. Forces trade-offs.
- **Block** — block chance × block strength. STR-based. Block Strength has diminishing returns too.

You can go pure HP, pure ES, hybrid HP+Armour, evasion-based, block-focused. The point is that no single defense is the "right" one. Each has a weakness that a smart boss design can exploit.

## Resistances — And The Part People Won't Like

Resistances are straight damage reduction from an element.

| Resist | Cap | Covers |
|---|---|---|
| Fire | 75% | Fire damage |
| Cold | 75% | Cold damage |
| Lightning | 75% | Lightning damage |
| Chaos | 60% | DoTs, poison, bleed, curses, acid, ignitions |

Here's the spicy part: at levels **50, 75, and 100**, you get hit with a **−20% penalty to all resistances**. Cumulative.

| Level | Total Penalty |
|---|---|
| 50 | −20% |
| 75 | −40% |
| 100 | −60% |

This is stolen directly from PoE because it works. Without resistance penalties, you cap your res at level 20 and never think about it again. With them, endgame gearing forces real decisions — you're constantly juggling fire vs cold vs lightning vs chaos, deciding what to sacrifice on each gear piece.

It's going to feel bad the first time you hit 50 and watch your resists drop. That's the point.

## Crit Is Equal For Everyone

Crit Damage base is **200%** for every character. No cap. Not scaled by any attribute. Items and the skill tree scale it from there.

This is different from most ARPGs where crit damage scales from some stat. I wanted crit to be a **flat universal baseline** so a melee Warrior who crits hits just as hard *as a crit* as a Mage who crits — the difference in damage comes from what the base hit was, not from some hidden multiplier on their sheet.

## What's NOT Scaling From Attributes

Important clarifications:

- **Spell damage is not a stat.** Skills scale from their own attribute — INT spells scale from INT, etc.
- **DoT damage** does not scale from main attributes. It uses the DPS number on the skill tooltip.
- **Minion stats** scale from player attributes (formulas TBD per minion type — Beastmaster pets probably DEX, Arcanist summons INT).

The goal everywhere: attributes support, items and tree drive.

## What's Still TBD

I'm not going to pretend this is all solved. There are 11 open questions on the [full Stats & Resistances page](../systems/stats-and-resistances.md) — things like the exact Armour formula, the Evasion diminishing returns curve, the Hit Rate vs Evasion math. These get tuned once combat is playable and I can actually feel the numbers.

But the framework is locked. No more "should we even have Energy Shield?" questions. That era is over.

## What's Next

Next system: **the skill tree**.

I'm going Last Epoch-style — per-skill specialization trees plus a class passive tree. Not PoE's giant shared web. The reason: I'm one person. PoE's tree is incredible but requires years of balancing. Last Epoch's approach gives the same build depth with a fraction of the complexity surface.

That's devlog #003.

— Maks

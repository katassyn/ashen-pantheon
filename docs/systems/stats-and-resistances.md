---
tags:
  - systems
  - stats
  - resistances
---

# Stats & Resistances

**Status:** :material-check-circle:{ .ap-ok } Finalized · **Version:** 1.0

Core attribute and defense system. Each level grants **2 attribute points** to spend freely across Strength, Dexterity, and Intelligence.

---

## Core Attributes

| Attribute | Per Point | Role |
|---|---|---|
| **Strength (STR)** | +7 HP, +1% physical damage, scales Block Strength | Melee playstyle stat — primary for Warrior |
| **Dexterity (DEX)** | +2 Evasion Rating, +1% Hit Rate | Evasion-based defense + accuracy — primary for Archer |
| **Intelligence (INT)** | +2 Mana, +5 Energy Shield (flat) | Caster stat, scales spell skills — primary for Mage |

**Notes:**

- **Attributes are a supplement, not the primary source of scaling.** Main power scaling comes from items and the skill tree. Attributes provide baseline stats and unlock item/skill requirements.
- Spell damage is not a standalone stat. Skills scale off attributes — INT-based skills scale from INT, etc.
- Hybrid scaling is possible: e.g., a spell that requires a sword scales from INT **and** the weapon's base damage (indirectly making STR relevant through weapon choice).
- DoT damage does **not** scale from main attributes. DoTs use the DPS number shown on the skill tooltip.
- Minion stats scale from player attributes (exact formulas TBD per minion type).
- Attributes also serve as **item requirements** (STR/DEX/INT thresholds for equipping gear).

---

## Defensive Stats

| Stat | Description |
|---|---|
| **HP** | Life total. |
| **Life Regen** | Base X HP/sec for every character (flat starting value, scales with items/tree). |
| **Energy Shield (ES)** | Absorbs damage before HP. Fully recharges over 5 seconds if no damage is taken. Any hit during recharge resets the timer. |
| **Evasion Rating** | Chance to fully dodge incoming attacks. Diminishing returns. Hard cap: **75%**. |
| **Armour** | % Physical damage reduction. PoE-style scaling — strong vs small/frequent hits, weak vs big single hits. |
| **Block Chance** | Chance to block an incoming hit. |
| **Block Strength** | % of damage negated when a block occurs. Scaled by STR with diminishing returns — harder to cap the higher it gets. |

---

## Offensive Stats

| Stat | Description |
|---|---|
| **Attack Speed** | Number of attacks per second. |
| **Cast Speed** | Number of spells cast per second. |
| **Hit Rate** | Chance for an attack to land vs enemy Evasion. Calculated against enemy level and evasion rating. |
| **Crit Chance** | Chance for a hit to crit. *(Source TBD — not from main stats. Likely items + skill tree.)* |
| **Crit Damage** | **Base 200% for all characters. No cap.** Acts as a multiplier on **all player damage** (physical, elemental, spell, DoT — everything). Not scaled by any attribute — equal baseline for everyone. Can be increased by items / skill tree nodes. |

---

## Utility Stats

| Stat | Description |
|---|---|
| **Mana** | Resource spent on skills. |
| **Mana Regen** | Mana restored per second. |
| **Movement Speed** | Character movement velocity. Critical for dodging boss telegraphs. |

---

## Resistances

Resistances reduce damage from their respective element. Physical damage is handled by Armour — not a resistance.

| Resistance | Max Cap | Covers |
|---|---|---|
| **Fire Resist** | 75% | Fire damage |
| **Cold Resist** | 75% | Cold damage |
| **Lightning Resist** | 75% | Lightning damage |
| **Chaos Resist** | 60% | Non-elemental DoTs: ignitions, acid, curses, poison, bleeds, etc. |

### Resistance Penalties

At **levels 50, 75, and 100**, the player receives a **-20% penalty to all resistances** (elemental and chaos). Cumulative.

| Level Reached | Total Penalty |
|---|---|
| 50 | −20% all res |
| 75 | −40% all res |
| 100 | −60% all res |

This forces active resistance gearing through the endgame.

---

## Level-Based Combat Mechanics

Combat math scales with level difference between attacker and defender.

- Each character (player and enemy) has a **base level** that influences base Hit Rate and base Evasion.
- If a mob is a higher level than the player, the player's effective Hit Rate drops — resulting in missed attacks.
- Likewise, lower-level enemies struggle to hit the player's Evasion.
- **Exact level-diff curves:** TBD — will be tuned during balance passes.

---

## Open Questions / TBD

The following need decisions before implementation:

1. **Crit Chance source** — Items only? Skill tree? Any base crit chance for everyone?
2. **Armour formula** — exact PoE-style curve (e.g., `armour / (armour + K × damage)`) and K value.
3. **Evasion diminishing returns curve** — at what rating does evasion reach 50%, 75%?
4. **Hit Rate vs Evasion formula** — how level difference modifies hit chance.
5. **Base Life Regen value** — what's X? Same for all classes or class-dependent?
6. **Base Attack Speed / Cast Speed per class** — starting values.
7. **Minion scaling formulas** — how Arcanist summons scale from INT, Beastmaster pets from DEX, etc.
8. **Status effects / CC** — is there stun? If yes, is there Stun Resist / Stagger mechanic?
9. **Weapon base damage** — how base weapon damage interacts with STR % scaling on melee attacks.
10. **Block Strength diminishing returns curve** — STR-to-block-strength ratio.
11. **Hybrid scaling weights** — when a spell requires a weapon and scales from both INT and weapon damage, what's the split ratio?

---

**Status:** :material-check-circle:{ .ap-ok } Finalized · **Version:** 1.0

**Next section:** Skill Tree — Last Epoch-style (per-skill specialization trees + class passive tree), NOT PoE's shared web layout.

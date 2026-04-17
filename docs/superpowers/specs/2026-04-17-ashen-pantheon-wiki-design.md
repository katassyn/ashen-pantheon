# Ashen Pantheon Wiki — Design Spec
**Date:** 2026-04-17  
**Status:** Approved

---

## Overview

A public game wiki for **Ashen Pantheon**, a top-down 2D instanced action RPG. Built with MkDocs Material, hosted on GitHub Pages at `https://katassyn.github.io/ashen-pantheon/`.

---

## Tech Stack

| Layer | Choice |
|---|---|
| Static site generator | MkDocs Material |
| Hosting | GitHub Pages (`gh-pages` branch) |
| CI/CD | GitHub Actions |
| Python deps | `mkdocs-material`, `mkdocs-git-revision-date-localized`, `mkdocs-tags-plugin` (via Material), `mkdocs-minify-plugin` |

---

## Project Structure

```
Ashen Pantheon/
├── mkdocs.yml
├── logo_256x256.png
├── requirements.txt
├── docs/
│   ├── index.md
│   ├── game-overview.md
│   ├── tags.md                     # auto-generated tags index
│   ├── classes/
│   │   ├── index.md
│   │   ├── warrior.md
│   │   ├── mage.md
│   │   └── archer.md
│   ├── ascendancies/
│   │   ├── berserker.md
│   │   ├── guardian.md
│   │   ├── paladin.md
│   │   ├── elementalist.md
│   │   ├── arcanist.md
│   │   ├── hexblade.md
│   │   ├── shadowstalker.md
│   │   ├── beastmaster.md
│   │   └── pathfinder.md
│   ├── systems/
│   │   ├── skill-tree.md
│   │   ├── ascendancy-tree.md
│   │   ├── skill-slots.md
│   │   ├── progression.md
│   │   └── instancing.md
│   ├── bosses/
│   │   ├── index.md
│   │   └── boss-phases.md
│   ├── world/
│   │   ├── index.md
│   │   └── maps.md
│   └── devlog/
│       ├── index.md
│       └── 001-the-beginning.md
└── .github/
    └── workflows/
        └── deploy.yml
```

---

## mkdocs.yml Configuration

```yaml
site_name: Ashen Pantheon Wiki
site_url: https://katassyn.github.io/ashen-pantheon/
repo_url: https://github.com/katassyn/ashen-pantheon

theme:
  name: material
  logo: logo_256x256.png
  favicon: logo_256x256.png
  palette:
    scheme: slate
    primary: amber
    accent: amber
  features:
    - navigation.tabs
    - navigation.sections
    - navigation.top
    - search.suggest
    - search.highlight

plugins:
  - search
  - tags
  - git-revision-date-localized:
      enable_creation_date: true
  - minify:
      minify_html: true
```

---

## Content Strategy

Each page includes:
- Real intro paragraph contextualizing the topic
- All known content from the game spec
- `[TODO]` blocks for sections that will grow (skill names, boss HP, loot tables, screenshots)

### Page-by-page breakdown

**`index.md`** — Hero landing page: tagline, brief game description, quick-link cards to major sections.

**`game-overview.md`** — Core concept, world, lore intro. Covers: instanced world model, global layer (chat/rankings/trading), level cap 100, mythology boss theme.

**`classes/index.md`** — Overview of all 3 base classes with links. Brief role summary for each.

**`classes/warrior.md`** — Warrior lore/role + links to Berserker, Guardian, Paladin ascendancies.

**`classes/mage.md`** — Mage lore/role + links to Elementalist, Arcanist, Hexblade.

**`classes/archer.md`** — Archer lore/role + links to Shadowstalker, Beastmaster, Pathfinder.

**Ascendancy pages (9 files)** — Each gets: flavor description, playstyle summary, core mechanics (rage stacking, lifesteal, etc.), [TODO] skill list.

**`systems/skill-tree.md`** — Levels 1–50 broad skill tree. Class-specific skills, many nodes. [TODO] node map.

**`systems/ascendancy-tree.md`** — Level 50–100. 1 point per 2 levels = 25 total points. Fewer, stronger nodes. [TODO] tree diagram.

**`systems/skill-slots.md`** — Free keybind system: any unlocked skill assigned to any key, no restrictions.

**`systems/progression.md`** — Full level 1–100 arc: skill tree phase → ascendancy choice → ascendancy tree phase → endgame gear farming.

**`systems/instancing.md`** — Each player owns their world instance. Party up to 4. Global layer visible to all (chat, rankings, trading).

**`bosses/index.md`** — Boss design philosophy: mythologies covered (Greek, Norse, Egyptian, Japanese, Slavic), epic multi-phase fights. [TODO] boss roster.

**`bosses/boss-phases.md`** — Multi-phase fight system overview. [TODO] phase mechanics per boss.

**`world/index.md`** — World overview. [TODO] zone list, lore regions.

**`world/maps.md`** — Map/zone system overview. [TODO] zone details, instance types.

**`devlog/index.md`** — Devlog hub with links to all entries.

**`devlog/001-the-beginning.md`** — First entry: what the project is, why it's being built, early vision.

---

## GitHub Actions Deployment

`.github/workflows/deploy.yml`:
- Trigger: push to `main`
- Steps: checkout → setup Python → pip install → `mkdocs gh-deploy --force`
- Deploys to `gh-pages` branch; GitHub Pages serves from there

---

## Go-Live Commands

```bash
# 1. Create GitHub repo
gh repo create katassyn/ashen-pantheon --public

# 2. Initialize git and push
git init
git add .
git commit -m "Initial wiki scaffold"
git remote add origin https://github.com/katassyn/ashen-pantheon
git push -u origin main

# 3. Enable GitHub Pages in repo settings:
#    Settings → Pages → Source: Deploy from branch → gh-pages / root
#    (GitHub Actions will create the gh-pages branch on first push)
```

---

## Out of Scope (this iteration)

- Boss-specific individual pages (covered by [TODO] in boss index)
- Item/gear database
- Interactive skill tree visualizer
- User accounts or comments
- MkDocs Material Insiders features (social cards, etc.)

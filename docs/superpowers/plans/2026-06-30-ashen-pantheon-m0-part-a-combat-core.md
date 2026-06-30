# Ashen Pantheon — M0 Part A: Combat Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **For a human beginner:** Each step is labeled **🖱️ Godot** (klikasz w edytorze) lub **⌨️ Kod** (piszesz w Riderze). Idź po kolei, nie przeskakuj.

**Goal:** Grywalna scena, w której jedną klasą chodzisz (click-to-move), dashujesz, rzucasz 2 skille na treningowy manekin, i możesz przełączyć boga (Pyr/Vael), co wyraźnie zmienia działanie tych samych skilli — plus czysta, otestowana biblioteka logiki.

**Architecture:** Logika gry (skille, bogowie, obliczenia walki) żyje w osobnej, czystej bibliotece C# `AshenPantheon.Core` **bez zależności od Godota** — dzięki temu jest testowalna zwykłym xUnit bez uruchamiania silnika. Projekt Godot referuje tę bibliotekę i tylko „ożywia" jej wyniki na węzłach (ruch, sprite'y, kolizje). To realizuje zasadę ze speca: bóg = zestaw modyfikatorów nakładanych na „głupie" skille przez hooki.

**Tech Stack:** Godot 4.7 (.NET/mono), C# (.NET 8), xUnit (testy), Rider (IDE), git.

---

## Struktura plików (co tworzymy)

```
ashen-pantheon/
├── project.godot                       # 🖱️ generuje Godot
├── AshenPantheon.csproj                # 🖱️ generuje Godot (projekt gry)
├── AshenPantheon.sln                   # 🖱️ generuje Godot
├── scenes/
│   ├── Main.tscn                       # arena/scena testowa
│   ├── Player.tscn                     # postać gracza
│   └── Dummy.tscn                      # treningowy manekin
├── scripts/
│   ├── PlayerController.cs             # ruch, dash, input skilli (bridge → Core)
│   ├── Dummy.cs                        # manekin: trzyma Combatant, przyjmuje obrażenia
│   └── GodCatalog.cs                   # definicje Pyr/Vael + skille (most danych → Core)
├── core/
│   ├── AshenPantheon.Core.csproj       # ⌨️ czysta biblioteka logiki (.NET 8)
│   ├── Damage.cs                       # enumy: SkillTag, SkillShape, StatusType
│   ├── SkillDefinition.cs             # bazowa definicja skilla (dane)
│   ├── ResolvedSkill.cs                # skill po nałożeniu modyfikatorów boga
│   ├── God.cs                          # bóg = lista modyfikatorów
│   ├── GodModifierSystem.cs            # Resolve(skill, god) → ResolvedSkill  ⭐
│   ├── Combatant.cs                    # HP + aktywne statusy
│   └── CombatResolver.cs               # liczy obrażenia/statusy/śmierć
└── tests/
    ├── AshenPantheon.Core.Tests.csproj # ⌨️ xUnit
    ├── GodModifierSystemTests.cs
    └── CombatResolverTests.cs
```

---

## Task 0: Projekt Godot + C# + git

**Files:** tworzy Godot (`project.godot`, `AshenPantheon.csproj`, `AshenPantheon.sln`)

- [ ] **Step 1 (🖱️ Godot): Załóż projekt**

Odpal `Godot_v4.7-stable_mono_win64.exe`. W Project Manager → **Create** → New Project:
- Project Name: `Ashen Pantheon`
- Project Path: **wskaż istniejący folder** `C:\Users\mastu\Desktop\Maks\ashen-pantheon` (ten z README i git). Godot ostrzeże, że folder nie jest pusty — to OK, **Create & Edit**.
- Renderer: **Mobile** (lekki, w zupełności wystarczy do 2D pixel).

- [ ] **Step 2 (🖱️ Godot): Wygeneruj C# solution**

Górne menu **Project → Tools → C#... → Create C# solution**. To utworzy `AshenPantheon.csproj` i `AshenPantheon.sln`.

- [ ] **Step 3 (🖱️ Godot): Ustaw pixel-perfect rendering**

**Project → Project Settings**:
- `Rendering → Textures → Canvas Textures → Default Texture Filter` = **Nearest** (ostre piksele, bez rozmycia).
- `Display → Window → Stretch → Mode` = **canvas_items**.

- [ ] **Step 4 (🖱️ Godot): Zdefiniuj Input Map**

**Project Settings → Input Map**. Dodaj akcje (pole „Add New Action", potem `+` by przypisać klawisz):
- `move_click` → Mouse Button **Left**
- `dash` → Key **Space**
- `skill_q` → Key **Q**
- `skill_w` → Key **W**
- `skill_e` → Key **E**
- `skill_r` → Key **R**

- [ ] **Step 5 (⌨️ Kod): Commit**

```bash
cd "C:/Users/mastu/Desktop/Maks/ashen-pantheon"
git add -A
git commit -m "chore: scaffold Godot 4 C# project with input map"
```

---

## Task 1: Biblioteka Core + enumy (fundament logiki)

**Files:**
- Create: `core/AshenPantheon.Core.csproj`
- Create: `core/Damage.cs`

- [ ] **Step 1 (⌨️ Kod): Utwórz projekt biblioteki**

W terminalu Ridera (lub PowerShell) w katalogu repo:

```bash
cd "C:/Users/mastu/Desktop/Maks/ashen-pantheon"
dotnet new classlib -n AshenPantheon.Core -o core -f net8.0
# usuń domyślny plik szablonu
rm core/Class1.cs
```

- [ ] **Step 2 (⌨️ Kod): Dodaj bibliotekę do solution i jako referencję projektu gry**

```bash
dotnet sln AshenPantheon.sln add core/AshenPantheon.Core.csproj
dotnet add AshenPantheon.csproj reference core/AshenPantheon.Core.csproj
```

- [ ] **Step 3 (⌨️ Kod): Napisz enumy `core/Damage.cs`**

```csharp
namespace AshenPantheon.Core;

public enum SkillTag { Damage, Melee, Projectile }

public enum SkillShape { SingleTarget, Cone, Nova, Projectile }

public enum StatusType { None, Burn, Chill }
```

- [ ] **Step 4 (⌨️ Kod): Zbuduj, by potwierdzić że kompiluje**

Run: `dotnet build core/AshenPantheon.Core.csproj`
Expected: `Build succeeded`.

- [ ] **Step 5 (⌨️ Kod): Commit**

```bash
git add core AshenPantheon.csproj AshenPantheon.sln
git commit -m "feat(core): add Core library and damage enums"
```

---

## Task 2: Model skilla — SkillDefinition i ResolvedSkill

**Files:**
- Create: `core/SkillDefinition.cs`
- Create: `core/ResolvedSkill.cs`

- [ ] **Step 1 (⌨️ Kod): `core/SkillDefinition.cs` — bazowe dane skilla**

```csharp
using System.Collections.Generic;

namespace AshenPantheon.Core;

/// <summary>Niezmienna, bazowa definicja skilla — "głupia", bez wiedzy o bogach.</summary>
public sealed class SkillDefinition
{
    public required string Id { get; init; }
    public required float BaseDamage { get; init; }
    public required float Cooldown { get; init; }
    public required SkillShape Shape { get; init; }
    public HashSet<SkillTag> Tags { get; init; } = new();
}
```

- [ ] **Step 2 (⌨️ Kod): `core/ResolvedSkill.cs` — skill po modyfikatorach boga**

```csharp
namespace AshenPantheon.Core;

/// <summary>Skill po nałożeniu modyfikatorów boga. To z tego korzysta warstwa Godota i CombatResolver.</summary>
public sealed class ResolvedSkill
{
    public required string Id { get; init; }
    public float Damage { get; set; }
    public SkillShape Shape { get; set; }
    public StatusType OnHitStatus { get; set; } = StatusType.None;
    public float StatusDuration { get; set; }
    public bool Explodes { get; set; }
    public bool Pierces { get; set; }
}
```

- [ ] **Step 3 (⌨️ Kod): Zbuduj**

Run: `dotnet build core/AshenPantheon.Core.csproj`
Expected: `Build succeeded`.

- [ ] **Step 4 (⌨️ Kod): Commit**

```bash
git add core
git commit -m "feat(core): add SkillDefinition and ResolvedSkill models"
```

---

## Task 3: ⭐ GodModifierSystem (TDD — serce gry)

**Files:**
- Create: `core/God.cs`
- Create: `core/GodModifierSystem.cs`
- Create: `tests/AshenPantheon.Core.Tests.csproj`
- Test: `tests/GodModifierSystemTests.cs`

- [ ] **Step 1 (⌨️ Kod): Zdefiniuj typ boga `core/God.cs`**

Bóg = lista modyfikatorów. Modyfikator to funkcja, która dostaje bazową definicję i mutuje `ResolvedSkill`.

```csharp
using System;
using System.Collections.Generic;

namespace AshenPantheon.Core;

/// <summary>Modyfikator: dostaje bazową definicję (by sprawdzić tagi) i mutuje wynik.</summary>
public delegate void SkillModifier(SkillDefinition def, ResolvedSkill resolved);

public sealed class God
{
    public required string Name { get; init; }
    public List<SkillModifier> Modifiers { get; init; } = new();
}
```

- [ ] **Step 2 (⌨️ Kod): Utwórz projekt testów i podłącz**

```bash
cd "C:/Users/mastu/Desktop/Maks/ashen-pantheon"
dotnet new xunit -n AshenPantheon.Core.Tests -o tests -f net8.0
dotnet add tests/AshenPantheon.Core.Tests.csproj reference core/AshenPantheon.Core.csproj
dotnet sln AshenPantheon.sln add tests/AshenPantheon.Core.Tests.csproj
rm tests/UnitTest1.cs
```

- [ ] **Step 3 (⌨️ Kod): Napisz failing test `tests/GodModifierSystemTests.cs`**

```csharp
using AshenPantheon.Core;
using Xunit;

public class GodModifierSystemTests
{
    private static SkillDefinition Bolt() => new()
    {
        Id = "bolt", BaseDamage = 10f, Cooldown = 0.5f,
        Shape = SkillShape.Projectile,
        Tags = new() { SkillTag.Damage, SkillTag.Projectile }
    };

    [Fact]
    public void NoGod_ResolvesToBaseValues()
    {
        var resolved = GodModifierSystem.Resolve(Bolt(), god: null);
        Assert.Equal(10f, resolved.Damage);
        Assert.Equal(SkillShape.Projectile, resolved.Shape);
        Assert.Equal(StatusType.None, resolved.OnHitStatus);
        Assert.False(resolved.Explodes);
        Assert.False(resolved.Pierces);
    }

    [Fact]
    public void Pyr_MakesBoltExplodeAndBurn()
    {
        var resolved = GodModifierSystem.Resolve(Bolt(), GodCatalogForTests.Pyr);
        Assert.True(resolved.Explodes);
        Assert.Equal(StatusType.Burn, resolved.OnHitStatus);
    }

    [Fact]
    public void Vael_MakesBoltPierceAndChill()
    {
        var resolved = GodModifierSystem.Resolve(Bolt(), GodCatalogForTests.Vael);
        Assert.True(resolved.Pierces);
        Assert.Equal(StatusType.Chill, resolved.OnHitStatus);
    }
}

/// <summary>Mini-katalog bogów na potrzeby testów logiki (warstwa Godota użyje własnego, patrz Task 7).</summary>
internal static class GodCatalogForTests
{
    public static readonly God Pyr = new()
    {
        Name = "Pyr",
        Modifiers =
        {
            (def, r) => { if (def.Tags.Contains(SkillTag.Damage)) { r.OnHitStatus = StatusType.Burn; r.StatusDuration = 3f; } },
            (def, r) => { if (def.Tags.Contains(SkillTag.Projectile)) r.Explodes = true; },
        }
    };

    public static readonly God Vael = new()
    {
        Name = "Vael",
        Modifiers =
        {
            (def, r) => { if (def.Tags.Contains(SkillTag.Damage)) { r.OnHitStatus = StatusType.Chill; r.StatusDuration = 2f; } },
            (def, r) => { if (def.Tags.Contains(SkillTag.Projectile)) r.Pierces = true; },
        }
    };
}
```

- [ ] **Step 4 (⌨️ Kod): Uruchom test — ma NIE przejść (klasa nie istnieje)**

Run: `dotnet test tests/AshenPantheon.Core.Tests.csproj`
Expected: FAIL — `GodModifierSystem` does not exist (błąd kompilacji).

- [ ] **Step 5 (⌨️ Kod): Napisz `core/GodModifierSystem.cs` (minimalna implementacja)**

```csharp
namespace AshenPantheon.Core;

public static class GodModifierSystem
{
    /// <summary>Buduje ResolvedSkill z bazowej definicji i (opcjonalnego) boga.</summary>
    public static ResolvedSkill Resolve(SkillDefinition def, God? god)
    {
        var resolved = new ResolvedSkill
        {
            Id = def.Id,
            Damage = def.BaseDamage,
            Shape = def.Shape,
        };

        if (god is not null)
            foreach (var modifier in god.Modifiers)
                modifier(def, resolved);

        return resolved;
    }
}
```

- [ ] **Step 6 (⌨️ Kod): Uruchom testy — mają przejść**

Run: `dotnet test tests/AshenPantheon.Core.Tests.csproj`
Expected: PASS (3 passed).

- [ ] **Step 7 (⌨️ Kod): Commit**

```bash
git add core tests
git commit -m "feat(core): god modifier system that transforms skills (tested)"
```

---

## Task 4: Combatant + CombatResolver (TDD)

**Files:**
- Create: `core/Combatant.cs`
- Create: `core/CombatResolver.cs`
- Test: `tests/CombatResolverTests.cs`

- [ ] **Step 1 (⌨️ Kod): `core/Combatant.cs`**

```csharp
namespace AshenPantheon.Core;

public sealed class Combatant
{
    public required float MaxHealth { get; init; }
    public float Health { get; set; }
    public StatusType ActiveStatus { get; set; } = StatusType.None;
    public float StatusTimeLeft { get; set; }

    public bool IsDead => Health <= 0f;
    public bool IsChilled => ActiveStatus == StatusType.Chill && StatusTimeLeft > 0f;
}
```

- [ ] **Step 2 (⌨️ Kod): Napisz failing test `tests/CombatResolverTests.cs`**

```csharp
using AshenPantheon.Core;
using Xunit;

public class CombatResolverTests
{
    private static Combatant Target(float hp = 100f) => new() { MaxHealth = hp, Health = hp };

    [Fact]
    public void BasicHit_SubtractsDamage()
    {
        var target = Target();
        var skill = new ResolvedSkill { Id = "x", Damage = 30f, Shape = SkillShape.SingleTarget };

        CombatResolver.ApplyHit(skill, target);

        Assert.Equal(70f, target.Health);
    }

    [Fact]
    public void OnHitStatus_IsAppliedToTarget()
    {
        var target = Target();
        var skill = new ResolvedSkill { Id = "x", Damage = 10f, Shape = SkillShape.Projectile,
            OnHitStatus = StatusType.Burn, StatusDuration = 3f };

        CombatResolver.ApplyHit(skill, target);

        Assert.Equal(StatusType.Burn, target.ActiveStatus);
        Assert.Equal(3f, target.StatusTimeLeft);
    }

    [Fact]
    public void ChilledTarget_TakesBonusDamage()
    {
        var target = Target();
        target.ActiveStatus = StatusType.Chill;
        target.StatusTimeLeft = 2f;
        var skill = new ResolvedSkill { Id = "x", Damage = 100f, Shape = SkillShape.SingleTarget };

        CombatResolver.ApplyHit(skill, target);

        // 100 dmg + 25% bonus za chill = 125 → HP 100 - 125 = dead (<=0)
        Assert.True(target.IsDead);
    }

    [Fact]
    public void LethalDamage_MarksDead()
    {
        var target = Target(20f);
        var skill = new ResolvedSkill { Id = "x", Damage = 25f, Shape = SkillShape.SingleTarget };

        CombatResolver.ApplyHit(skill, target);

        Assert.True(target.IsDead);
    }
}
```

- [ ] **Step 3 (⌨️ Kod): Uruchom — ma NIE przejść**

Run: `dotnet test tests/AshenPantheon.Core.Tests.csproj`
Expected: FAIL — `CombatResolver` does not exist.

- [ ] **Step 4 (⌨️ Kod): Napisz `core/CombatResolver.cs`**

```csharp
namespace AshenPantheon.Core;

public static class CombatResolver
{
    public const float ChillBonusMultiplier = 1.25f;

    /// <summary>Aplikuje pojedyncze trafienie skilla na cel: obrażenia (z bonusem za chill) + status.</summary>
    public static void ApplyHit(ResolvedSkill skill, Combatant target)
    {
        float damage = skill.Damage;
        if (target.IsChilled)
            damage *= ChillBonusMultiplier;

        target.Health -= damage;

        if (skill.OnHitStatus != StatusType.None)
        {
            target.ActiveStatus = skill.OnHitStatus;
            target.StatusTimeLeft = skill.StatusDuration;
        }
    }
}
```

- [ ] **Step 5 (⌨️ Kod): Uruchom testy — mają przejść**

Run: `dotnet test tests/AshenPantheon.Core.Tests.csproj`
Expected: PASS (7 passed łącznie z Task 3).

- [ ] **Step 6 (⌨️ Kod): Commit**

```bash
git add core tests
git commit -m "feat(core): combat resolver with chill bonus and status application (tested)"
```

---

## Task 5: Scena gracza + ruch click-to-move

**Files:**
- Create: `scenes/Player.tscn`
- Create: `scripts/PlayerController.cs`
- Create: `scenes/Main.tscn`

- [ ] **Step 1 (🖱️ Godot): Zbuduj scenę Player**

Scene → **New Scene** → jako root dodaj **CharacterBody2D**, zmień nazwę na `Player`. Dodaj dzieci (prawy klik na Player → Add Child Node):
- **Sprite2D** — w Inspektorze `Texture` ustaw na wbudowane `icon.svg` (placeholder; podmienimy na pixel-art później). Przeskaluj `Scale` na `0.3, 0.3`.
- **CollisionShape2D** — w Inspektorze `Shape` → New CircleShape2D, promień ~16.
- **Camera2D** — zostaw domyślne (kamera podąża za graczem).

Zapisz jako `scenes/Player.tscn`.

- [ ] **Step 2 (⌨️ Kod): Napisz `scripts/PlayerController.cs` (ruch do kliknięcia)**

```csharp
using Godot;

public partial class PlayerController : CharacterBody2D
{
    [Export] public float Speed = 220f;
    [Export] public float ArriveDistance = 6f;

    private Vector2 _targetPosition;
    private bool _hasTarget;

    public override void _Ready()
    {
        _targetPosition = GlobalPosition;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("move_click"))
        {
            _targetPosition = GetGlobalMousePosition();
            _hasTarget = true;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_hasTarget && GlobalPosition.DistanceTo(_targetPosition) > ArriveDistance)
        {
            Vector2 direction = (_targetPosition - GlobalPosition).Normalized();
            Velocity = direction * Speed;
        }
        else
        {
            Velocity = Vector2.Zero;
            _hasTarget = false;
        }

        MoveAndSlide();
    }
}
```

- [ ] **Step 3 (🖱️ Godot): Podłącz skrypt do węzła Player**

Zaznacz węzeł `Player` → w Inspektorze ikona zwoju („Attach Script") → **Load** → wskaż `scripts/PlayerController.cs` (albo wklej ścieżkę). Zapisz scenę.

- [ ] **Step 4 (🖱️ Godot): Zbuduj scenę Main**

New Scene → root **Node2D** o nazwie `Main`. Dodaj dziecko: instancję sceny (ikona łańcucha „Instantiate Child Scene") → `scenes/Player.tscn`. Dodaj też **ColorRect** (tło) rozciągnięty na ekran, kolor ciemny `#15131c` (paleta „ashen"). Ustaw Main jako główną scenę: zapisz `scenes/Main.tscn`, potem **Project → Project Settings → Application → Run → Main Scene** = `scenes/Main.tscn`.

- [ ] **Step 5 (🖱️ Godot): Uruchom i sprawdź ruch**

Naciśnij **F5** (Run Project). Expected: postać (ikonka) stoi na ciemnym tle; klikasz LPM gdziekolwiek → postać płynnie idzie do kursora i zatrzymuje się. Kamera podąża.

- [ ] **Step 6 (⌨️ Kod): Commit**

```bash
git add -A
git commit -m "feat: player scene with click-to-move movement"
```

---

## Task 6: Dash z i-frames

**Files:**
- Modify: `scripts/PlayerController.cs`

- [ ] **Step 1 (⌨️ Kod): Dodaj dash do `PlayerController.cs`**

Dodaj pola pod istniejące `[Export]`-y:

```csharp
    [Export] public float DashSpeed = 700f;
    [Export] public float DashDuration = 0.15f;
    [Export] public float DashCooldown = 0.8f;
    [Export] public float IFrameDuration = 0.2f;

    private float _dashTimeLeft;
    private float _dashCdLeft;
    private float _iFrameLeft;
    private Vector2 _dashDirection;

    public bool IsInvulnerable => _iFrameLeft > 0f;
```

Dodaj obsługę inputu w `_UnhandledInput` (pod istniejący `if`):

```csharp
        if (@event.IsActionPressed("dash") && _dashCdLeft <= 0f && _dashTimeLeft <= 0f)
        {
            Vector2 dir = (GetGlobalMousePosition() - GlobalPosition).Normalized();
            if (dir == Vector2.Zero) dir = Vector2.Down;
            _dashDirection = dir;
            _dashTimeLeft = DashDuration;
            _iFrameLeft = IFrameDuration;
            _dashCdLeft = DashCooldown;
            _hasTarget = false;
        }
```

Zmień `_PhysicsProcess`, by dash miał priorytet nad ruchem:

```csharp
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_dashCdLeft > 0f) _dashCdLeft -= dt;
        if (_iFrameLeft > 0f) _iFrameLeft -= dt;

        if (_dashTimeLeft > 0f)
        {
            _dashTimeLeft -= dt;
            Velocity = _dashDirection * DashSpeed;
            MoveAndSlide();
            return;
        }

        if (_hasTarget && GlobalPosition.DistanceTo(_targetPosition) > ArriveDistance)
        {
            Vector2 direction = (_targetPosition - GlobalPosition).Normalized();
            Velocity = direction * Speed;
        }
        else
        {
            Velocity = Vector2.Zero;
            _hasTarget = false;
        }

        MoveAndSlide();
    }
```

- [ ] **Step 2 (🖱️ Godot): Uruchom i sprawdź dash**

F5. Expected: wciśnięcie **Spacji** szarpie postać w stronę kursora szybkim skokiem; zaraz po nie da się dashować ponownie (cooldown ~0.8s). Ruch normalny działa jak wcześniej.

- [ ] **Step 3 (⌨️ Kod): Commit**

```bash
git add scripts/PlayerController.cs
git commit -m "feat: dash with i-frames and cooldown"
```

---

## Task 7: Most danych — GodCatalog (skille + bogowie po stronie gry)

**Files:**
- Create: `scripts/GodCatalog.cs`

- [ ] **Step 1 (⌨️ Kod): `scripts/GodCatalog.cs` — definicje content w warstwie gry, oparte o Core**

To jedyne miejsce, gdzie warstwa Godota zna konkretny content. Logikę liczy Core.

```csharp
using System.Collections.Generic;
using AshenPantheon.Core;

/// <summary>Definicje skilli klasy Acolyte i bogów Pyr/Vael. Korzysta z czystej logiki Core.</summary>
public static class GodCatalog
{
    public static readonly SkillDefinition Strike = new()
    {
        Id = "strike", BaseDamage = 18f, Cooldown = 0.4f,
        Shape = SkillShape.SingleTarget,
        Tags = new() { SkillTag.Damage, SkillTag.Melee }
    };

    public static readonly SkillDefinition Bolt = new()
    {
        Id = "bolt", BaseDamage = 12f, Cooldown = 0.6f,
        Shape = SkillShape.Projectile,
        Tags = new() { SkillTag.Damage, SkillTag.Projectile }
    };

    public static readonly God Pyr = new()
    {
        Name = "Pyr",
        Modifiers =
        {
            (def, r) => { if (def.Tags.Contains(SkillTag.Damage)) { r.OnHitStatus = StatusType.Burn; r.StatusDuration = 3f; } },
            (def, r) => { if (def.Tags.Contains(SkillTag.Projectile)) r.Explodes = true; },
            (def, r) => { if (def.Tags.Contains(SkillTag.Melee)) r.Shape = SkillShape.Cone; },
        }
    };

    public static readonly God Vael = new()
    {
        Name = "Vael",
        Modifiers =
        {
            (def, r) => { if (def.Tags.Contains(SkillTag.Damage)) { r.OnHitStatus = StatusType.Chill; r.StatusDuration = 2f; } },
            (def, r) => { if (def.Tags.Contains(SkillTag.Projectile)) r.Pierces = true; },
        }
    };
}
```

- [ ] **Step 2 (⌨️ Kod): Zbuduj projekt gry, by potwierdzić referencję do Core działa**

Run: `dotnet build AshenPantheon.csproj`
Expected: `Build succeeded`.

- [ ] **Step 3 (⌨️ Kod): Commit**

```bash
git add scripts/GodCatalog.cs
git commit -m "feat: god catalog bridging game content to Core logic"
```

---

## Task 8: Treningowy manekin + zadawanie obrażeń skillami

**Files:**
- Create: `scenes/Dummy.tscn`
- Create: `scripts/Dummy.cs`
- Modify: `scripts/PlayerController.cs`
- Modify: `scenes/Main.tscn` (przez Godot)

- [ ] **Step 1 (⌨️ Kod): `scripts/Dummy.cs` — manekin trzyma Combatant**

```csharp
using Godot;
using AshenPantheon.Core;

public partial class Dummy : Area2D
{
    private readonly Combatant _combatant = new() { MaxHealth = 200f, Health = 200f };

    public override void _Process(double delta)
    {
        // tykanie statusów (Burn DoT / wygasanie Chill)
        if (_combatant.StatusTimeLeft > 0f)
        {
            _combatant.StatusTimeLeft -= (float)delta;
            if (_combatant.ActiveStatus == StatusType.Burn)
                _combatant.Health -= 8f * (float)delta; // DoT
            if (_combatant.StatusTimeLeft <= 0f)
                _combatant.ActiveStatus = StatusType.None;
        }
    }

    /// <summary>Wywoływane przez gracza przy trafieniu.</summary>
    public void ReceiveHit(ResolvedSkill skill)
    {
        CombatResolver.ApplyHit(skill, _combatant);
        GD.Print($"Dummy HP: {_combatant.Health:0} | status: {_combatant.ActiveStatus} | chill?: {_combatant.IsChilled}");
        Modulate = _combatant.ActiveStatus switch
        {
            StatusType.Burn => new Color(1f, 0.4f, 0.2f),
            StatusType.Chill => new Color(0.4f, 0.7f, 1f),
            _ => Colors.White
        };
        if (_combatant.IsDead)
            QueueFree();
    }
}
```

- [ ] **Step 2 (🖱️ Godot): Zbuduj scenę Dummy**

New Scene → root **Area2D** o nazwie `Dummy`. Dzieci:
- **Sprite2D** z `icon.svg`, `Scale` `0.25,0.25`, `Modulate` czerwonawy.
- **CollisionShape2D** → CircleShape2D promień ~18.

Podłącz skrypt `scripts/Dummy.cs` do roota. Zapisz `scenes/Dummy.tscn`.

- [ ] **Step 3 (🖱️ Godot): Wstaw manekin do Main**

Otwórz `scenes/Main.tscn` → Instantiate Child Scene → `scenes/Dummy.tscn`. Przesuń go kawałek od gracza (np. pozycja `300, 0`).

- [ ] **Step 4 (⌨️ Kod): Dodaj rzucanie skilli w `PlayerController.cs`**

Dodaj `using AshenPantheon.Core;` na górze. Dodaj pole boga i metodę rzucania, oraz obsługę Q/W w `_UnhandledInput`:

```csharp
    public God ActiveGod = GodCatalog.Pyr; // domyślny bóg; zmieniany w Task 9

    private void CastOnNearestDummy(SkillDefinition def)
    {
        ResolvedSkill resolved = GodModifierSystem.Resolve(def, ActiveGod);

        // znajdź najbliższego Dummy w zasięgu i zaaplikuj trafienie (uproszczenie M0 Part A)
        Dummy nearest = null;
        float best = float.MaxValue;
        foreach (Node node in GetTree().GetNodesInGroup("dummies"))
        {
            if (node is Dummy d)
            {
                float dist = GlobalPosition.DistanceTo(d.GlobalPosition);
                if (dist < best) { best = dist; nearest = d; }
            }
        }
        nearest?.ReceiveHit(resolved);
    }
```

W `_UnhandledInput` dodaj:

```csharp
        if (@event.IsActionPressed("skill_q")) CastOnNearestDummy(GodCatalog.Strike);
        if (@event.IsActionPressed("skill_w")) CastOnNearestDummy(GodCatalog.Bolt);
```

- [ ] **Step 5 (🖱️ Godot): Dodaj Dummy do grupy „dummies"**

Otwórz `scenes/Dummy.tscn` → zaznacz root `Dummy` → zakładka **Node → Groups** (obok Inspektora) → wpisz `dummies` → Add. Zapisz.

- [ ] **Step 6 (🖱️ Godot): Uruchom i sprawdź walkę**

F5. Otwórz dolny panel **Output**. Expected: wciskasz **Q/W** → w Output leci `Dummy HP: ...` malejące; manekin zmienia kolor na pomarańczowy (Burn, bo domyślny bóg Pyr); po zejściu HP do 0 manekin znika.

- [ ] **Step 7 (⌨️ Kod): Commit**

```bash
git add -A
git commit -m "feat: training dummy taking damage and statuses from skills"
```

---

## Task 9: Ekran wyboru boga — dowód hooka

**Files:**
- Create: `scenes/GodSelect.tscn`
- Create: `scripts/GodSelect.cs`
- Modify: `scenes/Main.tscn`

- [ ] **Step 1 (⌨️ Kod): `scripts/GodSelect.cs` — 2 przyciski ustawiają boga gracza**

```csharp
using Godot;

public partial class GodSelect : CanvasLayer
{
    [Export] public NodePath PlayerPath;

    private PlayerController _player;

    public override void _Ready()
    {
        _player = GetNode<PlayerController>(PlayerPath);
        GetNode<Button>("%PyrButton").Pressed += () => SelectGod(GodCatalog.Pyr, "Pyr (ogień)");
        GetNode<Button>("%VaelButton").Pressed += () => SelectGod(GodCatalog.Vael, "Vael (mróz)");
    }

    private void SelectGod(AshenPantheon.Core.God god, string label)
    {
        _player.ActiveGod = god;
        GetNode<Label>("%CurrentGod").Text = $"Bóg: {label}";
        GD.Print($"Wybrano boga: {god.Name}");
    }
}
```

- [ ] **Step 2 (🖱️ Godot): Zbuduj UI wyboru boga**

New Scene → root **CanvasLayer** o nazwie `GodSelect`. Dzieci (Add Child):
- **VBoxContainer** (gdzieś w rogu).
  - **Label** — w Inspektorze zaznacz **Unique Name in Owner** (`%`), nazwij `CurrentGod`, tekst „Bóg: Pyr (ogień)".
  - **Button** — Unique Name `PyrButton`, tekst „Pyr — ogień".
  - **Button** — Unique Name `VaelButton`, tekst „Vael — mróz".

Podłącz skrypt `scripts/GodSelect.cs` do roota. Zapisz `scenes/GodSelect.tscn`.

- [ ] **Step 3 (🖱️ Godot): Wstaw GodSelect do Main i wskaż gracza**

Otwórz `scenes/Main.tscn` → Instantiate Child Scene → `scenes/GodSelect.tscn`. Zaznacz `GodSelect` → w Inspektorze `Player Path` ustaw na węzeł `Player`.

- [ ] **Step 4 (🖱️ Godot): Uruchom — KLUCZOWY TEST HOOKA**

F5. Spawnij manekin, kliknij **Vael**, wciśnij **W** (Bolt). Expected: w Output status manekina = **Chill**, manekin robi się niebieski, kolejne trafienia liczą bonus (`chill?: True`). Kliknij **Pyr**, **W** ponownie → status **Burn**, manekin pomarańczowy, HP spada też między trafieniami (DoT).
**To jest dowód, że ten sam skill pod różnymi bogami działa inaczej** — czyli hook ze speca żyje.

- [ ] **Step 5 (⌨️ Kod): Commit**

```bash
git add -A
git commit -m "feat: god selection UI proving skill-transform hook"
```

---

## Task 10: Domknięcie Part A — README statusu + tag

**Files:**
- Modify: `README.md`

- [ ] **Step 1 (⌨️ Kod): Dopisz sekcję statusu w README**

Pod „## Status" dodaj:

```markdown
**M0 Part A ukończone:** click-to-move, dash z i-frames, czysta otestowana logika (skille/bogowie/walka), 2 skille (Strike/Bolt) × 2 bogów (Pyr/Vael) na treningowym manekinie z dowodem działania hooka. Następne: M0 Part B (wrogowie, mini-boss z telegrafami, pętla areny).
```

- [ ] **Step 2 (⌨️ Kod): Uruchom pełny zestaw testów na koniec**

Run: `dotnet test`
Expected: PASS (wszystkie testy Core zielone).

- [ ] **Step 3 (⌨️ Kod): Commit + tag + push**

```bash
git add README.md
git commit -m "docs: mark M0 Part A complete"
git tag m0-part-a
git push origin main --tags
```

---

## Czego Part A świadomie NIE robi (→ M0 Part B, osobny plan)

- Realni wrogowie z AI (Husk, Spitter) zamiast manekina.
- Mini-boss „Ashen Warden" + **system telegrafów** (kręgi/stożki/linie na ziemi).
- Realne pociski/AoE jako węzły (zamiast „uderz najbliższego") — kształt Cone/Nova/Projectile renderowany i kolizyjny.
- Życie gracza, śmierć, i-frame'y kontra ataki wroga.
- Pętla areny: spawn fal → clear → death → szybki restart.
- Juice: hit-stop, błyski trafień, screen shake.
- Skille E/R i dźwięk.

Te punkty wymagają działającego rdzenia z Part A jako fundamentu — dlatego dostają własny plan po ukończeniu Part A.
```

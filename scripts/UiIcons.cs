using Godot;
using AshenPantheon.Core;

/// <summary>Proceduralne ikony UI rysowane kodem (bez zewnętrznych assetów): skille, typy itemów,
/// składniki sakwy, statusy. Każda metoda rysuje w lokalnym układzie [-1..1] przeskalowanym przez r.
/// Kolory z motywu; gotowe pod podmianę na pixel-art (te same węzły, inny render).</summary>
public static class UiIcons
{
    // ── SKILLE (charakterystyczny symbol per skill rangera) ──
    public static void Skill(CanvasItem ci, string id, Vector2 c, float r, Color col)
    {
        switch (id)
        {
            case "basic":  Arrow(ci, c, r, col); break;
            case "spread": SpreadArrows(ci, c, r, col); break;
            case "exec":   Reticle(ci, c, r, col); break;
            case "rain":   Rain(ci, c, r, col); break;
            case "mine":   Trap(ci, c, r, col); break;
            case "hedge":  Spikes(ci, c, r, col); break;
            case "dash":   Chevrons(ci, c, r, col); break;
            case "adrenaline": Bolt(ci, c, r, col); break;
            case "hawk":   Bird(ci, c, r, col); break;
            default:       ci.DrawCircle(c, r * 0.5f, col); break;
        }
    }

    private static void Arrow(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawLine(c + new Vector2(-r * 0.7f, r * 0.7f), c + new Vector2(r * 0.7f, -r * 0.7f), col, 3f);
        ci.DrawColoredPolygon(new[] {
            c + new Vector2(r * 0.7f, -r * 0.7f), c + new Vector2(r * 0.2f, -r * 0.7f), c + new Vector2(r * 0.7f, -r * 0.2f)
        }, col);
    }

    private static void SpreadArrows(CanvasItem ci, Vector2 c, float r, Color col)
    {
        foreach (float a in new[] { -0.5f, 0f, 0.5f })
        {
            var d = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            ci.DrawLine(c + d * r * 0.2f, c + d * r * 0.9f, col, 2.5f);
            var tip = c + d * r * 0.9f;
            var perp = new Vector2(-d.Y, d.X) * r * 0.18f;
            ci.DrawColoredPolygon(new[] { tip, tip - d * r * 0.25f + perp, tip - d * r * 0.25f - perp }, col);
        }
    }

    private static void Reticle(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawArc(c, r * 0.75f, 0, Mathf.Tau, 28, col, 2.5f);
        foreach (var d in new[] { Vector2.Right, Vector2.Left, Vector2.Up, Vector2.Down })
            ci.DrawLine(c + d * r * 0.55f, c + d * r * 0.95f, col, 2.5f);
        ci.DrawCircle(c, r * 0.12f, col);
    }

    private static void Rain(CanvasItem ci, Vector2 c, float r, Color col)
    {
        foreach (var ox in new[] { -0.5f, 0f, 0.5f })
        {
            var top = c + new Vector2(ox * r, -r * 0.8f);
            ci.DrawLine(top, top + new Vector2(0, r * 1.0f), col, 2.5f);
            ci.DrawColoredPolygon(new[] {
                top + new Vector2(0, r * 1.0f), top + new Vector2(-r * 0.12f, r * 0.7f), top + new Vector2(r * 0.12f, r * 0.7f)
            }, col);
        }
    }

    private static void Trap(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawArc(c, r * 0.8f, 0, Mathf.Tau, 24, col, 2f);
        ci.DrawLine(c + new Vector2(-r * 0.5f, -r * 0.5f), c + new Vector2(r * 0.5f, r * 0.5f), col, 3f);
        ci.DrawLine(c + new Vector2(-r * 0.5f, r * 0.5f), c + new Vector2(r * 0.5f, -r * 0.5f), col, 3f);
    }

    private static void Spikes(CanvasItem ci, Vector2 c, float r, Color col)
    {
        for (int i = -1; i <= 1; i++)
        {
            float x = c.X + i * r * 0.55f;
            ci.DrawColoredPolygon(new[] {
                new Vector2(x - r * 0.22f, c.Y + r * 0.7f), new Vector2(x, c.Y - r * 0.8f), new Vector2(x + r * 0.22f, c.Y + r * 0.7f)
            }, col);
        }
    }

    private static void Chevrons(CanvasItem ci, Vector2 c, float r, Color col)
    {
        foreach (float ox in new[] { -0.35f, 0.25f })
        {
            var b = c + new Vector2(ox * r, 0);
            ci.DrawLine(b + new Vector2(-r * 0.35f, -r * 0.55f), b + new Vector2(r * 0.35f, 0), col, 3f);
            ci.DrawLine(b + new Vector2(r * 0.35f, 0), b + new Vector2(-r * 0.35f, r * 0.55f), col, 3f);
        }
    }

    private static void Bolt(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawColoredPolygon(new[] {
            c + new Vector2(r * 0.15f, -r * 0.9f), c + new Vector2(-r * 0.4f, r * 0.1f), c + new Vector2(r * 0.02f, r * 0.1f),
            c + new Vector2(-r * 0.15f, r * 0.9f), c + new Vector2(r * 0.45f, -r * 0.15f), c + new Vector2(r * 0.02f, -r * 0.15f)
        }, col);
    }

    private static void Bird(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawLine(c + new Vector2(-r * 0.8f, r * 0.1f), c + new Vector2(0, -r * 0.4f), col, 3f);
        ci.DrawLine(c + new Vector2(0, -r * 0.4f), c + new Vector2(r * 0.8f, r * 0.1f), col, 3f);
        ci.DrawLine(c + new Vector2(-r * 0.35f, r * 0.0f), c + new Vector2(0, -r * 0.15f), col, 2.5f);
        ci.DrawLine(c + new Vector2(0, -r * 0.15f), c + new Vector2(r * 0.35f, r * 0.0f), col, 2.5f);
    }

    // ── TYPY ITEMÓW (sylwetka slotu) ──
    public static void ItemKind(CanvasItem ci, AshenPantheon.Core.ItemKind kind, Vector2 c, float r, Color col)
    {
        switch (kind)
        {
            case AshenPantheon.Core.ItemKind.Helmet: DrawHelmet(ci, c, r, col); break;
            case AshenPantheon.Core.ItemKind.BodyArmour: DrawChest(ci, c, r, col); break;
            case AshenPantheon.Core.ItemKind.Shoulders: DrawChest(ci, c, r * 0.9f, col); break;
            case AshenPantheon.Core.ItemKind.Gloves: DrawGlove(ci, c, r, col); break;
            case AshenPantheon.Core.ItemKind.Boots: DrawBoot(ci, c, r, col); break;
            case AshenPantheon.Core.ItemKind.Belt: DrawBelt(ci, c, r, col); break;
            case AshenPantheon.Core.ItemKind.Amulet: DrawAmulet(ci, c, r, col); break;
            case AshenPantheon.Core.ItemKind.Ring: DrawRing(ci, c, r, col); break;
            case AshenPantheon.Core.ItemKind.OneHandWeapon: DrawSword(ci, c, r * 0.85f, col); break;
            case AshenPantheon.Core.ItemKind.TwoHandWeapon: DrawSword(ci, c, r, col); break;
            case AshenPantheon.Core.ItemKind.OffHand: DrawShield(ci, c, r, col); break;
            case AshenPantheon.Core.ItemKind.Jewel: DrawGem(ci, c, r, col); break;
            case AshenPantheon.Core.ItemKind.Soul: DrawSoul(ci, c, r, col); break;
        }
    }

    private static void DrawHelmet(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawArc(c + new Vector2(0, r * 0.1f), r * 0.7f, Mathf.Pi, Mathf.Tau, 20, col, 3f);
        ci.DrawLine(c + new Vector2(-r * 0.7f, r * 0.1f), c + new Vector2(r * 0.7f, r * 0.1f), col, 3f);
        ci.DrawLine(c + new Vector2(0, r * 0.1f), c + new Vector2(0, r * 0.6f), col, 2.5f);
    }
    private static void DrawChest(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawRect(new Rect2(c + new Vector2(-r * 0.55f, -r * 0.6f), new Vector2(r * 1.1f, r * 1.2f)), col, false, 3f);
        ci.DrawLine(c + new Vector2(0, -r * 0.6f), c + new Vector2(0, r * 0.6f), col, 2f);
    }
    private static void DrawGlove(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawRect(new Rect2(c + new Vector2(-r * 0.4f, -r * 0.2f), new Vector2(r * 0.8f, r * 0.8f)), col, false, 3f);
        for (int i = 0; i < 3; i++) ci.DrawLine(c + new Vector2(-r * 0.25f + i * r * 0.25f, -r * 0.2f), c + new Vector2(-r * 0.25f + i * r * 0.25f, -r * 0.6f), col, 2.5f);
    }
    private static void DrawBoot(CanvasItem ci, Vector2 c, float r, Color col) =>
        ci.DrawColoredPolygon(new[] {
            c + new Vector2(-r * 0.3f, -r * 0.6f), c + new Vector2(0, -r * 0.6f), c + new Vector2(0, r * 0.3f),
            c + new Vector2(r * 0.6f, r * 0.3f), c + new Vector2(r * 0.6f, r * 0.6f), c + new Vector2(-r * 0.3f, r * 0.6f)
        }, col);
    private static void DrawBelt(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawRect(new Rect2(c + new Vector2(-r * 0.7f, -r * 0.25f), new Vector2(r * 1.4f, r * 0.5f)), col, false, 3f);
        ci.DrawRect(new Rect2(c + new Vector2(-r * 0.18f, -r * 0.18f), new Vector2(r * 0.36f, r * 0.36f)), col, false, 2.5f);
    }
    private static void DrawAmulet(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawArc(c + new Vector2(0, -r * 0.3f), r * 0.5f, Mathf.Pi * 0.9f, Mathf.Pi * 2.1f, 18, col, 2.5f);
        ci.DrawColoredPolygon(new[] { c + new Vector2(0, r * 0.1f), c + new Vector2(-r * 0.25f, r * 0.4f), c + new Vector2(0, r * 0.7f), c + new Vector2(r * 0.25f, r * 0.4f) }, col);
    }
    private static void DrawRing(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawArc(c + new Vector2(0, r * 0.15f), r * 0.5f, 0, Mathf.Tau, 22, col, 3f);
        ci.DrawColoredPolygon(new[] { c + new Vector2(0, -r * 0.65f), c + new Vector2(-r * 0.2f, -r * 0.35f), c + new Vector2(r * 0.2f, -r * 0.35f) }, col);
    }
    private static void DrawSword(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawLine(c + new Vector2(r * 0.6f, -r * 0.8f), c + new Vector2(-r * 0.3f, r * 0.5f), col, 3.5f);
        ci.DrawLine(c + new Vector2(-r * 0.5f, r * 0.2f), c + new Vector2(-r * 0.1f, r * 0.6f), col, 3f); // gard
        ci.DrawLine(c + new Vector2(-r * 0.4f, r * 0.4f), c + new Vector2(-r * 0.6f, r * 0.7f), col, 3f); // rękojeść
    }
    private static void DrawShield(CanvasItem ci, Vector2 c, float r, Color col) =>
        ci.DrawColoredPolygon(new[] {
            c + new Vector2(-r * 0.55f, -r * 0.6f), c + new Vector2(r * 0.55f, -r * 0.6f),
            c + new Vector2(r * 0.55f, r * 0.2f), c + new Vector2(0, r * 0.75f), c + new Vector2(-r * 0.55f, r * 0.2f)
        }, new Color(col, 0.25f));
    private static void DrawGem(CanvasItem ci, Vector2 c, float r, Color col) =>
        ci.DrawColoredPolygon(new[] {
            c + new Vector2(0, -r * 0.7f), c + new Vector2(r * 0.6f, -r * 0.1f), c + new Vector2(0, r * 0.7f), c + new Vector2(-r * 0.6f, -r * 0.1f)
        }, col);
    private static void DrawSoul(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawArc(c, r * 0.7f, 0, Mathf.Tau, 28, col, 2.5f);
        ci.DrawCircle(c, r * 0.28f, col);
        foreach (var d in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
            ci.DrawLine(c + d * r * 0.4f, c + d * r * 0.9f, new Color(col, 0.6f), 2f);
    }

    // ── RAMKA RZADKOŚCI (obramowanie slotu itemu) ──
    public static void RarityFrame(CanvasItem ci, Rect2 rect, Rarity rarity, float width = 2.5f)
    {
        var col = ItemPickup.RarityColor(rarity);
        ci.DrawRect(rect, new Color(0.06f, 0.05f, 0.09f, 0.85f));   // tło slotu
        ci.DrawRect(rect, col, false, width);                       // ramka koloru rzadkości
        if (rarity >= Rarity.Legendary)                             // poświata narożników dla topowych tierów
        {
            float k = rect.Size.X * 0.22f;
            var p = rect.Position; var s = rect.Size;
            ci.DrawLine(p, p + new Vector2(k, 0), col, width + 1.5f);
            ci.DrawLine(p, p + new Vector2(0, k), col, width + 1.5f);
            ci.DrawLine(p + s, p + s - new Vector2(k, 0), col, width + 1.5f);
            ci.DrawLine(p + s, p + s - new Vector2(0, k), col, width + 1.5f);
        }
    }
}

/// <summary>Mały widget rysujący ikonę skilla (pasek dolny, panel skilli).</summary>
public partial class SkillIcon : Control
{
    public string SkillId = "";
    public Color IconColor = Colors.White;

    public override void _Draw()
    {
        if (SkillId.Length == 0) return;
        var c = Size / 2f;
        UiIcons.Skill(this, SkillId, c, Mathf.Min(Size.X, Size.Y) * 0.34f, IconColor);
    }
}

/// <summary>Mały widget: sylwetka typu itemu (sloty ekwipunku, plecak).</summary>
public partial class ItemIcon : Control
{
    public ItemKind Kind;
    public Color IconColor = new(0.8f, 0.78f, 0.86f);

    public override void _Draw() =>
        UiIcons.ItemKind(this, Kind, Size / 2f, Mathf.Min(Size.X, Size.Y) * 0.32f, IconColor);
}

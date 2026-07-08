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

    /// <summary>Czaszka (pasek bossa, śmierć).</summary>
    public static void Skull(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawCircle(c + new Vector2(0, -r * 0.15f), r * 0.6f, col);                              // czaszka
        ci.DrawRect(new Rect2(c + new Vector2(-r * 0.28f, r * 0.25f), new Vector2(r * 0.56f, r * 0.4f)), col); // szczęka
        var eye = new Color(0f, 0f, 0f, 0.7f);
        ci.DrawCircle(c + new Vector2(-r * 0.22f, -r * 0.18f), r * 0.15f, eye);
        ci.DrawCircle(c + new Vector2(r * 0.22f, -r * 0.18f), r * 0.15f, eye);
    }

    // ── HERBY BOGÓW ──
    public static void GodCrest(CanvasItem ci, GodId god, Vector2 c, float r, Color col)
    {
        switch (god)
        {
            case GodId.Wilds: // dzika natura: rogi/liść
                ci.DrawArc(c + new Vector2(0, r * 0.2f), r * 0.55f, Mathf.Pi * 0.15f, Mathf.Pi * 0.85f, 16, col, 2.5f);
                ci.DrawLine(c + new Vector2(-r * 0.5f, 0), c + new Vector2(-r * 0.9f, -r * 0.8f), col, 2.5f); // róg L
                ci.DrawLine(c + new Vector2(r * 0.5f, 0), c + new Vector2(r * 0.9f, -r * 0.8f), col, 2.5f);   // róg P
                ci.DrawLine(c + new Vector2(-r * 0.75f, -r * 0.4f), c + new Vector2(-r * 0.55f, -r * 0.7f), col, 2f);
                ci.DrawLine(c + new Vector2(r * 0.75f, -r * 0.4f), c + new Vector2(r * 0.55f, -r * 0.7f), col, 2f);
                break;
            case GodId.Blood: // kropla krwi + pazur
                ci.DrawColoredPolygon(new[] { c + new Vector2(0, -r * 0.85f), c + new Vector2(r * 0.6f, r * 0.35f), c + new Vector2(0, r * 0.8f), c + new Vector2(-r * 0.6f, r * 0.35f) }, col);
                ci.DrawCircle(c + new Vector2(-r * 0.15f, -r * 0.1f), r * 0.12f, new Color(0f, 0f, 0f, 0.4f));
                break;
            default: // brak patrona — pusty krąg z ukośnikiem
                ci.DrawArc(c, r * 0.7f, 0, Mathf.Tau, 24, col, 2f);
                ci.DrawLine(c + new Vector2(-r * 0.45f, r * 0.45f), c + new Vector2(r * 0.45f, -r * 0.45f), col, 2f);
                break;
        }
    }

    // ── HERBY KLAS (emblemat/portret klasy) ──
    public static void ClassCrest(CanvasItem ci, string classId, Vector2 c, float r, Color col)
    {
        switch (classId)
        {
            case "ranger": // łuk + strzała
                ci.DrawArc(c + new Vector2(-r * 0.55f, 0), r * 0.9f, -Mathf.Pi * 0.5f, Mathf.Pi * 0.5f, 18, col, 2.5f); // łuk ")"
                ci.DrawLine(c + new Vector2(-r * 0.55f, -r * 0.9f), c + new Vector2(-r * 0.55f, r * 0.9f), new Color(col, 0.5f), 1.5f); // cięciwa
                ci.DrawLine(c + new Vector2(-r * 0.5f, 0), c + new Vector2(r * 0.85f, 0), col, 2.5f); // trzon strzały
                ci.DrawColoredPolygon(new[] { c + new Vector2(r * 0.85f, 0), c + new Vector2(r * 0.55f, -r * 0.2f), c + new Vector2(r * 0.55f, r * 0.2f) }, col); // grot
                break;
            case "dragonknight": // miecz + skrzydła
                ci.DrawLine(c + new Vector2(0, -r * 0.85f), c + new Vector2(0, r * 0.7f), col, 3f); // ostrze
                ci.DrawLine(c + new Vector2(-r * 0.4f, r * 0.3f), c + new Vector2(r * 0.4f, r * 0.3f), col, 2.5f); // gard
                ci.DrawLine(c + new Vector2(-r * 0.15f, -r * 0.35f), c + new Vector2(-r * 0.85f, -r * 0.7f), col, 2f); // skrzydło L
                ci.DrawLine(c + new Vector2(r * 0.15f, -r * 0.35f), c + new Vector2(r * 0.85f, -r * 0.7f), col, 2f);   // skrzydło P
                break;
            case "spellweaver": // kostur + kula + iskry
                ci.DrawLine(c + new Vector2(-r * 0.55f, r * 0.85f), c + new Vector2(r * 0.35f, -r * 0.5f), col, 3f); // kostur
                ci.DrawArc(c + new Vector2(r * 0.4f, -r * 0.55f), r * 0.32f, 0, Mathf.Tau, 18, col, 2.5f); // kula
                foreach (var d in new[] { Vector2.Up, Vector2.Right, new Vector2(0.7f, 0.7f) })
                    ci.DrawLine(c + new Vector2(r * 0.4f, -r * 0.55f) + d * r * 0.45f, c + new Vector2(r * 0.4f, -r * 0.55f) + d * r * 0.7f, new Color(col, 0.7f), 1.5f);
                break;
            default: // pusty slot: "+" (nowa postać)
                ci.DrawLine(c + new Vector2(0, -r * 0.6f), c + new Vector2(0, r * 0.6f), col, 3f);
                ci.DrawLine(c + new Vector2(-r * 0.6f, 0), c + new Vector2(r * 0.6f, 0), col, 3f);
                break;
        }
    }

    // ── GLIFY (potwierdzenie / odmowa) ──
    public static void Check(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawLine(c + new Vector2(-r * 0.7f, 0), c + new Vector2(-r * 0.15f, r * 0.55f), col, 3f);
        ci.DrawLine(c + new Vector2(-r * 0.15f, r * 0.55f), c + new Vector2(r * 0.7f, -r * 0.6f), col, 3f);
    }
    public static void Cross(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawLine(c + new Vector2(-r * 0.6f, -r * 0.6f), c + new Vector2(r * 0.6f, r * 0.6f), col, 3f);
        ci.DrawLine(c + new Vector2(-r * 0.6f, r * 0.6f), c + new Vector2(r * 0.6f, -r * 0.6f), col, 3f);
    }
    public static void Play(CanvasItem ci, Vector2 c, float r, Color col) =>
        ci.DrawColoredPolygon(new[] { c + new Vector2(-r * 0.55f, -r * 0.7f), c + new Vector2(r * 0.7f, 0), c + new Vector2(-r * 0.55f, r * 0.7f) }, col);
    public static void Pause(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawRect(new Rect2(c + new Vector2(-r * 0.5f, -r * 0.65f), new Vector2(r * 0.32f, r * 1.3f)), col);
        ci.DrawRect(new Rect2(c + new Vector2(r * 0.18f, -r * 0.65f), new Vector2(r * 0.32f, r * 1.3f)), col);
    }
    public static void Home(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawColoredPolygon(new[] { c + new Vector2(0, -r * 0.8f), c + new Vector2(r * 0.8f, 0), c + new Vector2(-r * 0.8f, 0) }, col); // dach
        ci.DrawRect(new Rect2(c + new Vector2(-r * 0.5f, 0), new Vector2(r, r * 0.7f)), col);                                            // korpus
        ci.DrawRect(new Rect2(c + new Vector2(-r * 0.15f, r * 0.2f), new Vector2(r * 0.3f, r * 0.5f)), new Color(0.1f, 0.09f, 0.12f));   // drzwi
    }
    public static void Power(CanvasItem ci, Vector2 c, float r, Color col)
    {
        ci.DrawArc(c, r * 0.7f, Mathf.Pi * 1.75f, Mathf.Pi * 3.25f, 26, col, 3f); // przerwany krąg
        ci.DrawLine(c + new Vector2(0, -r * 0.85f), c + new Vector2(0, -r * 0.1f), col, 3f); // pion
    }
    public static void Gear(CanvasItem ci, Vector2 c, float r, Color col)
    {
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.Tau / 8f;
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            ci.DrawLine(c + d * r * 0.6f, c + d * r * 0.95f, col, 2.5f);
        }
        ci.DrawArc(c, r * 0.55f, 0, Mathf.Tau, 24, col, 2.5f);
    }

    // ── PORTRET NPC (popiersie w medalionie, rysowane kodem) ──
    public static void Portrait(CanvasItem ci, string npcId, Vector2 c, float r, Color accent)
    {
        ci.DrawCircle(c, r, new Color(0.1f, 0.09f, 0.14f));               // tło medalionu
        ci.DrawArc(c, r, 0, Mathf.Tau, 40, accent, 2.5f);                 // ramka
        // barki
        ci.DrawColoredPolygon(new[] {
            c + new Vector2(-r * 0.72f, r * 0.98f), c + new Vector2(-r * 0.5f, r * 0.28f),
            c + new Vector2(r * 0.5f, r * 0.28f), c + new Vector2(r * 0.72f, r * 0.98f)
        }, new Color(accent, 0.3f));
        // głowa
        ci.DrawCircle(c + new Vector2(0, -r * 0.08f), r * 0.32f, new Color(0.68f, 0.62f, 0.55f));
        // kaptur (łuk obejmujący głowę)
        ci.DrawArc(c + new Vector2(0, -r * 0.06f), r * 0.52f, Mathf.Pi * 0.78f, Mathf.Pi * 2.22f, 30, new Color(accent, 0.9f), 4f);
        // oczy
        var eye = new Color(0.1f, 0.09f, 0.12f);
        ci.DrawCircle(c + new Vector2(-r * 0.12f, -r * 0.1f), r * 0.045f, eye);
        ci.DrawCircle(c + new Vector2(r * 0.12f, -r * 0.1f), r * 0.045f, eye);
        // detal per NPC
        switch (npcId)
        {
            case "amuun": // mistyk — gwiazda mocy nad głową
                DrawStar4(ci, c + new Vector2(0, -r * 0.72f), r * 0.16f, new Color(0.85f, 0.75f, 1f));
                break;
            case "guildmaster": // dowódca — pióropusz/grzebień na kapturze
                ci.DrawLine(c + new Vector2(0, -r * 0.62f), c + new Vector2(0, -r * 0.95f), accent, 3f);
                break;
        }
    }

    private static void DrawStar4(CanvasItem ci, Vector2 c, float r, Color col) =>
        ci.DrawColoredPolygon(new[] {
            c + new Vector2(0, -r), c + new Vector2(r * 0.22f, -r * 0.22f), c + new Vector2(r, 0), c + new Vector2(r * 0.22f, r * 0.22f),
            c + new Vector2(0, r), c + new Vector2(-r * 0.22f, r * 0.22f), c + new Vector2(-r, 0), c + new Vector2(-r * 0.22f, -r * 0.22f)
        }, col);

    // ── STATY / ATRYBUTY (małe symbole obok liczb) ──
    public static void Stat(CanvasItem ci, string kind, Vector2 c, float r, Color col)
    {
        switch (kind)
        {
            case "life": // serce
                ci.DrawColoredPolygon(new[] { c + new Vector2(0, r * 0.85f), c + new Vector2(-r, -r * 0.1f), c + new Vector2(-r * 0.5f, -r * 0.7f), c + new Vector2(0, -r * 0.25f), c + new Vector2(r * 0.5f, -r * 0.7f), c + new Vector2(r, -r * 0.1f) }, col);
                break;
            case "es": // tarcza-diament (energia)
                ci.DrawColoredPolygon(new[] { c + new Vector2(0, -r), c + new Vector2(r * 0.8f, 0), c + new Vector2(0, r), c + new Vector2(-r * 0.8f, 0) }, new Color(col, 0.5f));
                ci.DrawArc(c, r, 0, Mathf.Tau, 4, col, 2f);
                break;
            case "armour": ci.DrawColoredPolygon(new[] { c + new Vector2(-r * 0.7f, -r * 0.7f), c + new Vector2(r * 0.7f, -r * 0.7f), c + new Vector2(r * 0.7f, r * 0.3f), c + new Vector2(0, r), c + new Vector2(-r * 0.7f, r * 0.3f) }, col); break;
            case "evasion": ci.DrawArc(c, r * 0.8f, 0.3f, Mathf.Tau - 0.3f, 22, col, 3f); break; // uchyl (przerwany krąg)
            case "damage": DrawSword(ci, c, r, col); break;
            case "crit": Bolt(ci, c, r, col); break;
            case "speed": Chevrons(ci, c, r, col); break;
            case "str": ci.DrawString(ThemeDB.FallbackFont, c + new Vector2(-r * 0.5f, r * 0.5f), "S", HorizontalAlignment.Left, -1, (int)(r * 1.8f), col); break;
            case "dex": ci.DrawString(ThemeDB.FallbackFont, c + new Vector2(-r * 0.5f, r * 0.5f), "D", HorizontalAlignment.Left, -1, (int)(r * 1.8f), col); break;
            case "int": ci.DrawString(ThemeDB.FallbackFont, c + new Vector2(-r * 0.5f, r * 0.5f), "I", HorizontalAlignment.Left, -1, (int)(r * 1.8f), col); break;
            case "fire":  ci.DrawColoredPolygon(Flame(c, r), col); break;
            case "cold":  ci.DrawColoredPolygon(new[] { c + new Vector2(0, -r), c + new Vector2(r * 0.5f, r * 0.6f), c + new Vector2(-r * 0.5f, r * 0.6f) }, col); break;
            case "poison": case "bleed": // kropla
                ci.DrawColoredPolygon(new[] { c + new Vector2(0, -r), c + new Vector2(r * 0.6f, r * 0.4f), c + new Vector2(0, r * 0.75f), c + new Vector2(-r * 0.6f, r * 0.4f) }, col); break;
            case "light": Bolt(ci, c, r, col); break;
            case "chaos": ci.DrawArc(c, r * 0.75f, 0, Mathf.Tau, 3, col, 2.5f); break; // triquetra-uproszczona
            default: ci.DrawCircle(c, r * 0.5f, col); break;
        }
    }

    private static Vector2[] Flame(Vector2 c, float r) => new[] {
        c + new Vector2(0, -r), c + new Vector2(r * 0.55f, -r * 0.1f), c + new Vector2(r * 0.35f, r * 0.6f),
        c + new Vector2(0, r * 0.85f), c + new Vector2(-r * 0.35f, r * 0.6f), c + new Vector2(-r * 0.5f, 0f)
    };

    /// <summary>Typ ikony dla PUSTEGO slotu ekwipunku (sylwetka-placeholder).</summary>
    public static AshenPantheon.Core.ItemKind SlotKind(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Helmet => AshenPantheon.Core.ItemKind.Helmet,
        EquipmentSlot.Shoulders => AshenPantheon.Core.ItemKind.Shoulders,
        EquipmentSlot.BodyArmour => AshenPantheon.Core.ItemKind.BodyArmour,
        EquipmentSlot.Gloves => AshenPantheon.Core.ItemKind.Gloves,
        EquipmentSlot.Boots => AshenPantheon.Core.ItemKind.Boots,
        EquipmentSlot.Belt => AshenPantheon.Core.ItemKind.Belt,
        EquipmentSlot.Amulet => AshenPantheon.Core.ItemKind.Amulet,
        EquipmentSlot.Ring1 or EquipmentSlot.Ring2 => AshenPantheon.Core.ItemKind.Ring,
        EquipmentSlot.Weapon => AshenPantheon.Core.ItemKind.OneHandWeapon,
        EquipmentSlot.OffHand => AshenPantheon.Core.ItemKind.OffHand,
        EquipmentSlot.Soul => AshenPantheon.Core.ItemKind.Soul,
        _ => AshenPantheon.Core.ItemKind.Ring,
    };

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

    /// <summary>Mała ikona itemu do wstawienia w wiersz HBox (przed nazwą) — listy AH/handlu.</summary>
    public static ItemIcon Chip(AshenPantheon.Core.ItemKind kind, Rarity rarity) => new()
    {
        Kind = kind, IconColor = ItemPickup.RarityColor(rarity),
        CustomMinimumSize = new Vector2(26, 26), MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    /// <summary>Dodaje ikonę typu itemu (kolor rzadkości) po lewej stronie przycisku listy + wcięcie tekstu.
    /// Reużywalne w Vendor/Stash/AH/Blacksmith/Inspect zamiast gołych przycisków tekstowych.</summary>
    public static void DecorateItemButton(Button b, AshenPantheon.Core.ItemKind kind, Rarity rarity)
    {
        b.Alignment = HorizontalAlignment.Left;
        b.Text = "      " + b.Text; // miejsce na ikonę
        var icon = new ItemIcon
        {
            Kind = kind, IconColor = ItemPickup.RarityColor(rarity),
            AnchorTop = 0.5f, AnchorBottom = 0.5f, OffsetTop = -13, OffsetBottom = 13,
            OffsetLeft = 6, Size = new Vector2(26, 26),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        b.AddChild(icon);
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

/// <summary>Herb boga (przyciski wyboru patrona).</summary>
public partial class GodCrestIcon : Control
{
    public GodId God;
    public Color IconColor = new(0.85f, 0.82f, 0.95f);

    public override void _Draw() =>
        UiIcons.GodCrest(this, God, Size / 2f, Mathf.Min(Size.X, Size.Y) * 0.36f, IconColor);
}

/// <summary>Mały widget: symbol statystyki (StatsPanel, obok liczby).</summary>
public partial class StatIcon : Control
{
    public string Kind = "";
    public Color IconColor = new(0.75f, 0.8f, 0.95f);

    public override void _Draw() =>
        UiIcons.Stat(this, Kind, Size / 2f, Mathf.Min(Size.X, Size.Y) * 0.34f, IconColor);
}

/// <summary>Mały glif potwierdzenia/odmowy (przyciski dialogów).</summary>
public partial class GlyphIcon : Control
{
    public string Kind = ""; // check | cross | play | pause | home | power | gear
    public Color IconColor = Colors.White;

    public override void _Draw()
    {
        var c = Size / 2f;
        float r = Mathf.Min(Size.X, Size.Y) * 0.36f;
        switch (Kind)
        {
            case "check": UiIcons.Check(this, c, r, IconColor); break;
            case "cross": UiIcons.Cross(this, c, r, IconColor); break;
            case "play":  UiIcons.Play(this, c, r, IconColor); break;
            case "pause": UiIcons.Pause(this, c, r, IconColor); break;
            case "home":  UiIcons.Home(this, c, r, IconColor); break;
            case "power": UiIcons.Power(this, c, r, IconColor); break;
            case "gear":  UiIcons.Gear(this, c, r, IconColor); break;
        }
    }
}

/// <summary>Portret NPC w medalionie (dialogi questowe).</summary>
public partial class NpcAvatar : Control
{
    public string NpcId = "";
    public Color Accent = new(0.6f, 0.5f, 0.85f);

    public override void _Draw() =>
        UiIcons.Portrait(this, NpcId, Size / 2f, Mathf.Min(Size.X, Size.Y) * 0.46f, Accent);
}

/// <summary>Herb/emblemat klasy (karty postaci w menu, kreator).</summary>
public partial class ClassCrestIcon : Control
{
    public string ClassId = "";
    public Color IconColor = new(0.9f, 0.82f, 0.5f);

    public override void _Draw() =>
        UiIcons.ClassCrest(this, ClassId, Size / 2f, Mathf.Min(Size.X, Size.Y) * 0.36f, IconColor);
}

/// <summary>Okrągły żeton z poziomem postaci (karty slotów, party frame).</summary>
public partial class LevelBadge : Control
{
    public int Level = 1;
    public Color Fill = new(0.18f, 0.15f, 0.26f);
    public Color Border = new(0.6f, 0.5f, 0.85f);

    public override void _Draw()
    {
        var c = Size / 2f;
        float r = Mathf.Min(Size.X, Size.Y) * 0.5f - 1f;
        DrawCircle(c, r, Fill);
        DrawArc(c, r, 0, Mathf.Tau, 24, Border, 2f);
        var f = ThemeDB.FallbackFont;
        string t = Level.ToString();
        int fs = (int)(r * 1.05f);
        var sz = f.GetStringSize(t, HorizontalAlignment.Left, -1, fs);
        DrawString(f, c + new Vector2(-sz.X / 2f, sz.Y * 0.32f), t, HorizontalAlignment.Left, -1, fs, new Color(0.95f, 0.92f, 1f));
    }
}

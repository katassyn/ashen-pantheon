using System;
using Godot;

/// <summary>Animowane tło proceduralne (bez assetów): gradient nieba, migoczące gwiazdy,
/// wznoszące się iskry-dusze i powolna pulsująca aura. Menu główne + tło stref.
/// Deterministyczne rozmieszczenie (seed), lekki koszt (jeden _Draw + kilka warstw).</summary>
public partial class AmbientBackground : Control
{
    [Export] public int Seed = 7;
    [Export] public bool Vignette = true;

    private struct Star { public Vector2 P; public float R, Phase, Speed; }
    private struct Ember { public float X, Y, Speed, Drift, R, Phase; }

    private Star[] _stars;
    private Ember[] _embers;
    private float _t;

    public override void _Ready()
    {
        AnchorRight = 1f; AnchorBottom = 1f;
        MouseFilter = MouseFilterEnum.Ignore;
        // kolejność dodania decyduje o warstwie (ambient dodawany jako pierwsze dziecko = pod resztą)
        var rng = new Random(Seed);

        _stars = new Star[120];
        for (int i = 0; i < _stars.Length; i++)
            _stars[i] = new Star
            {
                P = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()),
                R = 0.6f + (float)rng.NextDouble() * 1.6f,
                Phase = (float)(rng.NextDouble() * Math.Tau),
                Speed = 0.6f + (float)rng.NextDouble() * 1.8f,
            };

        _embers = new Ember[26];
        for (int i = 0; i < _embers.Length; i++)
            _embers[i] = NewEmber(rng, (float)rng.NextDouble());
    }

    private static Ember NewEmber(Random rng, float startY) => new()
    {
        X = (float)rng.NextDouble(),
        Y = startY,
        Speed = 0.02f + (float)rng.NextDouble() * 0.04f,
        Drift = ((float)rng.NextDouble() - 0.5f) * 0.03f,
        R = 1.4f + (float)rng.NextDouble() * 2.4f,
        Phase = (float)(rng.NextDouble() * Math.Tau),
    };

    public override void _Process(double delta)
    {
        _t += (float)delta;
        // iskry wznoszą się; po wyjściu górą wracają na dół
        for (int i = 0; i < _embers.Length; i++)
        {
            _embers[i].Y -= _embers[i].Speed * (float)delta;
            _embers[i].X += _embers[i].Drift * (float)delta;
            if (_embers[i].Y < -0.05f) { var r = new Random(Seed + i * 31 + (int)(_t * 60)); _embers[i] = NewEmber(r, 1.05f); }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var s = Size;
        // gradient nieba: ciemny fiolet u góry → głębsza czerń-purpura u dołu (paski)
        int bands = 20;
        for (int b = 0; b < bands; b++)
        {
            float k = b / (float)bands;
            var top = new Color(0.06f, 0.05f, 0.10f).Lerp(new Color(0.10f, 0.06f, 0.14f), k);
            DrawRect(new Rect2(0, k * s.Y, s.X, s.Y / bands + 1), top);
        }

        // pulsująca aura panteonu (dwa miękkie kręgi światła u góry)
        float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 0.7f);
        DrawCircle(new Vector2(s.X * 0.5f, s.Y * 0.24f), s.Y * 0.42f, new Color(0.30f, 0.20f, 0.45f, 0.05f + 0.04f * pulse));
        DrawCircle(new Vector2(s.X * 0.5f, s.Y * 0.20f), s.Y * 0.22f, new Color(0.45f, 0.30f, 0.65f, 0.05f + 0.05f * pulse));

        // gwiazdy (migotanie sinusem)
        foreach (var st in _stars)
        {
            float tw = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(_t * st.Speed + st.Phase));
            DrawCircle(st.P * s, st.R, new Color(0.8f, 0.8f, 0.95f, 0.5f * tw));
        }

        // iskry-dusze (jaśniejszy rdzeń + poświata)
        foreach (var e in _embers)
        {
            var p = new Vector2(e.X * s.X, e.Y * s.Y);
            float fl = 0.6f + 0.4f * Mathf.Sin(_t * 2.2f + e.Phase);
            DrawCircle(p, e.R * 2.2f, new Color(0.6f, 0.4f, 0.85f, 0.10f * fl));
            DrawCircle(p, e.R, new Color(0.85f, 0.7f, 1f, 0.55f * fl));
        }

        // winieta: przyciemnione narożniki (skupia wzrok na centrum)
        if (Vignette)
        {
            var edge = new Color(0f, 0f, 0f, 0.35f);
            float h = s.Y * 0.28f;
            DrawRect(new Rect2(0, 0, s.X, h), new Color(0, 0, 0, 0.28f)); // subtelne u góry
            DrawRect(new Rect2(0, s.Y - h, s.X, h), edge);                 // mocniejsze u dołu
        }
    }
}

/// <summary>Subtelne pyłki ambient dryfujące po ekranie (głębia świata) — bardzo niska alpha,
/// nie przeszkadza w rozgrywce. Dodawane w Hud (hub/strefy/arena).</summary>
public partial class AmbientDust : Control
{
    private struct Mote { public Vector2 P, V; public float R, Phase; }
    private Mote[] _motes;
    private float _t;

    public override void _Ready()
    {
        AnchorRight = 1f; AnchorBottom = 1f;
        MouseFilter = MouseFilterEnum.Ignore;
        var rng = new Random(3);
        _motes = new Mote[34];
        for (int i = 0; i < _motes.Length; i++)
            _motes[i] = new Mote
            {
                P = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()),
                V = new Vector2(((float)rng.NextDouble() - 0.5f) * 0.012f, -0.006f - (float)rng.NextDouble() * 0.01f),
                R = 1f + (float)rng.NextDouble() * 2f,
                Phase = (float)(rng.NextDouble() * Math.Tau),
            };
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        for (int i = 0; i < _motes.Length; i++)
        {
            _motes[i].P += _motes[i].V * (float)delta;
            if (_motes[i].P.Y < -0.02f) _motes[i].P.Y = 1.02f;
            if (_motes[i].P.X < -0.02f) _motes[i].P.X = 1.02f;
            else if (_motes[i].P.X > 1.02f) _motes[i].P.X = -0.02f;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var m in _motes)
        {
            float fl = 0.5f + 0.5f * Mathf.Sin(_t * 0.8f + m.Phase);
            DrawCircle(m.P * Size, m.R, new Color(0.75f, 0.7f, 0.9f, 0.10f * fl));
        }
    }
}

/// <summary>Płynne wejście do sceny: czarny overlay 1→0 w 0.4 s. Dodawane w Hud/MainMenu _Ready —
/// każde wejście do mapy/menu zaczyna się miękkim rozjaśnieniem zamiast twardego cięcia.</summary>
public partial class SceneFadeIn : CanvasLayer
{
    private ColorRect _rect;
    private float _t;
    private const float Dur = 0.45f;

    public override void _Ready()
    {
        Layer = 90;
        _rect = new ColorRect { Color = Colors.Black, AnchorRight = 1f, AnchorBottom = 1f, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_rect);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        float k = _t / Dur;
        if (k >= 1f) { QueueFree(); return; }
        _rect.Color = new Color(0f, 0f, 0f, 1f - k);
    }
}

/// <summary>Ekran ładowania: ciemna zasłona + obracający się pierścień z iskrami + tytuł + losowa
/// wskazówka; trzyma się chwilę i płynnie zanika. Dodawany przy wejściu do scen rozgrywki (Hud).</summary>
public partial class LoadingScreen : CanvasLayer
{
    private ColorRect _cover;
    private float _t;
    private const float Hold = 0.3f;
    private const float Fade = 0.5f;

    private static readonly string[] Tips =
    {
        "Tip: Mark enemies, then strike — your Ranger skills hit marked foes harder.",
        "Tip: Dash through danger — a well-timed roll grants brief invulnerability.",
        "Tip: Pledge to a god to transform your skills into new forms.",
        "Tip: Socket jewels into your gear to reshape your build.",
        "Tip: Upgrade Rare+ items at the Blacksmith using boss parts.",
        "Tip: Bloodshed difficulty is the only source of Boss Souls.",
        "Tip: Fragments of Infernal Passage open the harder trials.",
        "Tip: Press TAB for the map, J for your journal, K for skills.",
    };

    public override void _Ready()
    {
        Layer = 95;
        // nieblokujący (Ignore) — czysto wizualny, nie gatuje ruchu przy wejściu do strefy
        _cover = new ColorRect { Color = new Color(0.04f, 0.03f, 0.06f), AnchorRight = 1f, AnchorBottom = 1f, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_cover);

        _cover.AddChild(new LoadingSpinner
        {
            AnchorLeft = 0.5f, AnchorTop = 0.5f, AnchorRight = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -66, OffsetTop = -66, OffsetRight = 66, OffsetBottom = 66,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        AddCenteredLabel("ASHEN PANTHEON", 28, new Color(0.9f, 0.82f, 1f), -150, -118, 300);
        AddCenteredLabel("Loading…", 16, new Color(0.7f, 0.65f, 0.82f), 82, 108, 200);

        var tip = new Label
        {
            Text = Tips[(int)(GD.Randi() % Tips.Length)], HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = -360, OffsetRight = 360, OffsetTop = -80, OffsetBottom = -36,
        };
        tip.AddThemeColorOverride("font_color", new Color(0.6f, 0.56f, 0.72f));
        _cover.AddChild(tip);
    }

    private void AddCenteredLabel(string text, int fontSize, Color col, float top, float bottom, float halfWidth)
    {
        var l = new Label
        {
            Text = text, HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -halfWidth, OffsetRight = halfWidth, OffsetTop = top, OffsetBottom = bottom,
        };
        l.AddThemeFontSizeOverride("font_size", fontSize);
        l.AddThemeColorOverride("font_color", col);
        _cover.AddChild(l);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (_t < Hold) return;
        float k = (_t - Hold) / Fade;
        if (k >= 1f) { QueueFree(); return; }
        _cover.Modulate = new Color(1f, 1f, 1f, 1f - k); // dzieci (spinner/tekst) gasną razem z zasłoną
    }
}

/// <summary>Obracający się pierścień ładowania (łuk z przerwą + orbitujące iskry + pulsujący rdzeń).</summary>
public partial class LoadingSpinner : Control
{
    private float _t;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
    public override void _Process(double delta) { _t += (float)delta; QueueRedraw(); }

    public override void _Draw()
    {
        var c = Size / 2f;
        float r = Mathf.Min(Size.X, Size.Y) * 0.42f;
        DrawArc(c, r, 0, Mathf.Tau, 40, new Color(0.3f, 0.26f, 0.4f, 0.5f), 3f);       // pierścień tła
        float a0 = _t * 3f;
        DrawArc(c, r, a0, a0 + Mathf.Pi * 0.6f, 24, new Color(0.75f, 0.6f, 1f), 4f);    // obracający się łuk
        for (int i = 0; i < 3; i++)                                                     // orbitujące iskry
        {
            float a = _t * 2.2f + i * Mathf.Tau / 3f;
            DrawCircle(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 3.5f, new Color(0.85f, 0.72f, 1f));
        }
        float pulse = 0.6f + 0.4f * Mathf.Sin(_t * 4f);                                 // pulsujący rdzeń-dusza
        DrawCircle(c, 7f * pulse, new Color(0.7f, 0.5f, 0.95f, 0.8f));
    }
}

/// <summary>Ozdobna linia pod tytułem menu — akcent z diamentem w środku.</summary>
public partial class TitleRule : Control
{
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; QueueRedraw(); }

    public override void _Draw()
    {
        float y = Size.Y / 2f, cx = Size.X / 2f;
        var col = new Color(0.55f, 0.45f, 0.75f, 0.8f);
        DrawLine(new Vector2(cx - 150, y), new Vector2(cx - 12, y), col, 1.5f);
        DrawLine(new Vector2(cx + 12, y), new Vector2(cx + 150, y), col, 1.5f);
        DrawColoredPolygon(new[] { new Vector2(cx, y - 5), new Vector2(cx + 6, y), new Vector2(cx, y + 5), new Vector2(cx - 6, y) }, col);
    }
}

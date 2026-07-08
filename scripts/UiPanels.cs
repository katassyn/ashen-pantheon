using Godot;

/// <summary>Wspólne zachowanie paneli UI: wzajemne wykluczanie (jeden otwarty naraz) i solidne tło.</summary>
public interface IUiPanel
{
    void CloseUi();
    bool IsOpen { get; }
}

public static class UiPanels
{
    public const string Group = "ui_panels";

    public static void CloseAllExcept(SceneTree tree, IUiPanel except)
    {
        foreach (var n in tree.GetNodesInGroup(Group))
            if (n is IUiPanel p && !ReferenceEquals(p, except))
                p.CloseUi();
    }

    public static bool AnyOpen(SceneTree tree)
    {
        foreach (var n in tree.GetNodesInGroup(Group))
            if (n is IUiPanel { IsOpen: true }) return true;
        return false;
    }

    /// <summary>Nieprzezroczyste tło panelu (panele nie prześwitują na siebie/grę).</summary>
    public static void Solidify(Panel root)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.085f, 0.075f, 0.125f, 0.99f),
            BorderColor = new Color(0.5f, 0.44f, 0.66f),
            ShadowColor = new Color(0f, 0f, 0f, 0.5f),
            ShadowSize = 10,
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(7);
        style.SetContentMarginAll(2);
        root.AddThemeStyleboxOverride("panel", style);
        // dekoracyjny obrys — narożniki + akcentowa linia u góry (rysowane nad tłem)
        if (root.GetNodeOrNull("PanelDecor") == null)
            root.AddChild(new PanelDecor { Name = "PanelDecor" });
    }
}

/// <summary>Ozdobna oprawa okna: akcentowa linia pod górną krawędzią + wzmocnione narożniki.
/// Wspólna dla wszystkich paneli (UiPanels.Solidify) — spójny „ramowany" wygląd.</summary>
public partial class PanelDecor : Godot.Control
{
    public override void _Ready()
    {
        AnchorRight = 1f; AnchorBottom = 1f;
        MouseFilter = MouseFilterEnum.Ignore;
        (GetParent() as Godot.Control)!.Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        var s = Size;
        var accent = new Godot.Color(0.6f, 0.5f, 0.85f, 0.85f);
        var faint = new Godot.Color(0.5f, 0.44f, 0.66f, 0.35f);
        // akcentowa linia u góry (pasek tytułu)
        DrawLine(new Godot.Vector2(14, 30), new Godot.Vector2(s.X - 14, 30), faint, 1.5f);
        // wzmocnione narożniki
        float k = 18f;
        foreach (var (o, dx, dy) in new[] {
            (new Godot.Vector2(4, 4), 1f, 1f), (new Godot.Vector2(s.X - 4, 4), -1f, 1f),
            (new Godot.Vector2(4, s.Y - 4), 1f, -1f), (new Godot.Vector2(s.X - 4, s.Y - 4), -1f, -1f) })
        {
            DrawLine(o, o + new Godot.Vector2(k * dx, 0), accent, 2.5f);
            DrawLine(o, o + new Godot.Vector2(0, k * dy), accent, 2.5f);
        }
    }
}

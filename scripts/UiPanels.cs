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
            BgColor = new Color(0.09f, 0.08f, 0.13f, 0.99f),
            BorderColor = new Color(0.45f, 0.4f, 0.6f),
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(4);
        root.AddThemeStyleboxOverride("panel", style);
    }
}

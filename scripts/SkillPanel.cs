using Godot;

/// <summary>Panel skilli — klawisz K. Per-skill wybór: wersja BAZOWA albo BOGA (Dzikie Ostępy).</summary>
public partial class SkillPanel : CanvasLayer
{
    private static readonly (string Id, string Name)[] Skills =
    {
        ("basic", "Strzał (basic)"),
        ("spread", "Rozbryzg"),
        ("exec", "Egzekutor"),
        ("rain", "Deszcz strzał"),
        ("mine", "Mina"),
        ("hedge", "Przesieka"),
        ("hawk", "Jastrząb"),
    };

    private Panel _root;
    private VBoxContainer _list;
    private Label _info;
    private Button[] _buttons;

    public override void _Ready()
    {
        _root = GetNode<Panel>("%Root");
        _list = GetNode<VBoxContainer>("%List");
        _info = GetNode<Label>("%Info");

        _buttons = new Button[Skills.Length];
        for (int i = 0; i < Skills.Length; i++)
        {
            var id = Skills[i].Id;
            var b = new Button();
            b.Pressed += () => Toggle(id);
            _list.AddChild(b);
            _buttons[i] = b;
        }

        _root.Visible = false;
        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.K)
        {
            _root.Visible = !_root.Visible;
            if (_root.Visible) Refresh();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Toggle(string id)
    {
        if (!GameState.GodSkills.Add(id))
            GameState.GodSkills.Remove(id);
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < Skills.Length; i++)
        {
            bool god = GameState.GodSkills.Contains(Skills[i].Id);
            _buttons[i].Text = $"{Skills[i].Name}:  {(god ? "BÓG (Dzikie Ostępy)" : "BAZA")}";
        }
        _info.Text = GameState.GodSkills.Count > 0
            ? "Pledged: Dzikie Ostępy  →  pasywka: +15% ruchu, dłuższe oznaczenia"
            : "Bez boga — wszystkie skille bazowo";
    }
}

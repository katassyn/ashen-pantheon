using System.IO;
using System.Text.Json;
using Godot;
using AshenPantheon.Core;

/// <summary>Menu główne: realm → sloty postaci (12) → kreator (nick + klasa) → gra.</summary>
public partial class MainMenu : Control
{
    public const int SlotCount = 12;

    private OptionButton _realm;
    private VBoxContainer _slotList;
    private VBoxContainer _creator;
    private LineEdit _nick;
    private Label _status;
    private int _selectedSlot = -1;

    private string RealmId => "ashen"; // na razie jeden realm lokalny; online-realmy = faza meta-serwera
    private string SlotPath(int i) => $"{OS.GetUserDataDir()}/characters/{RealmId}/slot{i}.json";

    public override void _Ready()
    {
        DataLoader.LoadAll();

        var center = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0f, AnchorBottom = 1f,
            OffsetLeft = -360, OffsetRight = 360, OffsetTop = 30, OffsetBottom = -30,
        };
        center.AddThemeConstantOverride("separation", 10);
        AddChild(center);

        var title = new Label { Text = "ASHEN PANTHEON", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 34);
        center.AddChild(title);

        var realmRow = new HBoxContainer();
        realmRow.AddThemeConstantOverride("separation", 8);
        realmRow.AddChild(new Label { Text = "Realm:" });
        _realm = new OptionButton();
        _realm.AddItem("Ashen (local)");
        _realm.AddItem("— online (Steam account) coming soon —");
        _realm.SetItemDisabled(1, true);
        realmRow.AddChild(_realm);
        center.AddChild(realmRow);

        center.AddChild(new Label { Text = "Characters:" });
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _slotList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_slotList);
        center.AddChild(scroll);

        // kreator (pokazywany po wybraniu pustego slotu)
        _creator = new VBoxContainer { Visible = false };
        _creator.AddThemeConstantOverride("separation", 8);
        _creator.AddChild(new Label { Text = "NEW CHARACTER" });
        _nick = new LineEdit { PlaceholderText = "name (3-16 characters)", MaxLength = 16 };
        _creator.AddChild(_nick);
        var classRow = new HBoxContainer();
        classRow.AddThemeConstantOverride("separation", 8);
        classRow.AddChild(new Button { Text = "🏹 Ranger", Disabled = false, TooltipText = "Hunter — Concentration, Mark" });
        var dk = new Button { Text = "Dragonknight (soon)", Disabled = true };
        var sw = new Button { Text = "Spellweaver (soon)", Disabled = true };
        classRow.AddChild(dk);
        classRow.AddChild(sw);
        _creator.AddChild(classRow);
        var create = new Button { Text = "Create & play" };
        create.Pressed += CreateAndPlay;
        _creator.AddChild(create);
        center.AddChild(_creator);

        _status = new Label();
        center.AddChild(_status);

        RefreshSlots();
    }

    private void RefreshSlots()
    {
        foreach (Node c in _slotList.GetChildren()) c.QueueFree();

        for (int i = 0; i < SlotCount; i++)
        {
            var meta = ReadMeta(SlotPath(i));
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);

            var main = new Button
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Text = meta == null
                    ? $"{i + 1}.  — empty slot —"
                    : $"{i + 1}.  {meta.Name}   ({ClassName(meta.ClassId)}, level {meta.Level})",
            };
            int slot = i;
            main.Pressed += () => SelectSlot(slot, meta != null);
            row.AddChild(main);

            if (meta != null)
            {
                var del = new Button { Text = "Delete", TooltipText = "Deletes the character permanently" };
                del.Pressed += () =>
                {
                    File.Delete(SlotPath(slot));
                    _status.Text = $"Character in slot {slot + 1} deleted.";
                    RefreshSlots();
                };
                row.AddChild(del);
            }
            _slotList.AddChild(row);
        }
    }

    private static string ClassName(string id) =>
        GameData.Classes.TryGetValue(id, out var c) ? c.Name : id;

    private void SelectSlot(int slot, bool occupied)
    {
        _selectedSlot = slot;
        if (occupied)
        {
            var repo = new JsonGameStateRepository(SlotPath(slot));
            GameState.SwitchRepository(repo);
            EnterGame();
        }
        else
        {
            _creator.Visible = true;
            _status.Text = $"Creating a character in slot {slot + 1}.";
        }
    }

    private void CreateAndPlay()
    {
        string nick = _nick.Text.StripEdges();
        if (nick.Length < 3) { _status.Text = "Name must be at least 3 characters."; return; }
        if (_selectedSlot < 0) return;

        GameState.NewCharacter(nick, "ranger", new JsonGameStateRepository(SlotPath(_selectedSlot)));
        EnterGame();
    }

    private void EnterGame()
    {
        PlayerController.Local?.Refresh();
        GetTree().ChangeSceneToFile("res://scenes/Main.tscn");
    }

    /// <summary>Lekki odczyt metadanych slotu (nazwa/klasa/poziom) bez ładowania całej postaci.</summary>
    private static SaveData ReadMeta(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path), JsonGameStateRepository.Options);
        }
        catch { return null; }
    }
}

using Godot;
using AshenPantheon.Core;

/// <summary>Panel skilli (K): pledge boga, per-skill BAZA/BÓG, drzewka ulepszeń (punkty skilli),
/// przeciąganie skilla na pasek na dole ekranu (drag&drop), respec drzewek za złoto.</summary>
public partial class SkillPanel : CanvasLayer, IUiPanel
{
    private Panel _root;
    private VBoxContainer _list;
    private Label _header;
    private HBoxContainer _godRow;
    private PlayerController _player;

    public void CloseUi() => _root.Visible = false;

    public override void _Ready()
    {
        _root = GetNode<Panel>("%Root");
        _list = GetNode<VBoxContainer>("%List");
        _header = GetNode<Label>("%Info");
        AddToGroup(UiPanels.Group);
        UiPanels.Solidify(_root);
        BuildGodRow();
        _root.Visible = false;
    }

    private void BuildGodRow()
    {
        var vb = _root.GetNode<VBoxContainer>("VB");
        _godRow = new HBoxContainer();
        _godRow.AddThemeConstantOverride("separation", 8);
        vb.AddChild(_godRow);
        vb.MoveChild(_godRow, 2); // po Title i Info, przed Scroll

        foreach (var god in Gods.All)
        {
            var b = new Button { Text = Gods.Name(god), TooltipText = Gods.Passive(god) };
            var captured = god;
            b.Pressed += () =>
            {
                GameState.PledgedGod = captured;
                GameState.Save();
                _player?.Refresh();
                Refresh();
            };
            _godRow.AddChild(b);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.K)
        {
            if (_root.Visible) { _root.Visible = false; }
            else
            {
                UiPanels.CloseAllExcept(GetTree(), this);
                _root.Visible = true;
                Refresh();
            }
            GetViewport().SetInputAsHandled();
        }
    }

    private void Refresh()
    {
        _player = PlayerController.Local;

        foreach (Node c in _list.GetChildren()) c.QueueFree();

        var prog = GameState.Progress;
        int treeCost = (int)Respec.SkillTreeCost(GameState.Trees.SpentPoints);
        _header.Text = $"Pledge: {Gods.Name(GameState.PledgedGod)} — {Gods.Passive(GameState.PledgedGod)}\n" +
                       $"Punkty skilli: {prog.SkillPoints}    (węzeł = 1 pkt)";

        // respec drzewek
        if (GameState.Trees.SpentPoints > 0)
        {
            var respec = new Button { Text = $"Reset wszystkich drzewek ({treeCost} złota)" };
            respec.Pressed += () =>
            {
                if (GameState.Wallet.Gold < treeCost) return;
                GameState.Wallet.Gold -= treeCost;
                GameState.Progress.SkillPoints += GameState.Trees.ResetAll();
                GameState.Save();
                Refresh();
            };
            _list.AddChild(respec);
        }

        foreach (var info in GameState.Class.Skills)
        {
            var row = new VBoxContainer();
            row.AddThemeConstantOverride("separation", 2);
            _list.AddChild(row);

            var head = new HBoxContainer();
            head.AddThemeConstantOverride("separation", 6);
            row.AddChild(head);

            // uchwyt drag&drop — przeciągnij na slot paska na dole
            var drag = new SkillDragSource { SkillId = info.Id, Text = $"≡ {info.Name}", TooltipText = $"{info.Description}\nPrzeciągnij na pasek skilli." };
            head.AddChild(drag);

            int? slot = GameState.Loadout.SlotOf(info.Id);
            head.AddChild(new Label { Text = slot.HasValue ? $"[{Loadout.SlotKeys[slot.Value]}]" : "" });

            bool god = GameState.GodSkills.Contains(info.Id);
            var toggle = new Button { Text = god ? "BÓG ✦" : "BAZA", Disabled = GameState.PledgedGod == GodId.None };
            var capturedId = info.Id;
            toggle.Pressed += () =>
            {
                if (!GameState.GodSkills.Add(capturedId)) GameState.GodSkills.Remove(capturedId);
                GameState.Save();
                Refresh();
            };
            head.AddChild(toggle);

            // drzewko
            var nodesRow = new HBoxContainer();
            nodesRow.AddThemeConstantOverride("separation", 4);
            row.AddChild(nodesRow);
            foreach (var node in RangerTrees.BySkill[info.Id])
            {
                bool allocated = GameState.Trees.IsAllocated(info.Id, node.Id);
                bool canTake = !allocated && prog.SkillPoints > 0 && GameState.Trees.CanAllocate(info.Id, node.Id);
                var nb = new Button
                {
                    Text = allocated ? $"✔ {node.Name}" : node.Name,
                    Disabled = !canTake && !allocated,
                    TooltipText = node.Description + (node.ExclusiveGroup != null ? "\n(wyklucza się z inną gałęzią)" : ""),
                };
                if (allocated) nb.Modulate = new Color(0.6f, 1f, 0.6f);
                var sid = info.Id; var nid = node.Id;
                nb.Pressed += () =>
                {
                    if (GameState.Trees.IsAllocated(sid, nid) || GameState.Progress.SkillPoints <= 0) return;
                    if (GameState.Trees.Allocate(sid, nid))
                    {
                        GameState.Progress.SkillPoints--;
                        GameState.Save();
                        Refresh();
                    }
                };
                nodesRow.AddChild(nb);
            }

            row.AddChild(new HSeparator());
        }
    }
}

/// <summary>Źródło drag&drop: niesie id skilla na slot paska.</summary>
public partial class SkillDragSource : Button
{
    public string SkillId = "";

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var preview = new Label { Text = Text };
        SetDragPreview(preview);
        return SkillId;
    }
}

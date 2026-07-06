using Godot;
using AshenPantheon.Core;

/// <summary>Pantheon Gate (blok w mieście, E): wybór dungeonów endgame.
/// Lewa kolumna: dungeony grupowe T1-T5 w trudnościach Blood/Hell/Infernal (opłata złotem;
/// klucze itemowe = przyszła faza). Prawa: solo The Final Proving Q1-Q10 (sekwencyjne odblokowanie).
/// Wejście wymaga ukończonej kampanii; w co-op podróż odpala HOST.</summary>
public partial class EndgamePanel : CanvasLayer
{
    private Panel _root;
    private VBoxContainer _group, _solo;
    private Label _status;

    public static void Toggle(SceneTree tree)
    {
        if (tree.Root.GetNodeOrNull<EndgamePanel>("EndgamePanel") is { } existing) { existing.QueueFree(); return; }
        tree.Root.AddChild(new EndgamePanel { Name = "EndgamePanel", Layer = 7 });
    }

    public override void _Ready()
    {
        _root = UiKit.Window(this, "PANTHEON GATE — ENDGAME    [E/Esc] close");
        var vb = _root.GetNode<VBoxContainer>("VB");
        _status = new Label();
        vb.AddChild(_status);

        var cols = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 24);
        vb.AddChild(cols);

        _group = MakeColumn(cols, "GROUP DUNGEONS  (party up to 4 — Blood / Hell / Infernal)");
        _solo = MakeColumn(cols, "THE FINAL PROVING  (solo challenge Q1-Q10)");
        Refresh();
    }

    private static VBoxContainer MakeColumn(HBoxContainer parent, string title)
    {
        var vb = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        vb.AddChild(new Label { Text = title });
        var scroll = UiKit.VScroll();
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(list);
        vb.AddChild(scroll);
        parent.AddChild(vb);
        return list;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode is Key.E or Key.Escape)
        {
            QueueFree();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Refresh()
    {
        foreach (Node c in _group.GetChildren()) c.QueueFree();
        foreach (Node c in _solo.GetChildren()) c.QueueFree();

        if (!GameState.CampaignCompleted)
        {
            _status.Text = "The gate is sealed. Complete the campaign (defeat Pharaoh Nefertari in the Great Desert) to enter.";
            _status.Modulate = new Color(0.9f, 0.6f, 0.4f);
            return;
        }
        bool canTravel = !Net.Online || Net.IsServer;
        _status.Text = $"Your gold: {GameState.Wallet.Gold}" +
                       (canTravel ? "" : "    (only the host starts a dungeon)");

        // ── dungeony grupowe ──
        foreach (var dun in EndgameCatalog.Dungeons)
        {
            var header = new Label { Text = $"T{dun.Tier}  {dun.Name}" + (dun.Enabled ? "" : "   — coming soon") };
            header.Modulate = dun.Enabled ? new Color(1f, 0.85f, 0.5f) : new Color(0.5f, 0.5f, 0.55f);
            _group.AddChild(header);
            if (!dun.Enabled) continue;

            foreach (var diff in EndgameCatalog.Difficulties)
            {
                string clearKey = $"{dun.Id}/{diff.Id}";
                bool cleared = GameState.EndgameCleared.Contains(clearKey);
                var prev = EndgameCatalog.Previous(diff.Id);
                bool prevOk = !diff.RequiresPrevious || prev == null || GameState.EndgameCleared.Contains($"{dun.Id}/{prev.Id}");

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                var info = new Label
                {
                    Text = $"   {diff.Name}   —   HP x{diff.HpMult:0.0}, item lvl {diff.ItemLevel}, fee {diff.GoldFee}g" +
                           (cleared ? "   ✔" : ""),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                info.Modulate = cleared ? new Color(0.6f, 0.9f, 0.6f) : Colors.White;
                row.AddChild(info);

                var enter = new Button { Text = "Enter", CustomMinimumSize = new Vector2(90, 0) };
                if (!canTravel) { enter.Disabled = true; }
                else if (!prevOk) { enter.Disabled = true; enter.TooltipText = $"Clear {prev.Name} first"; info.Modulate = new Color(0.55f, 0.55f, 0.6f); }
                else if (GameState.Wallet.Gold < diff.GoldFee) { enter.Disabled = true; enter.TooltipText = "Not enough gold"; }
                var dunId = dun.Id; var diffId = diff.Id; var zone = dun.Zone; long fee = diff.GoldFee;
                enter.Pressed += () => EnterChallenge(zone, EndgameCatalog.GroupChallenge(dunId, diffId), fee);
                row.AddChild(enter);
                _group.AddChild(row);
            }
        }

        // ── solo Q1-Q10 ──
        _solo.AddChild(new Label { Text = $"Highest unlocked: Q{GameState.EndgameQ}   (clear a stage to unlock the next)" });
        bool solo = Net.PlayerCount() <= 1;
        if (!solo) _solo.AddChild(new Label { Text = "Solo only — leave your party to enter.", Modulate = new Color(0.9f, 0.6f, 0.4f) });
        for (int q = 1; q <= EndgameCatalog.QMax; q++)
        {
            var s = EndgameCatalog.QScale(q);
            bool unlocked = q <= GameState.EndgameQ;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var info = new Label
            {
                Text = $"   Q{q}   —   HP x{s.Hp:0.0}, item lvl {s.ItemLevel}, fee {s.Fee}g" + (unlocked ? "" : "   🔒"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            info.Modulate = unlocked ? Colors.White : new Color(0.55f, 0.55f, 0.6f);
            row.AddChild(info);

            var enter = new Button { Text = "Enter", CustomMinimumSize = new Vector2(90, 0) };
            enter.Disabled = !unlocked || !solo || GameState.Wallet.Gold < s.Fee;
            int captured = q; long fee = s.Fee;
            enter.Pressed += () => EnterQ(captured, fee);
            row.AddChild(enter);
            _solo.AddChild(row);
        }
    }

    /// <summary>Wejście w run Q: opłata + POWTARZALNY auto-quest + podróż na M1 runu
    /// (tryb world = statyczne mapy z interakcjami; arena = proceduralne pokoje).</summary>
    private void EnterQ(int q, long fee)
    {
        var run = EndgameCatalog.RunFor(q);
        if (run == null || run.Maps.Count == 0 || GameState.Wallet.Gold < fee) return;
        GameState.Wallet.Gold -= fee;
        StartQRunQuest(run.Quest);
        GameState.Save();
        string scene = run.Mode == "world" ? "res://scenes/WorldZone.tscn" : "res://scenes/Arena.tscn";
        Travel(scene, run.Maps[0], EndgameCatalog.QChallenge(q));
    }

    private void EnterChallenge(string zoneId, string challenge, long fee)
    {
        if (zoneId.Length == 0 || GameState.Wallet.Gold < fee) return;
        GameState.Wallet.Gold -= fee; // opłata wejścia = sink złota (klucze itemowe dojdą później)
        GameState.Save();
        Travel("res://scenes/Arena.tscn", zoneId, challenge);
    }

    private void Travel(string scene, string zoneId, string challenge)
    {
        int seed = (int)(GD.Randi() % int.MaxValue);
        if (seed == 0) seed = 1;
        QueueFree();
        Net.TravelAll(scene, seed, zoneId, challenge);
    }

    /// <summary>Run Q = POWTARZALNY auto-quest (M1→M2→M3): reset poprzedniego przebiegu i przyjęcie od nowa.
    /// Tracker pod minimapą prowadzi gracza przez mapy.</summary>
    private static void StartQRunQuest(string questId)
    {
        var q = QuestCatalog.Find(questId);
        if (q == null) return;
        GameState.Quests.Abandon(q.Id);          // porzuć niedokończony poprzedni run
        GameState.Quests.Completed.Remove(q.Id); // powtarzalny — ukończenie nie blokuje kolejnych
        if (GameState.Quests.Accept(q, GameState.Progress.Level))
            Net.SendChatLocal($"Quest accepted: {q.Name}");
    }
}

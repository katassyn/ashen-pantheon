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

    /// <summary>Nagłówek sekcji: glif + tytuł w kolorze.</summary>
    private static HBoxContainer HeaderRow(string glyph, string text, Color col)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 8);
        h.AddChild(new GlyphIcon { Kind = glyph, IconColor = col, CustomMinimumSize = new Vector2(22, 22), MouseFilter = Control.MouseFilterEnum.Ignore });
        h.AddChild(new Label { Text = text, Modulate = col });
        return h;
    }

    /// <summary>Przycisk „Enter" z glifem play.</summary>
    private static void DecorateEnter(Button b)
    {
        b.Text = "   " + b.Text;
        b.Alignment = HorizontalAlignment.Center;
        b.AddChild(new GlyphIcon
        {
            Kind = "play", IconColor = new Color(0.85f, 1f, 0.85f),
            AnchorTop = 0.5f, AnchorBottom = 0.5f, OffsetTop = -8, OffsetBottom = 8,
            OffsetLeft = 8, Size = new Vector2(16, 16), MouseFilter = Control.MouseFilterEnum.Ignore,
        });
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
            _group.AddChild(HeaderRow("gate", $"T{dun.Tier}  {dun.Name}" + (dun.Enabled ? "" : "   — coming soon"),
                dun.Enabled ? new Color(1f, 0.85f, 0.5f) : new Color(0.5f, 0.5f, 0.55f)));
            if (!dun.Enabled) continue;

            foreach (var diff in EndgameCatalog.Difficulties)
            {
                string clearKey = $"{dun.Id}/{diff.Id}";
                bool cleared = GameState.EndgameCleared.Contains(clearKey);
                var prev = EndgameCatalog.Previous(diff.Id);
                bool prevOk = !diff.RequiresPrevious || prev == null || GameState.EndgameCleared.Contains($"{dun.Id}/{prev.Id}");

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                row.AddChild(new DifficultyIcon { Id = diff.Id, CustomMinimumSize = new Vector2(26, 26), MouseFilter = Control.MouseFilterEnum.Ignore });
                var info = new Label
                {
                    Text = $"{diff.Name}   —   HP x{diff.HpMult:0.0}, item lvl {diff.ItemLevel}, [T{dun.Tier}] key + {diff.GoldFee}g" +
                           (cleared ? "   ✔" : ""),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                info.Modulate = cleared ? new Color(0.6f, 0.9f, 0.6f) : Colors.White;
                row.AddChild(info);

                string keyId = $"t{dun.Tier}_key";
                bool hasKey = GameState.Pouch.Count(keyId) > 0;
                var enter = new Button { Text = "Enter", CustomMinimumSize = new Vector2(90, 0) };
                if (!canTravel) { enter.Disabled = true; }
                else if (!prevOk) { enter.Disabled = true; enter.TooltipText = $"Clear {prev.Name} first"; info.Modulate = new Color(0.55f, 0.55f, 0.6f); }
                else if (!hasKey) { enter.Disabled = true; enter.TooltipText = $"Requires a [T{dun.Tier}] Mythological Dungeon Key"; }
                else if (GameState.Wallet.Gold < diff.GoldFee) { enter.Disabled = true; enter.TooltipText = "Not enough gold"; }
                var dunId = dun.Id; var diffId = diff.Id; var zone = dun.Zone; long fee = diff.GoldFee; string capturedKey = keyId;
                enter.Pressed += () => EnterChallenge(zone, EndgameCatalog.GroupChallenge(dunId, diffId), fee, capturedKey);
                DecorateEnter(enter);
                row.AddChild(enter);
                _group.AddChild(row);
            }
        }

        // ── solo Q1-Q10: każdy Q × 3 trudności (Infernal/Hell/Bloodshed) za Fragments of Infernal Passage ──
        long ips = GameState.Pouch.Count("ips");
        int level = GameState.Progress.Level;
        _solo.AddChild(new Label { Text = $"Highest unlocked: Q{GameState.EndgameQ}   ·   Fragments of Infernal Passage: {ips}   ·   your level: {level}" });
        bool solo = Net.PlayerCount() <= 1;
        if (!solo) _solo.AddChild(new Label { Text = "Solo only — leave your party to enter.", Modulate = new Color(0.9f, 0.6f, 0.4f) });

        for (int q = 1; q <= EndgameCatalog.QMax; q++)
        {
            bool unlocked = q <= GameState.EndgameQ;
            _solo.AddChild(HeaderRow("skull", $"Q{q}" + (unlocked ? "" : "   🔒 clear the previous stage first"),
                unlocked ? new Color(1f, 0.85f, 0.5f) : new Color(0.55f, 0.55f, 0.6f)));
            if (!unlocked) continue;

            foreach (var qd in EndgameCatalog.QDifficulties)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                row.AddChild(new DifficultyIcon { Id = qd.Id, CustomMinimumSize = new Vector2(26, 26), MouseFilter = Control.MouseFilterEnum.Ignore });
                var info = new Label
                {
                    Text = $"{qd.Name}   —   req lvl {qd.LevelReq}, HP x{qd.HpMult:0.#}, item lvl {qd.ItemLevel}, entry {qd.IpsFee} IPS",
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                bool levelOk = level >= qd.LevelReq;
                bool ipsOk = ips >= qd.IpsFee;
                info.Modulate = levelOk ? Colors.White : new Color(0.55f, 0.55f, 0.6f);
                row.AddChild(info);

                var enter = new Button { Text = "Enter", CustomMinimumSize = new Vector2(90, 0) };
                enter.Disabled = !solo || !levelOk || !ipsOk;
                if (!levelOk) enter.TooltipText = $"Requires level {qd.LevelReq}";
                else if (!ipsOk) enter.TooltipText = $"Requires {qd.IpsFee} Fragments of Infernal Passage";
                int cq = q; string cd = qd.Id; int fee = qd.IpsFee;
                enter.Pressed += () => EnterQ(cq, cd, fee);
                DecorateEnter(enter);
                row.AddChild(enter);
                _solo.AddChild(row);
            }
        }
    }

    /// <summary>Wejście w run Q na trudności: opłata Fragments of Infernal Passage (kanon) + POWTARZALNY
    /// auto-quest + podróż na M1 runu (world = statyczne mapy; arena = proceduralne pokoje).</summary>
    private void EnterQ(int q, string difficultyId, int ipsFee)
    {
        var run = EndgameCatalog.RunFor(q);
        if (run == null || run.Maps.Count == 0) return;
        if (!GameState.Pouch.TryTake("ips", ipsFee)) return; // fragment-sink (Sakwa)
        StartQRunQuest(run.Quest);
        GameState.Save();
        string scene = run.Mode == "world" ? "res://scenes/WorldZone.tscn" : "res://scenes/Arena.tscn";
        Travel(scene, run.Maps[0], EndgameCatalog.QChallenge(q, difficultyId));
    }

    /// <summary>Dungeon grupowy: zużyj [T?] klucz (kanon) + opłata złotem, potem podróż.</summary>
    private void EnterChallenge(string zoneId, string challenge, long fee, string keyId)
    {
        if (zoneId.Length == 0 || GameState.Wallet.Gold < fee) return;
        if (!GameState.Pouch.TryTake(keyId)) { Net.SendChatLocal("You need a Mythological Dungeon Key."); return; }
        GameState.Wallet.Gold -= fee; // klucz + złoto = podwójny sink
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
        GameState.CarryTokens.Clear();           // świeży run = puste ręce (Q10)
        if (GameState.Quests.Accept(q, GameState.Progress.Level))
            Net.SendChatLocal($"Quest accepted: {q.Name}");
    }
}

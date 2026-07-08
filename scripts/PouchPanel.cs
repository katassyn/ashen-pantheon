using System;
using Godot;
using AshenPantheon.Core;

/// <summary>Kafelek składnika w sakwie: kolorowa ikona-diament w ramce; materiały upgrade
/// dostają ramkę koloru rzadkości (common/rare/legendary).</summary>
public partial class IngredientSwatch : Control
{
    public IngredientDefinition Def;

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var tint = Color.FromString(Def.Tint, new Color(0.75f, 0.75f, 0.75f));
        var border = Def.Rarity switch
        {
            "legendary" => new Color(1f, 0.5f, 0.15f),
            "rare" => new Color(0.35f, 0.55f, 1f),
            "common" => new Color(0.7f, 0.7f, 0.75f),
            _ => new Color(0.4f, 0.38f, 0.5f),
        };
        DrawRect(rect, new Color(0.06f, 0.05f, 0.09f, 0.9f));
        DrawRect(rect, border, false, 2f);
        // diament w kolorze składnika
        var c = Size / 2f; float r = Size.X * 0.30f;
        DrawColoredPolygon(new[] {
            c + new Vector2(0, -r), c + new Vector2(r, 0), c + new Vector2(0, r), c + new Vector2(-r, 0)
        }, tint);
        DrawArc(c, r * 1.02f, 0, Mathf.Tau, 4, new Color(tint, 0.5f), 1.5f);
    }
}

/// <summary>Podniesienie składnika: wejście = prosto do SAKWY (nie do plecaka) + floating text.</summary>
public partial class IngredientPickup : Area2D
{
    public string IngredientId = "";
    public int Count = 1;
    private bool _taken;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1;
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 18f } });
        var def = IngredientCatalog.Find(IngredientId);
        var tint = Color.FromString(def?.Tint ?? "#c0c0c0", new Color(0.75f, 0.75f, 0.75f));
        var label = new Label { Text = "◆", Position = new Vector2(-7f, -14f) };
        label.AddThemeColorOverride("font_color", tint);
        label.AddThemeFontSizeOverride("font_size", 18);
        AddChild(label);
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_taken || body is not PlayerController p || !p.IsMultiplayerAuthority()) return;
        _taken = true;
        GameState.Pouch.Add(IngredientId, Count);
        GameState.Save();
        string name = IngredientCatalog.Find(IngredientId)?.Name ?? IngredientId;
        FloatingText.Spawn(GetParent(), GlobalPosition, $"+{Count} {name}", new Color(0.7f, 0.9f, 1f), 14);
        QueueFree();
    }
}

/// <summary>Sakwa składników (kanon IngredientPouchPlugin): przycisk w EKWIPUNKU → zakładki kategorii
/// z licznikami. Tu żyją Fragments of Infernal Passage, klucze T1-T5, Elite Lootboxy (z przyciskiem Open),
/// waluty i materiały craftingowe.</summary>
public partial class PouchPanel : CanvasLayer
{
    private Panel _root;
    private VBoxContainer _list;
    private HBoxContainer _tabs;
    private string _category = "dungeon";

    public static void Toggle(SceneTree tree)
    {
        if (tree.Root.GetNodeOrNull<PouchPanel>("PouchPanel") is { } existing) { existing.QueueFree(); return; }
        tree.Root.AddChild(new PouchPanel { Name = "PouchPanel", Layer = 9 });
    }

    public override void _Ready()
    {
        _root = UiKit.Window(this, "INGREDIENT POUCH    [Esc] close");
        var vb = _root.GetNode<VBoxContainer>("VB");

        _tabs = new HBoxContainer();
        _tabs.AddThemeConstantOverride("separation", 8);
        vb.AddChild(_tabs);
        foreach (var cat in new[] { "dungeon", "upgrade", "currency", "crafting", "quest" })
        {
            var b = new Button { Text = char.ToUpper(cat[0]) + cat[1..], CustomMinimumSize = new Vector2(130, 0) };
            string captured = cat;
            b.Pressed += () => { _category = captured; Refresh(); };
            _tabs.AddChild(b);
        }

        var scroll = UiKit.VScroll();
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);
        vb.AddChild(scroll);
        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo && k.PhysicalKeycode == Key.Escape)
        {
            QueueFree();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Refresh()
    {
        foreach (Node c in _list.GetChildren()) c.QueueFree();
        bool any = false;
        foreach (var def in IngredientCatalog.InCategory(_category))
        {
            any = true;
            long count = GameState.Pouch.Count(def.Id);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);

            row.AddChild(new IngredientSwatch { Def = def, CustomMinimumSize = new Vector2(30, 30) });

            var name = new Label
            {
                Text = def.Name + (def.Description.Length > 0 ? $"   — {def.Description}" : ""),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ClipText = true,
            };
            name.Modulate = count > 0 ? Colors.White : new Color(0.55f, 0.55f, 0.6f);
            row.AddChild(name);

            var amount = new Label { Text = $"x{count}", CustomMinimumSize = new Vector2(70, 0), HorizontalAlignment = HorizontalAlignment.Right };
            amount.Modulate = count > 0 ? new Color(1f, 0.9f, 0.5f) : new Color(0.5f, 0.5f, 0.55f);
            row.AddChild(amount);

            if (def.Id == "elite_lootbox")
            {
                var open = new Button { Text = "Open", Disabled = count < 1, CustomMinimumSize = new Vector2(90, 0) };
                open.Pressed += OpenLootbox;
                row.AddChild(open);
            }
            _list.AddChild(row);
        }
        if (!any) _list.AddChild(new Label { Text = "  Nothing here yet." });
    }

    /// <summary>Elite Lootbox (nagroda Q — kanon): roll tabeli lootu, zawartość do plecaka/sakwy/portfela.</summary>
    private void OpenLootbox()
    {
        if (!GameState.Pouch.TryTake("elite_lootbox")) return;
        var rng = new Random();
        var drops = LootTables.Roll("elite_lootbox", rng, new LootGenerator(), 56);
        foreach (var drop in drops)
        {
            if (drop.Item != null)
            {
                if (GameState.Bag.TryAutoPlace(drop.Item))
                    Net.SendChatLocal($"Lootbox: {drop.Item.Name} [{drop.Item.Rarity}]");
                else if (PlayerController.Local is { } pl)
                {
                    ItemPickup.Spawn(pl.GetParent(), pl.GlobalPosition, drop.Item);
                    Net.SendChatLocal($"Lootbox: bag full — {drop.Item.Name} dropped at your feet.");
                }
            }
            else if (drop.Gold > 0)
            {
                GameState.Wallet.Gold += drop.Gold;
                Net.SendChatLocal($"Lootbox: +{drop.Gold} gold");
            }
            else if (drop.Ingredient.Length > 0)
            {
                GameState.Pouch.Add(drop.Ingredient, drop.IngredientCount);
                string n = IngredientCatalog.Find(drop.Ingredient)?.Name ?? drop.Ingredient;
                Net.SendChatLocal($"Lootbox: +{drop.IngredientCount} {n}");
            }
        }
        GameState.Save();
        Refresh();
    }
}

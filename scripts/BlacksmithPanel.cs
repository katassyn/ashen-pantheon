using Godot;
using AshenPantheon.Core;

/// <summary>Kowal (kanon MyCraftingPlugin2 — NPC Robert): wybierasz kategorię, potem recepturę,
/// widzisz materiały (z sakwy) + koszt złota, klikasz Craft. Wynik = przedmiot do plecaka
/// lub składnik do sakwy. Pełnoekranowy jak reszta paneli.</summary>
public partial class BlacksmithPanel : CanvasLayer
{
    private Panel _root;
    private VBoxContainer _list;
    private string _category = "armor";

    public static void Toggle(SceneTree tree)
    {
        if (tree.Root.GetNodeOrNull<BlacksmithPanel>("BlacksmithPanel") is { } existing) { existing.QueueFree(); return; }
        tree.Root.AddChild(new BlacksmithPanel { Name = "BlacksmithPanel", Layer = 7 });
    }

    public override void _Ready()
    {
        _root = UiKit.Window(this, "BLACKSMITH — Robert    [E/Esc] close");
        var vb = _root.GetNode<VBoxContainer>("VB");

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 8);
        vb.AddChild(tabs);
        foreach (var cat in new[] { "armor", "weapons", "jewelry", "materials" })
        {
            var b = new Button { Text = char.ToUpper(cat[0]) + cat[1..], CustomMinimumSize = new Vector2(130, 0) };
            string captured = cat;
            b.Pressed += () => { _category = captured; Refresh(); };
            tabs.AddChild(b);
        }

        var scroll = UiKit.VScroll();
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_list);
        vb.AddChild(scroll);
        Refresh();
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
        foreach (Node c in _list.GetChildren()) c.QueueFree();
        _list.AddChild(new Label { Text = $"Your gold: {GameState.Wallet.Gold}", Modulate = new Color(1f, 0.9f, 0.5f) });

        bool any = false;
        foreach (var recipe in RecipeCatalog.InCategory(_category))
        {
            any = true;
            _list.AddChild(RecipeRow(recipe));
        }
        if (!any) _list.AddChild(new Label { Text = "  No recipes here." });
    }

    private Control RecipeRow(RecipeDefinition r)
    {
        var box = new VBoxContainer();
        var style = new StyleBoxFlat { BgColor = new Color(0.09f, 0.08f, 0.12f, 0.85f) };
        style.SetContentMarginAll(8);
        style.SetCornerRadiusAll(4);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", style);
        panel.AddChild(box);

        // tytuł + wynik
        string resultDesc = r.ResultType == "ingredient"
            ? $"→ {r.ResultCount}x {IngredientCatalog.Find(r.ResultIngredient)?.Name ?? r.ResultIngredient}"
            : $"→ {r.ResultRarity} {r.ResultKind}  (item lvl {r.ResultItemLevel})";
        var title = new Label { Text = $"{r.Name}   {resultDesc}" };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.Modulate = new Color(0.9f, 0.85f, 0.6f);
        box.AddChild(title);

        // materiały (kolor wg posiadania)
        var mats = new HBoxContainer();
        mats.AddThemeConstantOverride("separation", 14);
        foreach (var input in r.Inputs)
        {
            long have = GameState.Pouch.Count(input.Ingredient);
            var lbl = new Label { Text = $"{IngredientCatalog.Find(input.Ingredient)?.Name ?? input.Ingredient}: {have}/{input.Count}" };
            lbl.Modulate = have >= input.Count ? new Color(0.6f, 0.9f, 0.6f) : new Color(0.9f, 0.5f, 0.5f);
            mats.AddChild(lbl);
        }
        if (r.GoldCost > 0)
        {
            var g = new Label { Text = $"Gold: {r.GoldCost}" };
            g.Modulate = GameState.Wallet.Gold >= r.GoldCost ? new Color(0.6f, 0.9f, 0.6f) : new Color(0.9f, 0.5f, 0.5f);
            mats.AddChild(g);
        }
        box.AddChild(mats);

        // przycisk Craft
        var craft = new Button { Text = "Craft", CustomMinimumSize = new Vector2(120, 0) };
        craft.Disabled = !Crafting.CanCraft(r, GameState.Pouch, GameState.Wallet.Gold);
        craft.Pressed += () => DoCraft(r);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        row.AddChild(craft);
        box.AddChild(row);
        return panel;
    }

    private void DoCraft(RecipeDefinition r)
    {
        if (!Crafting.TakeCosts(r, GameState.Pouch, GameState.Wallet.Gold)) return;
        GameState.Wallet.Gold -= r.GoldCost;
        var (item, ingredient, count) = Crafting.Result(r, new LootGenerator());

        if (item != null)
        {
            if (GameState.Bag.TryAutoPlace(item))
                Net.SendChatLocal($"Crafted: {item.Name} [{item.Rarity}]");
            else if (PlayerController.Local is { } pl)
            {
                ItemPickup.Spawn(pl.GetParent(), pl.GlobalPosition, item);
                Net.SendChatLocal($"Bag full — {item.Name} dropped at your feet.");
            }
        }
        else if (ingredient.Length > 0)
        {
            GameState.Pouch.Add(ingredient, count);
            Net.SendChatLocal($"Crafted: {count}x {IngredientCatalog.Find(ingredient)?.Name ?? ingredient}");
        }
        GameState.Save();
        Refresh();
    }
}

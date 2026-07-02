using System.Collections.Generic;
using System.Linq;

namespace AshenPantheon.Core;

/// <summary>Plecak-siatka (tetris jak PoE): itemy zajmują W×H komórek, sprawdzamy kolizje przy wkładaniu.</summary>
public sealed class GridInventory
{
    public int Width { get; }
    public int Height { get; }

    private readonly List<PlacedItem> _placed = new();

    public GridInventory(int width = 12, int height = 6)
    {
        Width = width;
        Height = height;
    }

    public sealed record PlacedItem(Item Item, int X, int Y);

    public IReadOnlyList<PlacedItem> Placed => _placed;
    public IEnumerable<Item> Items => _placed.Select(p => p.Item);
    public int Count => _placed.Count;

    public bool Contains(Item item) => _placed.Any(p => p.Item == item);

    public PlacedItem? At(int x, int y) =>
        _placed.FirstOrDefault(p =>
            x >= p.X && x < p.X + p.Item.Size.W &&
            y >= p.Y && y < p.Y + p.Item.Size.H);

    /// <summary>Czy item zmieści się z lewym-górnym rogiem w (x,y)? `ignore` pozwala przesuwać item w obrębie siatki.</summary>
    public bool CanPlaceAt(Item item, int x, int y, Item? ignore = null)
    {
        var (w, h) = item.Size;
        if (x < 0 || y < 0 || x + w > Width || y + h > Height) return false;
        foreach (var p in _placed)
        {
            if (p.Item == ignore || p.Item == item) continue;
            var (pw, ph) = p.Item.Size;
            bool overlap = x < p.X + pw && p.X < x + w && y < p.Y + ph && p.Y < y + h;
            if (overlap) return false;
        }
        return true;
    }

    public bool PlaceAt(Item item, int x, int y)
    {
        if (!CanPlaceAt(item, x, y)) return false;
        Remove(item);
        _placed.Add(new PlacedItem(item, x, y));
        return true;
    }

    /// <summary>Pierwsze wolne miejsce (skan wierszami). False = plecak pełny dla tego rozmiaru.</summary>
    public bool TryAutoPlace(Item item)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (CanPlaceAt(item, x, y))
                    return PlaceAt(item, x, y);
        return false;
    }

    public bool Remove(Item item)
    {
        int idx = _placed.FindIndex(p => p.Item == item);
        if (idx < 0) return false;
        _placed.RemoveAt(idx);
        return true;
    }

    public void Clear() => _placed.Clear();
}

/// <summary>Pasek skilli: 5 slotów (LPM/PPM/Q/E/R), skille przypinane z drzewka. Dash też konkuruje o slot.</summary>
public sealed class Loadout
{
    public const int SlotCount = 5;
    public static readonly string[] SlotKeys = { "LPM", "PPM", "Q", "E", "R" };

    public string?[] Slots { get; } = new string?[SlotCount];

    /// <summary>Przypisuje skill do slotu (usuwa duplikat z innego slotu).</summary>
    public void Assign(int slot, string? skillId)
    {
        if (slot < 0 || slot >= SlotCount) return;
        if (skillId != null)
            for (int i = 0; i < SlotCount; i++)
                if (i != slot && Slots[i] == skillId)
                    Slots[i] = null;
        Slots[slot] = skillId;
    }

    public int? SlotOf(string skillId)
    {
        for (int i = 0; i < SlotCount; i++)
            if (Slots[i] == skillId) return i;
        return null;
    }
}

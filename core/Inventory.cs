using System.Collections.Generic;

namespace AshenPantheon.Core;

/// <summary>Plecak — itemy, które nie są założone.</summary>
public sealed class Inventory
{
    private readonly List<Item> _items = new();

    public IReadOnlyList<Item> Items => _items;
    public int Count => _items.Count;

    public void Add(Item item) => _items.Add(item);
    public bool Remove(Item item) => _items.Remove(item);
}

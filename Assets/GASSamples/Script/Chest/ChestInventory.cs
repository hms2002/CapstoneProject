using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ChestInventory
{
    [SerializeField] private int capacity = 16;
    [SerializeField] private List<WeaponDefinition> items = new();

    public int Capacity => capacity;
    public event Action OnChanged;

    public ChestInventory(int capacity)
    {
        this.capacity = Mathf.Max(0, capacity);
        items = new List<WeaponDefinition>(this.capacity);
        for (int i = 0; i < this.capacity; i++) items.Add(null);
    }

    public WeaponDefinition Get(int index) => (index >= 0 && index < items.Count) ? items[index] : null;

    public bool Set(int index, WeaponDefinition weapon)
    {
        if (index < 0 || index >= items.Count) return false;
        items[index] = weapon;
        OnChanged?.Invoke();
        return true;
    }

    public bool Clear(int index) => Set(index, null);

    public bool Swap(int a, int b)
    {
        if (a < 0 || b < 0 || a >= items.Count || b >= items.Count) return false;
        (items[a], items[b]) = (items[b], items[a]);
        OnChanged?.Invoke();
        return true;
    }

    public bool TryFindEmpty(out int idx)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null) { idx = i; return true; }
        }
        idx = -1;
        return false;
    }
}

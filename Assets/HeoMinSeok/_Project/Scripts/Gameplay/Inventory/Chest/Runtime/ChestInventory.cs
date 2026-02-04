using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ChestInventory
{
    [SerializeField] private int capacity = 16;
    [SerializeField] private List<ScriptableObject> items = new();

    public int Capacity => capacity;
    public event Action OnChanged;

    public ChestInventory(int capacity)
    {
        this.capacity = Mathf.Max(0, capacity);
        items = new List<ScriptableObject>(this.capacity);
        for (int i = 0; i < this.capacity; i++) items.Add(null);
    }

    private void EnsureSize()
    {
        if (items == null) items = new List<ScriptableObject>();
        if (items.Count == capacity) return;

        if (items.Count < capacity)
        {
            while (items.Count < capacity) items.Add(null);
        }
        else
        {
            items.RemoveRange(capacity, items.Count - capacity);
        }
    }

    public ScriptableObject Get(int index)
    {
        EnsureSize();
        if (index < 0 || index >= capacity) return null;
        return items[index];
    }

    public bool Set(int index, ScriptableObject item)
    {
        EnsureSize();
        if (index < 0 || index >= capacity) return false;

        items[index] = item;
        OnChanged?.Invoke();
        return true;
    }

    // =========================================================
    // [추가됨] 빈 슬롯을 찾아 아이템을 넣는 함수
    // =========================================================
    public bool TryAdd(ScriptableObject item)
    {
        // 1. 빈 공간(인덱스) 찾기
        if (TryFindEmpty(out int idx))
        {
            // 2. 해당 공간에 아이템 설정
            return Set(idx, item);
        }

        // 3. 꽉 찼으면 false 반환
        return false;
    }

    public bool Swap(int a, int b)
    {
        EnsureSize();
        if (a < 0 || a >= capacity) return false;
        if (b < 0 || b >= capacity) return false;
        if (a == b) return true;

        (items[a], items[b]) = (items[b], items[a]);
        OnChanged?.Invoke();
        return true;
    }

    public bool TryFindEmpty(out int idx)
    {
        EnsureSize();
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == null) { idx = i; return true; }
        }
        idx = -1;
        return false;
    }
}
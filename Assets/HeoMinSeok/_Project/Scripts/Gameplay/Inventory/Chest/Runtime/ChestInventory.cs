using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ChestInventory
{
    [Serializable]
    private class Slot
    {
        public ScriptableObject item;
        public int relicLevel; // item이 RelicDefinition일 때만 의미 있음
    }

    [SerializeField] private int capacity = 16;
    [SerializeField] private List<Slot> slots = new();

    public int Capacity => capacity;
    public event Action OnChanged;

    private void EnsureSize()
    {
        if (slots == null) slots = new List<Slot>();

        if (slots.Count < capacity)
        {
            while (slots.Count < capacity) slots.Add(new Slot());
        }
        else if (slots.Count > capacity)
        {
            slots.RemoveRange(capacity, slots.Count - capacity);
        }
    }

    public ScriptableObject Get(int index)
    {
        EnsureSize();
        if (index < 0 || index >= capacity) return null;
        return slots[index].item;
    }

    public int GetRelicLevelInSlot(int index)
    {
        EnsureSize();
        if (index < 0 || index >= capacity) return 0;

        var so = slots[index].item;
        if (so is not RelicDefinition r) return 0;

        int lvl = slots[index].relicLevel;
        if (lvl <= 0) lvl = (r.dropLevel > 0 ? r.dropLevel : 1);
        return Mathf.Max(1, lvl);
    }

    public bool Set(int index, ScriptableObject item)
    {
        EnsureSize();
        if (index < 0 || index >= capacity) return false;

        slots[index].item = item;

        if (item is RelicDefinition r)
        {
            // 레벨 정보 없이 들어온 경우: 기본 드롭레벨로
            int lvl = r.dropLevel > 0 ? r.dropLevel : 1;
            slots[index].relicLevel = Mathf.Max(1, lvl);
        }
        else
        {
            slots[index].relicLevel = 0;
        }

        OnChanged?.Invoke();
        return true;
    }

    public bool SetRelicWithLevel(int index, RelicDefinition relic, int level)
    {
        EnsureSize();
        if (index < 0 || index >= capacity) return false;

        if (relic == null)
        {
            slots[index].item = null;
            slots[index].relicLevel = 0;
        }
        else
        {
            int lvl = Mathf.Max(1, level);
            slots[index].item = relic;
            slots[index].relicLevel = relic.ClampLevel(lvl);
        }

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

        (slots[a], slots[b]) = (slots[b], slots[a]);
        OnChanged?.Invoke();
        return true;
    }

    public bool TryFindEmpty(out int idx)
    {
        EnsureSize();
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].item == null) { idx = i; return true; }
        }
        idx = -1;
        return false;
    }
}
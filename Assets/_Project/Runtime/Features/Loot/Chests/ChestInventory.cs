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

    public const int AcquisitionLimit = 2;
    [SerializeField] private int acquiredCount;
    [SerializeField] private List<ScriptableObject> outstandingAcquisitions = new();
    public int AcquiredCount => acquiredCount;
    public IReadOnlyList<ScriptableObject> OutstandingAcquisitions => outstandingAcquisitions;
    public bool CanAcquire => acquiredCount < AcquisitionLimit;
    public event Action AcquisitionRejected;

    public bool CheckAcquisitionAllowed(ScriptableObject returnedItem = null)
    {
        if (CanAcquire || CanReturnAcquisition(returnedItem)) return true;
        AcquisitionRejected?.Invoke();
        return false;
    }

    public bool CanReturnAcquisition(ScriptableObject item) =>
        acquiredCount > 0 && item != null &&
        outstandingAcquisitions.Contains(item);

    public void RecordReturn(ScriptableObject item)
    {
        if (!CanReturnAcquisition(item)) return;
        outstandingAcquisitions.Remove(item);
        acquiredCount--;
        OnChanged?.Invoke();
    }

    public void RecordAcquisition(ScriptableObject item)
    {
        if (item == null || !CanAcquire) return;
        acquiredCount = Mathf.Min(AcquisitionLimit, acquiredCount + 1);
        outstandingAcquisitions.Add(item);
        OnChanged?.Invoke();
    }

    public void RestoreAcquiredCount(int count, IReadOnlyList<ScriptableObject> outstandingItems = null)
    {
        acquiredCount = Mathf.Clamp(count, 0, AcquisitionLimit);
        outstandingAcquisitions.Clear();
        if (outstandingItems != null)
            for (int i = 0; i < outstandingItems.Count && outstandingAcquisitions.Count < acquiredCount; i++)
                if (outstandingItems[i] != null) outstandingAcquisitions.Add(outstandingItems[i]);
        OnChanged?.Invoke();
    }

    public int Capacity => capacity;
    public event Action OnChanged;

    public ChestInventory()
    {
        capacity = Mathf.Max(0, capacity);
        EnsureSize();
    }

    public ChestInventory(int capacity)
    {
        this.capacity = Mathf.Max(0, capacity);
        EnsureSize();
    }

    public int Count
    {
        get
        {
            EnsureSize();
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].item != null)
                    count++;
            }

            return count;
        }
    }

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

    public bool TryAddRelicWithLevel(RelicDefinition relic, int level)
    {
        if (relic == null)
            return false;

        return TryFindEmpty(out int idx) && SetRelicWithLevel(idx, relic, level);
    }

    public void Clear()
    {
        EnsureSize();
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].item = null;
            slots[i].relicLevel = 0;
        }

        OnChanged?.Invoke();
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

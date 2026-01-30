using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

public class RelicInventory : MonoBehaviour
{
    [Serializable]
    private class Entry
    {
        public RelicDefinition def;
        public Object token; // 중복 구분용
    }

    // 유물 1개당 1개 생성할 런타임 토큰(중복 구분)
    private class RelicRuntimeToken : ScriptableObject { }

    [SerializeField] private int capacity = 18;

    // 중복된 유물 습득/장착 block(원하면 켜기)
    [SerializeField] private bool disallowDuplicateRelics = false;

    // 디버그용(인스펙터 확인): 슬롯 수 = capacity, 빈 슬롯은 null
    [SerializeField] private List<RelicDefinition> debugView = new();

    private Entry[] slots;
    private RelicContext baseCtx;

    public event Action OnChanged;

    private void Awake()
    {
        slots = new Entry[Mathf.Max(0, capacity)];
        for (int i = 0; i < slots.Length; i++) slots[i] = new Entry();

        debugView = new List<RelicDefinition>(capacity);
        for (int i = 0; i < capacity; i++) debugView.Add(null);

        baseCtx = new RelicContext
        {
            owner = gameObject,
            abilitySystem = GetComponent<AbilitySystem>(),
            tagSystem = GetComponent<TagSystem>(),
            effectRunner = GetComponent<GameplayEffectRunner>(),
            attributeSet = GetComponent<AttributeSet>(),
            token = null
        };

        // (선택) 트리거 유물 매니저를 쓸 경우 미리 붙여두기
        if (GetComponent<RelicProcManager>() == null)
            gameObject.AddComponent<RelicProcManager>();

        RefreshDebugView();
    }

    public int Capacity => capacity;

    public int Count
    {
        get
        {
            int c = 0;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].def != null) c++;
            return c;
        }
    }

    /// <summary>슬롯 전체를 그대로 보여줌(빈 슬롯은 null)</summary>
    public IReadOnlyList<RelicDefinition> EquippedRelics => debugView;

    public RelicDefinition GetRelicInSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return null;
        return slots[slotIndex].def;
    }

    public bool CanPlaceRelicInSlot(int slotIndex, RelicDefinition relic, int ignoreIndex = -1)
    {
        if (!IsValidSlot(slotIndex)) return false;
        if (relic == null) return true;

        if (!disallowDuplicateRelics) return true;

        // 중복 금지: 다른 슬롯에 같은 relicId가 있으면 불가
        for (int i = 0; i < slots.Length; i++)
        {
            if (i == ignoreIndex) continue;
            if (i == slotIndex) continue;

            var d = slots[i].def;
            if (d == null) continue;

            if (!string.IsNullOrEmpty(d.relicId) && d.relicId == relic.relicId)
                return false;
        }
        return true;
    }

    public bool TrySetRelicSlot(int slotIndex, RelicDefinition relic)
    {
        if (!IsValidSlot(slotIndex)) return false;
        if (!CanPlaceRelicInSlot(slotIndex, relic, ignoreIndex: slotIndex)) return false;

        var e = slots[slotIndex];
        var prevDef = e.def;
        var prevToken = e.token;

        // 1) 이전 유물 해제
        if (prevDef != null)
        {
            var ctx = baseCtx;
            ctx.token = prevToken;
            prevDef.logic?.OnUnequipped(ctx);

            if (prevToken != null) Destroy(prevToken);
        }

        // 2) 새 유물 장착
        e.def = relic;
        e.token = null;

        if (relic != null)
        {
            var token = ScriptableObject.CreateInstance<RelicRuntimeToken>();
            e.token = token;

            var ctx = baseCtx;
            ctx.token = token;
            relic.logic?.OnEquipped(ctx);
        }

        slots[slotIndex] = e;

        RefreshDebugView();
        OnChanged?.Invoke();
        return true;
    }

    public bool TrySwapRelicSlots(int a, int b)
    {
        if (!IsValidSlot(a) || !IsValidSlot(b)) return false;
        if (a == b) return true;

        // 타입은 둘 다 relic-only 슬롯이므로 CanPlace는 생략 가능.
        // 혹시 중복 금지 체크를 엄격히 하고 싶으면 아래를 켜도 됨:
        // if (!CanPlaceRelicInSlot(b, slots[a].def, ignoreIndex: a)) return false;
        // if (!CanPlaceRelicInSlot(a, slots[b].def, ignoreIndex: b)) return false;

        (slots[a], slots[b]) = (slots[b], slots[a]);

        RefreshDebugView();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>빈 슬롯에 추가(기존 TryAdd 호환용)</summary>
    public bool TryAdd(RelicDefinition relic)
    {
        if (relic == null) return false;

        int empty = FindFirstEmptySlot();
        if (empty < 0) return false;

        return TrySetRelicSlot(empty, relic);
    }

    public bool RemoveAt(int index) => TrySetRelicSlot(index, null);

    // (선택) 특정 유물 1개 제거(중복 중 하나만)
    public bool RemoveOne(RelicDefinition def)
    {
        if (def == null) return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].def == def)
                return TrySetRelicSlot(i, null);
        }
        return false;
    }

    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].def == null) return i;
        return -1;
    }

    private bool IsValidSlot(int idx) => idx >= 0 && idx < slots.Length;

    private void RefreshDebugView()
    {
        if (debugView == null) debugView = new List<RelicDefinition>(capacity);

        if (debugView.Count != capacity)
        {
            debugView.Clear();
            for (int i = 0; i < capacity; i++) debugView.Add(null);
        }

        for (int i = 0; i < capacity; i++)
            debugView[i] = IsValidSlot(i) ? slots[i].def : null;
    }
}

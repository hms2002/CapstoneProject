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
        public int level = 0;
        public Object token; // 유물 인스턴스 식별용
    }

    // 유물 1개당 1개 생성할 런타임 토큰(중복 구분)
    private class RelicRuntimeToken : ScriptableObject { }

    [SerializeField] private int capacity = 18;

    // ✅ 게임 룰: 같은 relicId는 인벤토리에 1개만 존재해야 합니다.
    // 중복 획득은 슬롯을 늘리지 않고 강화 레벨을 합산하여 1개로 유지합니다.
    // (테스트 목적이면 false로 둘 수 있지만, 기본은 true 권장)
    [SerializeField] private bool enforceUniqueRelicId = true;

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
            relicDef = null,
            level = 0,
            token = null
        };

        // (선택) 트리거 유물 매니저를 쓸 경우 미리 붙여두기
        if (GetComponent<RelicProcManager>() == null)
            gameObject.AddComponent<RelicProcManager>();

        // 혹시 기존 저장/디버그로 인해 중복 relicId가 들어있다면 1개로 정리합니다.
        if (enforceUniqueRelicId)
            ConsolidateDuplicates();

        RefreshDebugView();
    }

    /// <summary>
    /// 같은 relicId가 여러 슬롯에 존재하는 경우, 효과 누수 없이 1개로 합칩니다.
    /// - 레벨은 합산 후 maxLevel로 clamp
    /// - 중복 슬롯은 OnUnequipped 후 비우고 token 파괴
    /// - 남는 슬롯은 (token 유지) old->new 레벨로 재적용
    /// </summary>
    private void ConsolidateDuplicates()
    {
        var firstIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < slots.Length; i++)
        {
            var e = slots[i];
            var d = e.def;
            if (d == null) continue;
            if (string.IsNullOrEmpty(d.relicId)) continue;

            if (!firstIndexById.TryGetValue(d.relicId, out int first))
            {
                firstIndexById[d.relicId] = i;
                continue;
            }

            // duplicate 발견: first에 레벨 합산, 현재 슬롯은 해제 후 비우기
            var keeper = slots[first];
            int keeperOld = Mathf.Max(1, keeper.level);
            int add = Mathf.Max(1, e.level);
            int merged = keeper.def != null ? keeper.def.ClampLevel(keeperOld + add) : keeperOld + add;

            // duplicate 슬롯 효과 제거
            if (e.token != null)
            {
                var ctx = baseCtx;
                ctx.relicDef = d;
                ctx.level = Mathf.Max(1, e.level);
                ctx.token = e.token;
                d.logic?.OnUnequipped(ctx);
                Destroy(e.token);
            }

            e.def = null;
            e.level = 0;
            e.token = null;
            slots[i] = e;

            // keeper는 token 유지한 채로 merged 레벨로 재적용
            if (keeper.def != null && keeper.token != null)
            {
                ReapplyLevel(first, merged);
            }
        }
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

    public int GetRelicLevelInSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return 0;
        return slots[slotIndex].def != null ? Mathf.Max(1, slots[slotIndex].level) : 0;
    }

    public bool TryGetRelicLevelById(string relicId, out int level)
    {
        level = 0;
        if (string.IsNullOrEmpty(relicId)) return false;
        for (int i = 0; i < slots.Length; i++)
        {
            var d = slots[i].def;
            if (d == null) continue;
            if (d.relicId == relicId)
            {
                level = Mathf.Max(1, slots[i].level);
                return true;
            }
        }
        return false;
    }

    public bool CanPlaceRelicInSlot(int slotIndex, RelicDefinition relic, int ignoreIndex = -1)
    {
        if (!IsValidSlot(slotIndex)) return false;
        if (relic == null) return true;

        // ✅ 게임 룰: 같은 relicId는 "인벤토리에 1개만" 유지.
        // 하지만 중복 획득/이동은 "불가"가 아니라 "강화(합산)"로 처리해야 합니다.
        // 따라서 '놓을 수 있나?'는 true로 반환하고, 실제 처리(합산/장착)는 TrySetRelicSlot에서 결정합니다.
        return true;
    }

    public bool TrySetRelicSlot(int slotIndex, RelicDefinition relic)
    {
        if (!IsValidSlot(slotIndex)) return false;
        if (!CanPlaceRelicInSlot(slotIndex, relic, ignoreIndex: slotIndex)) return false;

        // ✅ 중복 relicId가 이미 인벤토리에 있다면: "슬롯에 두 개로 놓지 말고" 기존 것을 강화한다.
        // (이 경우, slotIndex의 기존 내용은 건드리지 않음. 드래그/획득 흐름에서는 '성공'으로 반환되어
        //  소스(상자/드랍)에서 아이템이 제거되는 효과를 얻는다.)
        if (relic != null && enforceUniqueRelicId && !string.IsNullOrEmpty(relic.relicId))
        {
            int existing = FindSlotByRelicId(relic.relicId);
            if (existing >= 0 && existing != slotIndex)
            {
                int gain = relic.dropLevel > 0 ? relic.dropLevel : 1;
                var eExist = slots[existing];
                int oldLevel = Mathf.Max(1, eExist.level);
                int newLevel = relic.ClampLevel(oldLevel + gain);
                if (newLevel == oldLevel) return true; // 이미 만렙이면 소비만 발생(성공)

                return ReapplyLevel(existing, newLevel);
            }
        }

        var e = slots[slotIndex];
        var prevDef = e.def;
        var prevToken = e.token;
        var prevLevel = e.level;

        // 1) 이전 유물 해제
        if (prevDef != null)
        {
            var ctx = baseCtx;
            ctx.relicDef = prevDef;
            ctx.level = prevLevel;
            ctx.token = prevToken;
            prevDef.logic?.OnUnequipped(ctx);

            if (prevToken != null) Destroy(prevToken);
        }

        // 2) 새 유물 장착
        e.def = relic;
        e.token = null;
        e.level = 0;

        if (relic != null)
        {
            var token = ScriptableObject.CreateInstance<RelicRuntimeToken>();
            e.token = token;

            // 처음 장착 레벨은 dropLevel(기본 1)
            int lvl = relic.dropLevel > 0 ? relic.dropLevel : 1;
            e.level = relic.ClampLevel(lvl);

            var ctx = baseCtx;
            ctx.relicDef = relic;
            ctx.level = e.level;
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


    /// <summary>
    /// ✅ 유물 획득/추가용: 같은 relicId가 이미 있으면 강화 레벨을 합산하고,
    /// 없으면 빈 슬롯에 새로 장착합니다.
    ///
    /// gainedLevel을 지정하지 않으면 RelicDefinition.dropLevel(기본 1)을 사용합니다.
    /// </summary>
    public bool TryAcquireOrUpgrade(RelicDefinition relic, int gainedLevel = -1)
    {
        if (relic == null) return false;

        int gain = gainedLevel > 0 ? gainedLevel : (relic.dropLevel > 0 ? relic.dropLevel : 1);

        // 1) 이미 가진 유물이라면 강화
        int idx = FindSlotByRelicId(relic.relicId);
        if (idx >= 0)
        {
            var e = slots[idx];
            int oldLevel = Mathf.Max(1, e.level);
            int newLevel = relic.ClampLevel(oldLevel + gain);
            if (newLevel == oldLevel) return true; // 이미 만렙

            return ReapplyLevel(idx, newLevel);
        }

        // 2) 없으면 빈 슬롯에 추가
        int empty = FindFirstEmptySlot();
        if (empty < 0) return false;

        int initial = relic.ClampLevel(gain);
        return EquipIntoEmptySlot(empty, relic, initial);
    }

    private int FindSlotByRelicId(string relicId)
    {
        if (string.IsNullOrEmpty(relicId)) return -1;
        for (int i = 0; i < slots.Length; i++)
        {
            var d = slots[i].def;
            if (d == null) continue;
            if (d.relicId == relicId) return i;
        }
        return -1;
    }

    private bool EquipIntoEmptySlot(int slotIndex, RelicDefinition relic, int level)
    {
        if (!IsValidSlot(slotIndex)) return false;
        if (relic == null) return false;
        if (slots[slotIndex].def != null) return false;

        var e = slots[slotIndex];
        e.def = relic;
        e.level = relic.ClampLevel(level);

        var token = ScriptableObject.CreateInstance<RelicRuntimeToken>();
        e.token = token;

        var ctx = baseCtx;
        ctx.relicDef = relic;
        ctx.level = e.level;
        ctx.token = token;
        relic.logic?.OnEquipped(ctx);

        slots[slotIndex] = e;
        RefreshDebugView();
        OnChanged?.Invoke();
        return true;
    }

    private bool ReapplyLevel(int slotIndex, int newLevel)
    {
        if (!IsValidSlot(slotIndex)) return false;

        var e = slots[slotIndex];
        var def = e.def;
        if (def == null) return false;
        if (e.token == null) return false;

        int oldLevel = Mathf.Max(1, e.level);
        newLevel = def.ClampLevel(newLevel);
        if (newLevel == oldLevel) return true;

        // token은 유지한 채로, "제거 → 새 레벨로 적용" (누수/중복 방지)
        var ctx = baseCtx;
        ctx.relicDef = def;
        ctx.token = e.token;

        ctx.level = oldLevel;
        def.logic?.OnUnequipped(ctx);

        ctx.level = newLevel;
        def.logic?.OnEquipped(ctx);

        e.level = newLevel;
        slots[slotIndex] = e;

        RefreshDebugView();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>빈 슬롯에 추가(기존 TryAdd 호환용)</summary>
    public bool TryAdd(RelicDefinition relic)
    {
        if (relic == null) return false;

        // ✅ 게임 룰(유물은 1종당 1개): TryAdd도 중복이면 강화로 처리
        if (enforceUniqueRelicId)
            return TryAcquireOrUpgrade(relic);

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

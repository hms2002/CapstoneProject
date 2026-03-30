using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>
/// 책임 : 플레이어가 장착한 유물 슬롯, 레벨, 장착/해제/강화 흐름을 관리한다.
/// 일반 플레이 중 장착은 기존 OnEquipped/OnUnequipped 경로로 처리하고,
/// 씬 복원 시에는 effect-free shell restore와 runtime hook attach 경로를 제공한다.
/// </summary>
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

            var keeper = slots[first];
            int keeperOld = Mathf.Max(1, keeper.level);
            int add = Mathf.Max(1, e.level);
            int merged = keeper.def != null ? keeper.def.ClampLevel(keeperOld + add) : keeperOld + add;

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

        return true;
    }

    public bool TrySetRelicSlot(int slotIndex, RelicDefinition relic)
    {
        if (!IsValidSlot(slotIndex)) return false;
        if (!CanPlaceRelicInSlot(slotIndex, relic, ignoreIndex: slotIndex)) return false;

        if (relic != null && enforceUniqueRelicId && !string.IsNullOrEmpty(relic.relicId))
        {
            int existing = FindSlotByRelicId(relic.relicId);
            if (existing >= 0 && existing != slotIndex)
            {
                // 책임 : 동일 유물을 다른 슬롯에 배치하려 할 때,
                // 이미 보유 중인 유물의 강화 가능 여부를 판정하고 강화 또는 획득 거부를 처리한다.
                int gain = relic.dropLevel > 0 ? relic.dropLevel : 1;
                var eExist = slots[existing];
                int oldLevel = Mathf.Max(1, eExist.level);
                int newLevel = relic.ClampLevel(oldLevel + gain);

                // 책임 : 이미 최대 레벨이면 중복 획득을 실패 처리한다.
                if (newLevel == oldLevel) return false;

                return ReapplyLevel(existing, newLevel);
            }
        }

        var e = slots[slotIndex];
        var prevDef = e.def;
        var prevToken = e.token;
        var prevLevel = e.level;

        if (prevDef != null)
        {
            var ctx = baseCtx;
            ctx.relicDef = prevDef;
            ctx.level = prevLevel;
            ctx.token = prevToken;
            prevDef.logic?.OnUnequipped(ctx);

            if (prevToken != null) Destroy(prevToken);
        }

        e.def = relic;
        e.token = null;
        e.level = 0;

        if (relic != null)
        {
            var token = ScriptableObject.CreateInstance<RelicRuntimeToken>();
            e.token = token;

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

    public bool TrySetRelicSlotWithLevel(int slotIndex, RelicDefinition relic, int levelOverride)
    {
        if (!IsValidSlot(slotIndex)) return false;
        if (!CanPlaceRelicInSlot(slotIndex, relic, ignoreIndex: slotIndex)) return false;

        if (relic == null)
            return TrySetRelicSlot(slotIndex, null);

        int incomingLevel = Mathf.Max(1, levelOverride > 0 ? levelOverride : (relic.dropLevel > 0 ? relic.dropLevel : 1));
        incomingLevel = relic.ClampLevel(incomingLevel);

        if (enforceUniqueRelicId && !string.IsNullOrEmpty(relic.relicId))
        {
            int existing = FindSlotByRelicId(relic.relicId);
            if (existing >= 0 && existing != slotIndex)
            {
                // 책임 : 레벨이 지정된 유물 드롭/이동 시
                // 동일 유물의 강화 합산 또는 획득 거부를 처리한다.
                var eExist = slots[existing];
                int oldLevel = Mathf.Max(1, eExist.level);
                int newLevel = relic.ClampLevel(oldLevel + incomingLevel);

                // 책임 : 이미 최대 레벨이면 드롭/빠른이동을 실패 처리한다.
                if (newLevel == oldLevel) return false;

                return ReapplyLevel(existing, newLevel);
            }
        }

        var e = slots[slotIndex];
        var prevDef = e.def;
        var prevToken = e.token;
        var prevLevel = e.level;

        if (prevDef != null)
        {
            var ctx = baseCtx;
            ctx.relicDef = prevDef;
            ctx.level = prevLevel;
            ctx.token = prevToken;
            prevDef.logic?.OnUnequipped(ctx);

            if (prevToken != null) Destroy(prevToken);
        }

        e.def = relic;
        e.token = null;
        e.level = 0;

        var token = ScriptableObject.CreateInstance<RelicRuntimeToken>();
        e.token = token;

        e.level = incomingLevel;

        var ctx2 = baseCtx;
        ctx2.relicDef = relic;
        ctx2.level = e.level;
        ctx2.token = token;
        relic.logic?.OnEquipped(ctx2);

        slots[slotIndex] = e;

        RefreshDebugView();
        OnChanged?.Invoke();
        return true;
    }

    public bool TrySwapRelicSlots(int a, int b)
    {
        if (!IsValidSlot(a) || !IsValidSlot(b)) return false;
        if (a == b) return true;

        (slots[a], slots[b]) = (slots[b], slots[a]);

        RefreshDebugView();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// ✅ 유물 획득/추가용: 같은 relicId가 이미 있으면 강화 레벨을 합산하고,
    /// 없으면 빈 슬롯에 새로 장착합니다.
    /// gainedLevel을 지정하지 않으면 RelicDefinition.dropLevel(기본 1)을 사용합니다.
    /// </summary>
    public bool TryAcquireOrUpgrade(RelicDefinition relic, int gainedLevel = -1)
    {
        if (relic == null) return false;

        int gain = gainedLevel > 0 ? gainedLevel : (relic.dropLevel > 0 ? relic.dropLevel : 1);

        int idx = FindSlotByRelicId(relic.relicId);
        if (idx >= 0)
        {
            // 책임 : 유물 획득 시 이미 보유 중인 동일 유물이 있으면
            // 강화 가능 여부를 검사하고 강화 또는 획득 실패를 결정한다.
            var e = slots[idx];
            int oldLevel = Mathf.Max(1, e.level);
            int newLevel = relic.ClampLevel(oldLevel + gain);

            // 책임 : 이미 최대 레벨이면 추가 획득을 실패 처리한다.
            if (newLevel == oldLevel) return false;

            return ReapplyLevel(idx, newLevel);
        }

        int empty = FindFirstEmptySlot();
        if (empty < 0) return false;

        int initial = relic.ClampLevel(gain);
        return EquipIntoEmptySlot(empty, relic, initial);
    }

    /// <summary>
    /// 책임 : 씬 복원 시 유물 슬롯과 레벨 정보만 effect 없이 복원한다.
    /// OnEquipped는 호출하지 않으며, token도 아직 만들지 않는다.
    /// </summary>
    public void RestoreShellState(
        RelicInventoryState state,
        Func<string, RelicDefinition> relicResolver)
    {
        if (state == null)
            return;

        if (relicResolver == null)
        {
            Debug.LogError("[RelicInventory] relicResolver가 null입니다.");
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].def = null;
            slots[i].level = 0;
            slots[i].token = null;
        }

        if (state.slots != null)
        {
            int copyCount = Mathf.Min(slots.Length, state.slots.Length);
            for (int i = 0; i < copyCount; i++)
            {
                var src = state.slots[i];
                if (src == null || string.IsNullOrEmpty(src.relicId))
                    continue;

                var def = relicResolver(src.relicId);
                if (def == null)
                    continue;

                slots[i].def = def;
                slots[i].level = def.ClampLevel(Mathf.Max(1, src.level));
                slots[i].token = null;
            }
        }

        RefreshDebugView();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// 책임 : 껍데기 복원 후 각 유물에 새 runtime token을 부여하고,
    /// 복원용 runtime hook과 복원 후에도 반드시 살아 있어야 하는 상태 표식을 다시 연결한다.
    /// 이 단계는 explicit tag/GAS 복원 이후 호출되어, 복원 전용 태그가 다시 지워지지 않도록 한다.
    /// </summary>
    public void AttachRuntimeHooksForRestore()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var e = slots[i];
            if (e.def == null)
                continue;

            if (e.token != null)
                continue;

            var token = ScriptableObject.CreateInstance<RelicRuntimeToken>();
            e.token = token;
            slots[i] = e;

            var ctx = baseCtx;
            ctx.relicDef = e.def;
            ctx.level = Mathf.Max(1, e.level);
            ctx.token = token;

            e.def.logic?.OnRestoreAttached(ctx);
        }

        RefreshDebugView();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// 책임 : 특정 슬롯 유물의 현재 복원용 런타임 컨텍스트를 외부에 제공한다.
    /// 장비별 런타임 상태 복원 단계에서 새 token/level/context를 얻는 공식 창구로 사용한다.
    /// </summary>
    public bool TryGetRuntimeContextForSlot(int slotIndex, out RelicContext ctx)
    {
        ctx = default;

        if (!IsValidSlot(slotIndex))
            return false;

        var e = slots[slotIndex];
        if (e.def == null || e.token == null)
            return false;

        ctx = baseCtx;
        ctx.relicDef = e.def;
        ctx.level = Mathf.Max(1, e.level);
        ctx.token = e.token;
        return true;
    }

    /// <summary>
    /// 책임 : 현재 유물 장착 상태를 저장용 DTO로 캡처한다.
    /// 씬 이동 직전 유물 배치/레벨 저장의 공식 창구다.
    /// </summary>
    public RelicInventoryState CaptureInventoryState()
    {
        var state = new RelicInventoryState
        {
            slots = new RelicSlotState[slots.Length]
        };

        for (int i = 0; i < slots.Length; i++)
        {
            state.slots[i] = new RelicSlotState
            {
                relicId = slots[i].def != null ? slots[i].def.relicId : null,
                level = slots[i].def != null ? Mathf.Max(1, slots[i].level) : 0
            };
        }

        return state;
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

        if (enforceUniqueRelicId)
            return TryAcquireOrUpgrade(relic);

        int empty = FindFirstEmptySlot();
        if (empty < 0) return false;

        return TrySetRelicSlot(empty, relic);
    }

    public bool RemoveAt(int index) => TrySetRelicSlot(index, null);

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

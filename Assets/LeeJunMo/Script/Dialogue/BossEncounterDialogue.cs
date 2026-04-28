using System;
using System.Collections.Generic;
using UnityGAS;
using UnityEngine;
using Object = UnityEngine.Object;

public enum BossEncounterDialogueConditionType
{
    Always,
    EncounterCount,
    HasMetBoss,
    BossVictoryCount,
    BossDefeatCount,
    Affection,
    RunRemainingSeconds,
    RunRemainingRatio01,
    RunElapsedSeconds,
    PlayerHealth,
    PlayerHealthRatio01,
    ClearCount,
    MagicStone,
    LastRunEndReason,
    PlayerHasWeapon,
    PlayerHasRelic,
    PlayerHasUnlockedWeapon,
    PlayerHasUnlockedRelic,
    PlayerWeaponCount,
    PlayerRelicCount,
    BackpackItemCount,
    BackpackIsFull
}

public enum BossDialogueComparison
{
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    BetweenInclusive
}

[Serializable]
public sealed class BossEncounterDialogueEntry
{
    [Tooltip("조우 대사 룰을 구분하기 위한 에디터용 메모입니다. 선택 로직에는 영향을 주지 않습니다.")]
    [SerializeField] private string label;

    [Tooltip("NPCData의 Boss Encounter Ink 안에서 시작할 knot 또는 stitch 경로입니다. 비워두면 Ink 처음부터 시작합니다.")]
    [SerializeField] private string startPath;

    [SerializeField, HideInInspector] private TextAsset inkJSON;

    [Tooltip("여러 조우 대사 룰이 동시에 만족될 때 더 높은 우선순위의 룰이 선택됩니다.")]
    [SerializeField] private int priority;

    [Tooltip("이 룰이 선택되기 위해 만족해야 하는 조건 목록입니다. 비어 있으면 항상 참으로 처리됩니다.")]
    [SerializeField] private List<BossEncounterDialogueCondition> conditions = new List<BossEncounterDialogueCondition>();

    public string Label => label;
    public string StartPath => startPath;
    public TextAsset InkOverride => inkJSON;
    public int Priority => priority;

    public bool IsMatch(BossEncounterDialogueContext context)
    {
        if (context == null)
            return false;

        if (conditions == null || conditions.Count == 0)
            return true;

        for (int i = 0; i < conditions.Count; i++)
        {
            BossEncounterDialogueCondition condition = conditions[i];
            if (condition != null && !condition.Evaluate(context))
                return false;
        }

        return true;
    }
}

[Serializable]
public sealed class BossEncounterDialogueCondition
{
    [Tooltip("이 조건이 검사할 런타임 값을 선택합니다.")]
    [SerializeField] private BossEncounterDialogueConditionType type = BossEncounterDialogueConditionType.Always;

    [Tooltip("현재 런타임 값을 설정한 값과 어떤 방식으로 비교할지 정합니다.")]
    [SerializeField] private BossDialogueComparison comparison = BossDialogueComparison.GreaterOrEqual;

    [Tooltip("HasMetBoss, BackpackIsFull 같은 bool 조건에서 기대하는 true/false 값입니다.")]
    [SerializeField] private bool expectedBool = true;

    [Tooltip("정수 비교에 사용할 기준값입니다.")]
    [SerializeField] private int intValue;

    [Tooltip("BetweenInclusive 정수 비교에 사용할 포함 최소값입니다.")]
    [SerializeField] private int minIntValue;

    [Tooltip("BetweenInclusive 정수 비교에 사용할 포함 최대값입니다.")]
    [SerializeField] private int maxIntValue;

    [Tooltip("실수 비교에 사용할 기준값입니다. 비율 조건은 0~1 값을 사용합니다.")]
    [SerializeField] private float floatValue;

    [Tooltip("BetweenInclusive 실수 비교에 사용할 포함 최소값입니다.")]
    [SerializeField] private float minFloatValue;

    [Tooltip("BetweenInclusive 실수 비교에 사용할 포함 최대값입니다.")]
    [SerializeField] private float maxFloatValue;

    [Tooltip("아이템 조건에서 사용할 문자열 ID입니다. 예: weaponId 또는 relicId.")]
    [SerializeField] private string stringValue;

    [Tooltip("보스/호감도 조건에서 참조할 NPC 대상입니다. 비워두면 현재 NPCData를 기준으로 검사합니다.")]
    [SerializeField] private NPCData targetNpcOverride;

    [Tooltip("플레이어에게서 읽을 AttributeDefinition입니다. 체력 조건에서는 보통 HealthAttribute를 사용합니다.")]
    [SerializeField] private AttributeDefinition attribute;

    [Tooltip("비율 조건에서 최대값으로 사용할 AttributeDefinition입니다. 보통 MaxHealthAttribute를 사용합니다.")]
    [SerializeField] private AttributeDefinition maxAttribute;

    [Tooltip("LastRunEndReason 조건에서 비교할 런 종료 사유입니다.")]
    [SerializeField] private RunEndReason runEndReason;

    [Tooltip("활성화하면 이 조건의 최종 결과를 반전합니다.")]
    [SerializeField] private bool invert;

    public bool Evaluate(BossEncounterDialogueContext context)
    {
        if (context == null)
            return false;

        bool result = EvaluateInternal(context);
        return invert ? !result : result;
    }

    private bool EvaluateInternal(BossEncounterDialogueContext context)
    {
        int targetNpcId = ResolveTargetNpcId(context);

        switch (type)
        {
            case BossEncounterDialogueConditionType.Always:
                return true;
            case BossEncounterDialogueConditionType.EncounterCount:
                return CompareInt(context.GetEncounterCount(targetNpcId));
            case BossEncounterDialogueConditionType.HasMetBoss:
                return CompareBool(context.GetEncounterCount(targetNpcId) > 0);
            case BossEncounterDialogueConditionType.BossVictoryCount:
                return CompareInt(context.GetVictoryCount(targetNpcId));
            case BossEncounterDialogueConditionType.BossDefeatCount:
                return CompareInt(context.GetDefeatCount(targetNpcId));
            case BossEncounterDialogueConditionType.Affection:
                return CompareInt(context.GetAffection(targetNpcId));
            case BossEncounterDialogueConditionType.RunRemainingSeconds:
                return CompareFloat(context.RunRemainingSeconds);
            case BossEncounterDialogueConditionType.RunRemainingRatio01:
                return CompareFloat(context.RunRemainingRatio01);
            case BossEncounterDialogueConditionType.RunElapsedSeconds:
                return CompareFloat(context.RunElapsedSeconds);
            case BossEncounterDialogueConditionType.PlayerHealth:
                return CompareFloat(context.GetPlayerAttributeValue(attribute));
            case BossEncounterDialogueConditionType.PlayerHealthRatio01:
                return CompareFloat(context.GetPlayerAttributeRatio01(attribute, maxAttribute));
            case BossEncounterDialogueConditionType.ClearCount:
                return CompareInt(context.ClearCount);
            case BossEncounterDialogueConditionType.MagicStone:
                return CompareInt(context.MagicStone);
            case BossEncounterDialogueConditionType.LastRunEndReason:
                return CompareBool(context.LastRunEndReason == runEndReason);
            case BossEncounterDialogueConditionType.PlayerHasWeapon:
                return CompareBool(context.PlayerHasWeapon(stringValue));
            case BossEncounterDialogueConditionType.PlayerHasRelic:
                return CompareBool(context.PlayerHasRelic(stringValue));
            case BossEncounterDialogueConditionType.PlayerHasUnlockedWeapon:
                return CompareBool(context.PlayerHasUnlockedWeapon(stringValue));
            case BossEncounterDialogueConditionType.PlayerHasUnlockedRelic:
                return CompareBool(context.PlayerHasUnlockedRelic(stringValue));
            case BossEncounterDialogueConditionType.PlayerWeaponCount:
                return CompareInt(context.PlayerWeaponCount);
            case BossEncounterDialogueConditionType.PlayerRelicCount:
                return CompareInt(context.PlayerRelicCount);
            case BossEncounterDialogueConditionType.BackpackItemCount:
                return CompareInt(context.BackpackItemCount);
            case BossEncounterDialogueConditionType.BackpackIsFull:
                return CompareBool(context.IsBackpackFull);
            default:
                return false;
        }
    }

    private int ResolveTargetNpcId(BossEncounterDialogueContext context)
    {
        return targetNpcOverride != null ? targetNpcOverride.id : context.OwnerNpcId;
    }

    private bool CompareBool(bool current)
    {
        return current == expectedBool;
    }

    private bool CompareInt(int current)
    {
        switch (comparison)
        {
            case BossDialogueComparison.Equal:
                return current == intValue;
            case BossDialogueComparison.NotEqual:
                return current != intValue;
            case BossDialogueComparison.Less:
                return current < intValue;
            case BossDialogueComparison.LessOrEqual:
                return current <= intValue;
            case BossDialogueComparison.Greater:
                return current > intValue;
            case BossDialogueComparison.GreaterOrEqual:
                return current >= intValue;
            case BossDialogueComparison.BetweenInclusive:
                return current >= minIntValue && current <= maxIntValue;
            default:
                return false;
        }
    }

    private bool CompareFloat(float current)
    {
        switch (comparison)
        {
            case BossDialogueComparison.Equal:
                return Mathf.Approximately(current, floatValue);
            case BossDialogueComparison.NotEqual:
                return !Mathf.Approximately(current, floatValue);
            case BossDialogueComparison.Less:
                return current < floatValue;
            case BossDialogueComparison.LessOrEqual:
                return current <= floatValue;
            case BossDialogueComparison.Greater:
                return current > floatValue;
            case BossDialogueComparison.GreaterOrEqual:
                return current >= floatValue;
            case BossDialogueComparison.BetweenInclusive:
                return current >= minFloatValue && current <= maxFloatValue;
            default:
                return false;
        }
    }
}

public sealed class BossEncounterDialogueContext
{
    private readonly NPCData ownerNpc;
    private readonly GameData gameData;
    private readonly GamePlayData gameplayData;

    private bool weaponInventoryResolved;
    private WeaponInventory2D weaponInventory;
    private bool relicInventoryResolved;
    private RelicInventory relicInventory;
    private bool backpackResolved;
    private PlayerBackpackInventory backpackInventory;
    private bool attributeSetResolved;
    private AttributeSet playerAttributeSet;

    public BossEncounterDialogueContext(NPCData ownerNpc)
    {
        this.ownerNpc = ownerNpc;
        gameData = GameDataManager.Instance != null ? GameDataManager.Instance.Data : null;
        gameplayData = GamePlayDataManager.Instance != null ? GamePlayDataManager.Instance.Data : null;
    }

    public int OwnerNpcId => ownerNpc != null ? ownerNpc.id : 0;

    public float RunRemainingSeconds
    {
        get
        {
            if (RunTimeLimitSystem.Instance != null)
                return Mathf.Max(0f, RunTimeLimitSystem.Instance.RemainingSeconds);

            return GamePlayDataManager.Instance != null
                ? GamePlayDataManager.Instance.GetRunRemainingSeconds()
                : 0f;
        }
    }

    public float RunRemainingRatio01
    {
        get
        {
            float initialSeconds = RunTimeLimitSystem.Instance != null
                ? RunTimeLimitSystem.Instance.InactivePreviewSeconds
                : 0f;

            if (initialSeconds <= 0f)
                return 0f;

            return Mathf.Clamp01(RunRemainingSeconds / initialSeconds);
        }
    }

    public float RunElapsedSeconds => gameplayData != null ? Mathf.Max(0f, gameplayData.runElapsedSeconds) : 0f;
    public int ClearCount => gameData != null ? Mathf.Max(0, gameData.clearCount) : 0;
    public int MagicStone
    {
        get
        {
            int amount = gameData != null ? gameData.magicStone : 0;
            if (GamePlayDataManager.Instance != null)
                amount += GamePlayDataManager.Instance.GetPendingRunMagicStoneDelta();

            return amount;
        }
    }

    public RunEndReason LastRunEndReason => gameplayData != null
        ? gameplayData.lastRunEndReason
        : RunEndReason.None;

    public int PlayerWeaponCount
    {
        get
        {
            WeaponInventory2D inventory = ResolveWeaponInventory();
            if (inventory == null)
                return 0;

            int count = 0;
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                if (inventory.GetWeaponInSlot(i) != null)
                    count++;
            }

            return count;
        }
    }

    public int PlayerRelicCount
    {
        get
        {
            RelicInventory inventory = ResolveRelicInventory();
            return inventory != null ? Mathf.Max(0, inventory.Count) : 0;
        }
    }

    public int BackpackItemCount
    {
        get
        {
            PlayerBackpackInventory backpack = ResolveBackpackInventory();
            if (backpack == null)
                return 0;

            int count = 0;
            for (int i = 0; i < backpack.Capacity; i++)
            {
                if (backpack.Get(i) != null)
                    count++;
            }

            return count;
        }
    }

    public bool IsBackpackFull
    {
        get
        {
            PlayerBackpackInventory backpack = ResolveBackpackInventory();
            return backpack != null && backpack.Capacity > 0 && BackpackItemCount >= backpack.Capacity;
        }
    }

    public int GetEncounterCount(int npcId)
    {
        return BossDialogueProgressStore.GetEncounterCount(npcId);
    }

    public int GetVictoryCount(int npcId)
    {
        return BossDialogueProgressStore.GetVictoryCount(npcId);
    }

    public int GetDefeatCount(int npcId)
    {
        return BossDialogueProgressStore.GetDefeatCount(npcId);
    }

    public int GetAffection(int npcId)
    {
        int savedAmount = GetSavedAffection(npcId) + GetPendingRunAffectionDelta(npcId);
        if (AffectionManager.Instance == null)
            return savedAmount;

        int runtimeAmount = AffectionManager.Instance.GetAffection(npcId);
        return runtimeAmount != 0 || savedAmount == 0 ? runtimeAmount : savedAmount;
    }

    public bool PlayerHasWeapon(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId))
            return false;

        WeaponInventory2D inventory = ResolveWeaponInventory();
        if (inventory != null)
        {
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                WeaponDefinition weapon = inventory.GetWeaponInSlot(i);
                if (weapon != null && string.Equals(weapon.weaponId, weaponId, StringComparison.Ordinal))
                    return true;
            }
        }

        PlayerBackpackInventory backpack = ResolveBackpackInventory();
        if (backpack == null)
            return false;

        for (int i = 0; i < backpack.Capacity; i++)
        {
            if (backpack.Get(i) is WeaponDefinition weapon
                && string.Equals(weapon.weaponId, weaponId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public float GetPlayerAttributeValue(AttributeDefinition attribute)
    {
        AttributeSet attributeSet = ResolvePlayerAttributeSet();
        return attributeSet != null && attribute != null
            ? attributeSet.GetAttributeValue(attribute)
            : 0f;
    }

    public float GetPlayerAttributeRatio01(AttributeDefinition attribute, AttributeDefinition maxAttribute)
    {
        float maxValue = GetPlayerAttributeValue(maxAttribute);
        if (maxValue <= 0f)
            return 0f;

        return Mathf.Clamp01(GetPlayerAttributeValue(attribute) / maxValue);
    }

    public bool PlayerHasRelic(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId))
            return false;

        RelicInventory inventory = ResolveRelicInventory();
        if (inventory != null)
        {
            for (int i = 0; i < inventory.Capacity; i++)
            {
                RelicDefinition relic = inventory.GetRelicInSlot(i);
                if (relic != null && string.Equals(relic.relicId, relicId, StringComparison.Ordinal))
                    return true;
            }
        }

        PlayerBackpackInventory backpack = ResolveBackpackInventory();
        if (backpack == null)
            return false;

        for (int i = 0; i < backpack.Capacity; i++)
        {
            if (backpack.Get(i) is RelicDefinition relic
                && string.Equals(relic.relicId, relicId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool PlayerHasUnlockedWeapon(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId))
            return false;

        if (ItemManager.Instance != null && ItemManager.Instance.IsWeaponUnlocked(weaponId))
            return true;

        return gameData != null
            && gameData.itemData != null
            && gameData.itemData.unlockedWeaponIDs != null
            && gameData.itemData.unlockedWeaponIDs.Contains(weaponId);
    }

    public bool PlayerHasUnlockedRelic(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId))
            return false;

        if (ItemManager.Instance != null && ItemManager.Instance.IsRelicUnlocked(relicId))
            return true;

        return gameData != null
            && gameData.itemData != null
            && gameData.itemData.unlockedRelicIDs != null
            && gameData.itemData.unlockedRelicIDs.Contains(relicId);
    }

    private WeaponInventory2D ResolveWeaponInventory()
    {
        if (weaponInventoryResolved)
            return weaponInventory;

        weaponInventoryResolved = true;
        weaponInventory = ResolvePlayerComponent<WeaponInventory2D>();
        return weaponInventory;
    }

    private RelicInventory ResolveRelicInventory()
    {
        if (relicInventoryResolved)
            return relicInventory;

        relicInventoryResolved = true;
        relicInventory = ResolvePlayerComponent<RelicInventory>();
        return relicInventory;
    }

    private PlayerBackpackInventory ResolveBackpackInventory()
    {
        if (backpackResolved)
            return backpackInventory;

        backpackResolved = true;
        backpackInventory = ResolvePlayerComponent<PlayerBackpackInventory>();
        return backpackInventory;
    }

    private AttributeSet ResolvePlayerAttributeSet()
    {
        if (attributeSetResolved)
            return playerAttributeSet;

        attributeSetResolved = true;
        playerAttributeSet = ResolvePlayerComponent<AttributeSet>();
        return playerAttributeSet;
    }

    private int GetSavedAffection(int npcId)
    {
        if (gameData == null || gameData.affectionData == null || gameData.affectionData.affectionRecords == null)
            return 0;

        AffectionRecord record = gameData.affectionData.affectionRecords.Find(x => x != null && x.npcId == npcId);
        return record != null ? record.amount : 0;
    }

    private int GetPendingRunAffectionDelta(int npcId)
    {
        if (gameplayData == null || gameplayData.pendingRunAffectionChanges == null)
            return 0;

        int delta = 0;
        for (int i = 0; i < gameplayData.pendingRunAffectionChanges.Count; i++)
        {
            PendingRunAffectionChange change = gameplayData.pendingRunAffectionChanges[i];
            if (change != null && change.npcId == npcId)
                delta += change.delta;
        }

        return delta;
    }

    private static T ResolvePlayerComponent<T>() where T : Component
    {
        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform != null)
        {
            T component = playerTransform.GetComponent<T>();
            if (component != null)
                return component;

            component = playerTransform.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }
}

public static class BossEncounterDialogueSelector
{
    public static bool TrySelect(NPCData npcData, out BossEncounterDialogueEntry selectedEntry)
    {
        selectedEntry = null;

        if (npcData == null)
            return false;

        IReadOnlyList<BossEncounterDialogueEntry> entries = npcData.BossEncounterDialogues;
        if (entries == null || entries.Count == 0)
            return false;

        BossEncounterDialogueContext context = new BossEncounterDialogueContext(npcData);
        int selectedPriority = int.MinValue;

        for (int i = 0; i < entries.Count; i++)
        {
            BossEncounterDialogueEntry entry = entries[i];
            if (entry == null)
                continue;

            if (!entry.IsMatch(context))
                continue;

            if (selectedEntry != null && entry.Priority <= selectedPriority)
                continue;

            selectedEntry = entry;
            selectedPriority = entry.Priority;
        }

        return selectedEntry != null;
    }
}

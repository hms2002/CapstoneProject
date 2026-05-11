using System.Collections.Generic;
using UnityEngine;

public sealed class MerchantNPC : MonoBehaviour
{
    [Header("Data (Stable ID)")]
    [SerializeField] private string merchantId;

    [Header("Shop Setup")]
    [SerializeField] private ShopDefinitionSO shopDefinition;
    [SerializeField] private ShopSlot[] shopSlots;

    [Header("Presentation")]
    [SerializeField] private GameObject[] activationTargets;

    [Header("Legacy Shop Setup (Unused)")]
#pragma warning disable 0414
    [SerializeField, HideInInspector] private ShopStockRollWeights stockRollWeights = new ShopStockRollWeights
    {
        weaponWeight = 1,
        relicWeight = 1,
        consumableWeight = 1
    };
    [SerializeField, HideInInspector] private MerchantPriceSettings priceSettings = new MerchantPriceSettings
    {
        weaponPrice = 120,
        commonRelicPrice = 100,
        rareRelicPrice = 180,
        epicRelicPrice = 260,
        consumablePrice = 40
    };
    [SerializeField, HideInInspector] private bool requireShopUpgrade;
    [SerializeField, HideInInspector, Min(0)] private int baseVisibleSlotCount;
#pragma warning restore 0414

    [Header("Speech Bubble")]
    [SerializeField] private SpeechBubbleComponent speechBubble;
    [SerializeField] private string notEnoughCurrencySpeech = "마정석이 부족하군.";
    [SerializeField] private string inventoryFullSpeech = "가방이 가득 찼군.";

    private readonly MerchantRunStateService runStateService = new MerchantRunStateService();
    private readonly ShopInventoryRoll inventoryRoll = new ShopInventoryRoll();
    private readonly MerchantPurchaseService purchaseService = new MerchantPurchaseService();

    private MerchantRuntimeState runtimeState;
    private MerchantRefreshInteractable[] refreshInteractables;
    private RunModifierService subscribedRunModifierService;
    private bool hasLoggedMissingDefinition;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(merchantId) && !UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null)
                    return;

                GenerateID();
                CollectSlotsFromChildren();
                CollectRefreshInteractablesFromChildren();
            };
            return;
        }

        CollectSlotsFromChildren();
        CollectRefreshInteractablesFromChildren();
    }

    public void GenerateID()
    {
        string cleanName = name.Replace("(Clone)", string.Empty).Trim();
        string guid = System.Guid.NewGuid().ToString().Substring(0, 8);
        merchantId = $"{cleanName}_{guid}";
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void Reset()
    {
        CollectSlotsFromChildren();
        CollectRefreshInteractablesFromChildren();
    }

    private void Awake()
    {
        if (speechBubble == null)
            speechBubble = GetComponent<SpeechBubbleComponent>();

        CollectSlotsFromChildren();
        CollectRefreshInteractablesFromChildren();
        BindSlots();
        BindRefreshInteractables();
        RefreshRefreshInteractables();
    }

    private void OnEnable()
    {
        SubscribeToRunModifierService();
        RefreshRefreshInteractables();
    }

    private void OnDisable()
    {
        UnsubscribeFromRunModifierService();
    }

    private void Start()
    {
        SubscribeToRunModifierService();
        InitializeStock();
    }

    private void Update()
    {
        if (subscribedRunModifierService == null && SubscribeToRunModifierService())
            InitializeStock();
    }

    public void TryPurchase(int slotIndex, IPlayerInteractor player)
    {
        MerchantShopPolicySnapshot policy = ResolveShopPolicy();
        if (!policy.IsAvailable || slotIndex < 0 || slotIndex >= policy.VisibleSlotCount)
            return;

        if (!TryGetSlotEntry(slotIndex, out MerchantStockEntryState slotEntry, out ScriptableObject itemDefinition))
            return;

        MerchantPurchaseResult result = purchaseService.TryPurchase(player, slotEntry, itemDefinition);
        if (result.Succeeded)
        {
            runStateService.MarkSlotSold(runtimeState, slotIndex);
            RefreshSlot(slotIndex);
            return;
        }

        ShowPurchaseWarning(result.Type);
        SpeakFailure(result.Type);
    }

    private void InitializeStock()
    {
        MerchantShopPolicySnapshot policy = ResolveShopPolicy();
        if (!policy.HasDefinition)
        {
            WarnMissingDefinitionOnce();
            ApplyShopAvailability(false, 0);
            runtimeState = null;
            RefreshRefreshInteractables();
            return;
        }

        ApplyShopAvailability(policy.IsAvailable, policy.VisibleSlotCount);
        if (!policy.IsAvailable)
        {
            runtimeState = null;
            RefreshRefreshInteractables();
            return;
        }

        runtimeState = runStateService.GetOrCreateState(
            merchantId,
            policy.VisibleSlotCount,
            (slotCount, excludedEntries) => RollStock(slotCount, policy.EffectivePriceSettings, excludedEntries));
        ApplyEffectivePrices(runtimeState, policy.EffectivePriceSettings);

        BindSlots();
        RefreshAllSlots();
        RefreshRefreshInteractables();
    }

    public bool CanRefreshStock()
    {
        MerchantShopPolicySnapshot policy = ResolveShopPolicy();
        return policy.HasDefinition &&
               policy.IsAvailable &&
               runtimeState != null &&
               runtimeState.refreshCountUsed < policy.RefreshLimit;
    }

    public bool CanShowRefreshInteractable()
    {
        MerchantShopPolicySnapshot policy = ResolveShopPolicy();
        return policy.HasDefinition &&
               policy.IsAvailable &&
               policy.RefreshLimit > 0;
    }

    public int GetRemainingRefreshCount()
    {
        MerchantShopPolicySnapshot policy = ResolveShopPolicy();
        if (!policy.HasDefinition || !policy.IsAvailable || policy.RefreshLimit <= 0)
            return 0;

        int usedCount = runtimeState != null ? runtimeState.refreshCountUsed : 0;
        return Mathf.Max(0, policy.RefreshLimit - usedCount);
    }

    public bool TryRefreshStock()
    {
        MerchantShopPolicySnapshot policy = ResolveShopPolicy();
        if (!policy.HasDefinition)
        {
            WarnMissingDefinitionOnce();
            ApplyShopAvailability(false, 0);
            runtimeState = null;
            RefreshRefreshInteractables();
            return false;
        }

        if (!policy.IsAvailable)
        {
            ApplyShopAvailability(false, 0);
            RefreshRefreshInteractables();
            return false;
        }

        if (runtimeState == null)
            InitializeStock();

        if (runtimeState == null)
            return false;

        bool refreshed = runStateService.TryRefreshState(
            runtimeState,
            policy.RefreshLimit,
            policy.VisibleSlotCount,
            (slotCount, excludedEntries) => RollStock(slotCount, policy.EffectivePriceSettings, excludedEntries));

        if (!refreshed)
            return false;

        ApplyShopAvailability(true, policy.VisibleSlotCount);
        ApplyEffectivePrices(runtimeState, policy.EffectivePriceSettings);
        BindSlots();
        RefreshAllSlots();
        RefreshRefreshInteractables();
        return true;
    }

    private void HandleRunModifiersChanged()
    {
        InitializeStock();
    }

    private bool SubscribeToRunModifierService()
    {
        if (subscribedRunModifierService != null)
            return false;

        if (RunModifierService.Instance == null)
            return false;

        subscribedRunModifierService = RunModifierService.Instance;
        subscribedRunModifierService.OnModifiersChanged += HandleRunModifiersChanged;
        return true;
    }

    private void UnsubscribeFromRunModifierService()
    {
        if (subscribedRunModifierService != null)
            subscribedRunModifierService.OnModifiersChanged -= HandleRunModifiersChanged;

        subscribedRunModifierService = null;
    }

    private bool TryGetSlotEntry(
        int slotIndex,
        out MerchantStockEntryState slotEntry,
        out ScriptableObject itemDefinition)
    {
        slotEntry = null;
        itemDefinition = null;

        if (runtimeState?.slots == null || slotIndex < 0 || slotIndex >= runtimeState.slots.Count)
            return false;

        slotEntry = runtimeState.slots[slotIndex];
        itemDefinition = slotEntry != null ? slotEntry.ResolveDefinition() : null;
        return slotEntry != null;
    }

    private void RefreshAllSlots()
    {
        if (shopSlots == null)
            return;

        for (int i = 0; i < shopSlots.Length; i++)
            RefreshSlot(i);
    }

    private static void ApplyEffectivePrices(
        MerchantRuntimeState state,
        MerchantPriceSettings effectivePriceSettings)
    {
        if (state?.slots == null)
            return;

        for (int i = 0; i < state.slots.Count; i++)
        {
            MerchantStockEntryState entry = state.slots[i];
            ScriptableObject definition = entry != null ? entry.ResolveDefinition() : null;
            if (definition == null)
                continue;

            entry.price = effectivePriceSettings.ResolvePrice(definition);
        }
    }

    private ShopRunModifierDelta ResolveShopModifiers()
    {
        return RunModifierService.Instance != null
            ? RunModifierService.Instance.ShopModifiers
            : default;
    }

    private MerchantShopPolicySnapshot ResolveShopPolicy()
    {
        int authoredSlotCount = shopSlots != null ? shopSlots.Length : 0;
        return MerchantShopPolicy.Resolve(shopDefinition, ResolveShopModifiers(), authoredSlotCount);
    }

    private List<MerchantStockEntryState> RollStock(
        int slotCount,
        MerchantPriceSettings effectivePriceSettings,
        IReadOnlyCollection<MerchantStockEntryState> excludedEntries)
    {
        return shopDefinition != null
            ? inventoryRoll.RollStock(slotCount, shopDefinition.StockRollWeights, effectivePriceSettings, excludedEntries)
            : new List<MerchantStockEntryState>();
    }

    private void ApplyShopAvailability(bool isAvailable, int activeSlotCount)
    {
        if (activationTargets != null)
        {
            for (int i = 0; i < activationTargets.Length; i++)
            {
                if (activationTargets[i] != null)
                    activationTargets[i].SetActive(isAvailable);
            }
        }

        ApplySlotVisibility(isAvailable ? activeSlotCount : 0);
    }

    private void ApplySlotVisibility(int activeSlotCount)
    {
        if (shopSlots == null)
            return;

        for (int i = 0; i < shopSlots.Length; i++)
        {
            if (shopSlots[i] != null)
                shopSlots[i].gameObject.SetActive(i < activeSlotCount);
        }
    }

    private void RefreshSlot(int slotIndex)
    {
        if (shopSlots == null || slotIndex < 0 || slotIndex >= shopSlots.Length || shopSlots[slotIndex] == null)
            return;

        MerchantStockEntryState slotState = runtimeState != null &&
                                            runtimeState.slots != null &&
                                            slotIndex < runtimeState.slots.Count
            ? runtimeState.slots[slotIndex]
            : MerchantStockEntryState.Empty();

        shopSlots[slotIndex].ApplyState(slotState);
    }

    private void BindSlots()
    {
        if (shopSlots == null)
            return;

        for (int i = 0; i < shopSlots.Length; i++)
        {
            if (shopSlots[i] != null)
                shopSlots[i].AssignOwner(this, i);
        }
    }

    private void BindRefreshInteractables()
    {
        if (refreshInteractables == null)
            return;

        for (int i = 0; i < refreshInteractables.Length; i++)
        {
            if (refreshInteractables[i] != null)
                refreshInteractables[i].AssignOwner(this);
        }
    }

    private void RefreshRefreshInteractables()
    {
        CollectRefreshInteractablesFromChildren();

        BindRefreshInteractables();

        if (refreshInteractables == null)
            return;

        for (int i = 0; i < refreshInteractables.Length; i++)
        {
            if (refreshInteractables[i] != null)
                refreshInteractables[i].RefreshPresentation();
        }
    }

    private void SpeakFailure(MerchantPurchaseResultType resultType)
    {
        if (speechBubble == null)
            return;

        string line = resultType switch
        {
            MerchantPurchaseResultType.NotEnoughCurrency => notEnoughCurrencySpeech,
            MerchantPurchaseResultType.WeaponInventoryFull => inventoryFullSpeech,
            MerchantPurchaseResultType.RelicInventoryFull => inventoryFullSpeech,
            MerchantPurchaseResultType.ConsumableInventoryFull => inventoryFullSpeech,
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(line))
            speechBubble.Speak(line);
    }

    /// <summary>
    /// 책임 :
    /// - 상점 구매 실패 사유를 공통 경고 팝업 코드로 변환해 UIManager에 전달한다.
    /// - 상점 도메인 로직이 실제 UI 문구나 팝업 구현에 직접 의존하지 않도록 분리한다.
    /// </summary>
    private static void ShowPurchaseWarning(MerchantPurchaseResultType resultType)
    {
        WarningPopupCode code = resultType switch
        {
            MerchantPurchaseResultType.WeaponInventoryFull => WarningPopupCode.WeaponInventoryFull,
            MerchantPurchaseResultType.RelicInventoryFull => WarningPopupCode.RelicInventoryFull,
            MerchantPurchaseResultType.ConsumableInventoryFull => WarningPopupCode.ConsumableInventoryFull,
            MerchantPurchaseResultType.RelicAlreadyMaxLevel => WarningPopupCode.RelicAlreadyMaxLevel,
            _ => WarningPopupCode.None
        };

        if (code != WarningPopupCode.None)
            UIManager.Instance?.ShowWarning(code);
    }

    private void CollectSlotsFromChildren()
    {
        ShopSlot[] childSlots = GetComponentsInChildren<ShopSlot>(true);
        if (childSlots != null && childSlots.Length > 0)
            shopSlots = childSlots;
    }

    private void CollectRefreshInteractablesFromChildren()
    {
        List<MerchantRefreshInteractable> collectedInteractables = new List<MerchantRefreshInteractable>();
        HashSet<MerchantRefreshInteractable> seenInteractables = new HashSet<MerchantRefreshInteractable>();

        CollectRefreshInteractablesFromRoot(transform, collectedInteractables, seenInteractables);

        if (transform.parent != null)
            CollectRefreshInteractablesFromRoot(transform.parent, collectedInteractables, seenInteractables);

        CollectExplicitlyOwnedRefreshInteractables(collectedInteractables, seenInteractables);

        refreshInteractables = collectedInteractables.ToArray();
    }

    private void CollectRefreshInteractablesFromRoot(
        Transform root,
        List<MerchantRefreshInteractable> collectedInteractables,
        HashSet<MerchantRefreshInteractable> seenInteractables)
    {
        if (root == null)
            return;

        MerchantRefreshInteractable[] candidates = root.GetComponentsInChildren<MerchantRefreshInteractable>(true);
        if (candidates == null)
            return;

        for (int i = 0; i < candidates.Length; i++)
        {
            MerchantRefreshInteractable candidate = candidates[i];
            if (candidate == null || !candidate.CanAssignOwner(this) || !seenInteractables.Add(candidate))
                continue;

            collectedInteractables.Add(candidate);
        }
    }

    private void CollectExplicitlyOwnedRefreshInteractables(
        List<MerchantRefreshInteractable> collectedInteractables,
        HashSet<MerchantRefreshInteractable> seenInteractables)
    {
        MerchantRefreshInteractable[] candidates = FindObjectsByType<MerchantRefreshInteractable>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (candidates == null)
            return;

        for (int i = 0; i < candidates.Length; i++)
        {
            MerchantRefreshInteractable candidate = candidates[i];
            if (candidate == null || !candidate.IsAssignedOwner(this) || !seenInteractables.Add(candidate))
                continue;

            collectedInteractables.Add(candidate);
        }
    }

    private void WarnMissingDefinitionOnce()
    {
        if (hasLoggedMissingDefinition)
            return;

        hasLoggedMissingDefinition = true;
        Debug.LogWarning(
            $"[MerchantNPC] ShopDefinitionSO is not assigned. Shop is disabled. merchantId={merchantId}",
            this);
    }
}

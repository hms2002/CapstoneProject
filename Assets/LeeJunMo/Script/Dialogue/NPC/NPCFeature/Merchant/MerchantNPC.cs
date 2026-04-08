using UnityEngine;

public sealed class MerchantNPC : MonoBehaviour
{
    [Header("Data (Stable ID)")]
    [SerializeField] private string merchantId;

    [Header("Shop Setup")]
    [SerializeField] private ShopSlot[] shopSlots;
    [SerializeField] private ShopStockRollWeights stockRollWeights = new ShopStockRollWeights
    {
        weaponWeight = 1,
        relicWeight = 1,
        consumableWeight = 1
    };
    [SerializeField] private MerchantPriceSettings priceSettings = new MerchantPriceSettings
    {
        weaponPrice = 120,
        commonRelicPrice = 100,
        rareRelicPrice = 180,
        epicRelicPrice = 260,
        consumablePrice = 40
    };

    [Header("Speech Bubble")]
    [SerializeField] private SpeechBubbleComponent speechBubble;
    [SerializeField] private string notEnoughCurrencySpeech = "마정석이 부족하군.";
    [SerializeField] private string inventoryFullSpeech = "가방이 가득 찼군.";

    private readonly MerchantRunStateService runStateService = new MerchantRunStateService();
    private readonly ShopInventoryRoll inventoryRoll = new ShopInventoryRoll();
    private readonly MerchantPurchaseService purchaseService = new MerchantPurchaseService();

    private MerchantRuntimeState runtimeState;

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
            };
            return;
        }

        CollectSlotsFromChildren();
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
    }

    private void Awake()
    {
        if (speechBubble == null)
            speechBubble = GetComponent<SpeechBubbleComponent>();

        CollectSlotsFromChildren();
        BindSlots();
    }

    private void Start()
    {
        InitializeStock();
    }

    public void TryPurchase(int slotIndex, IPlayerInteractor player)
    {
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
        int slotCount = shopSlots != null ? shopSlots.Length : 0;
        runtimeState = runStateService.GetOrCreateState(
            merchantId,
            slotCount,
            () => inventoryRoll.RollStock(slotCount, stockRollWeights, priceSettings));

        BindSlots();
        RefreshAllSlots();
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
}

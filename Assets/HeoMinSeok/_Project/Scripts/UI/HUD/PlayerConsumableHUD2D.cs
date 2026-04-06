using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 :
/// - 플레이어의 1회용 아이템 4칸 상태를 HUD 슬롯 UI에 반영한다.
/// - 플레이어 등록/해제와 consumable 인벤토리 변경에 반응해 아이콘 표시를 동기화한다.
/// </summary>
public class PlayerConsumableHUD2D : MonoBehaviour
{
    [System.Serializable]
    public class ConsumableSlotUI
    {
        [Tooltip("가능하면 슬롯 루트 오브젝트를 직접 연결합니다. 비어 있으면 icon 등의 부모에서 추론합니다.")]
        public GameObject root;
        public Image icon;
        public TMP_Text keyText;
    }

    [Header("Refs")]
    [SerializeField] private PlayerConsumableInventory consumableInventory;

    [Header("Visibility")]
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private bool keepSlotVisibleWhenEmpty = true;

    [Header("UI Slots")]
    [SerializeField] private ConsumableSlotUI slot1UI;
    [SerializeField] private ConsumableSlotUI slot2UI;
    [SerializeField] private ConsumableSlotUI slot3UI;
    [SerializeField] private ConsumableSlotUI slot4UI;

    private CanvasGroup hudCanvasGroup;
    private Graphic[] hudGraphics;
    private bool hasBoundInventoryEvents;

    private void Awake()
    {
        if (hudRoot == null)
            hudRoot = gameObject;

        hudCanvasGroup = hudRoot.GetComponent<CanvasGroup>();
        if (hudRoot == gameObject && hudCanvasGroup == null)
            hudGraphics = hudRoot.GetComponentsInChildren<Graphic>(includeInactive: true);

        TryResolvePlayerRefs();
        RefreshAllSlots();
        RefreshVisibility();
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;

        TryRefreshBindingAndView();
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered -= HandlePlayerUnregistered;
        UnbindInventoryEvents();
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        UnbindInventoryEvents();
        TryResolvePlayerRefs(player);
        TryRefreshBindingAndView();
    }

    private void HandlePlayerUnregistered(PlayerInteractor2D player)
    {
        if (player == null)
            return;

        if (consumableInventory != null && consumableInventory.gameObject == player.gameObject)
        {
            UnbindInventoryEvents();
            consumableInventory = null;
            RefreshAllSlots();
            RefreshVisibility();
        }
    }

    private void TryResolvePlayerRefs(PlayerInteractor2D player = null)
    {
        if (player == null)
        {
            player = PlayerRuntimeRegistry.CurrentPlayer != null
                ? PlayerRuntimeRegistry.CurrentPlayer
                : PlayerInteractor2D.Instance;
        }

        if (player != null)
            consumableInventory = player.GetComponent<PlayerConsumableInventory>();

        if (consumableInventory == null)
            consumableInventory = FindFirstObjectByType<PlayerConsumableInventory>();
    }

    private void Start()
    {
        TryRefreshBindingAndView();
    }

    private void BindInventoryEvents()
    {
        if (consumableInventory == null)
            return;

        consumableInventory.OnChanged -= RefreshAllSlots;
        consumableInventory.OnChanged += RefreshAllSlots;
        hasBoundInventoryEvents = true;
    }

    private void UnbindInventoryEvents()
    {
        if (consumableInventory == null)
            return;

        consumableInventory.OnChanged -= RefreshAllSlots;
        hasBoundInventoryEvents = false;
    }

    private void LateUpdate()
    {
        if (consumableInventory != null)
            return;

        TryRefreshBindingAndView();
    }

    /// <summary>
    /// 책임 :
    /// - HUD가 플레이어 consumable 인벤토리를 뒤늦게 획득해도 안전하게 재바인딩한다.
    /// - 씬 시작 직후 플레이어/인벤토리 생성 순서가 엇갈리는 경우에도 초기 비주얼을 맞춘다.
    /// </summary>
    private void TryRefreshBindingAndView()
    {
        if (consumableInventory == null)
            TryResolvePlayerRefs();

        if (consumableInventory != null && !hasBoundInventoryEvents)
            BindInventoryEvents();

        RefreshAllSlots();
        RefreshVisibility();
    }

    private void RefreshAllSlots()
    {
        ApplySlot(slot1UI, 0);
        ApplySlot(slot2UI, 1);
        ApplySlot(slot3UI, 2);
        ApplySlot(slot4UI, 3);
    }

    private void RefreshVisibility()
    {
        if (hudRoot == null)
            return;

        bool hasPlayerRefs = consumableInventory != null;
        if (hudRoot != gameObject)
        {
            if (hudRoot.activeSelf != hasPlayerRefs)
                hudRoot.SetActive(hasPlayerRefs);
            return;
        }

        if (hudCanvasGroup != null)
        {
            hudCanvasGroup.alpha = hasPlayerRefs ? 1f : 0f;
            hudCanvasGroup.interactable = hasPlayerRefs;
            hudCanvasGroup.blocksRaycasts = hasPlayerRefs;
            return;
        }

        if (hudGraphics == null)
            hudGraphics = hudRoot.GetComponentsInChildren<Graphic>(includeInactive: true);

        for (int i = 0; i < hudGraphics.Length; i++)
        {
            if (hudGraphics[i] == null)
                continue;

            if (hudGraphics[i].gameObject == gameObject)
                continue;

            hudGraphics[i].enabled = hasPlayerRefs;
        }
    }

    private void ApplySlot(ConsumableSlotUI ui, int slotIndex)
    {
        if (ui == null)
            return;

        ConsumableDefinition consumable =
            consumableInventory != null ? consumableInventory.GetConsumableInSlot(slotIndex) : null;

        bool hasItem = consumable != null;
        SetSlotVisible(ui, keepSlotVisibleWhenEmpty || hasItem);

        if (ui.icon != null)
        {
            if (ui.icon.gameObject.activeSelf != hasItem)
                ui.icon.gameObject.SetActive(hasItem);

            ui.icon.enabled = hasItem;
            ui.icon.sprite = hasItem ? consumable.Icon : null;
        }
    }

    /// <summary>
    /// 책임 :
    /// - 슬롯 전체 표시 여부를 제어한다.
    /// - 루트가 명시되지 않았을 때는 현재 연결된 UI 참조의 부모를 이용해 보수적으로 루트를 추론한다.
    /// </summary>
    private static void SetSlotVisible(ConsumableSlotUI ui, bool visible)
    {
        if (ui == null)
            return;

        GameObject root = ResolveSlotRoot(ui);
        if (root != null)
        {
            root.SetActive(visible);
            return;
        }

        if (ui.icon != null) ui.icon.enabled = visible;
        if (ui.keyText != null) ui.keyText.enabled = visible;
    }

    /// <summary>
    /// 책임 :
    /// - ConsumableSlotUI가 가리키는 공통 루트 오브젝트를 찾아 슬롯 단위 토글에 사용한다.
    /// - 인스펙터 루트가 없으면 icon > keyText 순으로 부모를 추론한다.
    /// </summary>
    private static GameObject ResolveSlotRoot(ConsumableSlotUI ui)
    {
        if (ui.root != null)
            return ui.root;

        if (ui.icon != null && ui.icon.transform.parent != null)
            return ui.icon.transform.parent.gameObject;

        if (ui.keyText != null && ui.keyText.transform.parent != null)
            return ui.keyText.transform.parent.gameObject;

        return null;
    }
}

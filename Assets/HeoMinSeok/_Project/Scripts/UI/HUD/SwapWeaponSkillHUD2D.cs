using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 현재 들고 있지 않은 교체 무기의 Skill1, Skill2 쿨다운/충전 상태를 HUD에 표시한다.
/// - 무기 교체 입력 글리프를 현재 키 설정과 연동해 보여준다.
/// </summary>
public sealed class SwapWeaponSkillHUD2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WeaponInventory2D inventory;
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private WeaponEquipController weaponEquipController;

    [Header("Visibility")]
    [SerializeField] private GameObject hudRoot;

    [Header("UI Slots")]
    [SerializeField] private WeaponSkillHUD2D.SkillSlotUI skill1UI;
    [SerializeField] private WeaponSkillHUD2D.SkillSlotUI skill2UI;

    [Header("Swap Key Guide")]
    [SerializeField] private GameObject swapGuideRoot;
    [SerializeField] private Image swapGuideIcon;
    [SerializeField] private TMP_Text swapGuideLabel;
    [SerializeField] private Sprite swapGuideFallbackIcon;

    [Header("Style")]
    [SerializeField] private Color normalIconColor = Color.white;

    private CanvasGroup hudCanvasGroup;
    private Graphic[] hudGraphics;
    private AbilityDefinition skill1Def;
    private AbilityDefinition skill2Def;
    private WeaponAbilitySelector abilitySelector;
    private InputBindingService cachedInputBindingService;
    private KeyCode lastSwapGuideKey = KeyCode.None;

    private void Awake()
    {
        if (hudRoot == null)
            hudRoot = gameObject;

        hudCanvasGroup = hudRoot.GetComponent<CanvasGroup>();
        if (hudRoot == gameObject && hudCanvasGroup == null)
            hudGraphics = hudRoot.GetComponentsInChildren<Graphic>(includeInactive: true);

        TryResolvePlayerRefs();
        RebuildSelector();
        RefreshAbilityRefs();
        RefreshVisibility();
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;

        TryResolvePlayerRefs();
        RebuildSelector();
        BindInventoryEvents();
        RefreshAbilityRefs();
        RefreshVisibility();
        RefreshSwapGuide(force: true);
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered -= HandlePlayerUnregistered;
        UnbindInventoryEvents();
    }

    private void Update()
    {
        if (abilitySystem == null)
            return;

        WeaponSkillHudSlotPresenter.UpdateDynamicIcon(skill1UI, WeaponAbilitySlot.Skill1, skill1Def, null);
        WeaponSkillHudSlotPresenter.UpdateDynamicIcon(skill2UI, WeaponAbilitySlot.Skill2, skill2Def, null);
        WeaponSkillHudSlotPresenter.UpdateCooldownAndCharge(skill1UI, WeaponAbilitySlot.Skill1, skill1Def, abilitySystem, null);
        WeaponSkillHudSlotPresenter.UpdateCooldownAndCharge(skill2UI, WeaponAbilitySlot.Skill2, skill2Def, abilitySystem, null);
        RefreshSwapGuide(force: false);
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        UnbindInventoryEvents();
        TryResolvePlayerRefs(player);
        RebuildSelector();
        BindInventoryEvents();
        RefreshAbilityRefs();
        RefreshVisibility();
        RefreshSwapGuide(force: true);
    }

    private void HandlePlayerUnregistered(PlayerInteractor2D player)
    {
        if (player == null)
            return;

        if (inventory != null && inventory.gameObject == player.gameObject)
        {
            UnbindInventoryEvents();
            inventory = null;
            abilitySystem = null;
            weaponEquipController = null;
            abilitySelector = null;
            RefreshAbilityRefs();
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
        {
            inventory = player.GetComponent<WeaponInventory2D>();
            abilitySystem = player.GetComponent<AbilitySystem>();
            weaponEquipController = player.GetComponentInChildren<WeaponEquipController>(true);
        }

        if (inventory == null) inventory = FindFirstObjectByType<WeaponInventory2D>();
        if (abilitySystem == null && inventory != null) abilitySystem = inventory.GetComponent<AbilitySystem>();
        if (abilitySystem == null) abilitySystem = FindFirstObjectByType<AbilitySystem>();
        if (weaponEquipController == null && inventory != null)
            weaponEquipController = inventory.EquipController;
        if (weaponEquipController == null && inventory != null)
            weaponEquipController = inventory.GetComponentInChildren<WeaponEquipController>(true);
        if (weaponEquipController == null && abilitySystem != null)
            weaponEquipController = abilitySystem.GetComponentInChildren<WeaponEquipController>(true);
        if (weaponEquipController == null)
            weaponEquipController = FindFirstObjectByType<WeaponEquipController>();
    }

    private void RebuildSelector()
    {
        abilitySelector = inventory != null
            ? new WeaponAbilitySelector(inventory, weaponEquipController)
            : null;
    }

    private void BindInventoryEvents()
    {
        if (inventory == null)
            return;

        inventory.OnEquippedChanged -= HandleEquippedChanged;
        inventory.OnInventoryChanged -= RefreshAbilityRefs;
        inventory.OnEquippedChanged += HandleEquippedChanged;
        inventory.OnInventoryChanged += RefreshAbilityRefs;
    }

    private void UnbindInventoryEvents()
    {
        if (inventory == null)
            return;

        inventory.OnEquippedChanged -= HandleEquippedChanged;
        inventory.OnInventoryChanged -= RefreshAbilityRefs;
    }

    private void HandleEquippedChanged(int previousIndex, int newIndex, WeaponDefinition previousWeapon, WeaponDefinition newWeapon)
    {
        RefreshAbilityRefs();
    }

    private void RefreshAbilityRefs()
    {
        int inactiveSlotIndex = ResolveInactiveWeaponSlotIndex();
        WeaponDefinition inactiveWeapon = inactiveSlotIndex >= 0 && inventory != null
            ? inventory.GetWeaponInSlot(inactiveSlotIndex)
            : null;

        if (inactiveWeapon == null || abilitySelector == null)
        {
            skill1Def = null;
            skill2Def = null;
        }
        else
        {
            skill1Def = abilitySelector.ResolveAbilityForSlot(inactiveSlotIndex, WeaponAbilitySlot.Skill1);
            skill2Def = abilitySelector.ResolveAbilityForSlot(inactiveSlotIndex, WeaponAbilitySlot.Skill2);
        }

        WeaponSkillHudSlotPresenter.ApplySlot(skill1UI, skill1Def, normalIconColor, GetInputBindingService());
        WeaponSkillHudSlotPresenter.ApplySlot(skill2UI, skill2Def, normalIconColor, GetInputBindingService());
        RefreshVisibility();
        RefreshSwapGuide(force: true);
    }

    private int ResolveInactiveWeaponSlotIndex()
    {
        if (inventory == null)
            return -1;

        int activeIndex = inventory.ActiveIndex;
        if (activeIndex < 0)
            return -1;

        int otherIndex = inventory.GetOtherSlotIndex(activeIndex);
        return inventory.GetWeaponInSlot(otherIndex) != null ? otherIndex : -1;
    }

    private void RefreshVisibility()
    {
        if (hudRoot == null)
            return;

        bool hasInactiveWeapon = ResolveInactiveWeaponSlotIndex() >= 0;
        bool hasPlayerRefs = inventory != null && abilitySystem != null && hasInactiveWeapon;
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
            if (hudGraphics[i] == null || hudGraphics[i].gameObject == gameObject)
                continue;

            hudGraphics[i].enabled = hasPlayerRefs;
        }
    }

    private void RefreshSwapGuide(bool force)
    {
        InputBindingService input = GetInputBindingService();
        KeyCode currentKey = input != null
            ? input.GetKey(InputActionId.SwapWeapon)
            : KeyCode.None;

        if (!force && currentKey == lastSwapGuideKey)
            return;

        lastSwapGuideKey = currentKey;

        bool visible = ResolveInactiveWeaponSlotIndex() >= 0;
        if (swapGuideRoot != null)
            swapGuideRoot.SetActive(visible);

        if (!visible)
            return;

        InputGlyphPresentation glyph = input != null
            ? input.GetKeyGlyph(currentKey)
            : InputGlyphDatabase.Resolve(KeyCode.None);
        string fallbackLabel = input != null
            ? input.GetKeyDisplayLabel(currentKey)
            : string.Empty;
        InputGlyphVisualUtility.Apply(swapGuideLabel, swapGuideIcon, glyph, fallbackLabel, swapGuideFallbackIcon);
    }

    private InputBindingService GetInputBindingService()
    {
        if (cachedInputBindingService == null)
            cachedInputBindingService = InputBindingService.EnsureInstance();

        return cachedInputBindingService;
    }
}

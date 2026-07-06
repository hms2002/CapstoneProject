using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 현재 장착 무기의 Skill1, Skill2 쿨다운/충전 HUD를 플레이어 상태에 맞춰 갱신한다.
/// - 플레이어 등록/해제와 무기 장착 변경에 반응해 HUD 표시 대상을 안전하게 동기화한다.
/// </summary>
public class WeaponSkillHUD2D : MonoBehaviour, IDefaultHudVisibilityTarget
{
    /// <summary>
    /// 책임 :
    /// - 무기 HUD의 개별 스킬 슬롯이 사용하는 비주얼 참조를 묶는다.
    /// - 기본 상태와 시전 중 상태를 슬롯 단위로 전환할 수 있게 한다.
    /// - 각 슬롯이 어떤 입력 액션의 글리프를 표시할지 함께 보관한다.
    /// </summary>
    [System.Serializable]
    public class SkillSlotUI
    {
        [Tooltip("가능하면 슬롯 루트 오브젝트를 직접 연결합니다. 비어 있으면 icon 등의 부모에서 추론합니다.")]
        public GameObject root;
        [Tooltip("켜져 있으면 이 슬롯은 현재 입력 바인딩 글리프를 표시합니다.")]
        public bool useInputGuide;
        [Tooltip("이 슬롯이 표시할 입력 액션입니다. 아이콘 가이드가 있으면 InputBindingService를 통해 현재 바인딩 이미지를 읽습니다.")]
        public InputActionId inputActionId = InputActionId.PrimaryAttack;
        public Image icon;
        [Tooltip("입력 키 가이드 루트입니다. 비어 있으면 inputGuideIcon의 오브젝트를 직접 사용합니다.")]
        public GameObject inputGuideRoot;
        [Tooltip("InputGlyphDatabase에서 가져온 현재 바인딩 아이콘을 표시할 Image입니다.")]
        public Image inputGuideIcon;
        [Tooltip("스킬 시전/실행 중 기존 아이콘 위에 얹을 강조 비주얼입니다.")]
        public GameObject activeOverlay;
        [Tooltip("activeOverlay 안의 Image를 직접 연결하면, 현재 스킬 아이콘 스프라이트를 자동 동기화합니다.")]
        public Image activeOverlayImage;
        public Image cooldownFill;   // fillAmount = remaining/total (Image Type Filled 필요)
        [Tooltip("선택. 지정하면 Bloom 같은 활성 지속시간 표시는 cooldownFill 대신 이 Image를 사용합니다.")]
        public Image activeDurationFill;
        [Header("Ready Flash")]
        [Tooltip("선택. 쿨타임 완료 순간에 반짝일 별도 글로우 Image입니다. 비워두면 icon 자체를 색/스케일로 반짝입니다.")]
        public Image readyFlashImage;
        [Tooltip("쿨타임 완료 반짝임 지속 시간입니다.")]
        public float readyFlashDuration = 0.22f;
        [Tooltip("쿨타임 완료 반짝임 색입니다. readyFlashImage를 쓰면 이 색으로 글로우를 표시합니다.")]
        public Color readyFlashColor = new Color(1f, 0.95f, 0.55f, 1f);
        [Tooltip("readyFlashImage가 없을 때 아이콘을 얼마나 크게 튕길지 정합니다.")]
        public float readyFlashScale = 1.16f;
        public TMP_Text cooldownText; // 선택(초 표기)
        public TMP_Text chargeText;   // 선택(예: 2/3)

        [System.NonSerialized] public bool cooldownFillConfigCaptured;
        [System.NonSerialized] public Image.Type cooldownFillType;
        [System.NonSerialized] public Image.FillMethod cooldownFillMethod;
        [System.NonSerialized] public int cooldownFillOrigin;
        [System.NonSerialized] public bool cooldownFillVisibilityWarningLogged;
        [System.NonSerialized] public bool readyStateInitialized;
        [System.NonSerialized] public bool wasReady;
        [System.NonSerialized] public int lastChargeCount = -1;
        [System.NonSerialized] public float readyFlashRemaining;
        [System.NonSerialized] public bool readyFlashScaleCaptured;
        [System.NonSerialized] public Vector3 readyFlashBaseScale;
        [System.NonSerialized] public bool readyFlashColorCaptured;
        [System.NonSerialized] public Color readyFlashBaseIconColor;
        [System.NonSerialized] public Material readyFlashOriginalMaterial;
        [System.NonSerialized] public Material readyFlashMaterialInstance;
    }

    [Header("Refs")]
    [SerializeField] private WeaponInventory2D inventory;
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private WeaponEquipController weaponEquipController;

    [Header("Visibility")]
    [SerializeField] private GameObject hudRoot;

    [Header("UI Slots")]
    public SkillSlotUI attackUI;
    public SkillSlotUI skill1UI;
    public SkillSlotUI skill2UI;

    [Header("Casting Visual")]
    [SerializeField] private Color normalIconColor = Color.white;
    [SerializeField] private Color activeIconColor = new Color(1f, 0.92f, 0.55f, 1f);
    [SerializeField] private float activePulseSpeed = 8f;
    [SerializeField] private float activePulseStrength = 0.18f;

    private CanvasGroup hudCanvasGroup;
    private Graphic[] hudGraphics;
    private AbilityDefinition attackDef;
    private AbilityDefinition skill1Def;
    private AbilityDefinition skill2Def;
    private WeaponAbilitySelector abilitySelector;
    private InputBindingService cachedInputBindingService;

    private void Awake()
    {
        if (hudRoot == null)
            hudRoot = gameObject;

        hudCanvasGroup = hudRoot.GetComponent<CanvasGroup>();
        if (hudRoot == gameObject && hudCanvasGroup == null)
            hudGraphics = hudRoot.GetComponentsInChildren<Graphic>(includeInactive: true);

        TryResolvePlayerRefs();
        RebuildSelector();
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
        RebuildSelector();
        BindInventoryEvents();
        RefreshAbilityRefs();
        RefreshVisibility();
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
        inventory.OnInventoryChanged -= HandleInventoryChanged;
        inventory.OnEquippedChanged += HandleEquippedChanged;
        inventory.OnInventoryChanged += HandleInventoryChanged;
    }

    private void UnbindInventoryEvents()
    {
        if (inventory == null)
            return;

        inventory.OnEquippedChanged -= HandleEquippedChanged;
        inventory.OnInventoryChanged -= HandleInventoryChanged;
    }

    private void HandleEquippedChanged(int prevIdx, int newIdx, WeaponDefinition prevW, WeaponDefinition newW)
    {
        RebuildSelector();
        RefreshAbilityRefs();
    }

    private void HandleInventoryChanged()
    {
        RebuildSelector();
        RefreshAbilityRefs();
    }

    private void RefreshAbilityRefs()
    {
        if (inventory == null)
        {
            attackDef = skill1Def = skill2Def = null;
            ApplySlot(attackUI, null);
            ApplySlot(skill1UI, null);
            ApplySlot(skill2UI, null);
            RefreshVisibility();
            return;
        }

        attackDef = null;
        skill1Def = abilitySelector != null
            ? abilitySelector.ResolveAbility(WeaponAbilitySlot.Skill1)
            : inventory.GetActiveAbility(WeaponAbilitySlot.Skill1);
        skill2Def = abilitySelector != null
            ? abilitySelector.ResolveAbility(WeaponAbilitySlot.Skill2)
            : inventory.GetActiveAbility(WeaponAbilitySlot.Skill2);

        ApplySlot(attackUI, attackDef);
        ApplySlot(skill1UI, skill1Def);
        ApplySlot(skill2UI, skill2Def);
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (hudRoot == null)
            return;

        bool hasPlayerRefs = inventory != null && abilitySystem != null;
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

    private void ApplySlot(SkillSlotUI ui, AbilityDefinition def)
    {
        WeaponSkillHudSlotPresenter.ApplySlot(ui, def, normalIconColor, GetInputBindingService());
    }

    /// <summary>
    /// 책임 :
    /// - 슬롯 전체 표시 여부를 제어한다.
    /// - 루트가 명시되지 않았을 때는 현재 연결된 UI 참조의 부모를 이용해 보수적으로 루트를 추론한다.
    /// </summary>
    private static void SetSlotVisible(SkillSlotUI ui, bool visible)
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
        if (ui.cooldownFill != null) ui.cooldownFill.enabled = visible;
        if (ui.cooldownText != null) ui.cooldownText.enabled = visible;
        if (ui.chargeText != null) ui.chargeText.enabled = visible;
    }

    /// <summary>
    /// 책임 :
    /// - SkillSlotUI가 가리키는 공통 루트 오브젝트를 찾아 슬롯 단위 토글에 사용한다.
    /// - 인스펙터 루트가 없으면 icon > cooldownFill > text 순으로 부모를 추론한다.
    /// </summary>
    private static GameObject ResolveSlotRoot(SkillSlotUI ui)
    {
        if (ui.root != null)
            return ui.root;

        if (ui.icon != null && ui.icon.transform.parent != null)
            return ui.icon.transform.parent.gameObject;

        if (ui.cooldownFill != null && ui.cooldownFill.transform.parent != null)
            return ui.cooldownFill.transform.parent.gameObject;

        if (ui.cooldownText != null && ui.cooldownText.transform.parent != null)
            return ui.cooldownText.transform.parent.gameObject;

        if (ui.chargeText != null && ui.chargeText.transform.parent != null)
            return ui.chargeText.transform.parent.gameObject;

        return null;
    }

    private void Update()
    {
        if (abilitySystem == null) return;

        UpdateDynamicIcon(skill1UI, WeaponAbilitySlot.Skill1, skill1Def);
        UpdateDynamicIcon(skill2UI, WeaponAbilitySlot.Skill2, skill2Def);
        UpdateCooldownAndCharge(skill1UI, WeaponAbilitySlot.Skill1, skill1Def);
        UpdateCooldownAndCharge(skill2UI, WeaponAbilitySlot.Skill2, skill2Def);
        UpdateCastingVisual(skill1UI, skill1Def);
        UpdateCastingVisual(skill2UI, skill2Def);
    }

    private void UpdateDynamicIcon(SkillSlotUI ui, WeaponAbilitySlot slot, AbilityDefinition def)
    {
        WeaponSkillHudSlotPresenter.UpdateDynamicIcon(ui, slot, def, ResolveHudIconOverrideProvider());
    }

    private IWeaponAbilityHudIconOverrideProvider ResolveHudIconOverrideProvider()
    {
        WeaponAbilityRuntimeState runtimeState = weaponEquipController != null
            ? weaponEquipController.GetCurrentWeaponRuntimeState()
            : null;

        return runtimeState as IWeaponAbilityHudIconOverrideProvider;
    }

    private IWeaponAbilityHudDurationOverrideProvider ResolveHudDurationOverrideProvider()
    {
        WeaponAbilityRuntimeState runtimeState = weaponEquipController != null
            ? weaponEquipController.GetCurrentWeaponRuntimeState()
            : null;

        if (runtimeState is IWeaponAbilityHudDurationOverrideProvider weaponProvider)
            return weaponProvider;

        return abilitySystem != null
            ? abilitySystem.GetComponent<IWeaponAbilityHudDurationOverrideProvider>()
            : null;
    }

    /// <summary>
    /// 책임 :
    /// - 현재 AbilitySystem의 캐스팅/실행 상태를 슬롯 정의와 비교해 시전 중 비주얼을 전환한다.
    /// - 별도 대체 오브젝트가 없으면 기본 비주얼만 유지한다.
    /// </summary>
    private void UpdateCastingVisual(SkillSlotUI ui, AbilityDefinition def)
    {
        WeaponSkillHudSlotPresenter.UpdateCastingVisual(
            ui,
            def,
            abilitySystem,
            normalIconColor,
            activeIconColor,
            activePulseSpeed,
            activePulseStrength);
    }

    /// <summary>
    /// 책임 :
    /// - 현재 AbilityDefinition이 캐스팅 중이거나 실행 중인지 HUD 관점에서 판별한다.
    /// - Skill HUD는 슬롯별 정의와 현재 런타임 spec의 definition을 비교해 활성 상태를 판단한다.
    /// </summary>
    private bool IsAbilityActive(AbilityDefinition def)
    {
        if (abilitySystem == null || def == null)
            return false;

        var currentCast = abilitySystem.CurrentCastSpec != null
            ? abilitySystem.CurrentCastSpec.Definition
            : null;
        if (abilitySystem.IsCasting && currentCast == def)
            return true;

        var currentExec = abilitySystem.CurrentExecSpec != null
            ? abilitySystem.CurrentExecSpec.Definition
            : null;
        return abilitySystem.IsExecuting && currentExec == def;
    }

    /// <summary>
    /// 책임 :
    /// - 시전 중 아이콘에 적용할 펄스 색을 계산한다.
    /// - HUD 공통 시전 강조 톤을 한 곳에서 관리한다.
    /// </summary>
    private Color EvaluateActiveIconColor()
    {
        float pulse = (Mathf.Sin(Time.unscaledTime * activePulseSpeed) + 1f) * 0.5f;
        float t = Mathf.Lerp(1f - activePulseStrength, 1f, pulse);
        return Color.Lerp(normalIconColor, activeIconColor, t);
    }

    /// <summary>
    /// 책임 :
    /// - 시전 강조용 오버레이 Image가 현재 스킬 아이콘 스프라이트를 자동으로 따라가게 만든다.
    /// - 무기 교체로 icon sprite가 바뀌어도 오버레이 authoring을 다시 하지 않도록 돕는다.
    /// - 레이아웃을 망가뜨리지 않도록 transform/rect 크기는 건드리지 않는다.
    /// </summary>
    private static void SyncOverlaySprite(SkillSlotUI ui, AbilityDefinition def)
    {
        if (ui == null)
            return;

        Image overlayImage = ResolveOverlayImage(ui);
        if (overlayImage == null)
            return;

        overlayImage.enabled = def != null;
        overlayImage.sprite = def != null ? def.icon : null;
    }

    /// <summary>
    /// 책임 :
    /// - activeOverlay가 사용할 실제 Image 컴포넌트를 찾는다.
    /// - 인스펙터 지정이 없으면 activeOverlay 자신에게서 보수적으로 탐색한다.
    /// </summary>
    private static Image ResolveOverlayImage(SkillSlotUI ui)
    {
        if (ui == null)
            return null;

        if (ui.activeOverlayImage != null)
            return ui.activeOverlayImage;

        if (ui.activeOverlay == null)
            return null;

        return ui.activeOverlay.GetComponent<Image>();
    }

    /// <summary>
    /// 책임 :
    /// - 슬롯이 현재 표시 중일 때만 입력 키 가이드 아이콘을 켜고, 현재 바인딩 글리프를 반영한다.
    /// - 실제 아이콘 조회는 InputBindingService를 통해 수행해 InputGlyphDatabase와 동일한 경로를 사용한다.
    /// </summary>
    private void SyncInputGuide(SkillSlotUI ui, bool isSlotVisible)
    {
        WeaponSkillHudSlotPresenter.SyncInputGuide(ui, isSlotVisible, GetInputBindingService());
    }

    /// <summary>
    /// 책임 :
    /// - 슬롯의 입력 가이드 루트 오브젝트를 찾아 슬롯 on/off와 함께 제어할 수 있게 한다.
    /// - 별도 루트가 없으면 아이콘 Image 오브젝트 자체를 루트로 사용한다.
    /// </summary>
    private static GameObject ResolveInputGuideRoot(SkillSlotUI ui)
    {
        if (ui == null)
            return null;

        if (ui.inputGuideRoot != null)
            return ui.inputGuideRoot;

        return ui.inputGuideIcon != null ? ui.inputGuideIcon.gameObject : null;
    }

    /// <summary>
    /// 책임 :
    /// - HUD가 사용하는 입력 바인딩 서비스 참조를 한 번만 확보해 반복 조회 비용을 줄인다.
    /// - 서비스가 아직 없으면 필요 시점에 안전하게 다시 찾아 현재 글리프를 읽게 한다.
    /// </summary>
    private InputBindingService GetInputBindingService()
    {
        if (cachedInputBindingService == null)
            cachedInputBindingService = InputBindingService.EnsureInstance();

        return cachedInputBindingService;
    }
    private void UpdateCooldownAndCharge(SkillSlotUI ui, WeaponAbilitySlot slot, AbilityDefinition def)
    {
        WeaponSkillHudSlotPresenter.UpdateCooldownAndCharge(
            ui,
            slot,
            def,
            abilitySystem,
            ResolveHudDurationOverrideProvider());
    }

    private bool TryApplyActiveDurationOverride(SkillSlotUI ui, WeaponAbilitySlot slot, AbilityDefinition def)
    {
        IWeaponAbilityHudDurationOverrideProvider provider = ResolveHudDurationOverrideProvider();
        if (provider == null ||
            !provider.TryGetHudDurationOverride(slot, def, out WeaponAbilityHudDurationOverride duration))
        {
            return false;
        }

        Image fill = ResolveActiveDurationFill(ui);
        if (fill != null)
        {
            bool usesCooldownFill = fill == ui.cooldownFill;
            if (usesCooldownFill)
                CaptureCooldownFillConfig(ui);

            SetActiveDurationFillVisible(ui, true);
            fill.enabled = true;
            fill.type = Image.Type.Filled;
            if (duration.FillBottomToTop)
            {
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            }

            fill.fillAmount = Mathf.Clamp01(duration.RemainingSeconds / duration.MaxSeconds);
        }

        WarnIfDurationOverrideFillInvisible(ui, slot, fill);

        if (ui.cooldownText != null)
            ui.cooldownText.text = duration.ShowText && duration.RemainingSeconds > 0.01f
                ? duration.RemainingSeconds.ToString("0.0")
                : "";

        if (ui.chargeText != null)
            ui.chargeText.text = "";

        return true;
    }

    private static Image ResolveActiveDurationFill(SkillSlotUI ui)
    {
        if (ui == null)
            return null;

        return ui.activeDurationFill != null ? ui.activeDurationFill : ui.cooldownFill;
    }

    private static void SetActiveDurationFillVisible(SkillSlotUI ui, bool visible)
    {
        if (ui == null || ui.activeDurationFill == null || ui.activeDurationFill == ui.cooldownFill)
            return;

        if (ui.activeDurationFill.gameObject.activeSelf != visible)
            ui.activeDurationFill.gameObject.SetActive(visible);

        ui.activeDurationFill.enabled = visible;
        if (!visible)
            ui.activeDurationFill.fillAmount = 0f;
    }

    private static void WarnIfDurationOverrideFillInvisible(SkillSlotUI ui, WeaponAbilitySlot slot, Image fill)
    {
        if (ui == null || ui.cooldownFillVisibilityWarningLogged)
            return;

        if (fill == null)
        {
            ui.cooldownFillVisibilityWarningLogged = true;
            Debug.LogWarning($"[WeaponSkillHUD2D] {slot} active duration override is active, but neither activeDurationFill nor cooldownFill is assigned.");
            return;
        }

        bool invisible =
            !fill.gameObject.activeInHierarchy ||
            !fill.enabled ||
            fill.canvasRenderer.GetAlpha() <= 0.01f ||
            fill.color.a <= 0.01f ||
            fill.sprite == null;
        if (!invisible)
            return;

        ui.cooldownFillVisibilityWarningLogged = true;
        Debug.LogWarning($"[WeaponSkillHUD2D] {slot} active duration override is active, but the resolved fill Image may be invisible. Check activeDurationFill/cooldownFill active state, alpha, sprite, and hierarchy order.");
    }

    private static void CaptureCooldownFillConfig(SkillSlotUI ui)
    {
        if (ui == null || ui.cooldownFill == null || ui.cooldownFillConfigCaptured)
            return;

        ui.cooldownFillType = ui.cooldownFill.type;
        ui.cooldownFillMethod = ui.cooldownFill.fillMethod;
        ui.cooldownFillOrigin = ui.cooldownFill.fillOrigin;
        ui.cooldownFillConfigCaptured = true;
    }

    private static void RestoreCooldownFillConfig(SkillSlotUI ui)
    {
        if (ui == null || ui.cooldownFill == null || !ui.cooldownFillConfigCaptured)
            return;

        ui.cooldownFill.type = ui.cooldownFillType;
        ui.cooldownFill.fillMethod = ui.cooldownFillMethod;
        ui.cooldownFill.fillOrigin = ui.cooldownFillOrigin;
    }

}

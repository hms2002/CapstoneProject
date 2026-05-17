using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 현재 장착 무기의 Skill1, Skill2 쿨다운/충전 HUD를 플레이어 상태에 맞춰 갱신한다.
/// - 플레이어 등록/해제와 무기 장착 변경에 반응해 HUD 표시 대상을 안전하게 동기화한다.
/// </summary>
public class WeaponSkillHUD2D : MonoBehaviour
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
        public TMP_Text cooldownText; // 선택(초 표기)
        public TMP_Text chargeText;   // 선택(예: 2/3)
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
    private InputBindingService cachedInputBindingService;

    private void Awake()
    {
        if (hudRoot == null)
            hudRoot = gameObject;

        hudCanvasGroup = hudRoot.GetComponent<CanvasGroup>();
        if (hudRoot == gameObject && hudCanvasGroup == null)
            hudGraphics = hudRoot.GetComponentsInChildren<Graphic>(includeInactive: true);

        TryResolvePlayerRefs();
        RefreshVisibility();
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;

        TryResolvePlayerRefs();
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

    private void HandleEquippedChanged(int prevIdx, int newIdx, WeaponDefinition prevW, WeaponDefinition newW)
    {
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
        skill1Def = inventory.GetActiveAbility(WeaponAbilitySlot.Skill1);
        skill2Def = inventory.GetActiveAbility(WeaponAbilitySlot.Skill2);

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
        if (ui == null) return;

        bool has = (def != null);
        SetSlotVisible(ui, has);

        if (ui.icon != null)
        {
            ui.icon.enabled = has;
            ui.icon.color = normalIconColor;
            // AbilityDefinition에 아이콘이 있다면 여기서 연결해도 됨(없으면 유지)
            if(def != null)
                ui.icon.sprite = def.icon;
        }

        SyncOverlaySprite(ui, def);
        SyncInputGuide(ui, has);

        if (ui.activeOverlay != null)
            ui.activeOverlay.SetActive(false);

        if (ui.cooldownFill != null)
            ui.cooldownFill.fillAmount = has ? 0f : 0f;

        if (ui.cooldownText != null)
            ui.cooldownText.text = "";

        if (ui.chargeText != null)
            ui.chargeText.text = "";
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
        UpdateCooldownAndCharge(skill1UI, skill1Def);
        UpdateCooldownAndCharge(skill2UI, skill2Def);
        UpdateCastingVisual(skill1UI, skill1Def);
        UpdateCastingVisual(skill2UI, skill2Def);
    }

    private void UpdateDynamicIcon(SkillSlotUI ui, WeaponAbilitySlot slot, AbilityDefinition def)
    {
        if (ui == null || ui.icon == null)
            return;

        Sprite resolvedIcon = null;
        if (def != null)
        {
            IWeaponAbilityHudIconOverrideProvider provider = ResolveHudIconOverrideProvider();
            if (provider != null &&
                provider.TryGetHudIconOverride(slot, def, out Sprite overrideIcon) &&
                overrideIcon != null)
            {
                resolvedIcon = overrideIcon;
            }
            else
            {
                resolvedIcon = def.icon;
            }
        }

        if (ui.icon.sprite != resolvedIcon)
            ui.icon.sprite = resolvedIcon;

        Image overlayImage = ResolveOverlayImage(ui);
        if (overlayImage != null && overlayImage.sprite != resolvedIcon)
            overlayImage.sprite = resolvedIcon;
    }

    private IWeaponAbilityHudIconOverrideProvider ResolveHudIconOverrideProvider()
    {
        WeaponAbilityRuntimeState runtimeState = weaponEquipController != null
            ? weaponEquipController.GetCurrentWeaponRuntimeState()
            : null;

        return runtimeState as IWeaponAbilityHudIconOverrideProvider;
    }

    /// <summary>
    /// 책임 :
    /// - 현재 AbilitySystem의 캐스팅/실행 상태를 슬롯 정의와 비교해 시전 중 비주얼을 전환한다.
    /// - 별도 대체 오브젝트가 없으면 기본 비주얼만 유지한다.
    /// </summary>
    private void UpdateCastingVisual(SkillSlotUI ui, AbilityDefinition def)
    {
        if (ui == null)
            return;

        bool hasAbility = def != null;
        bool isActive = hasAbility && IsAbilityActive(def);
        if (ui.activeOverlay != null)
            ui.activeOverlay.SetActive(hasAbility && isActive);

        if (ui.icon != null)
            ui.icon.color = hasAbility && isActive
                ? EvaluateActiveIconColor()
                : normalIconColor;
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
        if (ui == null)
            return;

        GameObject guideRoot = ResolveInputGuideRoot(ui);
        Image guideIcon = ui.inputGuideIcon;
        bool shouldShow = isSlotVisible && ui.useInputGuide && guideIcon != null;

        if (guideRoot != null)
            guideRoot.SetActive(shouldShow);

        if (!shouldShow || guideIcon == null)
            return;

        InputBindingService input = GetInputBindingService();
        guideIcon.enabled = true;
        guideIcon.sprite = input != null
            ? input.GetBindingIcon(ui.inputActionId)
            : null;
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
    private void UpdateCooldownAndCharge(SkillSlotUI ui, AbilityDefinition def)
    {
        if (ui == null) return;

        if (def == null)
        {
            if (ui.cooldownFill != null) ui.cooldownFill.fillAmount = 0f;
            if (ui.cooldownText != null) ui.cooldownText.text = "";
            if (ui.chargeText != null) ui.chargeText.text = "";
            return;
        }

        float total = Mathf.Max(0.0001f, def.cooldown);

        // ✅ 충전형
        if (def.useCharges)
        {
            int charges = abilitySystem.GetChargesRemaining(def);
            int max = abilitySystem.GetMaxCharges(def);
            float recharge = abilitySystem.GetRechargeRemaining(def); // 다음 1회 충전까지 남은 시간

            // fill: "충전 중"이면 차오르는 형태(= 남은시간 기반)
            if (ui.cooldownFill != null)
            {
                // charges가 풀이면(= 충전 필요 없음) fill 0으로
                if (charges >= max) ui.cooldownFill.fillAmount = 0f;
                else ui.cooldownFill.fillAmount = Mathf.Clamp01(recharge / total);
            }

            if (ui.cooldownText != null)
            {
                // 충전 중이고 아직 풀충전 아니면 남은 시간 표시
                ui.cooldownText.text = (charges < max && recharge > 0.01f) ? recharge.ToString("0.0") : "";
            }

            if (ui.chargeText != null)
            {
                ui.chargeText.text = $"{charges}/{max}";
            }

            return;
        }

        // ✅ 일반 쿨다운형
        float remaining = abilitySystem.GetCooldownRemaining(def);

        if (ui.cooldownFill != null)
            ui.cooldownFill.fillAmount = Mathf.Clamp01(remaining / total);

        if (ui.cooldownText != null)
            ui.cooldownText.text = remaining > 0.01f ? remaining.ToString("0.0") : "";

        if (ui.chargeText != null)
            ui.chargeText.text = "";
    }

}

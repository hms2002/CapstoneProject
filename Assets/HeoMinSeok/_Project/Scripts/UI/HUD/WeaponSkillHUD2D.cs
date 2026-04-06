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
    [System.Serializable]
    public class SkillSlotUI
    {
        [Tooltip("가능하면 슬롯 루트 오브젝트를 직접 연결합니다. 비어 있으면 icon 등의 부모에서 추론합니다.")]
        public GameObject root;
        public Image icon;
        public Image cooldownFill;   // fillAmount = remaining/total (Image Type Filled 필요)
        public TMP_Text cooldownText; // 선택(초 표기)
        public TMP_Text chargeText;   // 선택(예: 2/3)
    }

    [Header("Refs")]
    [SerializeField] private WeaponInventory2D inventory;
    [SerializeField] private AbilitySystem abilitySystem;

    [Header("Visibility")]
    [SerializeField] private GameObject hudRoot;

    [Header("UI Slots")]
    public SkillSlotUI attackUI;
    public SkillSlotUI skill1UI;
    public SkillSlotUI skill2UI;

    private CanvasGroup hudCanvasGroup;
    private Graphic[] hudGraphics;
    private AbilityDefinition attackDef;
    private AbilityDefinition skill1Def;
    private AbilityDefinition skill2Def;

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
        }

        if (inventory == null) inventory = FindFirstObjectByType<WeaponInventory2D>();
        if (abilitySystem == null && inventory != null) abilitySystem = inventory.GetComponent<AbilitySystem>();
        if (abilitySystem == null) abilitySystem = FindFirstObjectByType<AbilitySystem>();
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
            // AbilityDefinition에 아이콘이 있다면 여기서 연결해도 됨(없으면 유지)
            if(def != null)
                ui.icon.sprite = def.icon;
        }

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

        UpdateCooldownAndCharge(skill1UI, skill1Def);
        UpdateCooldownAndCharge(skill2UI, skill2Def);
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

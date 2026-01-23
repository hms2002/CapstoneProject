using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGAS;

public class WeaponSkillHUD2D : MonoBehaviour
{
    [System.Serializable]
    public class SkillSlotUI
    {
        public Image icon;
        public Image cooldownFill;   // fillAmount = remaining/total (Image Type Filled 필요)
        public TMP_Text cooldownText; // 선택(초 표기)
        public TMP_Text chargeText;   // 선택(예: 2/3)
    }

    [Header("Refs")]
    [SerializeField] private WeaponInventory2D inventory;
    [SerializeField] private AbilitySystem abilitySystem;

    [Header("UI Slots")]
    public SkillSlotUI attackUI;
    public SkillSlotUI skill1UI;
    public SkillSlotUI skill2UI;

    private AbilityDefinition attackDef;
    private AbilityDefinition skill1Def;
    private AbilityDefinition skill2Def;

    private void Awake()
    {
        if (inventory == null) inventory = FindFirstObjectByType<WeaponInventory2D>();
        if (abilitySystem == null && inventory != null) abilitySystem = inventory.GetComponent<AbilitySystem>();
        if (abilitySystem == null) abilitySystem = FindFirstObjectByType<AbilitySystem>();
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.OnEquippedChanged += HandleEquippedChanged;
            inventory.OnSlotChanged += (_, __, ___) => RefreshAbilityRefs(); // 슬롯 변경도 영향 가능
            inventory.OnInventoryChanged += RefreshAbilityRefs;
        }

        RefreshAbilityRefs();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnEquippedChanged -= HandleEquippedChanged;
            inventory.OnSlotChanged -= (_, __, ___) => RefreshAbilityRefs();
            inventory.OnInventoryChanged -= RefreshAbilityRefs;
        }
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
            return;
        }

        attackDef = inventory.GetActiveAbility(WeaponAbilitySlot.Attack);
        skill1Def = inventory.GetActiveAbility(WeaponAbilitySlot.Skill1);
        skill2Def = inventory.GetActiveAbility(WeaponAbilitySlot.Skill2);

        ApplySlot(attackUI, attackDef);
        ApplySlot(skill1UI, skill1Def);
        ApplySlot(skill2UI, skill2Def);
    }

    private void ApplySlot(SkillSlotUI ui, AbilityDefinition def)
    {
        if (ui == null) return;

        bool has = (def != null);
        if (ui.icon != null)
        {
            ui.icon.enabled = has;
            // AbilityDefinition에 아이콘이 있다면 여기서 연결해도 됨(없으면 유지)
            // ui.icon.sprite = def.icon;
        }

        if (ui.cooldownFill != null)
            ui.cooldownFill.fillAmount = has ? 0f : 0f;

        if (ui.cooldownText != null)
            ui.cooldownText.text = "";

        if (ui.chargeText != null)
            ui.chargeText.text = "";
    }

    private void Update()
    {
        if (abilitySystem == null) return;

        UpdateCooldownAndCharge(attackUI, attackDef);
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

        // 1) 쿨다운
        float remaining = abilitySystem.GetCooldownRemaining(def);
        float total = Mathf.Max(0.0001f, def.cooldown);

        if (ui.cooldownFill != null)
            ui.cooldownFill.fillAmount = Mathf.Clamp01(remaining / total);

        if (ui.cooldownText != null)
            ui.cooldownText.text = remaining > 0.01f ? remaining.ToString("0.0") : "";

        // 2) 차지(옵션): 네 프로젝트에 충전형 스킬이 있으면 여기 연결
        // 아래는 "AbilitySystem에 GetChargesRemaining/GetMaxCharges/GetRechargeRemaining가 있다"는 전제.
        // 없으면 그 3개만 추가하면 됨.
        if (ui.chargeText != null)
        {
            if (def.useCharges) // AbilityDefinition에 useCharges/maxCharges 도입된 상태라면
            {
                int c = abilitySystem.GetChargesRemaining(def);
                int m = abilitySystem.GetMaxCharges(def);
                ui.chargeText.text = $"{c}/{m}";
            }
            else
            {
                ui.chargeText.text = "";
            }
        }
    }
}

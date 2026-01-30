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
            inventory.OnInventoryChanged += RefreshAbilityRefs;
        }

        RefreshAbilityRefs();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnEquippedChanged -= HandleEquippedChanged;
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

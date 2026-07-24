using UnityEngine;
using UnityEngine.UI;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 무기 스킬 HUD 슬롯 하나의 아이콘, 입력 가이드, 쿨다운, 차지, 활성 지속시간 표시를 공통 갱신한다.
/// - 현재 무기 HUD와 교체 무기 HUD가 같은 슬롯 표현 규칙을 공유하게 한다.
/// </summary>
public static class WeaponSkillHudSlotPresenter
{
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashMultiplyId = Shader.PropertyToID("_FlashMultiply");
    private static Shader readyFlashShader;

    public static void ApplySlot(
        WeaponSkillHUD2D.SkillSlotUI ui,
        AbilityDefinition def,
        Color normalIconColor,
        InputBindingService inputBindingService)
    {
        if (ui == null) return;

        bool has = def != null;
        SetSlotVisible(ui, has);

        if (ui.icon != null)
        {
            ui.icon.enabled = has;
            ui.icon.color = normalIconColor;
            ui.icon.sprite = def != null ? def.icon : null;
            ui.readyFlashBaseIconColor = normalIconColor;
            ui.readyFlashColorCaptured = true;
        }

        SyncOverlaySprite(ui, def);
        SyncInputGuide(ui, has, inputBindingService);

        if (ui.activeOverlay != null)
            ui.activeOverlay.SetActive(false);

        if (ui.cooldownFill != null)
            ui.cooldownFill.fillAmount = 0f;

        SetActiveDurationFillVisible(ui, false);

        if (ui.cooldownText != null)
            ui.cooldownText.text = string.Empty;

        if (ui.chargeText != null)
            ui.chargeText.text = string.Empty;

        ResetReadyFlashState(ui, has);
    }

    public static void UpdateDynamicIcon(
        WeaponSkillHUD2D.SkillSlotUI ui,
        WeaponAbilitySlot slot,
        AbilityDefinition def,
        IWeaponAbilityHudIconOverrideProvider provider)
    {
        if (ui == null || ui.icon == null)
            return;

        Sprite resolvedIcon = null;
        if (def != null)
        {
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

    public static void UpdateCastingVisual(
        WeaponSkillHUD2D.SkillSlotUI ui,
        AbilityDefinition def,
        AbilitySystem abilitySystem,
        Color normalIconColor,
        Color activeIconColor,
        float activePulseSpeed,
        float activePulseStrength)
    {
        if (ui == null)
            return;

        bool hasAbility = def != null;
        bool isActive = hasAbility && IsAbilityActive(abilitySystem, def);
        if (ui.activeOverlay != null)
            ui.activeOverlay.SetActive(hasAbility && isActive);

        if (ui.icon != null)
            ui.icon.color = hasAbility && isActive
                ? EvaluateActiveIconColor(normalIconColor, activeIconColor, activePulseSpeed, activePulseStrength)
                : normalIconColor;
    }

    public static void UpdateCooldownAndCharge(
        WeaponSkillHUD2D.SkillSlotUI ui,
        WeaponAbilitySlot slot,
        AbilityDefinition def,
        AbilitySystem abilitySystem,
        IWeaponAbilityHudDurationOverrideProvider durationProvider)
    {
        if (ui == null) return;

        if (def == null || abilitySystem == null)
        {
            RestoreCooldownFillConfig(ui);
            SetActiveDurationFillVisible(ui, false);
            if (ui.cooldownFill != null) ui.cooldownFill.fillAmount = 0f;
            if (ui.cooldownText != null) ui.cooldownText.text = string.Empty;
            if (ui.chargeText != null) ui.chargeText.text = string.Empty;
            ResetReadyFlashState(ui, false);
            return;
        }

        if (TryApplyActiveDurationOverride(ui, slot, def, durationProvider))
        {
            UpdateReadyFlash(ui, false, -1);
            return;
        }

        SetActiveDurationFillVisible(ui, false);
        RestoreCooldownFillConfig(ui);

        float total = Mathf.Max(0.0001f, def.cooldown);

        if (def.useCharges)
        {
            int charges = abilitySystem.GetChargesRemaining(def);
            int max = abilitySystem.GetMaxCharges(def);
            float recharge = abilitySystem.GetRechargeRemaining(def);
            bool ready = charges > 0;

            if (ui.cooldownFill != null)
                ui.cooldownFill.fillAmount = charges >= max ? 0f : Mathf.Clamp01(recharge / total);

            if (ui.cooldownText != null)
                ui.cooldownText.text = charges < max && recharge > 0.01f ? recharge.ToString("0.0") : string.Empty;

            if (ui.chargeText != null)
                ui.chargeText.text = $"{charges}/{max}";

            UpdateReadyFlash(ui, ready, charges);
            return;
        }

        float remaining = abilitySystem.GetCooldownRemaining(def);
        bool cooldownReady = remaining <= 0.01f;

        if (ui.cooldownFill != null)
            ui.cooldownFill.fillAmount = Mathf.Clamp01(remaining / total);

        if (ui.cooldownText != null)
            ui.cooldownText.text = remaining > 0.01f ? remaining.ToString("0.0") : string.Empty;

        if (ui.chargeText != null)
            ui.chargeText.text = string.Empty;

        UpdateReadyFlash(ui, cooldownReady, -1);
    }

    public static void SyncInputGuide(
        WeaponSkillHUD2D.SkillSlotUI ui,
        bool isSlotVisible,
        InputBindingService inputBindingService)
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

        guideIcon.enabled = true;
        guideIcon.sprite = inputBindingService != null
            ? inputBindingService.GetBindingIcon(ui.inputActionId)
            : null;
    }

    private static void SetSlotVisible(WeaponSkillHUD2D.SkillSlotUI ui, bool visible)
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
        if (ui.readyFlashImage != null) ui.readyFlashImage.enabled = false;
    }

    private static GameObject ResolveSlotRoot(WeaponSkillHUD2D.SkillSlotUI ui)
    {
        if (ui == null)
            return null;

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

    private static GameObject ResolveInputGuideRoot(WeaponSkillHUD2D.SkillSlotUI ui)
    {
        if (ui == null)
            return null;

        if (ui.inputGuideRoot != null)
            return ui.inputGuideRoot;

        return ui.inputGuideIcon != null ? ui.inputGuideIcon.gameObject : null;
    }

    private static void SyncOverlaySprite(WeaponSkillHUD2D.SkillSlotUI ui, AbilityDefinition def)
    {
        if (ui == null)
            return;

        if (ui.readyFlashImage != null)
        {
            ui.readyFlashImage.sprite = def != null ? def.icon : null;
            ui.readyFlashImage.enabled = false;
            ui.readyFlashImage.color = Color.clear;
        }

        Image overlayImage = ResolveOverlayImage(ui);
        if (overlayImage == null)
            return;

        overlayImage.enabled = def != null;
        overlayImage.sprite = def != null ? def.icon : null;
    }

    private static void ResetReadyFlashState(WeaponSkillHUD2D.SkillSlotUI ui, bool hasAbility)
    {
        if (ui == null)
            return;

        ui.readyStateInitialized = false;
        ui.wasReady = hasAbility;
        ui.lastChargeCount = -1;
        ui.readyFlashRemaining = 0f;
        ApplyReadyFlashVisual(ui, 0f);
    }

    private static void UpdateReadyFlash(WeaponSkillHUD2D.SkillSlotUI ui, bool ready, int chargeCount)
    {
        if (ui == null)
            return;

        if (!ui.readyStateInitialized)
        {
            ui.readyStateInitialized = true;
            ui.wasReady = ready;
            ui.lastChargeCount = chargeCount;
            ApplyReadyFlashVisual(ui, 0f);
            return;
        }

        bool chargeRestored = chargeCount >= 0 && ui.lastChargeCount >= 0 && chargeCount > ui.lastChargeCount;
        bool becameReady = chargeCount < 0 && !ui.wasReady && ready;
        bool shouldTrigger = becameReady || chargeRestored;
        if (shouldTrigger)
            TriggerReadyFlash(ui);
        else
            AdvanceReadyFlash(ui);

        ui.wasReady = ready;
        ui.lastChargeCount = chargeCount;
    }

    private static void TriggerReadyFlash(WeaponSkillHUD2D.SkillSlotUI ui)
    {
        if (ui == null)
            return;

        ui.readyFlashRemaining = Mathf.Max(0.01f, ui.readyFlashDuration);
        CaptureReadyFlashScale(ui);
        ApplyReadyFlashVisual(ui, 1f);
    }

    private static void AdvanceReadyFlash(WeaponSkillHUD2D.SkillSlotUI ui)
    {
        if (ui == null || ui.readyFlashRemaining <= 0f)
        {
            ApplyReadyFlashVisual(ui, 0f);
            return;
        }

        float duration = Mathf.Max(0.01f, ui.readyFlashDuration);
        ui.readyFlashRemaining = Mathf.Max(0f, ui.readyFlashRemaining - Time.unscaledDeltaTime);
        float normalized = Mathf.Clamp01(ui.readyFlashRemaining / duration);
        ApplyReadyFlashVisual(ui, Mathf.SmoothStep(0f, 1f, normalized));
    }

    private static void CaptureReadyFlashScale(WeaponSkillHUD2D.SkillSlotUI ui)
    {
        if (ui == null || ui.readyFlashScaleCaptured || ui.icon == null)
            return;

        ui.readyFlashBaseScale = ui.icon.rectTransform.localScale;
        ui.readyFlashScaleCaptured = true;
    }

    private static void ApplyReadyFlashVisual(WeaponSkillHUD2D.SkillSlotUI ui, float amount)
    {
        if (ui == null)
            return;

        amount = Mathf.Clamp01(amount);

        if (ui.readyFlashImage != null)
        {
            ui.readyFlashImage.enabled = amount > 0.001f;
            ui.readyFlashImage.color = new Color(
                ui.readyFlashColor.r,
                ui.readyFlashColor.g,
                ui.readyFlashColor.b,
                ui.readyFlashColor.a * amount);
        }

        if (ui.icon == null)
            return;

        if (!ui.readyFlashScaleCaptured)
            CaptureReadyFlashScale(ui);

        if (TryApplyReadyFlashMaterial(ui, amount))
        {
            if (ui.readyFlashColorCaptured && amount <= 0.001f)
                ui.icon.color = ui.readyFlashBaseIconColor;
        }
        else if (ui.readyFlashImage == null)
        {
            Color baseColor = ui.readyFlashColorCaptured ? ui.readyFlashBaseIconColor : Color.white;
            Color targetColor = Color.white;
            ui.icon.color = Color.Lerp(baseColor, targetColor, amount);
        }

        float scale = Mathf.Lerp(1f, Mathf.Max(1f, ui.readyFlashScale), amount);
        ui.icon.rectTransform.localScale = ui.readyFlashScaleCaptured
            ? ui.readyFlashBaseScale * scale
            : Vector3.one * scale;
    }

    private static bool TryApplyReadyFlashMaterial(WeaponSkillHUD2D.SkillSlotUI ui, float amount)
    {
        if (ui == null || ui.icon == null)
            return false;

        if (ui.readyFlashMaterialInstance == null)
        {
            Shader shader = ResolveReadyFlashShader();
            if (shader == null)
                return false;

            ui.readyFlashOriginalMaterial = ui.icon.material;
            ui.readyFlashMaterialInstance = new Material(shader)
            {
                name = $"{ui.icon.name}_ReadyFlash_MaterialInstance",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };

            if (ui.readyFlashOriginalMaterial != null && ui.readyFlashOriginalMaterial.HasProperty("_MainTex"))
                ui.readyFlashMaterialInstance.mainTexture = ui.readyFlashOriginalMaterial.mainTexture;

            ui.icon.material = ui.readyFlashMaterialInstance;
        }
        else if (ui.icon.material != ui.readyFlashMaterialInstance)
        {
            ui.icon.material = ui.readyFlashMaterialInstance;
        }

        Color flashColor = amount > 0.001f ? Color.white : ui.readyFlashColor;
        ui.readyFlashMaterialInstance.SetColor(FlashColorId, flashColor);
        ui.readyFlashMaterialInstance.SetFloat(FlashAmountId, amount);
        ui.readyFlashMaterialInstance.SetFloat(FlashMultiplyId, 1f);
        return true;
    }

    private static Shader ResolveReadyFlashShader()
    {
        if (readyFlashShader == null)
            readyFlashShader = Shader.Find("UI/Icon Ready Flash");

        return readyFlashShader;
    }

    private static Image ResolveOverlayImage(WeaponSkillHUD2D.SkillSlotUI ui)
    {
        if (ui == null)
            return null;

        if (ui.activeOverlayImage != null)
            return ui.activeOverlayImage;

        if (ui.activeOverlay == null)
            return null;

        return ui.activeOverlay.GetComponent<Image>();
    }

    private static bool IsAbilityActive(AbilitySystem abilitySystem, AbilityDefinition def)
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

    private static Color EvaluateActiveIconColor(
        Color normalIconColor,
        Color activeIconColor,
        float activePulseSpeed,
        float activePulseStrength)
    {
        float pulse = (Mathf.Sin(Time.unscaledTime * activePulseSpeed) + 1f) * 0.5f;
        float t = Mathf.Lerp(1f - activePulseStrength, 1f, pulse);
        return Color.Lerp(normalIconColor, activeIconColor, t);
    }

    private static bool TryApplyActiveDurationOverride(
        WeaponSkillHUD2D.SkillSlotUI ui,
        WeaponAbilitySlot slot,
        AbilityDefinition def,
        IWeaponAbilityHudDurationOverrideProvider provider)
    {
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
                : string.Empty;

        if (ui.chargeText != null)
            ui.chargeText.text = string.Empty;

        return true;
    }

    private static Image ResolveActiveDurationFill(WeaponSkillHUD2D.SkillSlotUI ui)
    {
        if (ui == null)
            return null;

        return ui.activeDurationFill != null ? ui.activeDurationFill : ui.cooldownFill;
    }

    private static void SetActiveDurationFillVisible(WeaponSkillHUD2D.SkillSlotUI ui, bool visible)
    {
        if (ui == null || ui.activeDurationFill == null || ui.activeDurationFill == ui.cooldownFill)
            return;

        if (ui.activeDurationFill.gameObject.activeSelf != visible)
            ui.activeDurationFill.gameObject.SetActive(visible);

        ui.activeDurationFill.enabled = visible;
        if (!visible)
            ui.activeDurationFill.fillAmount = 0f;
    }

    private static void WarnIfDurationOverrideFillInvisible(
        WeaponSkillHUD2D.SkillSlotUI ui,
        WeaponAbilitySlot slot,
        Image fill)
    {
        if (ui == null || ui.cooldownFillVisibilityWarningLogged)
            return;

        if (fill == null)
        {
            ui.cooldownFillVisibilityWarningLogged = true;
            Debug.LogWarning($"[WeaponSkillHudSlotPresenter] {slot} active duration override is active, but neither activeDurationFill nor cooldownFill is assigned.");
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
        Debug.LogWarning($"[WeaponSkillHudSlotPresenter] {slot} active duration override is active, but the resolved fill Image may be invisible. Check activeDurationFill/cooldownFill active state, alpha, sprite, and hierarchy order.");
    }

    private static void CaptureCooldownFillConfig(WeaponSkillHUD2D.SkillSlotUI ui)
    {
        if (ui == null || ui.cooldownFill == null || ui.cooldownFillConfigCaptured)
            return;

        ui.cooldownFillType = ui.cooldownFill.type;
        ui.cooldownFillMethod = ui.cooldownFill.fillMethod;
        ui.cooldownFillOrigin = ui.cooldownFill.fillOrigin;
        ui.cooldownFillConfigCaptured = true;
    }

    private static void RestoreCooldownFillConfig(WeaponSkillHUD2D.SkillSlotUI ui)
    {
        if (ui == null || ui.cooldownFill == null || !ui.cooldownFillConfigCaptured)
            return;

        ui.cooldownFill.type = ui.cooldownFillType;
        ui.cooldownFill.fillMethod = ui.cooldownFillMethod;
        ui.cooldownFill.fillOrigin = ui.cooldownFillOrigin;
    }
}

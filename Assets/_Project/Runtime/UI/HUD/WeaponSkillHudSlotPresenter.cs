using UnityEngine;
using UnityEngine.UI;
using UnityGAS;
using System;
using System.Globalization;
using System.Collections.Generic;

/// <summary>
/// 책임 :
/// - 무기 스킬 HUD 슬롯 하나의 아이콘, 입력 가이드, 쿨다운, 차지, 활성 지속시간 표시를 공통 갱신한다.
/// - 현재 무기 HUD와 교체 무기 HUD가 같은 슬롯 표현 규칙을 공유하게 한다.
/// </summary>
public static class WeaponSkillHudSlotPresenter
{
    private const float CooldownIconBrightness = 0.6f;
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashMultiplyId = Shader.PropertyToID("_FlashMultiply");
    private static Shader readyFlashShader;
    private static readonly Dictionary<(Shader Shader, Texture Texture), SharedIdleFlashMaterial> idleFlashMaterials = new();

    private sealed class SharedIdleFlashMaterial
    {
        public Material Material;
        public int ReferenceCount;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static int DiagnosticSharedIdleMaterialCount => idleFlashMaterials.Count;
#endif

    public static void ApplySlot(
        WeaponSkillHUD2D.SkillSlotUI ui,
        AbilityDefinition def,
        Color normalIconColor,
        InputBindingService inputBindingService)
    {
        if (ui == null) return;

        ui.cooldownDisplayText ??= new HudDisplayTextCache();
        ui.chargeDisplayText ??= new HudDisplayTextCache();

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

        ui.cooldownDisplayText.Clear();
        ui.chargeDisplayText.Clear();

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
        {
            // 기본 아이콘은 "다시 사용할 수 있는가"를 우선 표시한다.
            // 실행 중 강조는 activeOverlay가 담당하므로, 실제 쿨타임이 시작되면 즉시 어둡게 한다.
            if (IsAbilityUnavailableFromCooldown(abilitySystem, def))
            {
                ui.icon.color = EvaluateCooldownIconColor(normalIconColor);
            }
            else if (hasAbility && isActive)
            {
                ui.icon.color = EvaluateActiveIconColor(
                    normalIconColor,
                    activeIconColor,
                    activePulseSpeed,
                    activePulseStrength);
            }
            else
            {
                ui.icon.color = normalIconColor;
            }
        }
    }

    /// <summary>
    /// 책임:
    /// - 시전 강조를 표시하지 않는 교체 무기 HUD에서도 쿨타임 중인 스킬 아이콘을 같은 밝기로 낮춘다.
    /// - 알파는 유지하고 RGB만 60%로 낮춰 배경에 따라 아이콘이 흐려지는 현상을 피한다.
    /// </summary>
    public static void UpdateCooldownIconVisual(
        WeaponSkillHUD2D.SkillSlotUI ui,
        AbilityDefinition def,
        AbilitySystem abilitySystem,
        Color normalIconColor)
    {
        if (ui?.icon == null)
            return;

        ui.icon.color = IsAbilityUnavailableFromCooldown(abilitySystem, def)
            ? EvaluateCooldownIconColor(normalIconColor)
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

        ui.cooldownDisplayText ??= new HudDisplayTextCache();
        ui.chargeDisplayText ??= new HudDisplayTextCache();

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
                ui.cooldownText.text = charges < max && recharge > 0.01f ? ui.cooldownDisplayText.Decimal(recharge) : string.Empty;

            if (ui.chargeText != null)
                ui.chargeText.text = ui.chargeDisplayText.Charges(charges, max);

            UpdateReadyFlash(ui, ready, charges);
            return;
        }

        float remaining = abilitySystem.GetCooldownRemaining(def);
        bool cooldownReady = remaining <= 0.01f;

        if (ui.cooldownFill != null)
            ui.cooldownFill.fillAmount = Mathf.Clamp01(remaining / total);

        if (ui.cooldownText != null)
            ui.cooldownText.text = remaining > 0.01f ? ui.cooldownDisplayText.Decimal(remaining) : string.Empty;

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

        if (guideRoot != null && guideRoot.activeSelf != shouldShow)
            guideRoot.SetActive(shouldShow);

        if (!shouldShow || guideIcon == null)
            return;

        if (!guideIcon.enabled)
            guideIcon.enabled = true;
        RefreshInputGuideIcon(ui, inputBindingService);
    }

    // May run while the HUD is inactive: update content without changing visibility or skill state.
    public static void RefreshInputGuideIcon(
        WeaponSkillHUD2D.SkillSlotUI ui,
        InputBindingService inputBindingService)
    {
        if (ui == null || !ui.useInputGuide || ui.inputGuideIcon == null)
            return;

        Image guideIcon = ui.inputGuideIcon;
        Sprite bindingIcon = inputBindingService != null
            ? inputBindingService.GetBindingIcon(ui.inputActionId)
            : null;
        if (guideIcon.sprite != bindingIcon)
            guideIcon.sprite = bindingIcon;
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

    public static void CancelReadyFlash(WeaponSkillHUD2D.SkillSlotUI ui)
    {
        if (ui == null)
            return;

        ui.readyStateInitialized = false;
        ui.wasReady = false;
        ui.lastChargeCount = -1;
        ui.readyFlashRemaining = 0f;
        ApplyReadyFlashVisualCore(ui, 0f, useSharedIdleMaterial: false);
    }

    public static void ReleaseReadyFlashMaterial(WeaponSkillHUD2D.SkillSlotUI ui)
    {
        if (ui == null)
            return;

        CancelReadyFlash(ui);
        Material ownedMaterial = ui.readyFlashMaterialInstance;
        ui.readyFlashMaterialInstance = null;
        ui.readyFlashOriginalMaterial = null;
        ReleaseSharedIdleMaterial(ui);
        DestroyOwnedFlashMaterial(ownedMaterial);
    }

    private static void DestroyOwnedFlashMaterial(Material ownedMaterial)
    {
        if (ownedMaterial == null)
            return;
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(ownedMaterial);
        else
            UnityEngine.Object.DestroyImmediate(ownedMaterial);
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
        ApplyReadyFlashVisualCore(ui, amount, useSharedIdleMaterial: true);
    }

    private static void ApplyReadyFlashVisualCore(WeaponSkillHUD2D.SkillSlotUI ui, float amount, bool useSharedIdleMaterial)
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

        if (TryApplyReadyFlashMaterialCore(ui, amount, useSharedIdleMaterial))
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
        return TryApplyReadyFlashMaterialCore(ui, amount, useSharedIdleMaterial: true);
    }

    private static bool TryApplyReadyFlashMaterialCore(WeaponSkillHUD2D.SkillSlotUI ui, float amount, bool useSharedIdleMaterial)
    {
        if (ui == null || ui.icon == null)
            return false;

        if (amount <= 0f)
        {
            if (!useSharedIdleMaterial)
            {
                RestoreReadyFlashOriginalMaterial(ui);
                return ui.readyFlashMaterialInstance != null || ui.readyFlashIdleMaterial != null || ResolveReadyFlashShader() != null;
            }

            if (ui.readyFlashIdleMaterialLeased && ui.readyFlashIdleMaterial != null && ui.icon.material == ui.readyFlashIdleMaterial)
                return true;

            Shader idleShader = ResolveReadyFlashShader();
            if (idleShader == null)
                return false;

            CaptureReadyFlashOriginalMaterial(ui);
            Material idleMaterial = RetainSharedIdleMaterial(ui, idleShader);
            if (ui.readyFlashMaterialInstance != null && ui.icon.material == ui.readyFlashMaterialInstance)
                ui.readyFlashMaterialInstance.SetFloat(FlashAmountId, 0f);
            if (ui.icon.material != idleMaterial)
                ui.icon.material = idleMaterial;
            return true;
        }

        if (ui.readyFlashMaterialInstance == null)
        {
            Shader shader = ResolveReadyFlashShader();
            if (shader == null)
                return false;

            ui.readyFlashMaterialInstance = new Material(shader)
            {
                name = $"{ui.icon.name}_ReadyFlash_MaterialInstance",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
        }

        if (ui.icon.material != ui.readyFlashMaterialInstance)
        {
            CaptureReadyFlashOriginalMaterial(ui);
            ui.readyFlashMaterialInstance.mainTexture =
                ui.readyFlashOriginalMaterial != null && ui.readyFlashOriginalMaterial.HasProperty("_MainTex")
                    ? ui.readyFlashOriginalMaterial.mainTexture
                    : Texture2D.whiteTexture;
            ui.icon.material = ui.readyFlashMaterialInstance;
        }

        Color flashColor = amount > 0.001f ? Color.white : ui.readyFlashColor;
        ui.readyFlashMaterialInstance.SetColor(FlashColorId, flashColor);
        ui.readyFlashMaterialInstance.SetFloat(FlashAmountId, amount);
        ui.readyFlashMaterialInstance.SetFloat(FlashMultiplyId, 1f);
        return true;
    }

    private static void CaptureReadyFlashOriginalMaterial(WeaponSkillHUD2D.SkillSlotUI ui)
    {
        Material current = ui.icon.material;
        if (current != ui.readyFlashMaterialInstance && current != ui.readyFlashIdleMaterial)
            ui.readyFlashOriginalMaterial = current;
    }

    private static void RestoreReadyFlashOriginalMaterial(WeaponSkillHUD2D.SkillSlotUI ui)
    {
        Material current = ui.icon.material;
        if (ui.readyFlashMaterialInstance != null && current == ui.readyFlashMaterialInstance)
            ui.readyFlashMaterialInstance.SetFloat(FlashAmountId, 0f);
        // Borrowed material changes remain owned by their external presentation writer.
        if ((ui.readyFlashMaterialInstance != null && current == ui.readyFlashMaterialInstance) ||
            (ui.readyFlashIdleMaterial != null && current == ui.readyFlashIdleMaterial))
            ui.icon.material = ui.readyFlashOriginalMaterial;
    }

    private static Material RetainSharedIdleMaterial(WeaponSkillHUD2D.SkillSlotUI ui, Shader shader)
    {
        Texture texture = ui.readyFlashOriginalMaterial != null && ui.readyFlashOriginalMaterial.HasProperty("_MainTex")
            ? ui.readyFlashOriginalMaterial.mainTexture
            : Texture2D.whiteTexture;
        if (ui.readyFlashIdleMaterialLeased && ui.readyFlashIdleMaterial != null &&
            ui.readyFlashIdleShader == shader && ui.readyFlashIdleTexture == texture)
            return ui.readyFlashIdleMaterial;

        ReleaseSharedIdleMaterial(ui);
        var key = (shader, texture);
        if (!idleFlashMaterials.TryGetValue(key, out SharedIdleFlashMaterial entry) || entry.Material == null)
        {
            var idleMaterial = new Material(shader)
            {
                name = "UI_ReadyFlash_SharedIdle",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                mainTexture = texture,
            };
            // Keep the original ready shader's alpha blend even with no visible flash.
            idleMaterial.SetColor(FlashColorId, Color.white);
            idleMaterial.SetFloat(FlashAmountId, 0f);
            idleMaterial.SetFloat(FlashMultiplyId, 1f);
            entry = new SharedIdleFlashMaterial { Material = idleMaterial };
            idleFlashMaterials[key] = entry;
        }

        entry.ReferenceCount++;
        ui.readyFlashIdleMaterial = entry.Material;
        ui.readyFlashIdleShader = shader;
        ui.readyFlashIdleTexture = texture;
        ui.readyFlashIdleMaterialLeased = true;
        return entry.Material;
    }

    private static void ReleaseSharedIdleMaterial(WeaponSkillHUD2D.SkillSlotUI ui)
    {
        if (!ui.readyFlashIdleMaterialLeased)
            return;

        var key = (ui.readyFlashIdleShader, ui.readyFlashIdleTexture);
        Material material = ui.readyFlashIdleMaterial;
        ui.readyFlashIdleMaterial = null;
        ui.readyFlashIdleShader = null;
        ui.readyFlashIdleTexture = null;
        ui.readyFlashIdleMaterialLeased = false;
        if (idleFlashMaterials.TryGetValue(key, out SharedIdleFlashMaterial entry) &&
            ReferenceEquals(entry.Material, material) && --entry.ReferenceCount == 0)
        {
            idleFlashMaterials.Remove(key);
            DestroyOwnedFlashMaterial(entry.Material);
        }
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

    private static bool IsAbilityUnavailableFromCooldown(AbilitySystem abilitySystem, AbilityDefinition def)
    {
        if (abilitySystem == null || def == null)
            return false;

        if (def.useCharges)
            return abilitySystem.GetChargesRemaining(def) <= 0;

        return abilitySystem.GetCooldownRemaining(def) > 0f;
    }

    private static Color EvaluateCooldownIconColor(Color normalIconColor)
    {
        return new Color(
            normalIconColor.r * CooldownIconBrightness,
            normalIconColor.g * CooldownIconBrightness,
            normalIconColor.b * CooldownIconBrightness,
            normalIconColor.a);
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
                ? ui.cooldownDisplayText.Decimal(duration.RemainingSeconds)
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
            CapstoneDiagnostics.EditorOnlyLog.LogWarning($"[WeaponSkillHudSlotPresenter] {slot} active duration override is active, but neither activeDurationFill nor cooldownFill is assigned.");
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
        CapstoneDiagnostics.EditorOnlyLog.LogWarning($"[WeaponSkillHudSlotPresenter] {slot} active duration override is active, but the resolved fill Image may be invisible. Check activeDurationFill/cooldownFill active state, alpha, sprite, and hierarchy order.");
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

// Use the runtime's numeric formatter, including its midpoint/culture rules, without
// allocating another string while the displayed characters are unchanged.
internal sealed class HudDisplayTextCache
{
    private readonly char[] buffer = new char[64];
    private string cachedValue;

    public void Clear() => cachedValue = null;

    public string Decimal(float value, string suffix = null, IFormatProvider provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        return value.TryFormat(buffer, out int written, "0.0", provider)
            ? Store(written, suffix)
            : Store(value.ToString("0.0", provider) + suffix);
    }

    public string Integer(int value, IFormatProvider provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        return value.TryFormat(buffer, out int written, default, provider)
            ? Store(written, null)
            : Store(value.ToString(provider));
    }

    public string Charges(int value, int maximum)
    {
        IFormatProvider provider = CultureInfo.CurrentCulture;
        if (!value.TryFormat(buffer, out int written, default, provider) || written >= buffer.Length)
            return Store($"{value}/{maximum}");
        buffer[written++] = '/';
        if (!maximum.TryFormat(buffer.AsSpan(written), out int maximumWritten, default, provider))
            return Store($"{value}/{maximum}");
        return Store(written + maximumWritten, null);
    }

    private string Store(int written, string suffix)
    {
        int suffixLength = suffix?.Length ?? 0;
        if (written + suffixLength > buffer.Length)
            return Store(new string(buffer, 0, written) + suffix);
        for (int i = 0; i < suffixLength; i++)
            buffer[written + i] = suffix[i];
        written += suffixLength;
        if (cachedValue != null && cachedValue.Length == written)
        {
            int i = 0;
            for (; i < written && cachedValue[i] == buffer[i]; i++) { }
            if (i == written) return cachedValue;
        }
        cachedValue = new string(buffer, 0, written);
        return cachedValue;
    }

    private string Store(string value)
    {
        if (cachedValue != value) cachedValue = value;
        return cachedValue;
    }
}

// Shared hover session for equipped and reserve weapon skill icons.
internal sealed class WeaponSkillHudTooltipPresenter
{
    private StatusHudTooltipView skillTooltip;
    private RectTransform hoveredSkillRect;
    private AbilityDefinition hoveredSkill;

    public void Update(WeaponInventory2D inventory, bool available,
        WeaponSkillHUD2D.SkillSlotUI skill1UI, AbilityDefinition skill1Def,
        WeaponSkillHUD2D.SkillSlotUI skill2UI, AbilityDefinition skill2Def)
    {
        UIManager manager = UIManager.Instance;
        if (!available || inventory == null ||
            MonsterSpawnRoomGroup.IsPlayerInCombat || manager == null || manager.HasBlockingUI())
        {
            Hide();
            return;
        }

        WeaponSkillHUD2D.SkillSlotUI slot = IsPointerOverSkill(skill1UI, skill1Def) ? skill1UI :
            IsPointerOverSkill(skill2UI, skill2Def) ? skill2UI : null;
        AbilityDefinition ability = slot == skill1UI ? skill1Def : slot == skill2UI ? skill2Def : null;
        if (slot == null || ability == null)
        {
            Hide();
            return;
        }
        if (hoveredSkillRect == slot.icon.rectTransform && hoveredSkill == ability &&
            skillTooltip != null && skillTooltip.IsActive) return;

        Hide();
        // Use the existing authored tooltip; never create the fallback UI hierarchy.
        if (GlobalUIRoot.GetStatusTooltipPrefab() == null) return;
        skillTooltip = StatusHudTooltipView.Instance;
        hoveredSkillRect = slot.icon.rectTransform;
        hoveredSkill = ability;
        manager.ShowHover(skillTooltip, hoveredSkillRect, ability, ItemDetailContext.FromOwner(inventory.gameObject));
    }

    private static bool IsPointerOverSkill(WeaponSkillHUD2D.SkillSlotUI slot, AbilityDefinition ability)
    {
        Image icon = slot?.icon;
        if (ability == null || icon == null || !icon.isActiveAndEnabled || icon.color.a <= 0.01f ||
            icon.canvasRenderer.GetInheritedAlpha() <= 0.01f) return false;
        Canvas canvas = icon.canvas;
        if (canvas == null || !canvas.isActiveAndEnabled || !canvas.rootCanvas.isActiveAndEnabled) return false;
        Camera camera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(icon.rectTransform, Input.mousePosition, camera);
    }

    public void Hide()
    {
        if (skillTooltip != null && hoveredSkillRect != null && UIManager.Instance != null)
            UIManager.Instance.HideHover(skillTooltip, hoveredSkillRect, immediate: true);
        hoveredSkillRect = null;
        hoveredSkill = null;
    }

}

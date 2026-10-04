using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

[Serializable]
public sealed class LocalizedTmpFont : LocalizedAsset<TMP_FontAsset> { }

[Serializable]
public sealed class TmpFontAssetEvent : UnityEvent<TMP_FontAsset> { }

// TMP adapter for the package's Asset Table event; no independent locale state.
[AddComponentMenu("Localization/Asset/Localize TMP Font Event")]
public sealed class LocalizeTmpFontEvent : LocalizedAssetEvent<TMP_FontAsset, LocalizedTmpFont, TmpFontAssetEvent>
{
    [SerializeField] private Material authoredMaterial;
    private TMP_Text target;
    private Material retainedMaterial;
    private bool layoutPending;

    public Material AuthoredMaterial => authoredMaterial;
    public void SetAuthoredMaterial(Material material) => authoredMaterial = material;

    protected override void OnEnable()
    {
        target = GetComponent<TMP_Text>();
        if (authoredMaterial == null && target != null) authoredMaterial = target.fontSharedMaterial;
        base.OnEnable();
    }

    // Persistent event target: the package still owns locale loading and dispatch.
    public void ApplyLocalizedFont(TMP_FontAsset font)
    {
        target ??= GetComponent<TMP_Text>();
        if (target == null || font == null) return;
        if (authoredMaterial == null) authoredMaterial = target.fontSharedMaterial;
        Material previous = retainedMaterial;
        Material matched = authoredMaterial == null ? font.material :
            TMP_MaterialManager.GetFallbackMaterial(authoredMaterial, font.material);
        if (matched != previous) TMP_MaterialManager.AddFallbackMaterialReference(matched);
        target.font = font;
        target.fontSharedMaterial = matched;
        // TMP outline setters can leave a CanvasRenderer texture override from the old font.
        // UpdateMaterial changes the material but does not replace that override.
        if (target is TextMeshProUGUI ui) ui.canvasRenderer.SetTexture(matched.mainTexture);
        retainedMaterial = matched;
        if (previous != null && previous != matched) TMP_MaterialManager.ReleaseFallbackMaterial(previous);
        target.UpdateMeshPadding();
        layoutPending = true;
    }

    private void LateUpdate()
    {
        if (!layoutPending || target == null) return;
        layoutPending = false;
        target.ForceMeshUpdate();
        if (target.transform.parent is RectTransform parent)
            UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(parent);
    }

    protected override void OnDisable()
    {
        layoutPending = false;
        base.OnDisable();
    }

    private void OnDestroy()
    {
        base.OnDisable();
        if (retainedMaterial != null) TMP_MaterialManager.ReleaseFallbackMaterial(retainedMaterial);
        retainedMaterial = null;
    }
}

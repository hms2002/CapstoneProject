using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Projects existing authored render anchors into same-Canvas layers. Layout slots,
/// logical controllers and target Graphic/Button objects retain their original owners.
/// No hierarchy or material is created at runtime.
/// </summary>
[DisallowMultipleComponent]
public sealed class UiRenderLayerLayout : MonoBehaviour
{
    [Serializable]
    public sealed class Binding
    {
        public RectTransform layoutSlot;
        public RectTransform renderAnchor;
        public GameObject target;
        public int layer;
    }

    [SerializeField] private RectTransform[] renderLayers = Array.Empty<RectTransform>();
    [SerializeField] private Binding[] bindings = Array.Empty<Binding>();
    private UiRenderLayerLayout destination;
    private bool attached;
    private bool subscribed;
    public int DiagnosticTransformWrites { get; private set; }
    public int BindingCount => bindings != null ? bindings.Length : 0;

    private void LateUpdate() => SynchronizeVisibility();

    private void OnEnable()
    {
        if (destination == null && renderLayers != null && renderLayers.Length > 0)
            destination = this;
        Attach();
        // Initialize the package registry before subscribing, so projection follows
        // its layout pass even on the first opened frame.
        _ = CanvasUpdateRegistry.instance;
        Canvas.willRenderCanvases += Project;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (subscribed) Canvas.willRenderCanvases -= Project;
        subscribed = false;
        Restore();
    }

    private void OnDestroy()
    {
        if (subscribed) Canvas.willRenderCanvases -= Project;
        // A row destroyed before first activation may still own detached anchors.
        if (bindings == null) return;
        foreach (Binding binding in bindings)
            if (binding != null && binding.renderAnchor != null && !binding.renderAnchor.IsChildOf(transform))
                Destroy(binding.renderAnchor.gameObject);
    }

    public void BindTo(UiRenderLayerLayout owner)
    {
        if (destination == owner && attached) return;
        Restore();
        destination = owner;
        if (isActiveAndEnabled) Attach();
    }

    private void Attach()
    {
        if (attached || destination == null || bindings == null) return;
        foreach (Binding binding in bindings)
        {
            if (!Valid(binding)) continue;
            // Keep hidden logical owners inactive while moving into an active layer;
            // otherwise child locale/presentation callbacks briefly run before Project.
            bool visible = binding.layoutSlot.gameObject.activeInHierarchy;
            if (binding.renderAnchor.gameObject.activeSelf != visible)
                binding.renderAnchor.gameObject.SetActive(visible);
            binding.renderAnchor.SetParent(destination.renderLayers[binding.layer], false);
            DiagnosticTransformWrites++;
        }
        attached = true;
        Project();
    }

    private bool Valid(Binding binding) => binding != null && binding.layoutSlot != null && binding.renderAnchor != null &&
        binding.target != null && destination != null && destination.renderLayers != null && binding.layer >= 0 &&
        binding.layer < destination.renderLayers.Length && destination.renderLayers[binding.layer] != null;

    private void Project()
    {
        if (!attached || !isActiveAndEnabled || bindings == null) return;
        SynchronizeVisibility();
        foreach (Binding binding in bindings)
        {
            if (!Valid(binding)) continue;
            RectTransform slot = binding.layoutSlot, anchor = binding.renderAnchor;
            // Preserve the original layout participation when glyph/secondary state changes.
            bool visible = slot.gameObject.activeInHierarchy;
            if (!visible) continue;

            Vector2 centerAnchor = new Vector2(.5f, .5f);
            if (anchor.anchorMin != centerAnchor) { anchor.anchorMin = centerAnchor; DiagnosticTransformWrites++; }
            if (anchor.anchorMax != centerAnchor) { anchor.anchorMax = centerAnchor; DiagnosticTransformWrites++; }
            if (anchor.pivot != slot.pivot) { anchor.pivot = slot.pivot; DiagnosticTransformWrites++; }
            Vector2 size = slot.rect.size;
            if ((anchor.sizeDelta - size).sqrMagnitude > 0.000001f) { anchor.sizeDelta = size; DiagnosticTransformWrites++; }
            Vector3 position = slot.position;
            Quaternion rotation = slot.rotation;
            if ((anchor.position - position).sqrMagnitude > 0.000001f || Quaternion.Angle(anchor.rotation, rotation) > .0001f)
            { anchor.SetPositionAndRotation(position, rotation); DiagnosticTransformWrites++; }
            Vector3 parentScale = anchor.parent != null ? anchor.parent.lossyScale : Vector3.one;
            Vector3 sourceScale = slot.lossyScale;
            Vector3 scale = new Vector3(Ratio(sourceScale.x, parentScale.x), Ratio(sourceScale.y, parentScale.y), Ratio(sourceScale.z, parentScale.z));
            if ((anchor.localScale - scale).sqrMagnitude > 0.000001f) { anchor.localScale = scale; DiagnosticTransformWrites++; }
        }
    }

    private void SynchronizeVisibility()
    {
        if (!attached || !isActiveAndEnabled || bindings == null) return;
        foreach (Binding binding in bindings)
        {
            if (!Valid(binding)) continue;
            bool active = binding.target.activeSelf;
            if (binding.layoutSlot.gameObject.activeSelf != active) binding.layoutSlot.gameObject.SetActive(active);
            bool visible = binding.layoutSlot.gameObject.activeInHierarchy;
            if (binding.renderAnchor.gameObject.activeSelf != visible) binding.renderAnchor.gameObject.SetActive(visible);
        }
    }

    private void Restore()
    {
        if (!attached || bindings == null) return;
        // Child render nodes return before the Button/control anchors containing their slots.
        for (int i = bindings.Length - 1; i >= 0; i--)
        {
            Binding binding = bindings[i];
            if (binding == null || binding.layoutSlot == null || binding.renderAnchor == null) continue;
            RectTransform anchor = binding.renderAnchor;
            anchor.SetParent(binding.layoutSlot, false);
            anchor.anchorMin = Vector2.zero; anchor.anchorMax = Vector2.one;
            anchor.offsetMin = Vector2.zero; anchor.offsetMax = Vector2.zero;
            anchor.pivot = binding.layoutSlot.pivot; anchor.localScale = Vector3.one; anchor.localRotation = Quaternion.identity;
            Vector3 position = anchor.localPosition; position.z = 0f; anchor.localPosition = position;
            if (!anchor.gameObject.activeSelf) anchor.gameObject.SetActive(true);
            DiagnosticTransformWrites++;
        }
        attached = false;
    }

    private static float Ratio(float value, float parent) => Mathf.Abs(parent) > .00001f ? value / parent : 1f;

#if UNITY_EDITOR
    public void ConfigureAuthored(RectTransform[] layers, Binding[] entries)
    {
        renderLayers = layers ?? Array.Empty<RectTransform>();
        bindings = entries ?? Array.Empty<Binding>();
    }
#endif
}

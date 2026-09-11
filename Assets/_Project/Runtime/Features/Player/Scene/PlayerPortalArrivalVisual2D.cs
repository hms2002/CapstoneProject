using System.Collections.Generic;
using UnityEngine;

/// <summary>Offsets only authored player/weapon visuals for portal falls and restores their poses and visibility on cancellation.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class PlayerPortalArrivalVisual2D : MonoBehaviour
{
    [SerializeField] private Transform[] liftedRoots;
    [SerializeField] private Transform shadow;
    private Vector3[] basePositions;
    private Vector3 shadowScale;
    private readonly Dictionary<Renderer, bool> visibility = new();
    private bool playing;
    private float height;
    private float shadowFactor = 1f;
    public bool IsConfigured => liftedRoots != null && liftedRoots.Length > 0 && liftedRoots[0] != null;

    public bool Begin()
    {
        if (playing || !IsConfigured) return false;
        basePositions = new Vector3[liftedRoots.Length];
        for (int i = 0; i < liftedRoots.Length; i++)
        {
            Transform root = liftedRoots[i];
            if (root == null) continue;
            // Authoring must never move the physics root or its colliders.
            if (root == transform || root.GetComponentInChildren<Collider2D>(true) != null) return false;
            basePositions[i] = root.localPosition;
        }
        visibility.Clear();
        foreach (Transform root in liftedRoots)
            if (root != null)
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    visibility[renderer] = renderer.forceRenderingOff;
        if (shadow != null)
        {
            shadowScale = shadow.localScale;
            foreach (Renderer renderer in shadow.GetComponentsInChildren<Renderer>(true))
                visibility[renderer] = renderer.forceRenderingOff;
        }
        playing = true;
        return true;
    }

    public void SetVisible(bool value)
    {
        foreach (var pair in visibility)
            if (pair.Key != null) pair.Key.forceRenderingOff = !value || pair.Value;
    }

    public void SetHeight(float value, float normalizedHeight)
    {
        height = Mathf.Max(0f, value);
        shadowFactor = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(normalizedHeight));
        ApplyPose();
    }

    private void LateUpdate() { if (playing) ApplyPose(); }

    private void ApplyPose()
    {
        if (!playing) return;
        for (int i = 0; i < liftedRoots.Length; i++)
            if (liftedRoots[i] != null)
                liftedRoots[i].localPosition = basePositions[i] +
                    liftedRoots[i].parent.InverseTransformVector(Vector3.up * height);
        if (shadow != null) shadow.localScale = shadowScale * shadowFactor;
    }

    public void Restore()
    {
        if (!playing) return;
        height = 0f;
        shadowFactor = 1f;
        ApplyPose();
        SetVisible(true);
        visibility.Clear();
        playing = false;
    }

    private void OnDisable() => Restore();
#if UNITY_EDITOR
    public void EditorConfigure(Transform[] roots, Transform shadowRoot) { liftedRoots = roots; shadow = shadowRoot; }
#endif
}

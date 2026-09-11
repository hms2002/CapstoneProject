using System.Collections.Generic;
using UnityEngine;

/// <summary>Temporarily hides authored player, weapon and shadow renderers until a portal opens. The shared Hub presentation owns fall poses.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class PlayerPortalArrivalVisual2D : MonoBehaviour
{
    [SerializeField] private Transform[] liftedRoots;
    [SerializeField] private Transform shadow;
    private readonly Dictionary<Renderer, bool> visibility = new();
    private bool playing;
    public bool IsConfigured => liftedRoots != null && liftedRoots.Length > 0 && liftedRoots[0] != null;

    public bool Begin()
    {
        if (playing || !IsConfigured) return false;
        visibility.Clear();
        foreach (Transform root in liftedRoots)
            if (root != null)
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    visibility[renderer] = renderer.forceRenderingOff;
        if (shadow != null)
        {
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

    public void Restore()
    {
        if (!playing) return;
        SetVisible(true);
        visibility.Clear();
        playing = false;
    }

    private void OnDisable() => Restore();
#if UNITY_EDITOR
    public void EditorConfigure(Transform[] roots, Transform shadowRoot) { liftedRoots = roots; shadow = shadowRoot; }
#endif
}

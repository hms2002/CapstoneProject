using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the visual instance's outline targets and authored sorting policy.
/// </summary>
[DisallowMultipleComponent]
public sealed class ItemDisplayVisualInstance2D : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] outlineRenderers;
    [SerializeField] private bool preserveAuthoredSorting;

    public bool PreserveAuthoredSorting => preserveAuthoredSorting;

    public SpriteRenderer[] ResolveOutlineRenderers()
    {
        if (outlineRenderers == null || outlineRenderers.Length == 0)
            return GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

        List<SpriteRenderer> validRenderers = new(outlineRenderers.Length);
        for (int i = 0; i < outlineRenderers.Length; i++)
        {
            if (outlineRenderers[i] != null)
                validRenderers.Add(outlineRenderers[i]);
        }

        return validRenderers.Count > 0
            ? validRenderers.ToArray()
            : GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
    }
}

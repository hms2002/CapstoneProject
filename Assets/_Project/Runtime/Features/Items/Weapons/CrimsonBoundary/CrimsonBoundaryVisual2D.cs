using UnityEngine;

/// <summary>Plays the authored Crimson sprite sequence and releases its transient ownership.</summary>
[DisallowMultipleComponent]
public sealed class CrimsonBoundaryVisual2D : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(0.01f)] private float duration = 0.36f;
    [SerializeField] private bool loop;
    private float elapsed;
    private CrimsonBoundaryRuntimeState owner;

    public static CrimsonBoundaryVisual2D Spawn(CrimsonBoundaryVisual2D prefab, Vector3 position,
        Quaternion rotation, CrimsonBoundaryRuntimeState owner = null)
    {
        if (prefab == null) return null;
        var visual = Instantiate(prefab, position, rotation);
        visual.owner = owner;
        owner?.Register(visual.gameObject);
        return visual;
    }

    private void OnEnable()
    {
        elapsed = 0f;
        ApplyFrame();
    }

    private void Update()
    {
        elapsed += TimeScalePausePlayback.PresentationDeltaTime;
        if (!loop && elapsed >= duration)
        {
            Destroy(gameObject);
            return;
        }
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (spriteRenderer == null || frames == null || frames.Length == 0) return;
        float progress = elapsed / Mathf.Max(0.01f, duration);
        if (loop) progress = Mathf.Repeat(progress, 1f);
        spriteRenderer.sprite = frames[Mathf.Clamp(Mathf.FloorToInt(progress * frames.Length), 0, frames.Length - 1)];
    }

    private void OnDestroy()
    {
        if (owner != null) owner.Forget(gameObject);
    }
}

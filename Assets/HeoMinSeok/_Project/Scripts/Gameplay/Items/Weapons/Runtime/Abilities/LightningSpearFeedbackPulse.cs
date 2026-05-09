using UnityEngine;

[DisallowMultipleComponent]
public sealed class LightningSpearFeedbackPulse : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] spriteRenderers;
    [SerializeField, Min(0f)] private float scaleAmplitude = 0.04f;
    [SerializeField, Min(0.01f)] private float pulseSpeed = 5f;
    [SerializeField, Range(0f, 2f)] private float minAlphaMultiplier = 0.7f;
    [SerializeField, Range(0f, 2f)] private float maxAlphaMultiplier = 1f;
    [SerializeField] private bool useUnscaledTime;

    private Vector3 baseLocalScale;
    private Color[] baseColors;
    private bool cached;

    private void Awake()
    {
        CacheBaseState();
    }

    private void OnEnable()
    {
        CacheBaseState();
    }

    private void OnDisable()
    {
        ResetVisuals();
    }

    private void Update()
    {
        CacheBaseState();

        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        float phase = (Mathf.Sin(time * pulseSpeed) + 1f) * 0.5f;
        float scale = 1f + scaleAmplitude * phase;
        float alphaMultiplier = Mathf.Lerp(minAlphaMultiplier, maxAlphaMultiplier, phase);

        transform.localScale = baseLocalScale * scale;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            Color color = baseColors[i];
            color.a *= alphaMultiplier;
            spriteRenderer.color = color;
        }
    }

    private void CacheBaseState()
    {
        if (cached)
            return;

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        baseLocalScale = transform.localScale;
        baseColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
            baseColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;

        cached = true;
    }

    private void ResetVisuals()
    {
        if (!cached)
            return;

        transform.localScale = baseLocalScale;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = baseColors[i];
        }
    }
}

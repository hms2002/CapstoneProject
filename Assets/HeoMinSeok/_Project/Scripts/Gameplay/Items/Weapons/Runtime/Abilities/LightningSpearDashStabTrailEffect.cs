using System.Collections;
using UnityEngine;

/// <summary>
/// 번개 창 표식 돌진 경로와 도착 충격의 짧은 시각 효과를 재생하고 정리할 책임을 가집니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LightningSpearDashStabTrailEffect : MonoBehaviour
{
    [Header("Renderers")]
    [SerializeField] private SpriteRenderer startCapRenderer;
    [SerializeField] private SpriteRenderer middleRenderer;
    [SerializeField] private SpriteRenderer endCapRenderer;
    [SerializeField] private SpriteRenderer impactRenderer;
    [SerializeField] private SpriteRenderer[] fadeRenderers;

    [Header("Layout")]
    [SerializeField, Min(0f)] private float middleHeight = 0.38f;
    [SerializeField, Min(0f)] private float capInset = 0.14f;
    [SerializeField, Min(0f)] private float impactOffset = 0.08f;

    [Header("Lifetime")]
    [SerializeField, Min(0.01f)] private float lifetimeSeconds = 0.16f;
    [SerializeField, Min(0f)] private float fadeStartDelay = 0.035f;

    private Coroutine lifetimeRoutine;
    private Color[] baseColors;

    public void Play(Vector2 start, Vector2 end)
    {
        if (!IsFinite(start) || !IsFinite(end))
        {
            Destroy(gameObject);
            return;
        }

        Vector2 delta = end - start;
        float distance = delta.magnitude;
        Vector2 direction = distance > 0.0001f ? delta / distance : Vector2.right;
        Vector2 midpoint = (start + end) * 0.5f;

        transform.position = new Vector3(midpoint.x, midpoint.y, transform.position.z);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        ApplyLayout(Mathf.Max(0.01f, distance));
        CacheFadeRenderers();
        CaptureBaseColors();
        ApplyAlphaMultiplier(1f);

        if (lifetimeRoutine != null)
            StopCoroutine(lifetimeRoutine);

        lifetimeRoutine = StartCoroutine(CoLifetime());
    }

    private void ApplyLayout(float distance)
    {
        float halfDistance = distance * 0.5f;
        float safeInset = Mathf.Min(capInset, halfDistance);

        SetLocalPosition(startCapRenderer, -halfDistance + safeInset);
        SetLocalPosition(endCapRenderer, halfDistance - safeInset);
        SetLocalPosition(impactRenderer, halfDistance + impactOffset);

        if (middleRenderer != null)
        {
            middleRenderer.transform.localPosition = Vector3.zero;
            middleRenderer.size = new Vector2(Mathf.Max(0.01f, distance - safeInset * 2f), middleHeight);
        }
    }

    private static void SetLocalPosition(SpriteRenderer renderer, float x)
    {
        if (renderer == null)
            return;

        renderer.transform.localPosition = new Vector3(x, 0f, 0f);
    }

    private IEnumerator CoLifetime()
    {
        float delay = Mathf.Min(fadeStartDelay, lifetimeSeconds);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float fadeDuration = Mathf.Max(0.01f, lifetimeSeconds - delay);
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / fadeDuration);
            ApplyAlphaMultiplier(1f - normalized);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void CacheFadeRenderers()
    {
        if (fadeRenderers == null || fadeRenderers.Length == 0)
            fadeRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void CaptureBaseColors()
    {
        if (fadeRenderers == null)
        {
            baseColors = null;
            return;
        }

        if (baseColors == null || baseColors.Length != fadeRenderers.Length)
            baseColors = new Color[fadeRenderers.Length];

        for (int i = 0; i < fadeRenderers.Length; i++)
            baseColors[i] = fadeRenderers[i] != null ? fadeRenderers[i].color : Color.clear;
    }

    private void ApplyAlphaMultiplier(float multiplier)
    {
        if (fadeRenderers == null || baseColors == null)
            return;

        for (int i = 0; i < fadeRenderers.Length && i < baseColors.Length; i++)
        {
            SpriteRenderer renderer = fadeRenderers[i];
            if (renderer == null)
                continue;

            Color color = baseColors[i];
            color.a *= Mathf.Clamp01(multiplier);
            renderer.color = color;
        }
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y);
    }
}

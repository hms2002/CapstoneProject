using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class WitchShieldVisualController : MonoBehaviour
{
    // 이 클래스의 책임:
    // 마녀 보호막 단계값을 읽어 외곽선 보호막과 파괴 시각 효과를 표현한다.

    [SerializeField] private WitchShieldController shieldController;
    [SerializeField] private SpriteRenderer ownerSpriteRenderer;
    [SerializeField] private float radiusX = 1.25f;
    [SerializeField] private float radiusY = 1.65f;
    [SerializeField] private float lineWidth = 0.12f;
    [SerializeField] private int segmentCount = 40;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.15f, 0f);

    private LineRenderer lineRenderer;
    private Coroutine breakRoutine;
    private int lastCurrentStage;
    private int lastMaxStage;

    private void Awake()
    {
        if (shieldController == null)
            shieldController = GetComponent<WitchShieldController>();

        if (ownerSpriteRenderer == null)
            ownerSpriteRenderer = GetComponent<SpriteRenderer>();

        EnsureLineRenderer();
        ApplySorting();
        HideImmediate();
    }

    private void OnEnable()
    {
        if (shieldController == null)
            return;

        shieldController.ShieldStageChanged += OnShieldStageChanged;
        shieldController.ShieldBroken += OnShieldBroken;
        SyncFromController();
    }

    private void OnDisable()
    {
        if (shieldController == null)
            return;

        shieldController.ShieldStageChanged -= OnShieldStageChanged;
        shieldController.ShieldBroken -= OnShieldBroken;
    }

    private void LateUpdate()
    {
        if (lineRenderer == null || !lineRenderer.enabled || lastCurrentStage <= 0)
            return;

        float ratio = lastMaxStage > 0 ? (float)lastCurrentStage / lastMaxStage : 0f;
        float pulse = 0.88f + (Mathf.Sin(Time.time * 5.5f) * 0.08f);
        Color color = GetShieldColor(ratio);
        color.a *= pulse;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }

    /// <summary>
    /// 책임 :
    /// - 현재 보호막 상태를 비주얼 상태와 즉시 동기화한다.
    /// </summary>
    private void SyncFromController()
    {
        if (shieldController == null)
        {
            HideImmediate();
            return;
        }

        lastCurrentStage = shieldController.CurrentShieldStage;
        lastMaxStage = Mathf.Max(1, shieldController.MaxShieldStage);

        if (shieldController.HasShield)
            ShowShield(lastCurrentStage, lastMaxStage);
        else
            HideImmediate();
    }

    /// <summary>
    /// 책임 :
    /// - 보호막 단계값 변화에 맞춰 색과 두께를 갱신한다.
    /// </summary>
    private void OnShieldStageChanged(int currentStage, int maxStage)
    {
        lastCurrentStage = currentStage;
        lastMaxStage = Mathf.Max(1, maxStage);

        if (currentStage > 0)
            ShowShield(currentStage, lastMaxStage);
        else
            HideImmediate();
    }

    /// <summary>
    /// 책임 :
    /// - 보호막이 파괴될 때 짧은 확산/소멸 연출을 재생한다.
    /// </summary>
    private void OnShieldBroken()
    {
        if (breakRoutine != null)
            StopCoroutine(breakRoutine);

        breakRoutine = StartCoroutine(PlayBreakRoutine());
    }

    private void ShowShield(int currentStage, int maxStage)
    {
        EnsureLineRenderer();
        ApplySorting();
        BuildEllipse(radiusX, radiusY);

        float ratio = maxStage > 0 ? (float)currentStage / maxStage : 0f;
        float widthScale = Mathf.Lerp(0.52f, 1f, ratio);
        float resolvedWidth = lineWidth * widthScale;
        Color color = GetShieldColor(ratio);

        lineRenderer.enabled = true;
        lineRenderer.startWidth = resolvedWidth;
        lineRenderer.endWidth = resolvedWidth;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }

    private IEnumerator PlayBreakRoutine()
    {
        EnsureLineRenderer();
        ApplySorting();

        float duration = 0.22f;
        float elapsed = 0f;
        float startRadiusX = radiusX * 0.94f;
        float startRadiusY = radiusY * 0.94f;
        float endRadiusX = radiusX * 1.24f;
        float endRadiusY = radiusY * 1.24f;
        Color startColor = new Color(1f, 0.86f, 0.72f, 0.92f);
        Color endColor = new Color(1f, 0.18f, 0.18f, 0f);

        lineRenderer.enabled = true;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            BuildEllipse(
                Mathf.Lerp(startRadiusX, endRadiusX, eased),
                Mathf.Lerp(startRadiusY, endRadiusY, eased));

            float width = Mathf.Lerp(lineWidth * 1.35f, 0.01f, eased);
            Color color = Color.Lerp(startColor, endColor, eased);

            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            yield return null;
        }

        HideImmediate();
        breakRoutine = null;
    }

    private void HideImmediate()
    {
        if (lineRenderer != null)
            lineRenderer.enabled = false;
    }

    private void EnsureLineRenderer()
    {
        if (lineRenderer != null)
            return;

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.positionCount = Mathf.Max(8, segmentCount);
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void ApplySorting()
    {
        if (lineRenderer == null)
            return;

        if (ownerSpriteRenderer != null)
        {
            lineRenderer.sortingLayerID = ownerSpriteRenderer.sortingLayerID;
            lineRenderer.sortingOrder = ownerSpriteRenderer.sortingOrder + 2;
        }
    }

    private void BuildEllipse(float ellipseRadiusX, float ellipseRadiusY)
    {
        if (lineRenderer == null)
            return;

        int count = Mathf.Max(8, segmentCount);
        if (lineRenderer.positionCount != count)
            lineRenderer.positionCount = count;

        for (int i = 0; i < count; i++)
        {
            float normalized = (float)i / count;
            float angle = normalized * Mathf.PI * 2f;
            Vector3 point = new Vector3(
                Mathf.Cos(angle) * ellipseRadiusX,
                Mathf.Sin(angle) * ellipseRadiusY,
                0f) + localOffset;
            lineRenderer.SetPosition(i, point);
        }
    }

    private Color GetShieldColor(float ratio)
    {
        if (ratio >= 0.75f)
            return new Color(0.48f, 0.9f, 1f, 0.92f);

        if (ratio >= 0.5f)
            return new Color(0.56f, 0.84f, 1f, 0.9f);

        if (ratio >= 0.25f)
            return new Color(0.98f, 0.7f, 0.28f, 0.92f);

        return new Color(1f, 0.34f, 0.34f, 0.96f);
    }
}

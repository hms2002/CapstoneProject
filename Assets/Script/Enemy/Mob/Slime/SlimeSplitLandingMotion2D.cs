using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 분열로 생성된 슬라임이 본체에서 튀어나와 착지하는 짧은 포물선 연출을 수행한다.
/// - 착지 전까지 CombatHurtbox2D가 소유한 피격 콜라이더를 비활성화해 공중 분열체가 피격되지 않게 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SlimeSplitLandingMotion2D : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float defaultDurationSeconds = 0.45f;
    [SerializeField, Min(0f)] private float defaultArcHeight = 0.55f;
    [SerializeField, Min(0f)] private float airborneBodyHeight = 1f;
    [Header("Runtime Shadow")]
    [SerializeField] private bool createRuntimeShadowWhenNoHeightPresentation = true;
    [SerializeField] private Vector3 runtimeShadowScale = new Vector3(0.55f, 0.18f, 1f);
    [SerializeField] private Color runtimeShadowColor = new Color(0f, 0f, 0f, 0.3f);
    [SerializeField, Range(0f, 1f)] private float runtimeShadowApexAlphaScale = 0.55f;
    [SerializeField, Range(0.1f, 1f)] private float runtimeShadowApexScale = 0.72f;
    [SerializeField] private string runtimeShadowSortingLayerName = "Entity";
    [SerializeField] private int runtimeShadowSortingOrder = -1;

    private readonly Collider2D[] cachedOwnedHurtboxColliders = new Collider2D[8];
    private readonly bool[] cachedColliderEnabledStates = new bool[8];
    private const int RuntimeShadowTextureWidth = 32;
    private const int RuntimeShadowTextureHeight = 12;
    private static Sprite runtimeShadowSprite;

    private Coroutine activeRoutine;
    private CombatHeightState2D heightState;
    private CombatHeightPresentation2D heightPresentation;
    private Transform runtimeShadowRoot;
    private SpriteRenderer runtimeShadowRenderer;

    /// <summary>분열체의 착지 이동을 시작하고, 진행 중에는 피격 판정을 잠근다.</summary>
    public void Begin(Vector2 startPosition, Vector2 landingPosition, float durationSeconds, float arcHeight)
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(RunLandingMotion(
            startPosition,
            landingPosition,
            Mathf.Max(0.01f, durationSeconds),
            Mathf.Max(0f, arcHeight)));
    }

    /// <summary>기본 설정값으로 분열체의 착지 이동을 시작한다.</summary>
    public void Begin(Vector2 startPosition, Vector2 landingPosition)
    {
        Begin(startPosition, landingPosition, defaultDurationSeconds, defaultArcHeight);
    }

    private void OnDisable()
    {
        RestoreHurtboxColliders();
        heightState?.SetGrounded();
        DestroyRuntimeShadow();
        activeRoutine = null;
    }

    private IEnumerator RunLandingMotion(Vector2 startPosition, Vector2 landingPosition, float durationSeconds, float arcHeight)
    {
        transform.position = startPosition;
        EnsureHeightState();
        CacheAndDisableHurtboxColliders();
        EnsureRuntimeShadowIfNeeded(startPosition);

        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            float normalized = Mathf.Clamp01(elapsed / durationSeconds);
            ApplyPose(startPosition, landingPosition, normalized, arcHeight);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = landingPosition;
        heightState?.SetGrounded();
        RestoreHurtboxColliders();
        DestroyRuntimeShadow();
        activeRoutine = null;
    }

    /// <summary>지상 위치는 선형 이동하고, 시각 높이는 사인 곡선으로 띄운다.</summary>
    private void ApplyPose(Vector2 startPosition, Vector2 landingPosition, float normalized, float arcHeight)
    {
        float visualHeight = Mathf.Sin(normalized * Mathf.PI) * arcHeight;
        Vector2 groundPosition = Vector2.Lerp(startPosition, landingPosition, normalized);

        if (heightPresentation != null)
        {
            transform.position = groundPosition;
            heightState?.SetAirborne(visualHeight, airborneBodyHeight);
            return;
        }

        transform.position = groundPosition + Vector2.up * visualHeight;
        UpdateRuntimeShadow(groundPosition, visualHeight, arcHeight);
    }

    /// <summary>CombatHeightState2D를 확보해 기존 높이 프레젠테이션 시스템과 연결한다.</summary>
    private void EnsureHeightState()
    {
        if (heightState != null)
            return;

        heightState = GetComponent<CombatHeightState2D>();
        if (heightState == null)
            heightState = gameObject.AddComponent<CombatHeightState2D>();

        heightPresentation = GetComponent<CombatHeightPresentation2D>();
    }

    /// <summary>프리팹에 높이 프레젠터가 없을 때 분열 착지 동안만 사용할 지상 그림자를 생성한다.</summary>
    private void EnsureRuntimeShadowIfNeeded(Vector2 startPosition)
    {
        if (!createRuntimeShadowWhenNoHeightPresentation || heightPresentation != null || runtimeShadowRenderer != null)
            return;

        GameObject shadowObject = new GameObject($"{name}_SplitLandingShadow");
        Transform parent = transform.parent;
        shadowObject.transform.SetParent(parent, true);
        shadowObject.transform.position = startPosition;
        shadowObject.transform.localScale = runtimeShadowScale;

        runtimeShadowRoot = shadowObject.transform;
        runtimeShadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        runtimeShadowRenderer.sprite = GetRuntimeShadowSprite();
        runtimeShadowRenderer.color = runtimeShadowColor;
        runtimeShadowRenderer.sortingLayerName = runtimeShadowSortingLayerName;
        runtimeShadowRenderer.sortingOrder = runtimeShadowSortingOrder;
    }

    /// <summary>착지 위치와 현재 높이에 맞춰 임시 그림자의 위치, 크기, 투명도를 갱신한다.</summary>
    private void UpdateRuntimeShadow(Vector2 groundPosition, float visualHeight, float arcHeight)
    {
        if (runtimeShadowRenderer == null || runtimeShadowRoot == null)
            return;

        float heightRatio = arcHeight <= 0f ? 0f : Mathf.Clamp01(visualHeight / arcHeight);
        float scale = Mathf.Lerp(1f, runtimeShadowApexScale, heightRatio);
        float alphaScale = Mathf.Lerp(1f, runtimeShadowApexAlphaScale, heightRatio);

        runtimeShadowRoot.position = groundPosition;
        runtimeShadowRoot.localScale = runtimeShadowScale * scale;

        Color color = runtimeShadowColor;
        color.a *= alphaScale;
        runtimeShadowRenderer.color = color;
    }

    /// <summary>분열 착지 전용 임시 그림자를 정리한다.</summary>
    private void DestroyRuntimeShadow()
    {
        if (runtimeShadowRoot != null)
            Destroy(runtimeShadowRoot.gameObject);

        runtimeShadowRoot = null;
        runtimeShadowRenderer = null;
    }

    /// <summary>CombatHurtbox2D가 소유한 실제 피격 콜라이더만 찾아 비활성화한다.</summary>
    private int CacheAndDisableHurtboxColliders()
    {
        CombatHurtbox2D[] hurtboxes = GetComponentsInChildren<CombatHurtbox2D>(true);
        if (hurtboxes == null || hurtboxes.Length == 0)
            return 0;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        int writeIndex = 0;
        for (int i = 0; i < colliders.Length && writeIndex < cachedOwnedHurtboxColliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (candidate == null || !IsOwnedByAnyHurtbox(hurtboxes, candidate))
                continue;

            cachedOwnedHurtboxColliders[writeIndex] = candidate;
            cachedColliderEnabledStates[writeIndex] = candidate.enabled;
            candidate.enabled = false;
            writeIndex++;
        }

        return writeIndex;
    }

    /// <summary>루트/자식 어느 위치에 있든 현재 분열체의 피격 박스에 속한 콜라이더인지 확인한다.</summary>
    private static bool IsOwnedByAnyHurtbox(CombatHurtbox2D[] hurtboxes, Collider2D candidate)
    {
        for (int i = 0; i < hurtboxes.Length; i++)
        {
            CombatHurtbox2D hurtbox = hurtboxes[i];
            if (hurtbox != null && hurtbox.OwnsCollider(candidate))
                return true;
        }

        return false;
    }

    /// <summary>착지 또는 비활성화 시 잠가둔 피격 콜라이더 상태를 원복한다.</summary>
    private void RestoreHurtboxColliders()
    {
        for (int i = 0; i < cachedOwnedHurtboxColliders.Length; i++)
        {
            Collider2D collider = cachedOwnedHurtboxColliders[i];
            if (collider != null)
                collider.enabled = cachedColliderEnabledStates[i];

            cachedOwnedHurtboxColliders[i] = null;
            cachedColliderEnabledStates[i] = false;
        }
    }

    /// <summary>분열 착지 임시 그림자에 사용할 작은 픽셀 타원 스프라이트를 한 번만 생성한다.</summary>
    private static Sprite GetRuntimeShadowSprite()
    {
        if (runtimeShadowSprite != null)
            return runtimeShadowSprite;

        Texture2D texture = new Texture2D(RuntimeShadowTextureWidth, RuntimeShadowTextureHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Runtime_SlimeSplitLandingShadow"
        };

        Color32 transparent = new Color32(255, 255, 255, 0);
        Color32 white = new Color32(255, 255, 255, 255);
        Color32[] pixels = new Color32[RuntimeShadowTextureWidth * RuntimeShadowTextureHeight];
        Vector2 center = new Vector2((RuntimeShadowTextureWidth - 1) * 0.5f, (RuntimeShadowTextureHeight - 1) * 0.5f);
        float radiusX = RuntimeShadowTextureWidth * 0.48f;
        float radiusY = RuntimeShadowTextureHeight * 0.44f;

        for (int y = 0; y < RuntimeShadowTextureHeight; y++)
        {
            for (int x = 0; x < RuntimeShadowTextureWidth; x++)
            {
                float normalizedX = (x - center.x) / radiusX;
                float normalizedY = (y - center.y) / radiusY;
                bool insideEllipse = normalizedX * normalizedX + normalizedY * normalizedY <= 1f;
                pixels[y * RuntimeShadowTextureWidth + x] = insideEllipse ? white : transparent;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        runtimeShadowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, RuntimeShadowTextureWidth, RuntimeShadowTextureHeight),
            new Vector2(0.5f, 0.5f),
            32f);
        runtimeShadowSprite.name = "Runtime_SlimeSplitLandingShadowSprite";
        return runtimeShadowSprite;
    }
}

using UnityEngine;

/// <summary>
/// 책임 :
/// - 월드에 떨어진 아이템 sprite를 공통 크기와 바닥 기준 위치로 정규화한다.
/// - UI icon의 원본 픽셀 크기, pivot, bounds 차이가 월드 드롭 표시 크기를 흔들지 않도록 SpriteRenderer를 보정한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldDropSpritePresenter2D : MonoBehaviour
{
    private enum NormalizeMode
    {
        Height,
        FitBox,
        RawSpriteSize
    }

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private NormalizeMode normalizeMode = NormalizeMode.FitBox;
    [SerializeField, Min(0.01f)] private float targetHeight = 0.65f;
    [SerializeField] private Vector2 targetBoxSize = new(1f, 1f);
    [SerializeField] private float bottomPadding = 0.08f;
    [SerializeField] private bool centerX = true;

    public SpriteRenderer Renderer
    {
        get
        {
            ResolveRenderer();
            return spriteRenderer;
        }
    }

    /// <summary>
    /// 책임 :
    /// - 전달받은 sprite를 렌더러에 적용하고, sprite bounds 기준으로 local scale/local position을 정규화한다.
    /// - sprite가 없을 때 renderer를 꺼 null icon 아이템이 빈 이미지로 남지 않게 한다.
    /// </summary>
    public void Apply(Sprite sprite)
    {
        Apply(sprite, false);
    }

    public void Apply(Sprite sprite, bool forceRawSpriteSize)
    {
        ResolveRenderer();
        if (spriteRenderer == null)
            return;

        spriteRenderer.sprite = sprite;
        spriteRenderer.enabled = sprite != null;

        if (sprite == null)
            return;

        ApplyNormalizedTransform(sprite, forceRawSpriteSize);
    }

    private void Reset()
    {
        ResolveRenderer();
    }

    private void OnValidate()
    {
        if (targetHeight <= 0f)
            targetHeight = 0.65f;

        if (targetBoxSize.x <= 0f)
            targetBoxSize.x = 1f;

        if (targetBoxSize.y <= 0f)
            targetBoxSize.y = 1f;

        ResolveRenderer();

        if (!Application.isPlaying && spriteRenderer != null && spriteRenderer.sprite != null)
            ApplyNormalizedTransform(spriteRenderer.sprite, false);
    }

    private void ResolveRenderer()
    {
        if (spriteRenderer != null)
            return;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>(includeInactive: true);
    }

    /// <summary>
    /// 책임 :
    /// - sprite 자체 bounds를 사용해 렌더러 Transform이 목표 높이 또는 목표 박스 크기를 만족하도록 계산한다.
    /// - x축 중앙 정렬 옵션을 통해 pivot이 치우친 icon도 드롭 중심에 맞춰 보이게 한다.
    /// </summary>
    private void ApplyNormalizedTransform(Sprite sprite, bool forceRawSpriteSize)
    {
        Bounds bounds = sprite.bounds;
        if (bounds.size.x <= 0f || bounds.size.y <= 0f)
            return;

        float uniformScale = forceRawSpriteSize ? 1f : ResolveUniformScale(bounds);
        Vector3 localScale = spriteRenderer.transform.localScale;
        spriteRenderer.transform.localScale = new Vector3(uniformScale, uniformScale, localScale.z);

        Vector3 localPosition = spriteRenderer.transform.localPosition;
        if (centerX)
            localPosition.x = -bounds.center.x * uniformScale;

        localPosition.y = bottomPadding - bounds.min.y * uniformScale;
        spriteRenderer.transform.localPosition = localPosition;
    }

    private float ResolveUniformScale(Bounds bounds)
    {
        if (normalizeMode == NormalizeMode.RawSpriteSize)
            return 1f;

        if (normalizeMode == NormalizeMode.FitBox)
        {
            float widthScale = targetBoxSize.x / bounds.size.x;
            float heightScale = targetBoxSize.y / bounds.size.y;
            return Mathf.Min(widthScale, heightScale);
        }

        return targetHeight / bounds.size.y;
    }
}

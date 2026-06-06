using UnityEngine;
using UnityGAS;

public sealed class RingTelegraphView : MonoBehaviour
{
    // 이 클래스의 책임:
    // 도넛형 텔레그래프를 셰이더 기반 radial fill로 표시하고, inner/outer 반경은 고정한 채 진행도만 갱신한다.

    private const int TextureSize = 128;
    private const float BorderThickness = 0.035f;
    private const string RingShaderName = "Custom/SpriteRadialTimerRing";

    private static Material sharedRingFillMaterial;

    private SpriteRenderer fillRenderer;
    private SpriteRenderer borderRenderer;
    private MaterialPropertyBlock fillPropertyBlock;
    private Texture2D fillTexture;
    private Texture2D borderTexture;
    private Sprite fillSprite;
    private Sprite borderSprite;

    private AttackTelegraphStyle activeStyle;
    private float startTime;
    private float duration;
    private float activeNormalizedInnerRadius;
    private bool isVisible;

    private void Awake()
    {
        EnsureRenderers();
        HideImmediate();
    }

    private void Update()
    {
        if (!isVisible)
            return;

        ApplyVisuals(GetNormalizedProgress());
    }

    /// <summary>도넛 텔레그래프를 지정 위치와 반경으로 표시합니다.</summary>
    public void Show(
        Vector3 center,
        float outerDiameter,
        float innerDiameter,
        float durationSeconds,
        AttackTelegraphStyle style,
        SpriteRenderer referenceRenderer)
    {
        EnsureRenderers();
        activeStyle = style;
        duration = Mathf.Max(0f, durationSeconds);
        startTime = Time.time;
        isVisible = true;

        transform.position = center;
        transform.rotation = Quaternion.identity;

        float safeOuterDiameter = Mathf.Max(0.0001f, outerDiameter);
        float normalizedInner = Mathf.Clamp01(Mathf.Max(0f, innerDiameter) / safeOuterDiameter);
        activeNormalizedInnerRadius = normalizedInner;
        BuildSprites(normalizedInner);
        ApplySorting(referenceRenderer);
        ApplyScale(safeOuterDiameter);
        ApplyVisuals(0f);

        gameObject.SetActive(true);
        fillRenderer.enabled = true;
        borderRenderer.enabled = true;
    }

    /// <summary>텔레그래프를 즉시 숨깁니다.</summary>
    public void HideImmediate()
    {
        isVisible = false;
        if (fillRenderer != null)
            fillRenderer.enabled = false;
        if (borderRenderer != null)
            borderRenderer.enabled = false;
    }

    private void EnsureRenderers()
    {
        if (fillRenderer != null && borderRenderer != null)
            return;

        if (fillRenderer == null)
            fillRenderer = CreateRenderer("RingFill");
        if (borderRenderer == null)
            borderRenderer = CreateRenderer("RingBorder");

        if (fillPropertyBlock == null)
            fillPropertyBlock = new MaterialPropertyBlock();

        if (sharedRingFillMaterial == null)
        {
            Shader ringShader = Shader.Find(RingShaderName);
            if (ringShader != null)
                sharedRingFillMaterial = new Material(ringShader);
        }

        if (sharedRingFillMaterial != null)
            fillRenderer.sharedMaterial = sharedRingFillMaterial;
    }

    private SpriteRenderer CreateRenderer(string childName)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        return renderer;
    }

    private void BuildSprites(float normalizedInner)
    {
        ReleaseSprites();

        fillTexture = MakeRingTexture(false, normalizedInner);
        borderTexture = MakeRingTexture(true, normalizedInner);
        fillSprite = MakeSprite(fillTexture, "RingTelegraphFill");
        borderSprite = MakeSprite(borderTexture, "RingTelegraphBorder");

        fillRenderer.sprite = fillSprite;
        borderRenderer.sprite = borderSprite;
    }

    private void ApplySorting(SpriteRenderer referenceRenderer)
    {
        if (referenceRenderer == null)
            return;

        fillRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
        borderRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
        fillRenderer.sortingOrder = referenceRenderer.sortingOrder;
        borderRenderer.sortingOrder = referenceRenderer.sortingOrder;
        fillRenderer.maskInteraction = referenceRenderer.maskInteraction;
        borderRenderer.maskInteraction = referenceRenderer.maskInteraction;
    }

    private void ApplyScale(float outerDiameter)
    {
        Vector3 scale = new Vector3(outerDiameter, outerDiameter, 1f);
        fillRenderer.transform.localScale = scale;
        borderRenderer.transform.localScale = scale;
    }

    private void ApplyVisuals(float normalizedProgress)
    {
        float curved = activeStyle != null && activeStyle.progressCurve != null
            ? Mathf.Clamp01(activeStyle.progressCurve.Evaluate(normalizedProgress))
            : normalizedProgress;

        float blinkMultiplier = 1f;
        if (activeStyle != null &&
            normalizedProgress >= activeStyle.blinkStartNormalized &&
            activeStyle.blinkFrequency > 0f)
        {
            float blinkWave = Mathf.Sin(Time.time * activeStyle.blinkFrequency * Mathf.PI * 2f);
            blinkMultiplier = Mathf.Lerp(activeStyle.blinkAlphaMin, 1f, (blinkWave + 1f) * 0.5f);
        }

        Color fillColor = activeStyle != null
            ? Color.Lerp(activeStyle.fillColorStart, activeStyle.fillColorEnd, curved)
            : new Color(1f, 0f, 0f, 0.3f);
        fillColor.a *= blinkMultiplier;

        Color borderColor = activeStyle != null
            ? Color.Lerp(activeStyle.borderColorStart, activeStyle.borderColorEnd, curved)
            : new Color(1f, 0f, 0f, 1f);
        borderColor.a *= blinkMultiplier;

        fillRenderer.color = fillColor;
        borderRenderer.color = borderColor;

        if (sharedRingFillMaterial != null)
        {
            fillPropertyBlock.Clear();
            fillRenderer.GetPropertyBlock(fillPropertyBlock);
            fillPropertyBlock.SetFloat("_FillAmount", curved);
            fillPropertyBlock.SetFloat("_StartAngleDegrees", 90f);
            fillPropertyBlock.SetFloat("_InvertFill", 0f);
            fillPropertyBlock.SetFloat("_FillMode", 1f);
            fillPropertyBlock.SetFloat("_InnerRadiusNormalized", activeNormalizedInnerRadius);
            fillRenderer.SetPropertyBlock(fillPropertyBlock);
        }
    }

    private float GetNormalizedProgress()
    {
        return duration <= 0f
            ? 1f
            : Mathf.Clamp01((Time.time - startTime) / duration);
    }

    private static Texture2D MakeRingTexture(bool borderOnly, float innerRadiusNormalized)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float halfSize = (TextureSize - 1) * 0.5f;
        float outerRadius = halfSize;
        float innerBorderStart = outerRadius * (1f - BorderThickness);
        float ringInnerRadius = outerRadius * Mathf.Clamp01(innerRadiusNormalized);
        float innerBorderEnd = Mathf.Min(outerRadius, ringInnerRadius + outerRadius * BorderThickness);
        Color clear = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float dx = x - halfSize;
                float dy = y - halfSize;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                bool insideOuter = distance <= outerRadius;
                bool outsideHole = distance >= ringInnerRadius;
                bool inside = insideOuter && outsideHole;
                bool outerBorder = distance >= innerBorderStart && distance <= outerRadius;
                bool innerBorder = ringInnerRadius > 0f &&
                                   distance >= ringInnerRadius &&
                                   distance <= innerBorderEnd;
                bool isBorder = outerBorder || innerBorder;

                texture.SetPixel(x, y, borderOnly ? (isBorder ? solid : clear) : (inside ? solid : clear));
            }
        }

        texture.Apply();
        return texture;
    }

    private static Sprite MakeSprite(Texture2D texture, string name)
    {
        texture.name = name;
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            TextureSize);
        sprite.name = name;
        return sprite;
    }

    private void OnDestroy()
    {
        ReleaseSprites();
    }

    private void ReleaseSprites()
    {
        if (fillSprite != null)
            Destroy(fillSprite);
        if (borderSprite != null)
            Destroy(borderSprite);
        if (fillTexture != null)
            Destroy(fillTexture);
        if (borderTexture != null)
            Destroy(borderTexture);

        fillSprite = null;
        borderSprite = null;
        fillTexture = null;
        borderTexture = null;
    }
}

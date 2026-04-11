using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 공통 공격 예고 사각형/원형의 위치, 회전, 크기, 진행도 색상 변화를 렌더링한다.
    /// - 실제 공격 판정 로직은 모르고, 전달받은 Spec과 Style만 시각적으로 표현한다.
    /// </summary>
    public sealed class AttackTelegraphView : MonoBehaviour
    {
        private const int CircleTextureSize = 128;
        private const float CircleBorderThickness = 0.08f;

        [Header("Refs")]
        [SerializeField] private Transform fillRoot;
        [SerializeField] private Transform borderRoot;
        [SerializeField] private SpriteRenderer fillRenderer;
        [SerializeField] private SpriteRenderer borderRenderer;

        private static Sprite circleFillSprite;
        private static Sprite circleBorderSprite;

        private bool baseScaleCaptured;
        private Vector3 fillBaseScale = Vector3.one;
        private Vector3 borderBaseScale = Vector3.one;
        private Sprite fillBaseSprite;
        private Sprite borderBaseSprite;

        private AttackTelegraphStyle activeStyle;
        private AttackTelegraphShape activeShape;
        private Vector2 activeSize = Vector2.one;
        private float startTime;
        private float duration;
        private bool isVisible;

        public bool IsVisible => isVisible;

        private void Awake()
        {
            if (fillRoot == null && fillRenderer != null)
                fillRoot = fillRenderer.transform;
            if (borderRoot == null && borderRenderer != null)
                borderRoot = borderRenderer.transform;

            CaptureBaseScaleIfNeeded();
            CaptureBaseSprites();
            HideImmediate();
        }

        private void Update()
        {
            if (!isVisible)
                return;

            float normalized = duration <= 0f
                ? 1f
                : Mathf.Clamp01((Time.time - startTime) / duration);

            ApplyStyle(normalized);
        }

        /// <summary>
        /// 책임 :
        /// - 공격 예고 도형의 위치, 크기, 회전과 진행도 시작 상태를 초기화한다.
        /// </summary>
        public void Show(AttackTelegraphSpec spec, AttackTelegraphStyle fallbackStyle = null)
        {
            CaptureBaseScaleIfNeeded();

            activeStyle = spec.style != null ? spec.style : fallbackStyle;
            duration = Mathf.Max(0f, spec.duration);
            startTime = Time.time;
            isVisible = true;

            gameObject.SetActive(true);
            transform.position = spec.center;
            transform.rotation = Quaternion.Euler(0f, 0f, spec.rotationDeg);

            ApplyShapeScale(spec.shape, spec.size);
            ApplyStyle(0f);
        }

        /// <summary>
        /// 책임 :
        /// - 공격 예고 연출을 즉시 숨기고 렌더러를 비활성화한다.
        /// </summary>
        public void HideImmediate()
        {
            isVisible = false;

            if (fillRenderer != null)
                fillRenderer.enabled = false;
            if (borderRenderer != null)
                borderRenderer.enabled = false;
        }

        private void CaptureBaseScaleIfNeeded()
        {
            if (baseScaleCaptured)
                return;

            baseScaleCaptured = true;
            fillBaseScale = fillRoot != null ? fillRoot.localScale : Vector3.one;
            borderBaseScale = borderRoot != null ? borderRoot.localScale : Vector3.one;
        }

        private void CaptureBaseSprites()
        {
            if (fillRenderer != null && fillBaseSprite == null)
                fillBaseSprite = fillRenderer.sprite;

            if (borderRenderer != null && borderBaseSprite == null)
                borderBaseSprite = borderRenderer.sprite;
        }

        private void ApplyShapeScale(AttackTelegraphShape shape, Vector2 size)
        {
            Vector2 safeSize = new Vector2(Mathf.Max(0.0001f, size.x), Mathf.Max(0.0001f, size.y));

            switch (shape)
            {
                case AttackTelegraphShape.Circle:
                {
                    float diameter = Mathf.Max(safeSize.x, safeSize.y);
                    safeSize = new Vector2(diameter, diameter);
                    break;
                }
            }

            activeShape = shape;
            activeSize = safeSize;

            ApplyShapeSprites();
            ApplyBorderScale();
            ApplyFillScale(0f);
        }

        private void ApplyShapeSprites()
        {
            if (activeShape == AttackTelegraphShape.Circle)
            {
                EnsureCircleSprites();

                if (fillRenderer != null)
                    fillRenderer.sprite = circleFillSprite;

                if (borderRenderer != null)
                    borderRenderer.sprite = circleBorderSprite;

                return;
            }

            if (fillRenderer != null)
                fillRenderer.sprite = fillBaseSprite;

            if (borderRenderer != null)
                borderRenderer.sprite = borderBaseSprite;
        }

        private void ApplyBorderScale()
        {
            if (borderRoot == null)
                return;

            borderRoot.localScale = new Vector3(
                borderBaseScale.x * activeSize.x,
                borderBaseScale.y * activeSize.y,
                borderBaseScale.z);
        }

        private void ApplyFillScale(float normalized)
        {
            if (fillRoot == null)
                return;

            Vector2 fillSize = activeSize;
            if (activeStyle != null && activeStyle.scaleFillWithProgress)
            {
                float start = Mathf.Clamp01(activeStyle.fillScaleStart);
                float end = Mathf.Clamp01(activeStyle.fillScaleEnd);
                float scale = Mathf.Lerp(start, end, normalized);
                fillSize *= scale;

                if (activeShape == AttackTelegraphShape.Circle)
                {
                    float diameter = Mathf.Max(fillSize.x, fillSize.y);
                    fillSize = new Vector2(diameter, diameter);
                }
            }

            fillRoot.localScale = new Vector3(
                fillBaseScale.x * fillSize.x,
                fillBaseScale.y * fillSize.y,
                fillBaseScale.z);
        }

        private static void EnsureCircleSprites()
        {
            if (circleFillSprite == null)
                circleFillSprite = MakeCircleSprite(false);

            if (circleBorderSprite == null)
                circleBorderSprite = MakeCircleSprite(true);
        }

        private static Sprite MakeCircleSprite(bool borderOnly)
        {
            Texture2D texture = new Texture2D(CircleTextureSize, CircleTextureSize, TextureFormat.RGBA32, false);
            texture.name = borderOnly ? "TelegraphCircleBorder" : "TelegraphCircleFill";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float halfSize = (CircleTextureSize - 1) * 0.5f;
            float outerRadius = halfSize;
            float innerRadius = outerRadius * (1f - CircleBorderThickness);
            Color clear = new Color(1f, 1f, 1f, 0f);
            Color solid = Color.white;

            for (int y = 0; y < CircleTextureSize; y++)
            {
                for (int x = 0; x < CircleTextureSize; x++)
                {
                    float dx = x - halfSize;
                    float dy = y - halfSize;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    bool isInside = distance <= outerRadius;
                    bool isBorder = distance >= innerRadius && distance <= outerRadius;

                    texture.SetPixel(x, y, borderOnly ? (isBorder ? solid : clear) : (isInside ? solid : clear));
                }
            }

            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, CircleTextureSize, CircleTextureSize),
                new Vector2(0.5f, 0.5f),
                100f);

            sprite.name = texture.name;
            return sprite;
        }

        private void ApplyStyle(float normalized)
        {
            if (fillRenderer == null && borderRenderer == null)
                return;

            float curved = activeStyle != null && activeStyle.progressCurve != null
                ? Mathf.Clamp01(activeStyle.progressCurve.Evaluate(normalized))
                : normalized;

            ApplyFillScale(curved);

            float blinkMultiplier = 1f;
            if (activeStyle != null &&
                normalized >= activeStyle.blinkStartNormalized &&
                activeStyle.blinkFrequency > 0f)
            {
                float blinkWave = Mathf.Sin(Time.time * activeStyle.blinkFrequency * Mathf.PI * 2f);
                blinkMultiplier = Mathf.Lerp(activeStyle.blinkAlphaMin, 1f, (blinkWave + 1f) * 0.5f);
            }

            if (fillRenderer != null)
            {
                Color fillColor = activeStyle != null
                    ? Color.Lerp(activeStyle.fillColorStart, activeStyle.fillColorEnd, curved)
                    : new Color(1f, 0.85f, 0.2f, 0.2f);

                fillColor.a *= blinkMultiplier;
                fillRenderer.color = fillColor;
                fillRenderer.enabled = true;
            }

            if (borderRenderer != null)
            {
                Color borderColor = activeStyle != null
                    ? Color.Lerp(activeStyle.borderColorStart, activeStyle.borderColorEnd, curved)
                    : new Color(1f, 0.9f, 0.4f, 1f);

                borderColor.a *= blinkMultiplier;
                borderRenderer.color = borderColor;
                borderRenderer.enabled = true;
            }
        }
    }
}

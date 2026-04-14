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
        private Sprite ringFillSprite;
        private Sprite ringBorderSprite;
        private Sprite sectorFillSprite;
        private Sprite sectorBorderSprite;
        private Texture2D ringFillTexture;
        private Texture2D ringBorderTexture;
        private Texture2D sectorFillTexture;
        private Texture2D sectorBorderTexture;
        private float ringInnerNormalized = -1f;
        private float sectorAngleNormalized = -1f;

        private bool baseScaleCaptured;
        private Vector3 fillBaseScale = Vector3.one;
        private Vector3 borderBaseScale = Vector3.one;
        private Sprite fillBaseSprite;
        private Sprite borderBaseSprite;

        private AttackTelegraphStyle activeStyle;
        private AttackTelegraphShape activeShape;
        private Vector2 activeSize = Vector2.one;
        private float activeInnerDiameter;
        private float activeSectorAngleDeg;
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

            ApplyStyle(GetCurrentNormalizedProgress());
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
            activeInnerDiameter = Mathf.Max(0f, spec.innerDiameter);
            activeSectorAngleDeg = Mathf.Clamp(spec.sectorAngleDeg, 0.1f, 360f);

            gameObject.SetActive(true);
            transform.position = spec.center;
            transform.rotation = Quaternion.Euler(0f, 0f, spec.rotationDeg);

            ApplyShapeScale(spec.shape, spec.size);
            ApplyStyle(0f);
        }

        /// <summary>
        /// 책임 :
        /// - 현재 표시 중인 텔레그래프의 진행도는 유지한 채 위치/회전/크기만 갱신한다.
        /// - 락온처럼 목표를 추적해야 하는 사각형 경고에 사용한다.
        /// </summary>
        public void UpdateGeometry(AttackTelegraphSpec spec)
        {
            if (!isVisible)
                return;

            activeInnerDiameter = Mathf.Max(0f, spec.innerDiameter);
            activeSectorAngleDeg = Mathf.Clamp(spec.sectorAngleDeg, 0.1f, 360f);
            transform.position = spec.center;
            transform.rotation = Quaternion.Euler(0f, 0f, spec.rotationDeg);

            ApplyShapeScale(spec.shape, spec.size);
            ApplyStyle(GetCurrentNormalizedProgress());
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

        /// <summary>
        /// 책임 :
        /// - 공격 예고 렌더러의 정렬 레이어만 기준 렌더러에 맞춘다.
        /// - 정렬 오더는 텔레그래프 프리팹이 가진 값을 유지해 연출 authoring을 존중한다.
        /// </summary>
        public void SyncSorting(SpriteRenderer referenceRenderer)
        {
            if (referenceRenderer == null)
                return;

            ApplySortingLayer(fillRenderer, referenceRenderer);
            ApplySortingLayer(borderRenderer, referenceRenderer);
        }

        /// <summary>
        /// 책임 :
        /// - 공격 예고 렌더러의 마스크 상호작용을 기준 렌더러와 동일하게 맞춘다.
        /// </summary>
        public void SyncMaskInteraction(SpriteRenderer referenceRenderer)
        {
            SpriteMaskInteraction maskInteraction = referenceRenderer != null
                ? referenceRenderer.maskInteraction
                : SpriteMaskInteraction.None;

            ApplyMaskInteraction(fillRenderer, maskInteraction);
            ApplyMaskInteraction(borderRenderer, maskInteraction);
        }

        private float GetCurrentNormalizedProgress()
        {
            return duration <= 0f
                ? 1f
                : Mathf.Clamp01((Time.time - startTime) / duration);
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
                case AttackTelegraphShape.Ring:
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

            if (activeShape == AttackTelegraphShape.Ring)
            {
                EnsureRingSprites();

                if (fillRenderer != null)
                    fillRenderer.sprite = ringFillSprite;

                if (borderRenderer != null)
                    borderRenderer.sprite = ringBorderSprite;

                return;
            }

            if (activeShape == AttackTelegraphShape.Sector)
            {
                EnsureSectorSprites();

                if (fillRenderer != null)
                    fillRenderer.sprite = sectorFillSprite;

                if (borderRenderer != null)
                    borderRenderer.sprite = sectorBorderSprite;

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

        private void EnsureRingSprites()
        {
            float outerDiameter = Mathf.Max(0.0001f, activeSize.x);
            float normalizedInner = Mathf.Clamp01(activeInnerDiameter / outerDiameter);

            if (ringFillSprite != null && ringBorderSprite != null && Mathf.Approximately(ringInnerNormalized, normalizedInner))
                return;

            ReleaseRingSprites();
            ringInnerNormalized = normalizedInner;

            ringFillTexture = MakeCircleTexture(false, normalizedInner);
            ringBorderTexture = MakeCircleTexture(true, normalizedInner);
            ringFillSprite = MakeSprite(ringFillTexture, "TelegraphRingFill");
            ringBorderSprite = MakeSprite(ringBorderTexture, "TelegraphRingBorder");
        }

        private void EnsureSectorSprites()
        {
            float normalizedAngle = Mathf.Clamp01(activeSectorAngleDeg / 360f);

            if (sectorFillSprite != null && sectorBorderSprite != null && Mathf.Approximately(sectorAngleNormalized, normalizedAngle))
                return;

            ReleaseSectorSprites();
            sectorAngleNormalized = normalizedAngle;

            sectorFillTexture = MakeSectorTexture(false, activeSectorAngleDeg);
            sectorBorderTexture = MakeSectorTexture(true, activeSectorAngleDeg);
            sectorFillSprite = MakeSprite(sectorFillTexture, "TelegraphSectorFill");
            sectorBorderSprite = MakeSprite(sectorBorderTexture, "TelegraphSectorBorder");
        }

        private static Sprite MakeCircleSprite(bool borderOnly)
        {
            Texture2D texture = MakeCircleTexture(borderOnly, 0f);
            string name = borderOnly ? "TelegraphCircleBorder" : "TelegraphCircleFill";
            Sprite sprite = MakeSprite(texture, name);
            return sprite;
        }

        private static Texture2D MakeCircleTexture(bool borderOnly, float innerRadiusNormalized)
        {
            Texture2D texture = new Texture2D(CircleTextureSize, CircleTextureSize, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float halfSize = (CircleTextureSize - 1) * 0.5f;
            float outerRadius = halfSize;
            float innerRadius = outerRadius * (1f - CircleBorderThickness);
            float ringInnerRadius = outerRadius * Mathf.Clamp01(innerRadiusNormalized);
            float innerBorderOuterRadius = Mathf.Min(outerRadius, ringInnerRadius + outerRadius * CircleBorderThickness);
            Color clear = new Color(1f, 1f, 1f, 0f);
            Color solid = Color.white;

            for (int y = 0; y < CircleTextureSize; y++)
            {
                for (int x = 0; x < CircleTextureSize; x++)
                {
                    float dx = x - halfSize;
                    float dy = y - halfSize;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    bool isInsideOuter = distance <= outerRadius;
                    bool isOutsideInnerHole = distance >= ringInnerRadius;
                    bool isInside = isInsideOuter && isOutsideInnerHole;
                    bool isOuterBorder = distance >= innerRadius && distance <= outerRadius;
                    bool isInnerBorder = ringInnerRadius > 0f &&
                                         distance >= ringInnerRadius &&
                                         distance <= innerBorderOuterRadius;
                    bool isBorder = isOuterBorder || isInnerBorder;

                    texture.SetPixel(x, y, borderOnly ? (isBorder ? solid : clear) : (isInside ? solid : clear));
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D MakeSectorTexture(bool borderOnly, float angleDeg)
        {
            Texture2D texture = new Texture2D(CircleTextureSize, CircleTextureSize, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float halfSize = (CircleTextureSize - 1) * 0.5f;
            float halfAngle = Mathf.Clamp(angleDeg, 0.1f, 360f) * 0.5f;
            float radialBorderAngle = Mathf.Max(1f, halfAngle * CircleBorderThickness);
            float outerBorderStart = 1f - CircleBorderThickness;
            bool useRadialBorders = angleDeg < 359.9f;
            Color clear = new Color(1f, 1f, 1f, 0f);
            Color solid = Color.white;

            for (int y = 0; y < CircleTextureSize; y++)
            {
                for (int x = 0; x < CircleTextureSize; x++)
                {
                    float localX = x / (CircleTextureSize - 1f);
                    float localY = (y - halfSize) / halfSize;
                    float radius = Mathf.Sqrt((localX * localX) + (localY * localY));
                    float angle = Mathf.Abs(Mathf.Atan2(localY, localX) * Mathf.Rad2Deg);
                    bool isInside = localX >= 0f &&
                                    radius <= 1f &&
                                    angle <= halfAngle;

                    bool isOuterBorder = isInside && radius >= outerBorderStart;
                    bool isRadialBorder = useRadialBorders &&
                                          isInside &&
                                          angle >= Mathf.Max(0f, halfAngle - radialBorderAngle);
                    bool isBorder = isOuterBorder || isRadialBorder;

                    texture.SetPixel(x, y, borderOnly ? (isBorder ? solid : clear) : (isInside ? solid : clear));
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
                new Rect(0f, 0f, CircleTextureSize, CircleTextureSize),
                new Vector2(0.5f, 0.5f),
                CircleTextureSize);

            sprite.name = texture.name;
            return sprite;
        }

        private void OnDestroy()
        {
            ReleaseRingSprites();
            ReleaseSectorSprites();
        }

        private void ReleaseRingSprites()
        {
            if (ringFillSprite != null)
                Destroy(ringFillSprite);

            if (ringBorderSprite != null)
                Destroy(ringBorderSprite);

            if (ringFillTexture != null)
                Destroy(ringFillTexture);

            if (ringBorderTexture != null)
                Destroy(ringBorderTexture);

            ringFillSprite = null;
            ringBorderSprite = null;
            ringFillTexture = null;
            ringBorderTexture = null;
        }

        private void ReleaseSectorSprites()
        {
            if (sectorFillSprite != null)
                Destroy(sectorFillSprite);

            if (sectorBorderSprite != null)
                Destroy(sectorBorderSprite);

            if (sectorFillTexture != null)
                Destroy(sectorFillTexture);

            if (sectorBorderTexture != null)
                Destroy(sectorBorderTexture);

            sectorFillSprite = null;
            sectorBorderSprite = null;
            sectorFillTexture = null;
            sectorBorderTexture = null;
            sectorAngleNormalized = -1f;
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

        /// <summary>
        /// 책임 :
        /// - 개별 렌더러의 정렬 레이어만 기준 렌더러에 맞춰 설정한다.
        /// - 프리팹에서 authoring한 sorting order는 유지한다.
        /// </summary>
        private static void ApplySortingLayer(
            SpriteRenderer targetRenderer,
            SpriteRenderer referenceRenderer)
        {
            if (targetRenderer == null || referenceRenderer == null)
                return;

            targetRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
        }

        /// <summary>
        /// 책임 :
        /// - 개별 렌더러의 스프라이트 마스크 상호작용을 지정 값으로 설정한다.
        /// </summary>
        private static void ApplyMaskInteraction(
            SpriteRenderer targetRenderer,
            SpriteMaskInteraction maskInteraction)
        {
            if (targetRenderer == null)
                return;

            targetRenderer.maskInteraction = maskInteraction;
        }
    }
}

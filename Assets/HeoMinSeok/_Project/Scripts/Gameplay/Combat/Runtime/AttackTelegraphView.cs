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
        private const string DefaultShaderName = "Sprites/Default";
        private const int WallClipHitBufferSize = 16;

        [Header("Refs")]
        [SerializeField] private Transform fillRoot;
        [SerializeField] private Transform borderRoot;
        [SerializeField] private SpriteRenderer fillRenderer;
        [SerializeField] private SpriteRenderer borderRenderer;

        private static Sprite circleFillSprite;
        private static Sprite circleBorderSprite;
        private static Sprite rectangleFillSprite;
        private static Sprite rectangleBorderSprite;
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
        private Vector3 fillBaseLocalPosition;
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
        private AttackTelegraphWallClippedMeshView wallClippedMeshView;
        private LineRenderer lineRenderer;
        private Material lineMaterial;
        private LineRenderer thinOutlineRenderer;
        private Material thinOutlineMaterial;
        private bool activeUseMeshOutline;
        private bool activeUseWallClipping;
        private LayerMask activeWallClipLayers;
        private int activeWallClipSampleCount;
        private float activeWallClipSkinWidth;
        private Vector3 activeLineStart;
        private Vector3 activeLineEnd;
        private float activeLineWidth = 0.05f;
        private readonly RaycastHit2D[] wallClipHitBuffer = new RaycastHit2D[WallClipHitBufferSize];

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
            CacheActiveWallClipping(spec);

            gameObject.SetActive(true);
            if (spec.shape == AttackTelegraphShape.Line)
            {
                HideWallClippedMesh();
                SetSpriteRenderersEnabled(false);
                ApplyLineGeometry(spec);
                ApplyStyle(0f);
                return;
            }

            HideLineRenderer();
            activeShape = spec.shape;
            activeSize = spec.size;
            if (TryApplyWallClippedMesh(spec, 0f))
            {
                SetSpriteRenderersEnabled(false);
                return;
            }

            transform.position = spec.center;
            transform.rotation = Quaternion.Euler(0f, 0f, spec.rotationDeg);

            HideWallClippedMesh();
            ApplyShapeScale(spec.shape, spec.size);
            ApplyThinOutlineGeometry(spec);
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

            spec = InheritActiveWallClipping(spec);
            activeInnerDiameter = Mathf.Max(0f, spec.innerDiameter);
            activeSectorAngleDeg = Mathf.Clamp(spec.sectorAngleDeg, 0.1f, 360f);
            float normalizedProgress = GetCurrentNormalizedProgress();
            if (spec.shape == AttackTelegraphShape.Line)
            {
                HideWallClippedMesh();
                SetSpriteRenderersEnabled(false);
                ApplyLineGeometry(spec);
                ApplyStyle(normalizedProgress);
                return;
            }

            HideLineRenderer();
            activeShape = spec.shape;
            activeSize = spec.size;
            if (TryApplyWallClippedMesh(spec, normalizedProgress))
            {
                SetSpriteRenderersEnabled(false);
                return;
            }

            transform.position = spec.center;
            transform.rotation = Quaternion.Euler(0f, 0f, spec.rotationDeg);

            HideWallClippedMesh();
            ApplyShapeScale(spec.shape, spec.size);
            ApplyThinOutlineGeometry(spec);
            ApplyStyle(normalizedProgress);
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

            HideWallClippedMesh();
            HideLineRenderer();
            HideThinOutlineRenderer();
            ClearActiveWallClipping();
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
            ApplyLineSorting();
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

        private bool TryApplyWallClippedMesh(AttackTelegraphSpec spec, float normalizedProgress)
        {
            if (!CanUseWallClippedMesh(spec))
                return false;

            AttackTelegraphWallClippedMeshView meshView = GetOrCreateWallClippedMeshView();
            meshView.ShowOrUpdate(spec, activeStyle, fillRenderer != null ? fillRenderer : borderRenderer, normalizedProgress);
            return meshView.IsVisible;
        }

        private static bool CanUseWallClippedMesh(AttackTelegraphSpec spec)
        {
            if (!spec.useWallClipping || spec.wallClipLayers.value == 0)
                return false;

            return spec.shape == AttackTelegraphShape.Rectangle ||
                   spec.shape == AttackTelegraphShape.Sector ||
                   spec.shape == AttackTelegraphShape.Circle ||
                   spec.shape == AttackTelegraphShape.Ring;
        }

        private AttackTelegraphWallClippedMeshView GetOrCreateWallClippedMeshView()
        {
            if (wallClippedMeshView != null)
                return wallClippedMeshView;

            wallClippedMeshView = GetComponent<AttackTelegraphWallClippedMeshView>();
            if (wallClippedMeshView == null)
                wallClippedMeshView = gameObject.AddComponent<AttackTelegraphWallClippedMeshView>();

            return wallClippedMeshView;
        }

        private void HideWallClippedMesh()
        {
            if (wallClippedMeshView != null)
                wallClippedMeshView.HideImmediate();
        }

        private void SetSpriteRenderersEnabled(bool enabled)
        {
            if (fillRenderer != null)
                fillRenderer.enabled = enabled;

            if (borderRenderer != null)
                borderRenderer.enabled = enabled;
        }

        /// <summary>
        /// 책임 :
        /// - 원본 Sprite fill 경로는 유지하면서 사각형 외곽선만 LineRenderer 기반 얇은 선으로 덮어쓴다.
        /// - wall clipping 없이도 최신 얇은 outline을 쓰되, 실제 피해 박스와 경고 fill 위치가 어긋나지 않게 한다.
        /// </summary>
        private void ApplyThinOutlineGeometry(AttackTelegraphSpec spec)
        {
            if (!spec.useMeshOutline || spec.useWallClipping || spec.shape != AttackTelegraphShape.Rectangle)
            {
                HideThinOutlineRenderer();
                return;
            }

            LineRenderer renderer = GetOrCreateThinOutlineRenderer();
            if (renderer == null)
                return;

            Vector2 half = new Vector2(Mathf.Max(0.0001f, spec.size.x), Mathf.Max(0.0001f, spec.size.y)) * 0.5f;
            renderer.useWorldSpace = false;
            renderer.loop = true;
            renderer.positionCount = 4;
            renderer.SetPosition(0, new Vector3(-half.x, -half.y, 0f));
            renderer.SetPosition(1, new Vector3(-half.x, half.y, 0f));
            renderer.SetPosition(2, new Vector3(half.x, half.y, 0f));
            renderer.SetPosition(3, new Vector3(half.x, -half.y, 0f));
            ApplyThinOutlineSorting();
            renderer.enabled = true;
        }

        private LineRenderer GetOrCreateThinOutlineRenderer()
        {
            if (thinOutlineRenderer != null)
                return thinOutlineRenderer;

            Transform outlineRoot = transform.Find("ThinOutlineLine");
            if (outlineRoot == null)
            {
                GameObject outlineObject = new("ThinOutlineLine");
                outlineRoot = outlineObject.transform;
                outlineRoot.SetParent(transform, false);
            }

            thinOutlineRenderer = outlineRoot.GetComponent<LineRenderer>();
            if (thinOutlineRenderer == null)
                thinOutlineRenderer = outlineRoot.gameObject.AddComponent<LineRenderer>();

            thinOutlineRenderer.useWorldSpace = false;
            thinOutlineRenderer.widthMultiplier = 0.045f;
            thinOutlineRenderer.numCapVertices = 0;
            thinOutlineRenderer.numCornerVertices = 0;
            thinOutlineRenderer.alignment = LineAlignment.TransformZ;
            thinOutlineRenderer.textureMode = LineTextureMode.Stretch;

            if (thinOutlineMaterial == null)
            {
                Shader shader = Shader.Find(DefaultShaderName);
                thinOutlineMaterial = new Material(shader);
            }

            thinOutlineRenderer.sharedMaterial = thinOutlineMaterial;
            return thinOutlineRenderer;
        }

        private void ApplyThinOutlineSorting()
        {
            if (thinOutlineRenderer == null)
                return;

            SpriteRenderer reference = borderRenderer != null ? borderRenderer : fillRenderer;
            if (reference == null)
                return;

            thinOutlineRenderer.sortingLayerID = reference.sortingLayerID;
            thinOutlineRenderer.sortingOrder = reference.sortingOrder + 1;
        }

        private void HideThinOutlineRenderer()
        {
            if (thinOutlineRenderer != null)
                thinOutlineRenderer.enabled = false;
        }

        /// <summary>
        /// 책임 :
        /// - 원거리 조준선처럼 시작점과 끝점이 명확한 선형 텔레그래프의 위치와 길이를 갱신한다.
        /// - 벽 clipping은 선분 끝점만 줄여 처리하고, 실제 공격 판정에는 관여하지 않는다.
        /// </summary>
        private void ApplyLineGeometry(AttackTelegraphSpec spec)
        {
            LineRenderer renderer = GetOrCreateLineRenderer();
            if (renderer == null)
                return;

            activeShape = AttackTelegraphShape.Line;
            activeSize = spec.size;
            activeLineStart = spec.lineStart;
            activeLineEnd = ResolveLineEndWithWallClipping(spec);
            activeLineWidth = Mathf.Max(0.001f, spec.size.y);

            transform.rotation = Quaternion.identity;
            renderer.widthMultiplier = activeLineWidth;
            renderer.positionCount = 2;
            renderer.SetPosition(0, activeLineStart);
            renderer.SetPosition(1, activeLineEnd);
            renderer.enabled = true;
        }

        private Vector3 ResolveLineEndWithWallClipping(AttackTelegraphSpec spec)
        {
            if (!spec.useWallClipping || spec.wallClipLayers.value == 0)
                return spec.lineEnd;

            Vector2 start = spec.lineStart;
            Vector2 end = spec.lineEnd;
            Vector2 delta = end - start;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
                return spec.lineEnd;

            Vector2 direction = delta / distance;
            if (!TryFindNearestWallClipHit(start, direction, distance, spec.wallClipLayers, out RaycastHit2D hit))
                return spec.lineEnd;

            float visibleDistance = Mathf.Clamp(hit.distance - spec.wallClipSkinWidth, 0f, distance);
            return start + direction * visibleDistance;
        }

        /// <summary>경고선 clipping은 실제 벽/문 같은 non-trigger 장애물만 사용하고, HoleTrap 같은 trigger 감지 영역은 무시합니다.</summary>
        private bool TryFindNearestWallClipHit(
            Vector2 start,
            Vector2 direction,
            float distance,
            LayerMask wallLayers,
            out RaycastHit2D nearestHit)
        {
            nearestHit = default;
            if (wallLayers.value == 0)
                return false;

            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = false
            };
            filter.SetLayerMask(wallLayers);

            int hitCount = Physics2D.Raycast(start, direction, filter, wallClipHitBuffer, distance);
            bool hasHit = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = wallClipHitBuffer[i];
                if (hit.collider == null || hit.collider.isTrigger)
                    continue;

                if (!hasHit || hit.distance < nearestHit.distance)
                {
                    nearestHit = hit;
                    hasHit = true;
                }
            }

            return hasHit;
        }

        private LineRenderer GetOrCreateLineRenderer()
        {
            if (lineRenderer != null)
                return lineRenderer;

            Transform lineRoot = transform.Find("LineTelegraph");
            if (lineRoot == null)
            {
                GameObject lineObject = new GameObject("LineTelegraph");
                lineRoot = lineObject.transform;
                lineRoot.SetParent(transform, false);
            }

            lineRenderer = lineRoot.GetComponent<LineRenderer>();
            if (lineRenderer == null)
                lineRenderer = lineRoot.gameObject.AddComponent<LineRenderer>();

            lineRenderer.useWorldSpace = true;
            lineRenderer.numCapVertices = 0;
            lineRenderer.numCornerVertices = 0;
            lineRenderer.alignment = LineAlignment.TransformZ;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.positionCount = 2;

            if (lineMaterial == null)
            {
                Shader shader = Shader.Find(DefaultShaderName);
                lineMaterial = new Material(shader);
            }

            lineRenderer.sharedMaterial = lineMaterial;
            ApplyLineSorting();
            return lineRenderer;
        }

        private void HideLineRenderer()
        {
            if (lineRenderer != null)
                lineRenderer.enabled = false;
        }

        private void ApplyLineSorting()
        {
            if (lineRenderer == null)
                return;

            SpriteRenderer referenceRenderer = borderRenderer != null ? borderRenderer : fillRenderer;
            if (referenceRenderer == null)
                return;

            lineRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
            lineRenderer.sortingOrder = referenceRenderer.sortingOrder;
        }

        private void CacheActiveWallClipping(AttackTelegraphSpec spec)
        {
            activeUseMeshOutline = spec.useMeshOutline;
            activeUseWallClipping = spec.useWallClipping;
            activeWallClipLayers = spec.wallClipLayers;
            activeWallClipSampleCount = spec.wallClipSampleCount;
            activeWallClipSkinWidth = spec.wallClipSkinWidth;
        }

        private AttackTelegraphSpec InheritActiveWallClipping(AttackTelegraphSpec spec)
        {
            if (spec.useMeshOutline || spec.useWallClipping)
            {
                CacheActiveWallClipping(spec);
                return spec;
            }

            if (!activeUseMeshOutline && (!activeUseWallClipping || activeWallClipLayers.value == 0))
                return spec;

            if (activeUseWallClipping && activeWallClipLayers.value != 0)
            {
                return spec.WithWallClipping(
                    activeWallClipLayers,
                    activeWallClipSampleCount,
                    activeWallClipSkinWidth);
            }

            return spec.WithMeshOutline(activeWallClipSampleCount);
        }

        private void ClearActiveWallClipping()
        {
            activeUseMeshOutline = false;
            activeUseWallClipping = false;
            activeWallClipLayers = default;
            activeWallClipSampleCount = 0;
            activeWallClipSkinWidth = 0f;
        }

        private void CaptureBaseScaleIfNeeded()
        {
            if (baseScaleCaptured)
                return;

            baseScaleCaptured = true;
            fillBaseScale = fillRoot != null ? fillRoot.localScale : Vector3.one;
            borderBaseScale = borderRoot != null ? borderRoot.localScale : Vector3.one;
            fillBaseLocalPosition = fillRoot != null ? fillRoot.localPosition : Vector3.zero;
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
            if (activeShape == AttackTelegraphShape.Rectangle)
            {
                EnsureRectangleSprites();

                if (fillRenderer != null)
                    fillRenderer.sprite = rectangleFillSprite;

                if (borderRenderer != null)
                    borderRenderer.sprite = rectangleBorderSprite;

                return;
            }

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
            Vector3 fillOffset = Vector3.zero;
            if (activeStyle != null && activeStyle.scaleFillWithProgress)
            {
                float start = Mathf.Clamp01(activeStyle.fillScaleStart);
                float end = Mathf.Clamp01(activeStyle.fillScaleEnd);
                float scale = Mathf.Lerp(start, end, normalized);

                if (activeShape == AttackTelegraphShape.Rectangle)
                {
                    fillSize.x *= scale;
                    fillOffset = ResolveStartAnchoredFillOffset(activeSize.x, fillSize.x);
                }
                else if (activeShape == AttackTelegraphShape.Sector)
                {
                    fillSize *= scale;
                    fillOffset = ResolveStartAnchoredFillOffset(activeSize.x, fillSize.x);
                }
                else
                {
                    fillSize *= scale;
                }

            }

            fillRoot.localPosition = fillBaseLocalPosition + fillOffset;
            fillRoot.localScale = new Vector3(
                fillBaseScale.x * fillSize.x,
                fillBaseScale.y * fillSize.y,
                fillBaseScale.z);
        }

        private Vector3 ResolveStartAnchoredFillOffset(float fullLength, float currentLength)
        {
            float offsetX = (currentLength - fullLength) * 0.5f * fillBaseScale.x;
            return new Vector3(offsetX, 0f, 0f);
        }

        private static void EnsureCircleSprites()
        {
            if (circleFillSprite == null)
                circleFillSprite = MakeCircleSprite(false);

            if (circleBorderSprite == null)
                circleBorderSprite = MakeCircleSprite(true);
        }

        private static void EnsureRectangleSprites()
        {
            if (rectangleFillSprite == null)
                rectangleFillSprite = MakeRectangleSprite(false);

            if (rectangleBorderSprite == null)
                rectangleBorderSprite = MakeRectangleSprite(true);
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

        private static Sprite MakeRectangleSprite(bool borderOnly)
        {
            Texture2D texture = MakeRectangleTexture(borderOnly);
            string name = borderOnly ? "TelegraphRectangleBorder" : "TelegraphRectangleFill";
            Sprite sprite = MakeSprite(texture, name);
            return sprite;
        }

        private static Texture2D MakeRectangleTexture(bool borderOnly)
        {
            Texture2D texture = new Texture2D(CircleTextureSize, CircleTextureSize, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            int borderPixels = Mathf.Max(1, Mathf.RoundToInt(CircleTextureSize * CircleBorderThickness));
            Color clear = new Color(1f, 1f, 1f, 0f);
            Color solid = Color.white;

            for (int y = 0; y < CircleTextureSize; y++)
            {
                for (int x = 0; x < CircleTextureSize; x++)
                {
                    bool isBorder =
                        x < borderPixels ||
                        y < borderPixels ||
                        x >= CircleTextureSize - borderPixels ||
                        y >= CircleTextureSize - borderPixels;

                    texture.SetPixel(x, y, borderOnly ? (isBorder ? solid : clear) : solid);
                }
            }

            texture.Apply();
            return texture;
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

            if (lineMaterial != null)
                Destroy(lineMaterial);

            if (thinOutlineMaterial != null)
                Destroy(thinOutlineMaterial);
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
            if (activeShape == AttackTelegraphShape.Line)
            {
                ApplyLineStyle(normalized);
                SetSpriteRenderersEnabled(false);
                return;
            }

            if (wallClippedMeshView != null && wallClippedMeshView.IsVisible)
            {
                wallClippedMeshView.ApplyStyle(activeStyle, normalized);
                SetSpriteRenderersEnabled(false);
                return;
            }

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
                borderRenderer.enabled = !(activeUseMeshOutline && !activeUseWallClipping && activeShape == AttackTelegraphShape.Rectangle);
                ApplyThinOutlineStyle(borderColor);
            }
            else
            {
                Color borderColor = activeStyle != null
                    ? Color.Lerp(activeStyle.borderColorStart, activeStyle.borderColorEnd, curved)
                    : new Color(1f, 0.9f, 0.4f, 1f);

                borderColor.a *= blinkMultiplier;
                ApplyThinOutlineStyle(borderColor);
            }
        }

        private void ApplyThinOutlineStyle(Color color)
        {
            if (thinOutlineRenderer == null || !thinOutlineRenderer.enabled)
                return;

            if (thinOutlineMaterial != null)
                thinOutlineMaterial.color = color;

            thinOutlineRenderer.startColor = color;
            thinOutlineRenderer.endColor = color;
        }

        private void ApplyLineStyle(float normalized)
        {
            if (lineRenderer == null)
                return;

            float curved = activeStyle != null && activeStyle.progressCurve != null
                ? Mathf.Clamp01(activeStyle.progressCurve.Evaluate(normalized))
                : normalized;

            float blinkMultiplier = 1f;
            if (activeStyle != null &&
                normalized >= activeStyle.blinkStartNormalized &&
                activeStyle.blinkFrequency > 0f)
            {
                float blinkWave = Mathf.Sin(Time.time * activeStyle.blinkFrequency * Mathf.PI * 2f);
                blinkMultiplier = Mathf.Lerp(activeStyle.blinkAlphaMin, 1f, (blinkWave + 1f) * 0.5f);
            }

            Color lineColor = activeStyle != null
                ? Color.Lerp(activeStyle.borderColorStart, activeStyle.borderColorEnd, curved)
                : new Color(1f, 0f, 0f, 0.9f);

            lineColor.a *= blinkMultiplier;
            if (lineMaterial != null)
                lineMaterial.color = lineColor;

            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
            lineRenderer.widthMultiplier = activeLineWidth;
            lineRenderer.SetPosition(0, activeLineStart);
            lineRenderer.SetPosition(1, activeLineEnd);
            lineRenderer.enabled = true;
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

using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 술/불 장판의 본체 표현을 shader material property로 구동한다.
    /// - gameplay 판정은 소유하지 않고, 장판 actor가 전달한 element/mode/radius/흡수 대상 방향을 렌더러에 반영한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PuddleShaderVisual : MonoBehaviour
    {
        private static readonly int ElementTypeId = Shader.PropertyToID("_ElementType");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int IgnitionProgressId = Shader.PropertyToID("_IgnitionProgress");
        private static readonly int AbsorbProgressId = Shader.PropertyToID("_AbsorbProgress");
        private static readonly int AbsorbDirectionId = Shader.PropertyToID("_AbsorbDirection");
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");

        [Header("Renderer")]
        [SerializeField] private Material materialTemplate;
        [SerializeField] private string sortingLayerName = "Entity";
        [SerializeField] private int sortingOrder = -2;
        [SerializeField, Min(1f)] private float quadScale = 2.2f;

        [Header("Wall Clipping")]
        [SerializeField] private bool useWallClipping;
        [SerializeField] private LayerMask wallClipLayers;
        [SerializeField, Min(3)] private int wallClipSampleCount = 48;
        [SerializeField, Min(0f)] private float wallClipSkinWidth = 0.03f;
        [SerializeField] private bool wallClipGroundModesOnly = true;

        [Header("Absorb")]
        [SerializeField, Min(0.01f)] private float absorbVisualDurationSeconds = 0.75f;
        [SerializeField, Min(0f)] private float preparingRenderPaddingScale = 0.75f;
        [SerializeField, Range(0.1f, 1f)] private float preparingEndRadiusScale = 0.42f;
        [SerializeField, Range(0f, 1f)] private float preparingRadiusShrinkStart = 0.35f;
        [SerializeField, Min(0f)] private float projectileFrontPaddingScale = 0.45f;
        [SerializeField, Min(0f)] private float projectileTailPaddingScale = 1.35f;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Mesh quadMesh;
        private Vector3[] quadVertices;
        private Vector2[] quadUvs;
        private readonly int[] quadTriangles = { 0, 1, 2, 0, 2, 3 };
        private Vector3[] wallClipVertices;
        private Vector2[] wallClipUvs;
        private int[] wallClipTriangles;
        private bool wallClipMeshDirty = true;
        private bool wallClipMeshActive;
        private Vector3 lastWallClipPosition;
        private float lastWallClipVisualRadius = -1f;
        private float lastWallClipQuadScale = -1f;
        private PuddleAreaMode lastWallClipMode;
        private int lastWallClipLayerMask;
        private int lastWallClipSampleCount;
        private float lastWallClipSkinWidth = -1f;
        private Transform absorbAnchor;
        private PuddleElementType elementType = PuddleElementType.Alcohol;
        private PuddleAreaMode mode = PuddleAreaMode.Ground;
        private float groundRadius = 1.35f;
        private float projectileRadius = 0.25f;
        private float visualRadius = 1.35f;
        private float ignitionProgress;
        private float absorbElapsedSeconds;
        private float timeOffset;

        private void Awake()
        {
            CacheComponents();
            EnsureMesh();
            ApplyRendererSettings();
            timeOffset = Random.value * 100f;
        }

        private void OnEnable()
        {
            CacheComponents();
            EnsureMesh();
            ApplyRendererSettings();
            ApplyProperties();
        }

        private void Update()
        {
            if (mode == PuddleAreaMode.AbsorbPreparing || mode == PuddleAreaMode.AbsorbProjectile)
                absorbElapsedSeconds += Time.deltaTime;

            ApplyVisualScale();
            ApplyProperties();
        }

        private void OnValidate()
        {
            quadScale = Mathf.Max(1f, quadScale);
            absorbVisualDurationSeconds = Mathf.Max(0.01f, absorbVisualDurationSeconds);
            preparingRenderPaddingScale = Mathf.Max(0f, preparingRenderPaddingScale);
            preparingEndRadiusScale = Mathf.Clamp(preparingEndRadiusScale, 0.1f, 1f);
            preparingRadiusShrinkStart = Mathf.Clamp01(preparingRadiusShrinkStart);
            projectileFrontPaddingScale = Mathf.Max(0f, projectileFrontPaddingScale);
            projectileTailPaddingScale = Mathf.Max(0f, projectileTailPaddingScale);
            wallClipSampleCount = Mathf.Max(3, wallClipSampleCount);
            wallClipSkinWidth = Mathf.Max(0f, wallClipSkinWidth);
            CacheComponents();
            EnsureMesh();
            ApplyRendererSettings();
            ApplyProperties();
        }

        public void SetElementType(PuddleElementType newElementType)
        {
            elementType = newElementType;
            ApplyProperties();
        }

        public void SetMode(PuddleAreaMode newMode)
        {
            if (mode != newMode)
            {
                mode = newMode;
                if (newMode == PuddleAreaMode.AbsorbPreparing || newMode == PuddleAreaMode.AbsorbProjectile)
                    absorbElapsedSeconds = 0f;
                if (newMode != PuddleAreaMode.Igniting)
                    ignitionProgress = newMode == PuddleAreaMode.Ground && elementType == PuddleElementType.Fire ? 1f : 0f;
                MarkWallClipDirty();
            }

            ApplyVisualScale();
            ApplyProperties();
        }

        public void SetIgnitionProgress(float normalizedProgress)
        {
            ignitionProgress = Mathf.Clamp01(normalizedProgress);
            ApplyProperties();
        }

        public void SetRadius(float newRadius)
        {
            SetRadii(newRadius, projectileRadius);
        }

        public void SetRadii(float newGroundRadius, float newProjectileRadius)
        {
            float resolvedGroundRadius = Mathf.Max(0.01f, newGroundRadius);
            float resolvedProjectileRadius = Mathf.Max(0.01f, newProjectileRadius);
            if (!Mathf.Approximately(groundRadius, resolvedGroundRadius) ||
                !Mathf.Approximately(projectileRadius, resolvedProjectileRadius))
            {
                MarkWallClipDirty();
            }

            groundRadius = resolvedGroundRadius;
            projectileRadius = resolvedProjectileRadius;
            ApplyVisualScale();
            ApplyProperties();
        }

        public void SetAbsorbAnchor(Transform newAbsorbAnchor)
        {
            absorbAnchor = newAbsorbAnchor;
            ApplyProperties();
        }

        private void CacheComponents()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();

            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            propertyBlock ??= new MaterialPropertyBlock();
        }

        private void EnsureMesh()
        {
            if (meshFilter == null)
                return;

            if (quadMesh == null)
            {
                quadVertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f)
                };
                quadUvs = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                };
                quadMesh = new Mesh
                {
                    name = "Puddle Shader Visual Quad",
                    vertices = quadVertices,
                    uv = quadUvs,
                    triangles = quadTriangles
                };
                quadMesh.RecalculateBounds();
            }

            meshFilter.sharedMesh = quadMesh;
            ApplyRenderRect();
            ApplyVisualScale();
        }

        private void ApplyRendererSettings()
        {
            if (meshRenderer == null)
                return;

            if (materialTemplate != null)
                meshRenderer.sharedMaterial = materialTemplate;

            meshRenderer.enabled = true;
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder;
        }

        private void ApplyProperties()
        {
            if (meshRenderer == null || propertyBlock == null)
                CacheComponents();

            if (meshRenderer == null)
                return;

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(ElementTypeId, elementType == PuddleElementType.Fire ? 1f : 0f);
            propertyBlock.SetFloat(ModeId, (float)mode);
            propertyBlock.SetFloat(RadiusId, visualRadius);
            propertyBlock.SetFloat(IgnitionProgressId, ResolveIgnitionProgress());
            propertyBlock.SetFloat(AbsorbProgressId, ResolveAbsorbProgress());
            propertyBlock.SetVector(AbsorbDirectionId, ResolveAbsorbDirection());
            propertyBlock.SetFloat(TimeOffsetId, timeOffset);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplyVisualScale()
        {
            visualRadius = ResolveVisualRadius();
            ApplyRenderRect();
            transform.localScale = Vector3.one * visualRadius * quadScale;
        }

        private void ApplyRenderRect()
        {
            if (quadMesh == null)
                return;

            if (ShouldUseWallClippedGroundMesh())
            {
                ApplyWallClippedGroundMeshIfNeeded();
                return;
            }

            bool wasWallClipMeshActive = wallClipMeshActive;
            wallClipMeshActive = false;

            float progress = ResolveAbsorbProgress();
            float frontPadding = mode == PuddleAreaMode.AbsorbPreparing
                ? progress * preparingRenderPaddingScale
                : 0f;
            float tailPadding = 0f;

            if (mode == PuddleAreaMode.AbsorbProjectile)
            {
                frontPadding = projectileFrontPaddingScale;
                tailPadding = projectileTailPaddingScale;
            }

            Vector2 direction = ResolveAbsorbDirection();
            Vector2 min = new Vector2(-0.5f, -0.5f);
            Vector2 max = new Vector2(0.5f, 0.5f);

            if (direction.x > 0f)
            {
                max.x += frontPadding * direction.x;
                min.x -= tailPadding * direction.x;
            }
            else
            {
                min.x += frontPadding * direction.x;
                max.x -= tailPadding * direction.x;
            }

            if (direction.y > 0f)
            {
                max.y += frontPadding * direction.y;
                min.y -= tailPadding * direction.y;
            }
            else
            {
                min.y += frontPadding * direction.y;
                max.y -= tailPadding * direction.y;
            }

            EnsureQuadBuffers();
            quadVertices[0] = new Vector3(min.x, min.y, 0f);
            quadVertices[1] = new Vector3(max.x, min.y, 0f);
            quadVertices[2] = new Vector3(max.x, max.y, 0f);
            quadVertices[3] = new Vector3(min.x, max.y, 0f);

            // UV는 원래 장판 중심을 유지한 채 확장 영역만 추가로 샘플링한다.
            quadUvs[0] = new Vector2(min.x + 0.5f, min.y + 0.5f);
            quadUvs[1] = new Vector2(max.x + 0.5f, min.y + 0.5f);
            quadUvs[2] = new Vector2(max.x + 0.5f, max.y + 0.5f);
            quadUvs[3] = new Vector2(min.x + 0.5f, max.y + 0.5f);

            if (wasWallClipMeshActive || quadMesh.vertexCount != quadVertices.Length)
                quadMesh.Clear();

            quadMesh.vertices = quadVertices;
            quadMesh.uv = quadUvs;
            quadMesh.triangles = quadTriangles;
            quadMesh.RecalculateBounds();
        }

        /// <summary>
        /// 책임:
        /// PuddleShaderVisual의 기본 quad 렌더링이 항상 4개 정점/UV 버퍼를 사용하도록 보장한다.
        /// </summary>
        private void EnsureQuadBuffers()
        {
            if (quadVertices == null || quadVertices.Length != 4)
                quadVertices = new Vector3[4];

            if (quadUvs == null || quadUvs.Length != 4)
                quadUvs = new Vector2[4];
        }

        private bool ShouldUseWallClippedGroundMesh()
        {
            if (!useWallClipping || wallClipLayers.value == 0)
                return false;

            if (!wallClipGroundModesOnly)
                return true;

            return mode == PuddleAreaMode.Ground || mode == PuddleAreaMode.Igniting;
        }

        private void ApplyWallClippedGroundMeshIfNeeded()
        {
            if (!wallClipMeshActive || wallClipMeshDirty || HasWallClipContextChanged())
                ApplyWallClippedGroundMesh();
        }

        private bool HasWallClipContextChanged()
        {
            return transform.position != lastWallClipPosition ||
                   !Mathf.Approximately(visualRadius, lastWallClipVisualRadius) ||
                   !Mathf.Approximately(quadScale, lastWallClipQuadScale) ||
                   mode != lastWallClipMode ||
                   wallClipLayers.value != lastWallClipLayerMask ||
                   wallClipSampleCount != lastWallClipSampleCount ||
                   !Mathf.Approximately(wallClipSkinWidth, lastWallClipSkinWidth);
        }

        private void ApplyWallClippedGroundMesh()
        {
            int sampleCount = Mathf.Max(3, wallClipSampleCount);
            int vertexCount = sampleCount + 1;
            EnsureWallClipBuffers(vertexCount, sampleCount);

            wallClipVertices[0] = Vector3.zero;
            wallClipUvs[0] = new Vector2(0.5f, 0.5f);

            float worldUnitsPerLocalUnit = Mathf.Max(0.0001f, visualRadius * quadScale);
            float maxLocalRadius = 0.5f;
            float maxWorldDistance = worldUnitsPerLocalUnit * maxLocalRadius;
            Vector2 origin = transform.position;

            for (int i = 0; i < sampleCount; i++)
            {
                float angle = i / (float)sampleCount * 360f;
                Vector2 direction = Rotate(Vector2.right, angle);
                float visibleWorldDistance = ResolveVisibleDistance(origin, direction, maxWorldDistance);
                float localDistance = Mathf.Clamp(visibleWorldDistance / worldUnitsPerLocalUnit, 0f, maxLocalRadius);
                Vector2 localVertex = direction * localDistance;

                wallClipVertices[i + 1] = localVertex;
                wallClipUvs[i + 1] = localVertex + new Vector2(0.5f, 0.5f);
            }

            int triangleIndex = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                int next = i + 1;
                if (next >= sampleCount)
                    next = 0;

                wallClipTriangles[triangleIndex++] = 0;
                wallClipTriangles[triangleIndex++] = i + 1;
                wallClipTriangles[triangleIndex++] = next + 1;
            }

            quadMesh.Clear();
            quadMesh.vertices = wallClipVertices;
            quadMesh.uv = wallClipUvs;
            quadMesh.triangles = wallClipTriangles;
            quadMesh.RecalculateBounds();

            wallClipMeshActive = true;
            wallClipMeshDirty = false;
            lastWallClipPosition = transform.position;
            lastWallClipVisualRadius = visualRadius;
            lastWallClipQuadScale = quadScale;
            lastWallClipMode = mode;
            lastWallClipLayerMask = wallClipLayers.value;
            lastWallClipSampleCount = wallClipSampleCount;
            lastWallClipSkinWidth = wallClipSkinWidth;
        }

        private void EnsureWallClipBuffers(int vertexCount, int triangleFanCount)
        {
            if (wallClipVertices == null || wallClipVertices.Length != vertexCount)
                wallClipVertices = new Vector3[vertexCount];

            if (wallClipUvs == null || wallClipUvs.Length != vertexCount)
                wallClipUvs = new Vector2[vertexCount];

            int triangleCount = Mathf.Max(0, triangleFanCount) * 3;
            if (wallClipTriangles == null || wallClipTriangles.Length != triangleCount)
                wallClipTriangles = new int[triangleCount];
        }

        private float ResolveVisibleDistance(Vector2 origin, Vector2 direction, float maxDistance)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, maxDistance, wallClipLayers);
            if (hit.collider == null)
                return maxDistance;

            return Mathf.Clamp(hit.distance - wallClipSkinWidth, 0f, maxDistance);
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(
                vector.x * cos - vector.y * sin,
                vector.x * sin + vector.y * cos);
        }

        private void MarkWallClipDirty()
        {
            wallClipMeshDirty = true;
        }

        private float ResolveVisualRadius()
        {
            if (mode == PuddleAreaMode.AbsorbPreparing)
            {
                float preparingShrinkProgress = Mathf.InverseLerp(preparingRadiusShrinkStart, 1f, ResolveAbsorbProgress());
                float smoothPreparingShrinkProgress = preparingShrinkProgress * preparingShrinkProgress * (3f - 2f * preparingShrinkProgress);
                return Mathf.Lerp(groundRadius, groundRadius * preparingEndRadiusScale, smoothPreparingShrinkProgress);
            }

            if (mode != PuddleAreaMode.AbsorbProjectile)
                return groundRadius;

            float progress = ResolveAbsorbProgress();
            float smoothProgress = progress * progress * (3f - 2f * progress);
            return Mathf.Lerp(groundRadius, projectileRadius, smoothProgress);
        }

        private float ResolveAbsorbProgress()
        {
            if (mode != PuddleAreaMode.AbsorbPreparing && mode != PuddleAreaMode.AbsorbProjectile)
                return 0f;

            return Mathf.Clamp01(absorbElapsedSeconds / absorbVisualDurationSeconds);
        }

        private float ResolveIgnitionProgress()
        {
            if (elementType == PuddleElementType.Fire)
                return 1f;

            return mode == PuddleAreaMode.Igniting ? ignitionProgress : 0f;
        }

        private Vector4 ResolveAbsorbDirection()
        {
            if (absorbAnchor == null)
                return new Vector4(0f, 1f, 0f, 0f);

            Vector2 direction = absorbAnchor.position - transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector2.up;

            direction.Normalize();
            return new Vector4(direction.x, direction.y, 0f, 0f);
        }
    }
}

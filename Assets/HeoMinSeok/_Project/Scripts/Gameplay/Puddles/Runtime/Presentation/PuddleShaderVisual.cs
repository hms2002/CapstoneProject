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
            groundRadius = Mathf.Max(0.01f, newGroundRadius);
            projectileRadius = Mathf.Max(0.01f, newProjectileRadius);
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
                    triangles = new[] { 0, 1, 2, 0, 2, 3 }
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

            quadVertices ??= new Vector3[4];
            quadUvs ??= new Vector2[4];
            quadVertices[0] = new Vector3(min.x, min.y, 0f);
            quadVertices[1] = new Vector3(max.x, min.y, 0f);
            quadVertices[2] = new Vector3(max.x, max.y, 0f);
            quadVertices[3] = new Vector3(min.x, max.y, 0f);

            // UV는 원래 장판 중심을 유지한 채 확장 영역만 추가로 샘플링한다.
            quadUvs[0] = new Vector2(min.x + 0.5f, min.y + 0.5f);
            quadUvs[1] = new Vector2(max.x + 0.5f, min.y + 0.5f);
            quadUvs[2] = new Vector2(max.x + 0.5f, max.y + 0.5f);
            quadUvs[3] = new Vector2(min.x + 0.5f, max.y + 0.5f);

            quadMesh.vertices = quadVertices;
            quadMesh.uv = quadUvs;
            quadMesh.RecalculateBounds();
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

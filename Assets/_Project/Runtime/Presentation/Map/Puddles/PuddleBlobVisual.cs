using System;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 장판을 여러 blob point가 합쳐진 하나의 2D mesh shape로 생성한다.
    /// - 기본 렌더링은 소유하지 않고, particle layer가 읽을 수 있는 표면 형태 데이터를 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class PuddleBlobVisual : MonoBehaviour, IPuddleBlobVisual
    {
        private enum PixelTexturePattern
        {
            SoftNoise,
            MaterialCells
        }

        /// <summary>
        /// 책임 :
        /// - 장판 actor가 교체될 때 이어받아야 하는 blob mesh 생성 파라미터를 담는다.
        /// - gameplay 상태와 particle 설정은 포함하지 않고, 표면 mesh의 연속성에 필요한 값만 보존한다.
        /// </summary>
        public readonly struct Snapshot
        {
            public Snapshot(
                int blobPointCount,
                int radialSampleCount,
                float baseRadius,
                float pointRadius,
                float pointSpread,
                float edgeWobble,
                int randomSeed,
                float absorbProgress,
                float absorbedRadiusScale,
                bool animateIdle,
                float idleWobbleAmplitude,
                float idleWobbleSpeed,
                Color surfaceColor)
            {
                BlobPointCount = blobPointCount;
                RadialSampleCount = radialSampleCount;
                BaseRadius = baseRadius;
                PointRadius = pointRadius;
                PointSpread = pointSpread;
                EdgeWobble = edgeWobble;
                RandomSeed = randomSeed;
                AbsorbProgress = absorbProgress;
                AbsorbedRadiusScale = absorbedRadiusScale;
                AnimateIdle = animateIdle;
                IdleWobbleAmplitude = idleWobbleAmplitude;
                IdleWobbleSpeed = idleWobbleSpeed;
                SurfaceColor = surfaceColor;
            }

            public int BlobPointCount { get; }
            public int RadialSampleCount { get; }
            public float BaseRadius { get; }
            public float PointRadius { get; }
            public float PointSpread { get; }
            public float EdgeWobble { get; }
            public int RandomSeed { get; }
            public float AbsorbProgress { get; }
            public float AbsorbedRadiusScale { get; }
            public bool AnimateIdle { get; }
            public float IdleWobbleAmplitude { get; }
            public float IdleWobbleSpeed { get; }
            public Color SurfaceColor { get; }
        }

        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");
        private static readonly int BaseMapProperty = Shader.PropertyToID("_BaseMap");

        [Header("Shape")]
        [SerializeField, Min(1)] private int blobPointCount = 7;
        [SerializeField, Range(16, 128)] private int radialSampleCount = 64;
        [SerializeField, Min(0.01f)] private float baseRadius = 1.35f;
        [SerializeField, Min(0.01f)] private float pointRadius = 0.55f;
        [SerializeField, Range(0f, 1f)] private float pointSpread = 0.65f;
        [SerializeField, Range(0f, 0.4f)] private float edgeWobble = 0.08f;
        [SerializeField] private int randomSeed = 1207;

        [Header("Absorb")]
        [SerializeField, Range(0f, 1f)] private float absorbProgress;
        [SerializeField, Min(0.01f)] private float absorbedRadiusScale = 0.28f;
        [SerializeField, Min(0.01f)] private float absorbDampTimeSeconds = 0.6f;

        [Header("Motion")]
        [SerializeField] private bool animateIdle = true;
        [SerializeField, Min(0f)] private float idleWobbleAmplitude = 0.06f;
        [SerializeField, Min(0f)] private float idleWobbleSpeed = 0.8f;

        [Header("Projectile Motion")]
        [SerializeField, Min(0f)] private float projectileWobbleAmplitude = 0.18f;
        [SerializeField, Min(0f)] private float projectileWobbleSpeed = 2f;

        [Header("Rendering")]
        [SerializeField] private bool renderSurface;
        [SerializeField] private Color surfaceColor = new(0.72f, 0.34f, 0.12f, 0.72f);
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder;

        [Header("Outline")]
        [SerializeField] private bool renderOutline;
        [SerializeField, Min(0f)] private float outlineWidth = 0.08f;
        [SerializeField] private Color outlineColor = new(0.12f, 0.06f, 0.025f, 0.78f);
        [SerializeField] private int outlineSortingOrderOffset = -1;

        [Header("Pixel Style")]
        [SerializeField] private bool usePixelStyle;
        [SerializeField, Min(0.001f)] private float pixelGridSize = 0.06f;
        [SerializeField, Range(0f, 1f)] private float pixelSnapStrength = 1f;
        [SerializeField, Min(0)] private int uvPixelSteps = 24;

        [Header("Pixel Texture")]
        [SerializeField] private bool useProceduralPixelTexture;
        [SerializeField] private PixelTexturePattern pixelTexturePattern = PixelTexturePattern.MaterialCells;
        [SerializeField, Range(4, 64)] private int pixelTextureSize = 16;
        [SerializeField, Range(0f, 1f)] private float pixelTextureNoiseStrength = 0.22f;
        [SerializeField, Range(0f, 1f)] private float pixelTextureCenterLight = 0.16f;
        [SerializeField, Range(0f, 1f)] private float pixelTextureCellContrast = 0.38f;
        [SerializeField, Range(0f, 1f)] private float pixelTextureEdgeDarkness = 0.28f;
        [SerializeField, Range(0f, 1f)] private float pixelTextureSparkleChance = 0.08f;
        [SerializeField] private int pixelTextureSeed = 2301;

        private readonly Vector2[] restPoints = Array.Empty<Vector2>();
        private Vector2[] generatedRestPoints;
        private Vector2[] currentPoints;
        private float[] pointPhaseOffsets;
        private Mesh mesh;
        private Mesh outlineMesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshFilter outlineMeshFilter;
        private MeshRenderer outlineMeshRenderer;
        private MaterialPropertyBlock materialProperties;
        private MaterialPropertyBlock outlineMaterialProperties;
        private Texture2D proceduralPixelTexture;
        private int generatedPixelTextureSize;
        private PixelTexturePattern generatedPixelTexturePattern;
        private float generatedPixelTextureNoiseStrength = -1f;
        private float generatedPixelTextureCenterLight = -1f;
        private float generatedPixelTextureCellContrast = -1f;
        private float generatedPixelTextureEdgeDarkness = -1f;
        private float generatedPixelTextureSparkleChance = -1f;
        private int generatedPixelTextureSeed;
        private Vector3[] vertices;
        private Vector3[] outlineVertices;
        private Vector2[] uvs;
        private int[] triangles;
        private float targetAbsorbProgress;
        private float absorbProgressVelocity;
        private float groundIdleWobbleAmplitude;
        private float groundIdleWobbleSpeed;
        private bool hasGroundIdleWobbleSpeed;
        private bool geometryDirty = true;

        public float AbsorbProgress => absorbProgress;
        public Color SurfaceColor => surfaceColor;
        public bool HasShape => mesh != null && vertices != null && vertices.Length > 2;

        private void Awake()
        {
            EnsureComponents();
            EnsureMesh();
            EnsureOutlineMesh();
            RebuildPointLayout();
            RebuildMeshTopology();
            ApplyRendererSettings();
            RebuildGeometry();
        }

        private void OnEnable()
        {
            geometryDirty = true;
        }

        private void Update()
        {
            UpdateAbsorbDamp();

            if (animateIdle && idleWobbleAmplitude > 0f)
                geometryDirty = true;

            if (geometryDirty)
                RebuildGeometry();
        }

        private void OnValidate()
        {
            blobPointCount = Mathf.Max(1, blobPointCount);
            radialSampleCount = Mathf.Clamp(radialSampleCount, 16, 128);
            baseRadius = Mathf.Max(0.01f, baseRadius);
            pointRadius = Mathf.Max(0.01f, pointRadius);
            absorbedRadiusScale = Mathf.Max(0.01f, absorbedRadiusScale);
            pixelGridSize = Mathf.Max(0.001f, pixelGridSize);
            pixelSnapStrength = Mathf.Clamp01(pixelSnapStrength);
            uvPixelSteps = Mathf.Max(0, uvPixelSteps);
            pixelTextureSize = Mathf.Clamp(pixelTextureSize, 4, 64);
            pixelTextureNoiseStrength = Mathf.Clamp01(pixelTextureNoiseStrength);
            pixelTextureCenterLight = Mathf.Clamp01(pixelTextureCenterLight);
            pixelTextureCellContrast = Mathf.Clamp01(pixelTextureCellContrast);
            pixelTextureEdgeDarkness = Mathf.Clamp01(pixelTextureEdgeDarkness);
            pixelTextureSparkleChance = Mathf.Clamp01(pixelTextureSparkleChance);

            EnsureComponents();
            EnsureMesh();
            EnsureOutlineMesh(false);
            RebuildPointLayout();
            RebuildMeshTopology();
            ApplyRendererSettings(false);
            geometryDirty = true;
        }

        private void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);

            if (outlineMesh != null)
                Destroy(outlineMesh);

            if (proceduralPixelTexture != null)
                Destroy(proceduralPixelTexture);

            mesh = null;
            outlineMesh = null;
            proceduralPixelTexture = null;
        }

        /// <summary>
        /// 책임 :
        /// - 흡수 패턴 진행도에 맞춰 술 blob point들을 중앙으로 모으고 전체 크기를 줄인다.
        /// - 장판 이동/충돌/결과 처리는 호출자가 관리한다.
        /// </summary>
        public void SetAbsorbProgress(float normalizedProgress)
        {
            float clampedProgress = Mathf.Clamp01(normalizedProgress);
            targetAbsorbProgress = clampedProgress;

            if (Mathf.Approximately(absorbProgress, clampedProgress))
                return;

            absorbProgress = clampedProgress;
            absorbProgressVelocity = 0f;
            geometryDirty = true;
        }

        /// <summary>
        /// 책임 :
        /// - 흡수 탄막화 목표값만 설정하고 실제 blob 수축은 damp로 자연스럽게 따라가게 한다.
        /// - 즉시 판정 전환과 시각 전환 속도를 분리해 총알화가 갑자기 튀지 않도록 한다.
        /// </summary>
        public void SetAbsorbTarget(float normalizedProgress)
        {
            targetAbsorbProgress = Mathf.Clamp01(normalizedProgress);
            geometryDirty = true;
        }

        /// <summary>
        /// 책임 :
        /// - 흡수 탄막 모드에서는 blob 흔들림 속도를 높이고, 바닥 장판 모드로 돌아가면 원래 속도를 복원한다.
        /// - 이동 속도와 충돌 판정은 장판 actor가 관리한다.
        /// </summary>
        public void SetProjectileMotion(bool isProjectile)
        {
            if (!hasGroundIdleWobbleSpeed)
            {
                groundIdleWobbleAmplitude = idleWobbleAmplitude;
                groundIdleWobbleSpeed = idleWobbleSpeed;
                hasGroundIdleWobbleSpeed = true;
            }

            idleWobbleAmplitude = isProjectile ? projectileWobbleAmplitude : groundIdleWobbleAmplitude;
            idleWobbleSpeed = isProjectile ? projectileWobbleSpeed : groundIdleWobbleSpeed;
            geometryDirty = true;
        }

        /// <summary>
        /// 책임 :
        /// - 장판 타입/상태 전환 중 material 색상만 교체한다.
        /// - mesh 형상과 gameplay 상태는 변경하지 않는다.
        /// </summary>
        public void SetColor(Color color)
        {
            surfaceColor = color;
            ApplyRendererSettings();
        }

        /// <summary>
        /// 책임 :
        /// - 현재 blob mesh의 형태와 진행 값을 다른 장판 actor가 이어받을 수 있게 캡처한다.
        /// - 술 장판이 불 장판 actor로 교체되는 순간 mesh 모양이 튀지 않게 하는 데 사용한다.
        /// </summary>
        public Snapshot CaptureSnapshot()
        {
            return new Snapshot(
                blobPointCount,
                radialSampleCount,
                baseRadius,
                pointRadius,
                pointSpread,
                edgeWobble,
                randomSeed,
                absorbProgress,
                absorbedRadiusScale,
                animateIdle,
                idleWobbleAmplitude,
                idleWobbleSpeed,
                surfaceColor);
        }

        /// <summary>
        /// 책임 :
        /// - 장판 actor가 교체될 때 표면 색과 흡수 진행도를 이어받는다.
        /// - 술에서 불로 바뀌는 순간 visual 값이 끊기지 않도록 한다.
        /// </summary>
        public void ApplySnapshot(Color color, float normalizedAbsorbProgress)
        {
            surfaceColor = color;
            absorbProgress = Mathf.Clamp01(normalizedAbsorbProgress);
            targetAbsorbProgress = absorbProgress;
            absorbProgressVelocity = 0f;
            ApplyRendererSettings();
            geometryDirty = true;
        }

        /// <summary>
        /// 책임 :
        /// - 다른 장판 actor에서 캡처한 blob mesh 파라미터를 현재 visual에 적용한다.
        /// - material/particle authoring은 건드리지 않고 mesh shape와 표면 색만 이어받는다.
        /// </summary>
        public void ApplySnapshot(Snapshot snapshot)
        {
            blobPointCount = Mathf.Max(1, snapshot.BlobPointCount);
            radialSampleCount = Mathf.Clamp(snapshot.RadialSampleCount, 16, 128);
            baseRadius = Mathf.Max(0.01f, snapshot.BaseRadius);
            pointRadius = Mathf.Max(0.01f, snapshot.PointRadius);
            pointSpread = Mathf.Clamp01(snapshot.PointSpread);
            edgeWobble = Mathf.Clamp(snapshot.EdgeWobble, 0f, 0.4f);
            randomSeed = snapshot.RandomSeed;
            absorbProgress = Mathf.Clamp01(snapshot.AbsorbProgress);
            targetAbsorbProgress = absorbProgress;
            absorbProgressVelocity = 0f;
            absorbedRadiusScale = Mathf.Max(0.01f, snapshot.AbsorbedRadiusScale);
            animateIdle = snapshot.AnimateIdle;
            idleWobbleAmplitude = Mathf.Max(0f, snapshot.IdleWobbleAmplitude);
            idleWobbleSpeed = Mathf.Max(0f, snapshot.IdleWobbleSpeed);
            groundIdleWobbleAmplitude = idleWobbleAmplitude;
            groundIdleWobbleSpeed = idleWobbleSpeed;
            hasGroundIdleWobbleSpeed = true;
            surfaceColor = snapshot.SurfaceColor;

            EnsureComponents();
            EnsureMesh();
            EnsureOutlineMesh();
            RebuildPointLayout();
            RebuildMeshTopology();
            ApplyRendererSettings();
            geometryDirty = true;
        }

        private void UpdateAbsorbDamp()
        {
            if (!Application.isPlaying)
                return;

            if (Mathf.Approximately(absorbProgress, targetAbsorbProgress))
                return;

            absorbProgress = Mathf.SmoothDamp(
                absorbProgress,
                targetAbsorbProgress,
                ref absorbProgressVelocity,
                Mathf.Max(0.01f, absorbDampTimeSeconds));

            if (Mathf.Abs(absorbProgress - targetAbsorbProgress) <= 0.001f)
            {
                absorbProgress = targetAbsorbProgress;
                absorbProgressVelocity = 0f;
            }

            geometryDirty = true;
        }

        private void EnsureComponents()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();

            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            materialProperties ??= new MaterialPropertyBlock();
            outlineMaterialProperties ??= new MaterialPropertyBlock();
        }

        private void EnsureMesh()
        {
            if (mesh != null)
                return;

            mesh = new Mesh
            {
                name = $"{nameof(PuddleBlobVisual)}Mesh",
                hideFlags = HideFlags.DontSave
            };

            if (meshFilter != null)
                meshFilter.sharedMesh = mesh;
        }

        private void EnsureOutlineMesh(bool allowCreateObject = true)
        {
            // Particle 기반 Noita풍 장판으로 전환하면서 outline mesh 생성은 폐기한다.
            if (outlineMeshRenderer != null)
                outlineMeshRenderer.enabled = false;

            return;

#pragma warning disable CS0162
            if (!renderOutline)
            {
                if (outlineMeshRenderer != null)
                    outlineMeshRenderer.enabled = false;

                return;
            }

            if (!EnsureOutlineComponents(allowCreateObject))
                return;

            if (outlineMesh == null)
            {
                outlineMesh = new Mesh
                {
                    name = $"{nameof(PuddleBlobVisual)}OutlineMesh",
                    hideFlags = HideFlags.DontSave
                };
            }

            if (outlineMeshFilter != null)
                outlineMeshFilter.sharedMesh = outlineMesh;
#pragma warning restore CS0162
        }

        private bool EnsureOutlineComponents(bool allowCreateObject)
        {
            Transform outlineTransform = transform.Find("PuddleBlobOutline");
            if (outlineTransform == null)
            {
                if (!allowCreateObject)
                    return false;

                GameObject outlineObject = new("PuddleBlobOutline");
                outlineTransform = outlineObject.transform;
                outlineTransform.SetParent(transform, false);
            }

            if (outlineMeshFilter == null)
                outlineMeshFilter = outlineTransform.GetComponent<MeshFilter>();

            if (outlineMeshFilter == null)
                outlineMeshFilter = outlineTransform.gameObject.AddComponent<MeshFilter>();

            if (outlineMeshRenderer == null)
                outlineMeshRenderer = outlineTransform.GetComponent<MeshRenderer>();

            if (outlineMeshRenderer == null)
                outlineMeshRenderer = outlineTransform.gameObject.AddComponent<MeshRenderer>();

            if (meshRenderer != null && outlineMeshRenderer.sharedMaterial == null)
                outlineMeshRenderer.sharedMaterial = meshRenderer.sharedMaterial;

            outlineMeshRenderer.enabled = true;
            return true;
        }

        private void RebuildPointLayout()
        {
            int count = Mathf.Max(1, blobPointCount);
            generatedRestPoints = new Vector2[count];
            currentPoints = new Vector2[count];
            pointPhaseOffsets = new float[count];

            System.Random random = new(randomSeed);
            generatedRestPoints[0] = Vector2.zero;
            pointPhaseOffsets[0] = RandomRange(random, 0f, Mathf.PI * 2f);

            float spreadRadius = baseRadius * Mathf.Clamp01(pointSpread);
            for (int i = 1; i < count; i++)
            {
                float angle = RandomRange(random, 0f, Mathf.PI * 2f);
                float radius = Mathf.Sqrt(RandomRange(random, 0f, 1f)) * spreadRadius;
                generatedRestPoints[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                pointPhaseOffsets[i] = RandomRange(random, 0f, Mathf.PI * 2f);
            }
        }

        private void RebuildMeshTopology()
        {
            int samples = Mathf.Clamp(radialSampleCount, 16, 128);
            vertices = new Vector3[samples + 1];
            outlineVertices = new Vector3[samples + 1];
            uvs = new Vector2[samples + 1];
            triangles = new int[samples * 3];

            vertices[0] = Vector3.zero;
            outlineVertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < samples; i++)
            {
                int vertexIndex = i + 1;
                int nextVertexIndex = i == samples - 1 ? 1 : vertexIndex + 1;
                int triangleIndex = i * 3;

                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = vertexIndex;
                triangles[triangleIndex + 2] = nextVertexIndex;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;

            if (outlineMesh != null)
            {
                outlineMesh.Clear();
                outlineMesh.vertices = outlineVertices;
                outlineMesh.uv = uvs;
                outlineMesh.triangles = triangles;
            }
        }

        private void RebuildGeometry()
        {
            if (mesh == null || generatedRestPoints == null || generatedRestPoints.Length == 0)
                return;

            UpdateCurrentPoints();

            float radiusScale = Mathf.Lerp(1f, absorbedRadiusScale, SmoothStep(absorbProgress));
            int samples = Mathf.Clamp(radialSampleCount, 16, 128);

            vertices[0] = Vector3.zero;
            outlineVertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < samples; i++)
            {
                float angle = (Mathf.PI * 2f * i) / samples;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float outerDistance = ResolveOuterDistance(direction);
                float wobble = 1f + Mathf.Sin(angle * 3.1f + randomSeed * 0.13f) * edgeWobble;
                Vector2 point = direction * outerDistance * wobble * radiusScale;
                Vector2 outlinePoint = ResolveOutlinePoint(point, direction);

                point = ResolvePixelPoint(point);
                outlinePoint = ResolvePixelPoint(outlinePoint);

                vertices[i + 1] = point;
                outlineVertices[i + 1] = outlinePoint;
                uvs[i + 1] = ResolvePixelUv(new Vector2(
                    Mathf.InverseLerp(-baseRadius, baseRadius, point.x),
                    Mathf.InverseLerp(-baseRadius, baseRadius, point.y)));
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.RecalculateBounds();

            if (outlineMesh != null && renderOutline)
            {
                outlineMesh.vertices = outlineVertices;
                outlineMesh.uv = uvs;
                outlineMesh.RecalculateBounds();
            }

            geometryDirty = false;
        }

        private void UpdateCurrentPoints()
        {
            float centerPull = SmoothStep(absorbProgress);
            float time = Application.isPlaying ? Time.time : 0f;

            for (int i = 0; i < generatedRestPoints.Length; i++)
            {
                Vector2 restPoint = generatedRestPoints[i];
                Vector2 idleOffset = Vector2.zero;

                if (animateIdle && idleWobbleAmplitude > 0f)
                {
                    float phase = pointPhaseOffsets[i] + time * idleWobbleSpeed;
                    idleOffset = new Vector2(Mathf.Cos(phase), Mathf.Sin(phase * 1.31f)) * idleWobbleAmplitude;
                }

                currentPoints[i] = Vector2.Lerp(restPoint + idleOffset, Vector2.zero, centerPull);
            }
        }

        private float ResolveOuterDistance(Vector2 direction)
        {
            float outerDistance = 0f;
            float scaledPointRadius = pointRadius;

            for (int i = 0; i < currentPoints.Length; i++)
            {
                Vector2 center = currentPoints[i];
                float projection = Vector2.Dot(direction, center);
                float perpendicularSqr = center.sqrMagnitude - projection * projection;
                float radiusSqr = scaledPointRadius * scaledPointRadius;

                if (perpendicularSqr > radiusSqr)
                    continue;

                float intersectionDistance = projection + Mathf.Sqrt(Mathf.Max(0f, radiusSqr - perpendicularSqr));
                if (intersectionDistance > outerDistance)
                    outerDistance = intersectionDistance;
            }

            return Mathf.Max(outerDistance, scaledPointRadius);
        }

        private bool ContainsGeneratedLocalPoint(Vector2 point)
        {
            bool inside = false;
            int samples = Mathf.Clamp(radialSampleCount, 16, 128);

            for (int i = 0, j = samples - 1; i < samples; j = i++)
            {
                Vector3 a = vertices[i + 1];
                Vector3 b = vertices[j + 1];
                bool crosses =
                    (a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / Mathf.Max(0.0001f, b.y - a.y) + a.x;

                if (crosses)
                    inside = !inside;
            }

            return inside;
        }

        private void ApplyRendererSettings(bool allowCreateOutlineObject = true)
        {
            if (meshRenderer == null)
                return;

            meshRenderer.enabled = renderSurface;
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder;
            meshRenderer.GetPropertyBlock(materialProperties);
            materialProperties.Clear();
            materialProperties.SetColor(ColorProperty, surfaceColor);
            materialProperties.SetColor(BaseColorProperty, surfaceColor);

            if (useProceduralPixelTexture)
            {
                EnsureProceduralPixelTexture();
                materialProperties.SetTexture(MainTexProperty, proceduralPixelTexture);
                materialProperties.SetTexture(BaseMapProperty, proceduralPixelTexture);
            }

            meshRenderer.SetPropertyBlock(materialProperties);

            ApplyOutlineRendererSettings(allowCreateOutlineObject);
        }

        private void ApplyOutlineRendererSettings(bool allowCreateObject)
        {
            // Particle 기반 Noita풍 장판으로 전환하면서 outline mesh 렌더링은 폐기한다.
            if (outlineMeshRenderer != null)
                outlineMeshRenderer.enabled = false;

            return;

#pragma warning disable CS0162
            if (!renderOutline)
            {
                if (outlineMeshRenderer != null)
                    outlineMeshRenderer.enabled = false;

                return;
            }

            if (!EnsureOutlineComponents(allowCreateObject))
                return;

            if (outlineMeshRenderer == null)
                return;

            outlineMeshRenderer.enabled = true;
            outlineMeshRenderer.sortingLayerName = sortingLayerName;
            outlineMeshRenderer.sortingOrder = sortingOrder + outlineSortingOrderOffset;
            outlineMeshRenderer.GetPropertyBlock(outlineMaterialProperties);
            outlineMaterialProperties.Clear();
            outlineMaterialProperties.SetColor(ColorProperty, outlineColor);
            outlineMaterialProperties.SetColor(BaseColorProperty, outlineColor);
            outlineMeshRenderer.SetPropertyBlock(outlineMaterialProperties);
#pragma warning restore CS0162
        }

        private Vector3 ResolveOutlinePoint(Vector2 point, Vector2 fallbackDirection)
        {
            if (!renderOutline || outlineWidth <= 0f)
                return point;

            Vector2 direction = point.sqrMagnitude > 0.0001f
                ? point.normalized
                : fallbackDirection;

            return point + direction * outlineWidth;
        }

        private Vector2 ResolvePixelPoint(Vector2 point)
        {
            if (!usePixelStyle || pixelSnapStrength <= 0f)
                return point;

            float gridSize = Mathf.Max(0.001f, pixelGridSize);
            Vector2 snapped = new(
                Mathf.Round(point.x / gridSize) * gridSize,
                Mathf.Round(point.y / gridSize) * gridSize);

            return Vector2.Lerp(point, snapped, pixelSnapStrength);
        }

        private Vector2 ResolvePixelUv(Vector2 uv)
        {
            if (!usePixelStyle || uvPixelSteps <= 0)
                return uv;

            float steps = Mathf.Max(1, uvPixelSteps);
            return new Vector2(
                Mathf.Round(uv.x * steps) / steps,
                Mathf.Round(uv.y * steps) / steps);
        }

        private void EnsureProceduralPixelTexture()
        {
            if (proceduralPixelTexture != null &&
                generatedPixelTextureSize == pixelTextureSize &&
                generatedPixelTexturePattern == pixelTexturePattern &&
                Mathf.Approximately(generatedPixelTextureNoiseStrength, pixelTextureNoiseStrength) &&
                Mathf.Approximately(generatedPixelTextureCenterLight, pixelTextureCenterLight) &&
                Mathf.Approximately(generatedPixelTextureCellContrast, pixelTextureCellContrast) &&
                Mathf.Approximately(generatedPixelTextureEdgeDarkness, pixelTextureEdgeDarkness) &&
                Mathf.Approximately(generatedPixelTextureSparkleChance, pixelTextureSparkleChance) &&
                generatedPixelTextureSeed == pixelTextureSeed)
            {
                return;
            }

            if (proceduralPixelTexture != null)
                Destroy(proceduralPixelTexture);

            int size = Mathf.Clamp(pixelTextureSize, 4, 64);
            proceduralPixelTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"{nameof(PuddleBlobVisual)}PixelTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            System.Random random = new(pixelTextureSeed);
            float center = (size - 1) * 0.5f;
            float maxDistance = Mathf.Max(0.001f, center);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float value = pixelTexturePattern == PixelTexturePattern.MaterialCells
                        ? ResolveMaterialCellPixelValue(x, y, size, center, maxDistance, random)
                        : ResolveSoftNoisePixelValue(x, y, center, maxDistance, random);

                    proceduralPixelTexture.SetPixel(x, y, new Color(value, value, value, 1f));
                }
            }

            proceduralPixelTexture.Apply(false, false);
            generatedPixelTextureSize = size;
            generatedPixelTexturePattern = pixelTexturePattern;
            generatedPixelTextureNoiseStrength = pixelTextureNoiseStrength;
            generatedPixelTextureCenterLight = pixelTextureCenterLight;
            generatedPixelTextureCellContrast = pixelTextureCellContrast;
            generatedPixelTextureEdgeDarkness = pixelTextureEdgeDarkness;
            generatedPixelTextureSparkleChance = pixelTextureSparkleChance;
            generatedPixelTextureSeed = pixelTextureSeed;
        }

        private float ResolveSoftNoisePixelValue(
            int x,
            int y,
            float center,
            float maxDistance,
            System.Random random)
        {
            float dx = (x - center) / maxDistance;
            float dy = (y - center) / maxDistance;
            float radial = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
            float noise = ((float)random.NextDouble() * 2f - 1f) * pixelTextureNoiseStrength;
            float checker = ((x + y) % 2 == 0 ? 1f : -1f) * pixelTextureNoiseStrength * 0.25f;
            return Mathf.Clamp01(1f + noise + checker + radial * pixelTextureCenterLight);
        }

        private float ResolveMaterialCellPixelValue(
            int x,
            int y,
            int size,
            float center,
            float maxDistance,
            System.Random random)
        {
            float dx = (x - center) / maxDistance;
            float dy = (y - center) / maxDistance;
            float radialDistance = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
            float radialLight = (1f - radialDistance) * pixelTextureCenterLight;
            float edgeDark = radialDistance * pixelTextureEdgeDarkness;

            int cellSize = Mathf.Max(2, Mathf.RoundToInt(size / 4f));
            int cellX = x / cellSize;
            int cellY = y / cellSize;
            float cellNoise = Hash01(cellX, cellY, pixelTextureSeed) * 2f - 1f;
            float fineNoise = ((float)random.NextDouble() * 2f - 1f) * pixelTextureNoiseStrength;
            float checker = ((x + y) % 2 == 0 ? 1f : -1f) * pixelTextureCellContrast * 0.12f;
            float sparkle = Hash01(x, y, pixelTextureSeed + 917) < pixelTextureSparkleChance
                ? pixelTextureCellContrast
                : 0f;

            float cellValue = cellNoise * pixelTextureCellContrast;
            return Mathf.Clamp01(1f + radialLight - edgeDark + cellValue + fineNoise + checker + sparkle);
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                int hash = seed;
                hash = (hash * 397) ^ x;
                hash = (hash * 397) ^ y;
                hash ^= hash << 13;
                hash ^= hash >> 17;
                hash ^= hash << 5;
                return (hash & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        private static float RandomRange(System.Random random, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        private static float SmoothStep(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}

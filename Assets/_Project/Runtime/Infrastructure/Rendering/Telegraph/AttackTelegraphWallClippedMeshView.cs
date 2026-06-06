using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 공통 공격 예고 도형 중 벽 차단 옵션이 켜진 원형/부채꼴/사각형을 raycast 샘플 기반 mesh로 렌더링한다.
    /// - AttackTelegraphView의 보조 렌더러로 동작하며, 공격 판정에는 관여하지 않는다.
    /// </summary>
    public sealed class AttackTelegraphWallClippedMeshView : MonoBehaviour
    {
        private const string DefaultShaderName = "Sprites/Default";

        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private LineRenderer borderLineRenderer;
        private LineRenderer innerBorderLineRenderer;
        private Material material;
        private Material borderMaterial;
        private Vector3[] vertices;
        private int[] triangles;
        private Vector3[] borderPositions;
        private const float BorderWidth = 0.045f;
        private const int WallClipHitBufferSize = 16;
        private readonly RaycastHit2D[] wallClipHitBuffer = new RaycastHit2D[WallClipHitBufferSize];
        private AttackTelegraphSpec activeSpec;
        private AttackTelegraphStyle activeStyle;

        public bool IsVisible => meshRenderer != null && meshRenderer.enabled;

        public void ShowOrUpdate(
            AttackTelegraphSpec spec,
            AttackTelegraphStyle style,
            SpriteRenderer sortingReference,
            float normalizedProgress)
        {
            EnsureComponents();
            activeSpec = spec;
            activeStyle = style;

            if (!TryRebuildMesh(spec, style, normalizedProgress))
            {
                HideImmediate();
                return;
            }

            ApplySorting(sortingReference);
            ApplyStyle(style, normalizedProgress);
            meshRenderer.enabled = true;
        }

        public void ApplyStyle(AttackTelegraphStyle style, float normalizedProgress)
        {
            if (material == null)
                return;

            activeStyle = style;
            if (IsVisible)
                TryRebuildMesh(activeSpec, activeStyle, normalizedProgress);

            float curved = style != null && style.progressCurve != null
                ? Mathf.Clamp01(style.progressCurve.Evaluate(normalizedProgress))
                : normalizedProgress;

            Color color = style != null
                ? Color.Lerp(style.fillColorStart, style.fillColorEnd, curved)
                : new Color(1f, 0.2f, 0.2f, 0.35f);

            if (style != null &&
                normalizedProgress >= style.blinkStartNormalized &&
                style.blinkFrequency > 0f)
            {
                float blinkWave = Mathf.Sin(Time.time * style.blinkFrequency * Mathf.PI * 2f);
                float blinkMultiplier = Mathf.Lerp(style.blinkAlphaMin, 1f, (blinkWave + 1f) * 0.5f);
                color.a *= blinkMultiplier;
            }

            material.color = color;

            if (borderMaterial != null)
            {
                Color borderColor = style != null
                    ? Color.Lerp(style.borderColorStart, style.borderColorEnd, curved)
                    : new Color(1f, 0.35f, 0.25f, 0.9f);

                if (style != null &&
                    normalizedProgress >= style.blinkStartNormalized &&
                    style.blinkFrequency > 0f)
                {
                    float blinkWave = Mathf.Sin(Time.time * style.blinkFrequency * Mathf.PI * 2f);
                    float blinkMultiplier = Mathf.Lerp(style.blinkAlphaMin, 1f, (blinkWave + 1f) * 0.5f);
                    borderColor.a *= blinkMultiplier;
                }

                borderMaterial.color = borderColor;
            }
        }

        public void HideImmediate()
        {
            if (meshRenderer != null)
                meshRenderer.enabled = false;

            if (borderLineRenderer != null)
                borderLineRenderer.enabled = false;

            if (innerBorderLineRenderer != null)
                innerBorderLineRenderer.enabled = false;
        }

        private void EnsureComponents()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
                if (meshFilter == null)
                    meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            if (borderLineRenderer == null)
            {
                borderLineRenderer = GetComponent<LineRenderer>();
                if (borderLineRenderer == null)
                    borderLineRenderer = gameObject.AddComponent<LineRenderer>();

                ConfigureBorderLineRenderer(borderLineRenderer);
            }

            if (innerBorderLineRenderer == null)
            {
                Transform innerBorderRoot = transform.Find("InnerBorderLine");
                if (innerBorderRoot == null)
                {
                    GameObject innerBorderObject = new GameObject("InnerBorderLine");
                    innerBorderRoot = innerBorderObject.transform;
                    innerBorderRoot.SetParent(transform, false);
                }

                innerBorderLineRenderer = innerBorderRoot.GetComponent<LineRenderer>();
                if (innerBorderLineRenderer == null)
                    innerBorderLineRenderer = innerBorderRoot.gameObject.AddComponent<LineRenderer>();

                ConfigureBorderLineRenderer(innerBorderLineRenderer);
            }

            if (mesh == null)
            {
                mesh = new Mesh { name = "AttackTelegraphWallClippedMesh" };
                meshFilter.sharedMesh = mesh;
            }

            if (material == null)
            {
                Shader shader = Shader.Find(DefaultShaderName);
                material = new Material(shader);
                meshRenderer.sharedMaterial = material;
            }

            if (borderMaterial == null)
            {
                Shader shader = Shader.Find(DefaultShaderName);
                borderMaterial = new Material(shader);
                borderLineRenderer.sharedMaterial = borderMaterial;
                innerBorderLineRenderer.sharedMaterial = borderMaterial;
            }
            else if (innerBorderLineRenderer.sharedMaterial == null)
            {
                innerBorderLineRenderer.sharedMaterial = borderMaterial;
            }
        }

        /// <summary>
        /// 책임 :
        /// - wall-clipped mesh 경계선을 그리는 LineRenderer의 공통 옵션을 맞춘다.
        /// </summary>
        private static void ConfigureBorderLineRenderer(LineRenderer renderer)
        {
            if (renderer == null)
                return;

            renderer.useWorldSpace = false;
            renderer.widthMultiplier = BorderWidth;
            renderer.numCapVertices = 0;
            renderer.numCornerVertices = 0;
            renderer.alignment = LineAlignment.TransformZ;
            renderer.textureMode = LineTextureMode.Stretch;
        }

        private bool TryRebuildMesh(AttackTelegraphSpec spec, AttackTelegraphStyle style, float normalizedProgress)
        {
            float fillScale = ResolveFillScale(style, normalizedProgress);
            switch (spec.shape)
            {
                case AttackTelegraphShape.Rectangle:
                    Vector2 rectangleDirection = Quaternion.Euler(0f, 0f, spec.rotationDeg) * Vector2.right;
                    float rectangleLength = Mathf.Max(0.01f, spec.size.x);
                    Vector2 rectangleStart = (Vector2)spec.center - (rectangleDirection.normalized * (rectangleLength * 0.5f));
                    RebuildRectangleMesh(
                        rectangleStart,
                        rectangleDirection,
                        rectangleLength,
                        Mathf.Max(0.01f, spec.size.y),
                        spec.wallClipLayers,
                        spec.wallClipSampleCount,
                        spec.wallClipSkinWidth,
                        fillScale);
                    return true;

                case AttackTelegraphShape.Circle:
                    RebuildRadialMesh(
                        spec.center,
                        Vector2.right,
                        new Vector2(
                            Mathf.Max(0.01f, spec.size.x * 0.5f),
                            Mathf.Max(0.01f, spec.size.y * 0.5f)),
                        360f,
                        spec.wallClipLayers,
                        spec.wallClipSampleCount,
                        spec.wallClipSkinWidth,
                        fillScale);
                    return true;

                case AttackTelegraphShape.Ring:
                    RebuildRingMesh(
                        spec.center,
                        Mathf.Max(spec.size.x, spec.size.y) * 0.5f,
                        Mathf.Max(0f, spec.innerDiameter) * 0.5f,
                        spec.wallClipLayers,
                        spec.wallClipSampleCount,
                        spec.wallClipSkinWidth,
                        fillScale);
                    return true;

                case AttackTelegraphShape.Sector:
                    RebuildRadialMesh(
                        spec.origin,
                        Quaternion.Euler(0f, 0f, spec.rotationDeg) * Vector2.right,
                        Mathf.Max(0.01f, spec.size.x),
                        spec.sectorAngleDeg,
                        spec.wallClipLayers,
                        spec.wallClipSampleCount,
                        spec.wallClipSkinWidth,
                        fillScale);
                    return true;

            }

            return false;
        }

        /// <summary>
        /// 책임 :
        /// - AttackTelegraphStyle의 진행도 기반 fill scale 설정을 wall-clipped mesh가 사용할 값으로 환산한다.
        /// </summary>
        private static float ResolveFillScale(AttackTelegraphStyle style, float normalizedProgress)
        {
            if (style == null || !style.scaleFillWithProgress)
                return 1f;

            float curved = style.progressCurve != null
                ? Mathf.Clamp01(style.progressCurve.Evaluate(normalizedProgress))
                : Mathf.Clamp01(normalizedProgress);
            return Mathf.Clamp01(Mathf.Lerp(Mathf.Clamp01(style.fillScaleStart), Mathf.Clamp01(style.fillScaleEnd), curved));
        }

        /// <summary>
        /// 책임 :
        /// - 원점에서 전방으로 뻗는 사각형 경고 영역을 폭 방향으로 샘플링해 벽에 닿는 지점까지만 mesh로 만든다.
        /// - 고블린 사수 조준선처럼 직선형 예고가 벽 너머까지 렌더링되지 않게 한다.
        /// </summary>
        private void RebuildRectangleMesh(
            Vector2 origin,
            Vector2 direction,
            float length,
            float width,
            LayerMask wallLayers,
            int sampleCount,
            float skinWidth,
            float fillScale)
        {
            transform.position = origin;
            transform.rotation = Quaternion.identity;

            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector2 right = new Vector2(-forward.y, forward.x);
            int safeSampleCount = Mathf.Max(2, sampleCount);
            int vertexCount = safeSampleCount * 2;
            int segmentCount = safeSampleCount - 1;
            EnsureMeshBuffers(vertexCount, segmentCount * 2);

            float halfWidth = Mathf.Max(0.01f, width) * 0.5f;
            float safeLength = Mathf.Max(0.01f, length);
            float safeSkin = Mathf.Max(0f, skinWidth);

            for (int i = 0; i < safeSampleCount; i++)
            {
                float t = safeSampleCount <= 1 ? 0.5f : i / (float)(safeSampleCount - 1);
                float offset = Mathf.Lerp(-halfWidth, halfWidth, t);
                Vector2 lateral = right * offset;
                float visibleDistance = ResolveVisibleDistance(origin + lateral, forward, safeLength, wallLayers, safeSkin);
                int vertexIndex = i * 2;
                vertices[vertexIndex] = lateral;
                vertices[vertexIndex + 1] = lateral + forward * visibleDistance;
            }

            int triangleIndex = 0;
            for (int i = 0; i < segmentCount; i++)
            {
                int startA = i * 2;
                int endA = startA + 1;
                int startB = startA + 2;
                int endB = startA + 3;

                triangles[triangleIndex++] = startA;
                triangles[triangleIndex++] = endA;
                triangles[triangleIndex++] = endB;

                triangles[triangleIndex++] = startA;
                triangles[triangleIndex++] = endB;
                triangles[triangleIndex++] = startB;
            }

            RebuildRectangleBorderLine(safeSampleCount);
            ApplyFillScaleToRectangleVertices(safeSampleCount, fillScale);

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// 책임 :
        /// - 도넛형 경고 영역을 각도별 raycast 거리로 잘라 annulus mesh로 갱신한다.
        /// - Witch 도넛 패턴처럼 안쪽 안전지대와 바깥 위험 경계를 동시에 보여야 하는 공격에 사용한다.
        /// </summary>
        private void RebuildRingMesh(
            Vector2 origin,
            float outerRadius,
            float innerRadius,
            LayerMask wallLayers,
            int sampleCount,
            float skinWidth,
            float fillScale)
        {
            transform.position = origin;
            transform.rotation = Quaternion.identity;

            int safeSampleCount = Mathf.Max(8, sampleCount);
            EnsureMeshBuffers(safeSampleCount * 2, safeSampleCount * 2);

            float safeOuterRadius = Mathf.Max(0.01f, outerRadius);
            float safeInnerRadius = Mathf.Clamp(innerRadius, 0f, safeOuterRadius - 0.01f);
            float safeSkin = Mathf.Max(0f, skinWidth);

            for (int i = 0; i < safeSampleCount; i++)
            {
                float angle = i / (float)safeSampleCount * 360f;
                Vector2 rayDirection = Rotate(Vector2.right, angle);
                float visibleOuterDistance = ResolveVisibleDistance(origin, rayDirection, safeOuterRadius, wallLayers, safeSkin);
                float visibleInnerDistance = Mathf.Min(safeInnerRadius, visibleOuterDistance);
                int vertexIndex = i * 2;
                vertices[vertexIndex] = rayDirection * visibleInnerDistance;
                vertices[vertexIndex + 1] = rayDirection * visibleOuterDistance;
            }

            int triangleIndex = 0;
            for (int i = 0; i < safeSampleCount; i++)
            {
                int next = i + 1;
                if (next >= safeSampleCount)
                    next = 0;

                int innerA = i * 2;
                int outerA = innerA + 1;
                int innerB = next * 2;
                int outerB = innerB + 1;

                triangles[triangleIndex++] = innerA;
                triangles[triangleIndex++] = outerA;
                triangles[triangleIndex++] = outerB;

                triangles[triangleIndex++] = innerA;
                triangles[triangleIndex++] = outerB;
                triangles[triangleIndex++] = innerB;
            }

            RebuildRingBorderLines(safeSampleCount);
            ApplyFillScaleToRingVertices(safeSampleCount, safeInnerRadius, fillScale);

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        private void RebuildRadialMesh(
            Vector2 origin,
            Vector2 direction,
            float range,
            float angleDegrees,
            LayerMask wallLayers,
            int sampleCount,
            float skinWidth,
            float fillScale)
        {
            float safeRange = Mathf.Max(0.01f, range);
            RebuildRadialMesh(
                origin,
                direction,
                new Vector2(safeRange, safeRange),
                angleDegrees,
                wallLayers,
                sampleCount,
                skinWidth,
                fillScale);
        }

        private void RebuildRadialMesh(
            Vector2 origin,
            Vector2 direction,
            Vector2 radii,
            float angleDegrees,
            LayerMask wallLayers,
            int sampleCount,
            float skinWidth,
            float fillScale)
        {
            transform.position = origin;
            transform.rotation = Quaternion.identity;

            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            bool isFullCircle = angleDegrees >= 359.9f;
            int safeSampleCount = Mathf.Max(3, sampleCount);
            int outerVertexCount = isFullCircle ? safeSampleCount : safeSampleCount + 1;
            int vertexCount = outerVertexCount + 1;
            EnsureMeshBuffers(vertexCount, isFullCircle ? safeSampleCount : safeSampleCount);

            vertices[0] = Vector3.zero;
            float halfAngle = Mathf.Clamp(angleDegrees, 0.1f, 360f) * 0.5f;
            Vector2 safeRadii = new(Mathf.Max(0.01f, radii.x), Mathf.Max(0.01f, radii.y));
            float safeSkin = Mathf.Max(0f, skinWidth);

            for (int i = 0; i < outerVertexCount; i++)
            {
                float t = isFullCircle
                    ? i / (float)outerVertexCount
                    : i / (float)(outerVertexCount - 1);
                float angle = isFullCircle
                    ? t * 360f
                    : Mathf.Lerp(-halfAngle, halfAngle, t);
                Vector2 rayDirection = Rotate(forward, angle);
                float rayRange = ResolveEllipseRayDistance(rayDirection, safeRadii);
                float distance = ResolveVisibleDistance(origin, rayDirection, rayRange, wallLayers, safeSkin);
                vertices[i + 1] = rayDirection * distance;
            }

            RebuildBorderLine(outerVertexCount, isFullCircle);
            ApplyFillScaleToRadialVertices(outerVertexCount, fillScale);

            int triangleIndex = 0;
            int triangleFanCount = isFullCircle ? outerVertexCount : outerVertexCount - 1;
            for (int i = 0; i < triangleFanCount; i++)
            {
                int next = i + 1;
                if (next >= outerVertexCount)
                    next = 0;

                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = i + 1;
                triangles[triangleIndex++] = next + 1;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        private static float ResolveEllipseRayDistance(Vector2 rayDirection, Vector2 radii)
        {
            Vector2 safeDirection = rayDirection.sqrMagnitude > 0.0001f ? rayDirection.normalized : Vector2.right;
            float radiusX = Mathf.Max(0.01f, radii.x);
            float radiusY = Mathf.Max(0.01f, radii.y);
            float denominator =
                (safeDirection.x * safeDirection.x) / (radiusX * radiusX) +
                (safeDirection.y * safeDirection.y) / (radiusY * radiusY);
            if (denominator <= 0.0001f)
                return radiusX;

            return 1f / Mathf.Sqrt(denominator);
        }

        /// <summary>
        /// 책임 :
        /// - 사각형 경고의 외곽선은 유지하고 fill mesh만 중심에서 진행도만큼 커지게 한다.
        /// </summary>
        private void ApplyFillScaleToRectangleVertices(int sampleCount, float fillScale)
        {
            if (fillScale >= 0.999f)
                return;

            for (int i = 0; i < sampleCount; i++)
            {
                int startIndex = i * 2;
                int endIndex = startIndex + 1;
                vertices[endIndex] = Vector3.Lerp(vertices[startIndex], vertices[endIndex], fillScale);
            }
        }

        /// <summary>
        /// 책임 :
        /// - 원형/부채꼴 경고의 외곽선은 유지하고 fill mesh만 원점에서 진행도만큼 확장한다.
        /// </summary>
        private void ApplyFillScaleToRadialVertices(int outerVertexCount, float fillScale)
        {
            if (fillScale >= 0.999f)
                return;

            for (int i = 0; i < outerVertexCount; i++)
                vertices[i + 1] *= fillScale;
        }

        /// <summary>
        /// 책임 :
        /// - 도넛 경고의 안쪽/바깥쪽 외곽선은 유지하고 fill mesh만 안쪽 안전지대에서 바깥 방향으로 차오르게 한다.
        /// </summary>
        private void ApplyFillScaleToRingVertices(int sampleCount, float innerRadius, float fillScale)
        {
            if (fillScale >= 0.999f)
                return;

            for (int i = 0; i < sampleCount; i++)
            {
                int innerIndex = i * 2;
                int outerIndex = innerIndex + 1;
                Vector3 inner = vertices[innerIndex];
                Vector3 outer = vertices[outerIndex];
                float outerDistance = outer.magnitude;
                float visibleThickness = Mathf.Max(0f, outerDistance - innerRadius);
                float scaledOuterDistance = innerRadius + visibleThickness * fillScale;
                vertices[outerIndex] = outerDistance > 0.0001f
                    ? outer.normalized * scaledOuterDistance
                    : inner;
            }
        }

        /// <summary>
        /// 책임 :
        /// - 벽에 의해 잘린 원형/부채꼴 mesh의 외곽 샘플점을 따라 테두리 선을 갱신한다.
        /// - 일반 Sprite border를 쓰지 못하는 wall-clipped 렌더링 경로에서도 위험 영역 경계를 읽기 쉽게 만든다.
        /// </summary>
        private void RebuildBorderLine(int outerVertexCount, bool isFullCircle)
        {
            if (borderLineRenderer == null)
                return;

            if (innerBorderLineRenderer != null)
                innerBorderLineRenderer.enabled = false;

            if (isFullCircle)
            {
                EnsureBorderBuffer(outerVertexCount);
                for (int i = 0; i < outerVertexCount; i++)
                    borderPositions[i] = vertices[i + 1];

                borderLineRenderer.loop = true;
                borderLineRenderer.positionCount = outerVertexCount;
                borderLineRenderer.SetPositions(borderPositions);
            }
            else
            {
                int positionCount = outerVertexCount + 2;
                EnsureBorderBuffer(positionCount);
                borderPositions[0] = Vector3.zero;
                for (int i = 0; i < outerVertexCount; i++)
                    borderPositions[i + 1] = vertices[i + 1];

                borderPositions[positionCount - 1] = Vector3.zero;
                borderLineRenderer.loop = false;
                borderLineRenderer.positionCount = positionCount;
                borderLineRenderer.SetPositions(borderPositions);
            }

            borderLineRenderer.enabled = true;
        }

        /// <summary>
        /// 책임 :
        /// - 도넛형 mesh의 바깥/안쪽 경계선을 각각 갱신해 안전지대와 위험지대를 명확히 분리한다.
        /// </summary>
        private void RebuildRingBorderLines(int sampleCount)
        {
            if (borderLineRenderer == null || innerBorderLineRenderer == null)
                return;

            EnsureBorderBuffer(sampleCount);

            for (int i = 0; i < sampleCount; i++)
                borderPositions[i] = vertices[i * 2 + 1];

            borderLineRenderer.loop = true;
            borderLineRenderer.positionCount = sampleCount;
            borderLineRenderer.SetPositions(borderPositions);
            borderLineRenderer.enabled = true;

            for (int i = 0; i < sampleCount; i++)
                borderPositions[i] = vertices[i * 2];

            innerBorderLineRenderer.loop = true;
            innerBorderLineRenderer.positionCount = sampleCount;
            innerBorderLineRenderer.SetPositions(borderPositions);
            innerBorderLineRenderer.enabled = true;
        }

        /// <summary>
        /// 책임 :
        /// - 벽에 의해 잘린 사각형 mesh의 바깥 윤곽선을 start edge, clipped front edge 순서로 갱신한다.
        /// </summary>
        private void RebuildRectangleBorderLine(int sampleCount)
        {
            if (borderLineRenderer == null)
                return;

            if (innerBorderLineRenderer != null)
                innerBorderLineRenderer.enabled = false;

            int positionCount = sampleCount * 2 + 1;
            EnsureBorderBuffer(positionCount);

            for (int i = 0; i < sampleCount; i++)
                borderPositions[i] = vertices[i * 2];

            for (int i = sampleCount - 1; i >= 0; i--)
            {
                int sourceVertex = i * 2 + 1;
                int targetIndex = sampleCount + (sampleCount - 1 - i);
                borderPositions[targetIndex] = vertices[sourceVertex];
            }

            borderPositions[positionCount - 1] = vertices[0];
            borderLineRenderer.loop = false;
            borderLineRenderer.positionCount = positionCount;
            borderLineRenderer.SetPositions(borderPositions);
            borderLineRenderer.enabled = true;
        }

        private float ResolveVisibleDistance(
            Vector2 origin,
            Vector2 rayDirection,
            float range,
            LayerMask wallLayers,
            float skinWidth)
        {
            if (wallLayers.value == 0)
                return range;

            if (!TryFindNearestWallClipHit(origin, rayDirection, range, wallLayers, out RaycastHit2D hit))
                return range;

            return Mathf.Clamp(hit.distance - skinWidth, 0f, range);
        }

        /// <summary>경고 mesh clipping은 실제 벽/문 같은 non-trigger 장애물만 사용하고, HoleTrap 같은 trigger 감지 영역은 무시합니다.</summary>
        private bool TryFindNearestWallClipHit(
            Vector2 origin,
            Vector2 rayDirection,
            float range,
            LayerMask wallLayers,
            out RaycastHit2D nearestHit)
        {
            nearestHit = default;
            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = false
            };
            filter.SetLayerMask(wallLayers);

            int hitCount = Physics2D.Raycast(origin, rayDirection, filter, wallClipHitBuffer, range);
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

        private void EnsureMeshBuffers(int vertexCount, int triangleFanCount)
        {
            if (vertices == null || vertices.Length != vertexCount)
                vertices = new Vector3[vertexCount];

            int triangleCount = Mathf.Max(0, triangleFanCount) * 3;
            if (triangles == null || triangles.Length != triangleCount)
                triangles = new int[triangleCount];
        }

        private void EnsureBorderBuffer(int positionCount)
        {
            if (borderPositions == null || borderPositions.Length != positionCount)
                borderPositions = new Vector3[positionCount];
        }

        private void ApplySorting(SpriteRenderer sortingReference)
        {
            if (meshRenderer == null || sortingReference == null)
                return;

            meshRenderer.sortingLayerID = sortingReference.sortingLayerID;
            meshRenderer.sortingOrder = sortingReference.sortingOrder;

            if (borderLineRenderer != null)
            {
                borderLineRenderer.sortingLayerID = sortingReference.sortingLayerID;
                borderLineRenderer.sortingOrder = sortingReference.sortingOrder + 1;
            }

            if (innerBorderLineRenderer != null)
            {
                innerBorderLineRenderer.sortingLayerID = sortingReference.sortingLayerID;
                innerBorderLineRenderer.sortingOrder = sortingReference.sortingOrder + 1;
            }
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

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);

            if (borderMaterial != null)
                Destroy(borderMaterial);

            if (mesh != null)
                Destroy(mesh);
        }
    }
}

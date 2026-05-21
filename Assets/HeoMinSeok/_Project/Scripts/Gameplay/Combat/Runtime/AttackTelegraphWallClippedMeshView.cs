using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 공통 공격 예고 도형 중 벽 차단 옵션이 켜진 원형/부채꼴을 raycast 샘플 기반 mesh로 렌더링한다.
    /// - AttackTelegraphView의 보조 렌더러로 동작하며, 공격 판정에는 관여하지 않는다.
    /// </summary>
    public sealed class AttackTelegraphWallClippedMeshView : MonoBehaviour
    {
        private const string DefaultShaderName = "Sprites/Default";

        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private LineRenderer borderLineRenderer;
        private Material material;
        private Material borderMaterial;
        private Vector3[] vertices;
        private int[] triangles;
        private Vector3[] borderPositions;
        private const float BorderWidth = 0.045f;

        public bool IsVisible => meshRenderer != null && meshRenderer.enabled;

        public void ShowOrUpdate(
            AttackTelegraphSpec spec,
            AttackTelegraphStyle style,
            SpriteRenderer sortingReference,
            float normalizedProgress)
        {
            EnsureComponents();

            if (!TryRebuildMesh(spec))
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

                borderLineRenderer.useWorldSpace = false;
                borderLineRenderer.widthMultiplier = BorderWidth;
                borderLineRenderer.numCapVertices = 0;
                borderLineRenderer.numCornerVertices = 0;
                borderLineRenderer.alignment = LineAlignment.TransformZ;
                borderLineRenderer.textureMode = LineTextureMode.Stretch;
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
            }
        }

        private bool TryRebuildMesh(AttackTelegraphSpec spec)
        {
            switch (spec.shape)
            {
                case AttackTelegraphShape.Circle:
                    RebuildRadialMesh(
                        spec.center,
                        Vector2.right,
                        Mathf.Max(spec.size.x, spec.size.y) * 0.5f,
                        360f,
                        spec.wallClipLayers,
                        spec.wallClipSampleCount,
                        spec.wallClipSkinWidth);
                    return true;

                case AttackTelegraphShape.Sector:
                    RebuildRadialMesh(
                        spec.origin,
                        Quaternion.Euler(0f, 0f, spec.rotationDeg) * Vector2.right,
                        Mathf.Max(0.01f, spec.size.x),
                        spec.sectorAngleDeg,
                        spec.wallClipLayers,
                        spec.wallClipSampleCount,
                        spec.wallClipSkinWidth);
                    return true;

            }

            return false;
        }

        private void RebuildRadialMesh(
            Vector2 origin,
            Vector2 direction,
            float range,
            float angleDegrees,
            LayerMask wallLayers,
            int sampleCount,
            float skinWidth)
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
            float safeRange = Mathf.Max(0.01f, range);
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
                float distance = ResolveVisibleDistance(origin, rayDirection, safeRange, wallLayers, safeSkin);
                vertices[i + 1] = rayDirection * distance;
            }

            RebuildBorderLine(outerVertexCount, isFullCircle);

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

        /// <summary>
        /// 책임 :
        /// - 벽에 의해 잘린 원형/부채꼴 mesh의 외곽 샘플점을 따라 테두리 선을 갱신한다.
        /// - 일반 Sprite border를 쓰지 못하는 wall-clipped 렌더링 경로에서도 위험 영역 경계를 읽기 쉽게 만든다.
        /// </summary>
        private void RebuildBorderLine(int outerVertexCount, bool isFullCircle)
        {
            if (borderLineRenderer == null)
                return;

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

        private float ResolveVisibleDistance(
            Vector2 origin,
            Vector2 rayDirection,
            float range,
            LayerMask wallLayers,
            float skinWidth)
        {
            if (wallLayers.value == 0)
                return range;

            RaycastHit2D hit = Physics2D.Raycast(origin, rayDirection, range, wallLayers);
            if (hit.collider == null)
                return range;

            return Mathf.Clamp(hit.distance - skinWidth, 0f, range);
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

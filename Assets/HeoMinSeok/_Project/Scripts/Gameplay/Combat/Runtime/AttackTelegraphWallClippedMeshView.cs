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
        private Material material;
        private Vector3[] vertices;
        private int[] triangles;

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
        }

        public void HideImmediate()
        {
            if (meshRenderer != null)
                meshRenderer.enabled = false;
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

        private void ApplySorting(SpriteRenderer sortingReference)
        {
            if (meshRenderer == null || sortingReference == null)
                return;

            meshRenderer.sortingLayerID = sortingReference.sortingLayerID;
            meshRenderer.sortingOrder = sortingReference.sortingOrder;
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

            if (mesh != null)
                Destroy(mesh);
        }
    }
}

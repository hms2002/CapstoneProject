using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 런타임에 생성된 파티클 렌더러 계층의 transform 값이 비정상(NaN, Infinity, 과도한 거리/스케일)인지 감시한다.
    /// - 비정상 상태를 처음 감지한 순간 프리팹 이름, 계층 경로, transform 값을 로그로 남겨 원인 파티클을 빠르게 특정하게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParticleRendererRuntimeGuard2D : MonoBehaviour
    {
        [SerializeField] private string sourcePrefabName;
        [SerializeField] private string spawnContext;
        [SerializeField] private float maxReasonableDistance = 10000f;
        [SerializeField] private float maxReasonableScale = 1000f;

        private bool hasReported;
        private ParticleSystemRenderer[] particleRenderers = System.Array.Empty<ParticleSystemRenderer>();

        /// <summary>
        /// 책임 :
        /// - 런타임 가드가 어느 프리팹/문맥에서 생성됐는지 기록한다.
        /// - 이후 오류 로그에 최소한의 재현 단서를 남긴다.
        /// </summary>
        public void Initialize(string sourcePrefabName, string spawnContext)
        {
            this.sourcePrefabName = sourcePrefabName;
            this.spawnContext = spawnContext;
            particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(includeInactive: true);
        }

        private void Awake()
        {
            if (particleRenderers == null || particleRenderers.Length == 0)
                particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(includeInactive: true);
        }

        private void LateUpdate()
        {
            if (hasReported)
                return;

            if (HasInvalidTransform(transform, out string reason))
            {
                Report(reason, transform);
                return;
            }

            for (int i = 0; i < particleRenderers.Length; i++)
            {
                ParticleSystemRenderer renderer = particleRenderers[i];
                if (renderer == null)
                    continue;

                Transform current = renderer.transform;
                if (!HasInvalidTransform(current, out reason))
                    continue;

                Report(reason, current);
                return;
            }
        }

        /// <summary>
        /// 책임 :
        /// - 개별 transform이 렌더러 bounds를 깨뜨릴 수 있는 비정상 상태인지 판단한다.
        /// - 유한값 여부뿐 아니라 과도한 거리/스케일도 함께 검출해 재현이 어려운 AABB 문제를 조기에 식별한다.
        /// </summary>
        private bool HasInvalidTransform(Transform target, out string reason)
        {
            reason = null;
            if (target == null)
            {
                reason = "transform is null";
                return true;
            }

            Vector3 position = target.position;
            Vector3 scale = target.lossyScale;
            Quaternion rotation = target.rotation;

            if (!IsFiniteVector3(position))
            {
                reason = $"non-finite position {position}";
                return true;
            }

            if (!IsFiniteVector3(scale))
            {
                reason = $"non-finite scale {scale}";
                return true;
            }

            if (!IsFiniteQuaternion(rotation))
            {
                reason = $"non-finite rotation {rotation}";
                return true;
            }

            if (position.sqrMagnitude > maxReasonableDistance * maxReasonableDistance)
            {
                reason = $"position too far {position}";
                return true;
            }

            if (Mathf.Abs(scale.x) > maxReasonableScale ||
                Mathf.Abs(scale.y) > maxReasonableScale ||
                Mathf.Abs(scale.z) > maxReasonableScale)
            {
                reason = $"scale too large {scale}";
                return true;
            }

            return false;
        }

        private void Report(string reason, Transform target)
        {
            hasReported = true;
            string hierarchy = BuildHierarchyPath(target);
            Debug.LogWarning(
                $"[ParticleRendererRuntimeGuard2D] Invalid particle transform detected. prefab={sourcePrefabName}, context={spawnContext}, hierarchy={hierarchy}, reason={reason}, position={target.position}, rotation={target.rotation}, scale={target.lossyScale}");
        }

        private static string BuildHierarchyPath(Transform target)
        {
            if (target == null)
                return "(null)";

            string path = target.name;
            Transform current = target.parent;
            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }
    }
}

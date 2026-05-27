using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 전투 객체가 원거리 조준, 몸 중심, 머리 위 표시처럼 목적별 기준점을 제공한다.
    /// - 명시 Transform이 없더라도 root 기준 offset으로 안정적인 fallback 조준점을 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatAimPointProvider2D : MonoBehaviour
    {
        [Header("Optional Points")]
        [SerializeField] private Transform bodyCenter;
        [SerializeField] private Transform projectileTarget;
        [SerializeField] private Transform overhead;

        [Header("Fallback Offsets")]
        [SerializeField] private Vector2 bodyCenterOffset = new(0f, 0.45f);
        [SerializeField] private Vector2 projectileTargetOffset = new(0f, 0.55f);
        [SerializeField] private Vector2 overheadOffset = new(0f, 1.05f);

        public Vector2 Resolve(CombatAimPointKind kind)
        {
            return kind switch
            {
                CombatAimPointKind.BodyCenter => ResolvePoint(bodyCenter, bodyCenterOffset),
                CombatAimPointKind.ProjectileTarget => ResolvePoint(projectileTarget, projectileTargetOffset),
                CombatAimPointKind.Overhead => ResolvePoint(overhead, overheadOffset),
                _ => transform.position
            };
        }

        private Vector2 ResolvePoint(Transform point, Vector2 fallbackOffset)
        {
            if (point != null)
                return point.position;

            return (Vector2)transform.position + fallbackOffset;
        }
    }
}

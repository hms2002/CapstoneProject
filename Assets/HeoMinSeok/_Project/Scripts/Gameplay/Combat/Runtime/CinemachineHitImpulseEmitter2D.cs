using Unity.Cinemachine;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 플레이어 피격 시 Cinemachine impulse를 발생시켜 카메라 흔들림을 전달한다.
    /// - 피격 강도와 가해자 방향을 읽어 impulse 세기를 보정하고, source 컴포넌트 자동 보장을 담당한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CinemachineHitImpulseEmitter2D : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private CinemachineImpulseSource impulseSource;

        [Header("Tuning")]
        [SerializeField] private Vector3 defaultDirection = new Vector3(0f, 1f, 0f);
        [SerializeField, Min(0f)] private float amplitudeMultiplier = 1f;

        private void Awake()
        {
            EnsureImpulseSource();
        }

        /// <summary>
        /// 책임 :
        /// - 현재 피격 문맥에 맞는 방향/세기로 impulse를 1회 발행한다.
        /// - 가해자 정보가 있으면 피격 반대 방향 기반으로, 없으면 기본 방향으로 흔들림을 만든다.
        /// </summary>
        public void Emit(GameObject causer, float amplitude)
        {
            if (amplitude <= 0f || !GameSettingsService.IsScreenShakeEnabled())
                return;

            var source = EnsureImpulseSource();
            if (source == null)
                return;

            Vector3 direction = ResolveDirection(causer);
            source.GenerateImpulse(direction * (amplitude * amplitudeMultiplier));
        }

        /// <summary>
        /// 책임 : 같은 오브젝트에 붙은 CinemachineImpulseSource를 자동으로 보장한다.
        /// </summary>
        private CinemachineImpulseSource EnsureImpulseSource()
        {
            if (impulseSource == null)
                impulseSource = GetComponent<CinemachineImpulseSource>();

            if (impulseSource == null)
                impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();

            return impulseSource;
        }

        /// <summary>
        /// 책임 : 카메라 흔들림의 기본 방향 벡터를 계산한다.
        /// </summary>
        private Vector3 ResolveDirection(GameObject causer)
        {
            if (causer != null)
            {
                Vector3 delta = transform.position - causer.transform.position;
                delta.z = 0f;

                if (delta.sqrMagnitude > 0.0001f)
                    return delta.normalized;
            }

            if (defaultDirection.sqrMagnitude > 0.0001f)
                return defaultDirection.normalized;

            return Vector3.up;
        }
    }
}

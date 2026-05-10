using System.Collections;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 기묘한 쇳덩이 장착 프리팹의 총구 소켓과 발사 반동 프레젠테이션을 소유한다.
    /// - 사격 AL이 무기 프리팹 계층을 직접 알지 않도록, 총구 위치와 recoil 실행만 공개한다.
    /// </summary>
    public sealed class OddIronRuntimeState : WeaponAbilityRuntimeState
    {
        [Header("Sockets")]
        [SerializeField] private Transform recoilRoot;
        [SerializeField] private Transform muzzleSocket;
        [SerializeField] private Vector3 fallbackMuzzleOffset = new(0.8f, 0.15f, 0f);

        [Header("Recoil")]
        [SerializeField, Min(0f)] private float recoilDistance = 0.16f;
        [SerializeField] private float verticalKickDistance = 0.06f;
        [SerializeField, Min(0.001f)] private float recoilOutSeconds = 0.035f;
        [SerializeField, Min(0.001f)] private float recoilReturnSeconds = 0.09f;
        [SerializeField] private AnimationCurve recoilCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Coroutine recoilRoutine;
        private Transform cachedRecoilRoot;
        private Vector3 baseLocalPosition;
        private bool hasBaseLocalPosition;

        private void Awake()
        {
            CacheBaseLocalPosition();
        }

        private void OnDisable()
        {
            StopRecoil(resetPosition: true);
        }

        public override void HandleEquippedWeaponChanged(WeaponDefinition previousWeapon, WeaponDefinition newWeapon)
        {
            StopRecoil(resetPosition: true);
            CacheBaseLocalPosition();
        }

        public Vector3 ResolveMuzzlePosition(AbilitySystem system, Vector2 direction, Vector3 dataOffset)
        {
            if (muzzleSocket != null)
                return muzzleSocket.position;

            Vector3 offset = dataOffset != Vector3.zero ? dataOffset : fallbackMuzzleOffset;
            return OddIronAbilityUtility.ResolveSpawnPosition(system, direction, offset);
        }

        public Quaternion ResolveMuzzleRotation(Vector2 direction)
        {
            if (muzzleSocket != null)
                return muzzleSocket.rotation;

            float angle = direction.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                : 0f;
            return Quaternion.Euler(0f, 0f, angle);
        }

        public void PlayFireRecoil(Vector2 direction)
        {
            Transform target = ResolveRecoilRoot();
            if (target == null || (recoilDistance <= 0f && Mathf.Approximately(verticalKickDistance, 0f)))
                return;

            CacheBaseLocalPosition();
            StopRecoil(resetPosition: false);
            recoilRoutine = StartCoroutine(PlayRecoilRoutine(target, direction));
        }

        private IEnumerator PlayRecoilRoutine(Transform target, Vector2 direction)
        {
            Vector3 recoilOffset = ResolveLocalRecoilOffset(target, direction);
            yield return TweenLocalPosition(target, baseLocalPosition, baseLocalPosition + recoilOffset, recoilOutSeconds);
            yield return TweenLocalPosition(target, target.localPosition, baseLocalPosition, recoilReturnSeconds);
            recoilRoutine = null;
        }

        private IEnumerator TweenLocalPosition(Transform target, Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.001f, duration);

            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float eased = recoilCurve != null ? recoilCurve.Evaluate(t) : t;
                target.localPosition = Vector3.LerpUnclamped(from, to, eased);
                yield return null;
            }

            target.localPosition = to;
        }

        private Vector3 ResolveLocalRecoilOffset(Transform target, Vector2 direction)
        {
            Vector3 worldBackward = direction.sqrMagnitude > 0.0001f
                ? -(Vector3)direction.normalized
                : -transform.right;

            Vector3 localBackward = target.parent != null
                ? target.parent.InverseTransformVector(worldBackward)
                : worldBackward;

            localBackward.z = 0f;
            return localBackward.sqrMagnitude > 0.0001f
                ? localBackward.normalized * recoilDistance + ResolveLocalVerticalKick(target)
                : Vector3.left * recoilDistance + ResolveLocalVerticalKick(target);
        }

        private Vector3 ResolveLocalVerticalKick(Transform target)
        {
            if (Mathf.Approximately(verticalKickDistance, 0f))
                return Vector3.zero;

            Vector3 worldUp = Vector3.up * verticalKickDistance;
            Vector3 localUp = target.parent != null
                ? target.parent.InverseTransformVector(worldUp)
                : worldUp;
            localUp.z = 0f;
            return localUp;
        }

        private Transform ResolveRecoilRoot()
        {
            return recoilRoot != null ? recoilRoot : transform;
        }

        private void CacheBaseLocalPosition()
        {
            Transform target = ResolveRecoilRoot();
            if (target == null)
                return;

            cachedRecoilRoot = target;
            baseLocalPosition = target.localPosition;
            hasBaseLocalPosition = true;
        }

        private void StopRecoil(bool resetPosition)
        {
            if (recoilRoutine != null)
            {
                StopCoroutine(recoilRoutine);
                recoilRoutine = null;
            }

            if (resetPosition && hasBaseLocalPosition && cachedRecoilRoot != null)
                cachedRecoilRoot.localPosition = baseLocalPosition;
        }
    }
}

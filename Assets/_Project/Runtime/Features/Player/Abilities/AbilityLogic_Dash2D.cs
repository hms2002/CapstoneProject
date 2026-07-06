using System.Collections;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 대쉬 실행 중 이동 제어와 임시 태그(무적/에임락)를 관리한다.
    /// - 씬 이동 시 남아 있으면 안 되는 motion/태그를 강제 정리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_Dash2D", menuName = "GAS/Samples/AbilityLogic/Dash 2D")]
    public class AbilityLogic_Dash2D : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec == null || spec.Definition == null) yield break;

            var data = spec.Definition.sourceObject as Dash2DData;
            if (data == null)
            {
                Debug.LogError("[Dash2D] AbilityDefinition.sourceObject must be Dash2DData.");
                yield break;
            }

            float duration = Mathf.Max(0.01f, data.duration);
            float distance = Mathf.Max(0f, data.distance);
            if (distance <= 0.0001f) yield break;

            Vector2 dir = ResolveMoveDirection(system, data.useAimWhenNoMoveInput);
            if (dir.sqrMagnitude < 0.0001f) yield break;
            dir.Normalize();

            IWeaponDashAugment dashAugment = ResolveDashAugment(system);
            dashAugment?.ModifyDash(ref duration, ref distance);

            duration = Mathf.Max(0.01f, duration);
            distance = Mathf.Max(0f, distance);
            if (distance <= 0.0001f) yield break;

            var tags = system.GetComponent<TagSystem>();
            var motion = system.GetComponent<AbilityMotionController2D>();

            if (motion == null)
            {
                Debug.LogError("[Dash2D] AbilityMotionController2D가 필요합니다.");
                yield break;
            }

            try
            {
                if (tags != null)
                {
                    if (data.invulnerableTag != null) tags.AddTag(data.invulnerableTag, 1);
                    if (data.aimLockedTag != null) tags.AddTag(data.aimLockedTag, 1);
                }

                if (spec.Definition.animationTriggerHash != 0)
                    system.TryPlayAnimationTriggerHash(spec.Definition.animationTriggerHash, spec.Definition);

                float dashSpeed = distance / duration;
                Vector2 startPosition = system.transform.position;
                motion.StartDash(dir, dashSpeed, duration);
                dashAugment?.HandleDashStarted(
                    system,
                    spec,
                    spec.Definition,
                    dir,
                    startPosition,
                    duration,
                    distance);

                float elapsed = 0f;
                bool cancelled = false;
                while (elapsed < duration)
                {
                    if (spec.Token != null && spec.Token.IsCancelled)
                    {
                        cancelled = true;
                        motion.CancelMotion();
                        dashAugment?.HandleDashFinished(
                            system,
                            spec,
                            spec.Definition,
                            dir,
                            startPosition,
                            system.transform.position,
                            cancelled);
                        yield break;
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                dashAugment?.HandleDashFinished(
                    system,
                    spec,
                    spec.Definition,
                    dir,
                    startPosition,
                    system.transform.position,
                    cancelled);

                if (data.postLockTime > 0f && tags != null)
                {
                    float end = Time.time + data.postLockTime;
                    while (Time.time < end)
                    {
                        if (spec.Token != null && spec.Token.IsCancelled)
                            break;

                        yield return null;
                    }
                }
            }
            finally
            {
                ForceCleanup(system, spec);
            }
        }

        /// <summary>
        /// 책임 :
        /// - 씬 이동 직전에 Dash가 만든 motion/임시 태그를 즉시 회수한다.
        /// </summary>
        public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
        {
            ForceCleanup(system, spec);
        }

        /// <summary>
        /// 책임 :
        /// - Dash 실행 중 생성한 motion, invulnerableTag, aimLockedTag를 강제로 정리한다.
        /// </summary>
        private void ForceCleanup(AbilitySystem system, AbilitySpec spec)
        {
            if (system == null || spec == null || spec.Definition == null)
                return;

            var data = spec.Definition.sourceObject as Dash2DData;
            if (data == null)
                return;

            var tags = system.GetComponent<TagSystem>();
            var motion = system.GetComponent<AbilityMotionController2D>();

            if (motion != null)
                motion.CancelMotion();

            if (tags != null)
            {
                if (data.invulnerableTag != null)
                    tags.RemoveTag(data.invulnerableTag, 1);

                if (data.aimLockedTag != null)
                    tags.RemoveTag(data.aimLockedTag, 1);
            }
        }

        private Vector2 ResolveMoveDirection(AbilitySystem system, bool fallbackToAim)
        {
            var intent = system.GetComponent<PlayerIntentInput2D>();
            if (intent != null && intent.RawMoveInput.sqrMagnitude > 0.0001f)
                return intent.RawMoveInput.normalized;

            Vector2 move = InputActionQuery.GetMoveVectorRaw();
            if (move.sqrMagnitude > 0.0001f)
                return move.normalized;

            if (fallbackToAim)
            {
                var aim = system.GetComponent<PlayerAim2D>();
                if (aim != null && aim.AimDirection.sqrMagnitude > 0.0001f)
                    return aim.AimDirection.normalized;
            }

            if (fallbackToAim)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 w = InputActionQuery.GetPointerWorldPosition(cam, 0f);
                    Vector2 d = (Vector2)(w - system.transform.position);
                    if (d.sqrMagnitude > 0.0001f)
                        return d.normalized;
                }
            }

            return Vector2.zero;
        }

        private static IWeaponDashAugment ResolveDashAugment(AbilitySystem system)
        {
            if (system == null)
                return null;

            IWeaponDashAugment directAugment = system.GetComponent<IWeaponDashAugment>();
            if (directAugment != null)
                return directAugment;

            MonoBehaviour[] components = system.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IWeaponDashAugment augment)
                    return augment;
            }

            return null;
        }
    }
}

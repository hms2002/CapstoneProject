using System.Collections;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
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

            var tags = system.GetComponent<TagSystem>();
            var motion = system.GetComponent<AbilityMotionController2D>();

            if (motion == null)
            {
                Debug.LogError("[Dash2D] AbilityMotionController2D가 필요합니다.");
                yield break;
            }

            try
            {
                // 태그 부여(무적/이동락/에임락)
                if (tags != null)
                {
                    if (data.invulnerableTag != null) tags.AddTag(data.invulnerableTag, 1);
                    if (data.aimLockedTag != null) tags.AddTag(data.aimLockedTag, 1);
                }

                // 애니 트리거
                if (spec.Definition.animationTriggerHash != 0)
                    system.TryPlayAnimationTriggerHash(spec.Definition.animationTriggerHash, spec.Definition);

                // 특수이동 시작
                float dashSpeed = distance / duration;
                motion.StartDash(dir, dashSpeed, duration);

                float elapsed = 0f;
                while (elapsed < duration)
                {
                    if (spec.Token != null && spec.Token.IsCancelled)
                    {
                        motion.CancelMotion();
                        yield break;
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                // 대쉬 후 잠깐 이동락(선택)
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
                if (motion != null)
                    motion.CancelMotion();

                // 무적/락 태그 회수
                if (tags != null)
                {
                    if (data.invulnerableTag != null) tags.RemoveTag(data.invulnerableTag, 1);
                    if (data.aimLockedTag != null) tags.RemoveTag(data.aimLockedTag, 1);
                }
            }
        }

        private Vector2 ResolveMoveDirection(AbilitySystem system, bool fallbackToAim)
        {
            // 새 구조: PlayerIntentInput2D 우선
            var intent = system.GetComponent<PlayerIntentInput2D>();
            if (intent != null && intent.MoveInput.sqrMagnitude > 0.0001f)
                return intent.MoveInput.normalized;

            if (fallbackToAim)
            {
                var aim = system.GetComponent<PlayerAim2D>();
                if (aim != null && aim.AimDirection.sqrMagnitude > 0.0001f)
                    return aim.AimDirection.normalized;
            }

            // 최소 fallback
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            var move = new Vector2(x, y);
            if (move.sqrMagnitude > 0.0001f)
                return move.normalized;

            if (fallbackToAim)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
                    w.z = 0f;
                    Vector2 d = (Vector2)(w - system.transform.position);
                    if (d.sqrMagnitude > 0.0001f)
                        return d.normalized;
                }
            }

            return Vector2.zero;
        }
    }
}
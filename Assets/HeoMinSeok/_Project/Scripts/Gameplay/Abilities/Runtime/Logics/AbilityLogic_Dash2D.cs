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

            // 방향(이동 입력) 결정
            Vector2 dir = ResolveMoveDirection(system, data.useAimWhenNoMoveInput);
            if (dir.sqrMagnitude < 0.0001f) yield break;
            dir.Normalize();

            var tags = system.GetComponent<TagSystem>();

            // 태그 부여(무적/이동락/에임락)
            try
            {
                if (tags != null)
                {
                    if (data.invulnerableTag != null) tags.AddTag(data.invulnerableTag, 1);
                    if (data.movementLockedTag != null) tags.AddTag(data.movementLockedTag, 1);
                    if (data.aimLockedTag != null) tags.AddTag(data.aimLockedTag, 1);
                }

                // 애니 트리거(AbilityDefinition.animationTrigger 사용)
                if (spec.Definition.animationTriggerHash != 0)
                    system.TryPlayAnimationTriggerHash(spec.Definition.animationTriggerHash, spec.Definition);

                var rb = system.GetComponent<Rigidbody2D>();

                if (rb != null && data.zeroVelocity)
                    rb.linearVelocity = Vector2.zero;

                Vector2 startPos = rb != null ? rb.position : (Vector2)system.transform.position;
                Vector2 endPos = startPos + dir * distance;

                float elapsed = 0f;

                // Rigidbody가 있으면 FixedUpdate 기반으로
                if (rb != null)
                {
                    while (elapsed < duration)
                    {
                        if (spec.Token != null && spec.Token.IsCancelled) yield break;

                        elapsed += Time.fixedDeltaTime;
                        float t = Mathf.Clamp01(elapsed / duration);
                        rb.MovePosition(Vector2.Lerp(startPos, endPos, t));
                        yield return new WaitForFixedUpdate();
                    }

                    if (data.zeroVelocity)
                        rb.linearVelocity = Vector2.zero;
                }
                else
                {
                    while (elapsed < duration)
                    {
                        if (spec.Token != null && spec.Token.IsCancelled) yield break;

                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / duration);
                        system.transform.position = Vector2.Lerp(startPos, endPos, t);
                        yield return null;
                    }
                }

                // 대쉬 후 잠깐 이동락(선택)
                if (data.postLockTime > 0f && tags != null && data.movementLockedTag != null)
                {
                    float end = Time.time + data.postLockTime;
                    while (Time.time < end)
                    {
                        if (spec.Token != null && spec.Token.IsCancelled) break;
                        yield return null;
                    }
                }
            }
            finally
            {
                // 무적/락 태그 회수
                if (tags != null)
                {
                    if (data.invulnerableTag != null) tags.RemoveTag(data.invulnerableTag, 1);
                    if (data.movementLockedTag != null) tags.RemoveTag(data.movementLockedTag, 1);
                    if (data.aimLockedTag != null) tags.RemoveTag(data.aimLockedTag, 1);
                }
            }
        }

        private Vector2 ResolveMoveDirection(AbilitySystem system, bool fallbackToAim)
        {
            // SampleTopDownPlayer가 있으면 MoveInput 사용(이미 정규화)
            var player = system.GetComponent<SampleTopDownPlayer>();
            if (player != null)
            {
                if (player.MoveInput.sqrMagnitude > 0.0001f)
                    return player.MoveInput;

                if (fallbackToAim && player.AimDirection.sqrMagnitude > 0.0001f)
                    return player.AimDirection.normalized;
            }

            // Input axis fallback
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            var move = new Vector2(x, y);
            if (move.sqrMagnitude > 0.0001f)
                return move.normalized;

            if (fallbackToAim)
            {
                // Combat input(마우스) 방향 fallback
                var input = system.GetComponent<PlayerCombatInput2D>();
                if (input != null && input.AimDirection.sqrMagnitude > 0.0001f)
                    return input.AimDirection.normalized;

                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
                    w.z = 0f;
                    Vector2 d = (Vector2)(w - system.transform.position);
                    if (d.sqrMagnitude > 0.0001f) return d.normalized;
                }
            }

            return Vector2.zero;
        }
    }
}

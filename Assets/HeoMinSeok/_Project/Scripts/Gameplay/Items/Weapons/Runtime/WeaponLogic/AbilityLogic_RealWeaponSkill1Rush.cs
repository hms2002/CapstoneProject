using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 스킬1 로직:
    /// - moveSpeedMultiplierAttribute에 Flat modifier를 단계적으로 누적
    /// - 충돌/입력/외부 캔슬(AbilitySystem CancelExecutionOnTags 등)로 토큰이 취소되면 즉시 해제
    ///
    /// NOTE:
    /// - WASD 무시는 Player(SampleTopDownPlayer) 쪽에서 forcedMoveTag를 통해 처리합니다.
    /// - forcedMoveTag 자체는 AbilityDefinition.grantedTagsWhileActive에 넣어두는 것을 권장합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_RW_Skill1_Rush", menuName = "GAS/Weapon/RealWeapon/Logic Skill1 Rush")]
    public sealed class AbilityLogic_RealWeaponSkill1Rush : AbilityLogic
    {
        public RealWeaponSkill1RushData data;

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec == null || data == null) yield break;

            var attrSet = system.AttributeSet;
            if (attrSet == null || data.moveSpeedMultiplierAttribute == null) yield break;

            var speedAttr = attrSet.GetAttribute(data.moveSpeedMultiplierAttribute);
            if (speedAttr == null) yield break;

            var added = new List<AttributeModifier>(Mathf.Max(1, data.stacks));

            void Cleanup()
            {
                // Remove only what THIS execution added
                for (int i = 0; i < added.Count; i++)
                    speedAttr.RemoveModifier(added[i]);

                added.Clear();
            }

            try
            {
                int stacks = Mathf.Max(1, data.stacks);
                float step = Mathf.Max(0.01f, data.stepIntervalSeconds);
                float add = data.addPerStack;

                // 0) first stack immediately
                var m0 = new AttributeModifier(ModifierType.Flat, add, source: this, duration: 0f);
                speedAttr.AddModifier(m0);
                added.Add(m0);

                // 1) subsequent stacks with interval, while not cancelled
                for (int s = 1; s < stacks; s++)
                {
                    float end = Time.time + step;
                    while (Time.time < end)
                    {
                        if (spec.Token != null && spec.Token.IsCancelled)
                            yield break;

                        // Cancel on collision
                        if (data.collisionCancelRadius > 0f && data.collisionCancelLayers.value != 0)
                        {
                            var hit = Physics2D.OverlapCircle(system.transform.position, data.collisionCancelRadius, data.collisionCancelLayers);
                            if (hit != null)
                            {
                                // Request cancel
                                system.CancelExecution(force: true);
                                yield break;
                            }
                        }

                        // Cancel on player inputs (attack / other skills)
                        if (data.cancelOnAttackOrSkillInput)
                        {
                            if (Input.GetMouseButtonDown(0) || /*Input.GetKeyDown(KeyCode.Q) ||*/ Input.GetKeyDown(KeyCode.E))
                            {
                                system.CancelExecution(force: true);
                                yield break;
                            }
                        }

                        yield return null;
                    }

                    if (spec.Token != null && spec.Token.IsCancelled)
                        yield break;

                    var m = new AttributeModifier(ModifierType.Flat, add, source: this, duration: 0f);
                    speedAttr.AddModifier(m);
                    added.Add(m);
                }

                // 2) Hold until cancelled (피격/충돌/입력 등)
                while (spec.Token != null && !spec.Token.IsCancelled)
                {
                    // Cancel on collision
                    if (data.collisionCancelRadius > 0f && data.collisionCancelLayers.value != 0)
                    {
                        var hit = Physics2D.OverlapCircle(system.transform.position, data.collisionCancelRadius, data.collisionCancelLayers);
                        if (hit != null)
                        {
                            system.CancelExecution(force: true);
                            break;
                        }
                    }

                    if (data.cancelOnAttackOrSkillInput)
                    {
                        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E))
                        {
                            system.CancelExecution(force: true);
                            break;
                        }
                    }

                    yield return null;
                }
            }
            finally
            {
                Cleanup();
            }
        }
    }
}

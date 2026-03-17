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

            if (data.moveSpeedMultiplierAttribute.IsBaseOnly())
            {
                Debug.LogWarning(
                    $"[AbilityLogic_RealWeaponSkill1Rush] '{data.moveSpeedMultiplierAttribute.attributeName}' 은 BaseOnly 속성이므로 Modifier를 적용할 수 없습니다.");
                yield break;
            }

            var added = new List<AttributeModifier>(Mathf.Max(1, data.stacks));

            void Cleanup()
            {
                for (int i = 0; i < added.Count; i++)
                    attrSet.TryRemoveModifier(data.moveSpeedMultiplierAttribute, added[i]);

                added.Clear();
            }

            try
            {
                int stacks = Mathf.Max(1, data.stacks);
                float step = Mathf.Max(0.01f, data.stepIntervalSeconds);
                float add = data.addPerStack;

                var m0 = new AttributeModifier(ModifierType.Flat, add, source: this, duration: 0f);
                if (attrSet.TryAddModifier(data.moveSpeedMultiplierAttribute, m0))
                    added.Add(m0);

                for (int s = 1; s < stacks; s++)
                {
                    float end = Time.time + step;
                    while (Time.time < end)
                    {
                        if (spec.Token != null && spec.Token.IsCancelled)
                            yield break;

                        if (data.collisionCancelRadius > 0f && data.collisionCancelLayers.value != 0)
                        {
                            var hit = Physics2D.OverlapCircle(system.transform.position, data.collisionCancelRadius, data.collisionCancelLayers);
                            if (hit != null)
                            {
                                system.CancelExecution(force: true);
                                yield break;
                            }
                        }

                        if (data.cancelOnAttackOrSkillInput)
                        {
                            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E))
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
                    if (attrSet.TryAddModifier(data.moveSpeedMultiplierAttribute, m))
                        added.Add(m);
                }

                while (spec.Token != null && !spec.Token.IsCancelled)
                {
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// - Skill1 Rush의 실행 수명을 관리한다.
    /// - Rush 실행 중 이동속도 버프를 단계적으로 누적한다.
    /// - 입력 취소 시 현재 Rush 누적량을 짧은 handoff modifier로 이어 붙인다.
    /// - 충돌 취소 / 피격 취소 시에는 handoff 없이 즉시 종료한다.
    /// - 시전자 AttributeSet의 특정 Attribute 감소를 감지해 피격 취소를 처리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_RW_Skill1_Rush", menuName = "GAS/Weapon/RealWeapon/Logic Skill1 Rush")]
    public sealed class AbilityLogic_RealWeaponSkill1Rush : AbilityLogic
    {
        public RealWeaponSkill1RushData data;

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec == null || data == null)
                yield break;

            var attrSet = system.AttributeSet;
            if (attrSet == null || data.moveSpeedMultiplierAttribute == null)
                yield break;

            if (data.moveSpeedMultiplierAttribute.IsBaseOnly())
            {
                Debug.LogWarning(
                    $"[AbilityLogic_RealWeaponSkill1Rush] '{data.moveSpeedMultiplierAttribute.attributeName}' 은 BaseOnly 속성이므로 Modifier를 적용할 수 없습니다.");
                yield break;
            }

            var added = new List<AttributeModifier>(Mathf.Max(1, data.stacks));

            float currentBonus = 0f;
            bool shouldApplyHandoff = false;
            bool wasDamaged = false;

            AttributeSet.AttributeChangedDelegate onAttributeChanged = null;

            void Cleanup()
            {
                if (onAttributeChanged != null)
                    attrSet.OnAttributeChanged -= onAttributeChanged;

                // 책임:
                // - 입력 취소로 종료될 때만, 현재 Rush 총합을 잠깐 이어 붙이는 handoff modifier를 추가한다.
                if (shouldApplyHandoff && currentBonus > 0f)
                {
                    var handoff = new AttributeModifier(
                        ModifierType.Flat,
                        currentBonus,
                        source: this,
                        duration: Mathf.Max(0.01f, data.handoffDurationSeconds));

                    attrSet.TryAddModifier(data.moveSpeedMultiplierAttribute, handoff);
                }

                for (int i = 0; i < added.Count; i++)
                    attrSet.TryRemoveModifier(data.moveSpeedMultiplierAttribute, added[i]);

                added.Clear();
            }

            try
            {
                if (data.cancelOnDamagedAttribute != null)
                {
                    // 책임:
                    // - 지정한 Attribute가 감소했을 때만 피격으로 간주한다.
                    // - 힐처럼 값이 증가한 경우에는 취소하지 않는다.
                    onAttributeChanged = (changedAttribute, oldValue, newValue) =>
                    {
                        if (changedAttribute != data.cancelOnDamagedAttribute)
                            return;

                        if (newValue < oldValue)
                            wasDamaged = true;
                    };

                    attrSet.OnAttributeChanged += onAttributeChanged;
                }

                // 책임:
                // - 스킬 발동 입력을 같은 프레임의 취소 입력으로 다시 읽지 않도록 1프레임 넘긴다.
                yield return null;

                int stacks = Mathf.Max(1, data.stacks);
                float step = Mathf.Max(0.01f, data.stepIntervalSeconds);
                float add = data.addPerStack;

                var m0 = new AttributeModifier(ModifierType.Flat, add, source: this, duration: 0f);
                if (attrSet.TryAddModifier(data.moveSpeedMultiplierAttribute, m0))
                {
                    added.Add(m0);
                    currentBonus += add;
                }

                for (int s = 1; s < stacks; s++)
                {
                    float end = Time.time + step;

                    while (Time.time < end)
                    {
                        if (spec.Token != null && spec.Token.IsCancelled)
                            yield break;

                        if (wasDamaged)
                        {
                            shouldApplyHandoff = false;
                            system.CancelExecution(force: true);
                            yield break;
                        }

                        if (data.collisionCancelRadius > 0f && data.collisionCancelLayers.value != 0)
                        {
                            var hit = Physics2D.OverlapCircle(
                                system.transform.position,
                                data.collisionCancelRadius,
                                data.collisionCancelLayers);

                            if (hit != null)
                            {
                                shouldApplyHandoff = false;
                                system.CancelExecution(force: true);
                                yield break;
                            }
                        }

                        if (data.cancelOnAttackOrSkillInput)
                        {
                            if (Input.GetMouseButtonDown(0) ||
                                Input.GetKeyDown(KeyCode.Q) ||
                                Input.GetKeyDown(KeyCode.E))
                            {
                                shouldApplyHandoff = true;
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
                    {
                        added.Add(m);
                        currentBonus += add;
                    }
                }

                while (spec.Token != null && !spec.Token.IsCancelled)
                {
                    if (wasDamaged)
                    {
                        shouldApplyHandoff = false;
                        system.CancelExecution(force: true);
                        break;
                    }

                    if (data.collisionCancelRadius > 0f && data.collisionCancelLayers.value != 0)
                    {
                        var hit = Physics2D.OverlapCircle(
                            system.transform.position,
                            data.collisionCancelRadius,
                            data.collisionCancelLayers);

                        if (hit != null)
                        {
                            shouldApplyHandoff = false;
                            system.CancelExecution(force: true);
                            break;
                        }
                    }

                    if (data.cancelOnAttackOrSkillInput)
                    {
                        if (Input.GetMouseButtonDown(0) ||
                            Input.GetKeyDown(KeyCode.Q) ||
                            Input.GetKeyDown(KeyCode.E))
                        {
                            shouldApplyHandoff = true;
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
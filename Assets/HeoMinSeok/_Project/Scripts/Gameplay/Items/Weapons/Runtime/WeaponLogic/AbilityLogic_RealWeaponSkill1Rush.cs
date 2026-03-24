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
    /// - 충돌 취소 / 피격 취소 / 씬 이동 취소 시에는 handoff 없이 즉시 종료한다.
    /// - 시전자 AttributeSet의 특정 Attribute 감소를 감지해 피격 취소를 처리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_RW_Skill1_Rush", menuName = "GAS/Weapon/RealWeapon/Logic Skill1 Rush")]
    public sealed class AbilityLogic_RealWeaponSkill1Rush : AbilityLogic
    {
        public RealWeaponSkill1RushData data;

        /// <summary>
        /// 책임 :
        /// - Rush 실행 중 AbilitySpec 단위로 생성된 런타임 modifier/구독 상태를 보관한다.
        /// - 씬 이동 강제 종료 시에도 같은 정리 데이터를 찾을 수 있게 해준다.
        /// </summary>
        private sealed class RushRuntimeState
        {
            public readonly List<AttributeModifier> Added = new();
            public float CurrentBonus;
            public bool ShouldApplyHandoff;
            public bool WasDamaged;
            public AttributeSet.AttributeChangedDelegate OnAttributeChanged;
        }

        private readonly Dictionary<AbilitySpec, RushRuntimeState> runtimeStates = new();

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

            var state = GetOrCreateState(spec);

            try
            {
                if (data.cancelOnDamagedAttribute != null)
                {
                    state.OnAttributeChanged = (changedAttribute, oldValue, newValue) =>
                    {
                        if (changedAttribute != data.cancelOnDamagedAttribute)
                            return;

                        if (newValue < oldValue)
                            state.WasDamaged = true;
                    };

                    attrSet.OnAttributeChanged += state.OnAttributeChanged;
                }

                yield return null;

                int stacks = Mathf.Max(1, data.stacks);
                float step = Mathf.Max(0.01f, data.stepIntervalSeconds);
                float add = data.addPerStack;

                AddRushModifier(attrSet, state, add);

                for (int s = 1; s < stacks; s++)
                {
                    float end = Time.time + step;

                    while (Time.time < end)
                    {
                        if (spec.Token != null && spec.Token.IsCancelled)
                            yield break;

                        if (state.WasDamaged)
                        {
                            state.ShouldApplyHandoff = false;
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
                                state.ShouldApplyHandoff = false;
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
                                state.ShouldApplyHandoff = true;
                                system.CancelExecution(force: true);
                                yield break;
                            }
                        }

                        yield return null;
                    }

                    if (spec.Token != null && spec.Token.IsCancelled)
                        yield break;

                    AddRushModifier(attrSet, state, add);
                }

                while (spec.Token != null && !spec.Token.IsCancelled)
                {
                    if (state.WasDamaged)
                    {
                        state.ShouldApplyHandoff = false;
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
                            state.ShouldApplyHandoff = false;
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
                            state.ShouldApplyHandoff = true;
                            system.CancelExecution(force: true);
                            break;
                        }
                    }

                    yield return null;
                }
            }
            finally
            {
                CleanupRuntimeState(system, spec, applyHandoff: state.ShouldApplyHandoff);
            }
        }

        /// <summary>
        /// 책임 :
        /// - 씬 이동 시 Rush가 만든 임시 이동속도 modifier와 피격 구독을 handoff 없이 강제 정리한다.
        /// </summary>
        public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
        {
            CleanupRuntimeState(system, spec, applyHandoff: false);
        }

        /// <summary>
        /// 책임 :
        /// - Rush 스택 1개 분량의 이속 modifier를 추가하고 런타임 상태에 기록한다.
        /// </summary>
        private void AddRushModifier(AttributeSet attrSet, RushRuntimeState state, float add)
        {
            if (attrSet == null || state == null)
                return;

            var modifier = new AttributeModifier(
                ModifierType.Flat,
                add,
                source: this,
                duration: 0f);

            if (attrSet.TryAddModifier(data.moveSpeedMultiplierAttribute, modifier))
            {
                state.Added.Add(modifier);
                state.CurrentBonus += add;
            }
        }

        /// <summary>
        /// 책임 :
        /// - AbilitySpec에 대응하는 Rush 런타임 상태를 찾아 없으면 생성한다.
        /// </summary>
        private RushRuntimeState GetOrCreateState(AbilitySpec spec)
        {
            if (spec == null)
                return null;

            if (!runtimeStates.TryGetValue(spec, out var state) || state == null)
            {
                state = new RushRuntimeState();
                runtimeStates[spec] = state;
            }

            return state;
        }

        /// <summary>
        /// 책임 :
        /// - Rush 실행 중 추가한 modifier, handoff, 피격 구독을 한 번만 안전하게 정리한다.
        /// - applyHandoff=false 면 씬 이동/강제 종료로 간주하여 이어 붙이기 버프를 남기지 않는다.
        /// </summary>
        private void CleanupRuntimeState(AbilitySystem system, AbilitySpec spec, bool applyHandoff)
        {
            if (system == null || spec == null)
                return;

            if (!runtimeStates.TryGetValue(spec, out var state) || state == null)
                return;

            var attrSet = system.AttributeSet;
            if (attrSet != null)
            {
                if (state.OnAttributeChanged != null)
                    attrSet.OnAttributeChanged -= state.OnAttributeChanged;

                if (applyHandoff && state.CurrentBonus > 0f)
                {
                    var handoff = new AttributeModifier(
                        ModifierType.Flat,
                        state.CurrentBonus,
                        source: this,
                        duration: Mathf.Max(0.01f, data.handoffDurationSeconds));

                    attrSet.TryAddModifier(data.moveSpeedMultiplierAttribute, handoff);
                }

                for (int i = 0; i < state.Added.Count; i++)
                {
                    attrSet.TryRemoveModifier(data.moveSpeedMultiplierAttribute, state.Added[i]);
                }
            }

            state.Added.Clear();
            state.CurrentBonus = 0f;
            state.ShouldApplyHandoff = false;
            state.WasDamaged = false;
            state.OnAttributeChanged = null;

            runtimeStates.Remove(spec);
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// - Skill1 Rush의 실행 수명을 관리한다.
    /// - Rush 실행 중 이동속도 버프를 단계적으로 누적한다.
    /// - 특정 유물 태그가 있으면 stack별 이동속도 증가량 오버라이드를 적용한다.
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
            public AbilitySpec PendingSkill2Spec;
            public float PendingSkill2ExpireTime;
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

                AddRushModifier(attrSet, state, ResolveStackAdd(system, 0));
                PlayRushStackAdvanceSound(system, spec, initialTarget, 0);
                InputBindingService input = InputBindingService.EnsureInstance();

                for (int s = 1; s < stacks; s++)
                {
                    float end = Time.time + step;

                    while (Time.time < end)
                    {
                        if (spec.Token != null && spec.Token.IsCancelled)
                            yield break;

                        if (TryCancelIfPendingSkill2WindowExpired(system, state))
                            yield break;

                        if (state.WasDamaged)
                        {
                            state.ShouldApplyHandoff = false;
                            system.CancelExecution(force: true);
                            yield break;
                        }

                        if (data.collisionCancelRadius > 0f &&
                            data.collisionCancelLayers.value != 0)
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
                        if (input.WasPressedThisFrame(InputActionId.PrimaryAttack) ||
                            input.WasPressedThisFrame(InputActionId.Skill1))
                        {
                            state.ShouldApplyHandoff = true;
                            PlayRushInputCancelSound(system, spec, initialTarget);
                            system.CancelExecution(force: true);
                            yield break;
                        }

                        if (input.WasPressedThisFrame(InputActionId.Skill2))
                        {
                            if (!TryBeginDeferredSkill2Cancel(system, state))
                            {
                                state.ShouldApplyHandoff = true;
                                PlayRushInputCancelSound(system, spec, initialTarget);
                                system.CancelExecution(force: true);
                                yield break;
                            }
                            }
                        }

                        yield return null;
                    }

                    if (spec.Token != null && spec.Token.IsCancelled)
                        yield break;

                    AddRushModifier(attrSet, state, ResolveStackAdd(system, s));
                    PlayRushStackAdvanceSound(system, spec, initialTarget, s);
                }

                while (spec.Token != null && !spec.Token.IsCancelled)
                {
                    if (TryCancelIfPendingSkill2WindowExpired(system, state))
                        break;

                    if (state.WasDamaged)
                    {
                        state.ShouldApplyHandoff = false;
                        system.CancelExecution(force: true);
                        break;
                    }

                    if (data.collisionCancelRadius > 0f &&
                        data.collisionCancelLayers.value != 0)
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
                        if (input.WasPressedThisFrame(InputActionId.PrimaryAttack) ||
                            input.WasPressedThisFrame(InputActionId.Skill1))
                        {
                            state.ShouldApplyHandoff = true;
                            PlayRushInputCancelSound(system, spec, initialTarget);
                            system.CancelExecution(force: true);
                            break;
                        }

                        if (input.WasPressedThisFrame(InputActionId.Skill2))
                        {
                            if (!TryBeginDeferredSkill2Cancel(system, state))
                            {
                                state.ShouldApplyHandoff = true;
                                PlayRushInputCancelSound(system, spec, initialTarget);
                                system.CancelExecution(force: true);
                                break;
                            }
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
        /// - Rush 스택 단계 상승 시 LogicData에 authoring된 단계별 사운드를 공통 Ability 오디오 경로로 재생한다.
        /// - 배열 길이를 넘는 단계는 무음으로 처리해 데이터 누락을 허용한다.
        /// </summary>
        private void PlayRushStackAdvanceSound(AbilitySystem system, AbilitySpec spec, GameObject target, int stackIndex)
        {
            if (data == null || data.stackAdvanceSounds == null)
                return;

            if (stackIndex < 0 || stackIndex >= data.stackAdvanceSounds.Length)
                return;

            AbilityAudioRouter.PlayOneShot(
                data.stackAdvanceSounds[stackIndex],
                system,
                spec,
                target,
                sourceObjectOverride: data);
        }

        /// <summary>
        /// 책임 :
        /// - Rush가 입력 취소로 종료될 때 LogicData 전용 취소 사운드를 재생한다.
        /// - 실제 재생은 공통 AbilityAudioRouter를 통해 SoundManager 정책을 그대로 따른다.
        /// </summary>
        private void PlayRushInputCancelSound(AbilitySystem system, AbilitySpec spec, GameObject target)
        {
            if (data == null)
                return;

            AbilityAudioRouter.PlayOneShot(
                data.cancelByInputSound,
                system,
                spec,
                target,
                sourceObjectOverride: data);
        }

        /// <summary>
        /// 책임 :
        /// - 현재 Rush stack index에 맞는 이동속도 증가량을 해석한다.
        /// - 지정 태그가 활성 상태면 stack별 override를 우선 사용하고, 아니면 기본 addPerStack을 사용한다.
        /// </summary>
        private float ResolveStackAdd(AbilitySystem system, int stackIndex)
        {
            if (data == null)
                return 0f;

            float fallback = data.addPerStack;
            if (data.stackAddOverrideTag == null || data.taggedAddPerStackOverrides == null || data.taggedAddPerStackOverrides.Length == 0)
                return fallback;

            var tagSystem = system != null ? system.TagSystem : null;
            if (tagSystem == null || !tagSystem.HasTag(data.stackAddOverrideTag))
                return fallback;

            int clampedIndex = Mathf.Clamp(stackIndex, 0, data.taggedAddPerStackOverrides.Length - 1);
            return data.taggedAddPerStackOverrides[clampedIndex];
        }

        /// <summary>
        /// 책임 :
        /// - 유물 태그가 활성 상태일 때 E 입력 취소를 바로 처리하지 않고 Skill2 킬 확인 window를 연다.
        /// - 실제로 병행 실행 중인 Skill2 spec을 찾지 못하면 false를 반환해 기존 즉시 취소 흐름으로 돌린다.
        /// </summary>
        private bool TryBeginDeferredSkill2Cancel(AbilitySystem system, RushRuntimeState state)
        {
            if (system == null || state == null || data == null)
                return false;

            var tagSystem = system.TagSystem;
            if (tagSystem == null || data.deferSkill2CancelTag == null || !tagSystem.HasTag(data.deferSkill2CancelTag))
                return false;

            var weaponInventory = system.GetComponent<WeaponInventory2D>();
            if (weaponInventory == null)
                return false;

            var skill2Def = weaponInventory.GetActiveAbility(WeaponAbilitySlot.Skill2);
            if (skill2Def == null)
                return false;

            var skill2Spec = system.FindSpec(skill2Def);
            if (skill2Spec == null)
                return false;

            if (HasSuccessfulSkill2PreserveKill(skill2Spec))
                return true;

            state.PendingSkill2Spec = skill2Spec;
            state.PendingSkill2ExpireTime = Time.time + Mathf.Max(0.01f, data.skill2KillConfirmWindowSeconds);
            return true;
        }

        /// <summary>
        /// 책임 :
        /// - 열려 있는 Skill2 킬 확인 window의 만료를 감시한다.
        /// - 시간 안에 KillConfirmed가 오지 않으면 기존 handoff 취소 규칙으로 Rush를 종료한다.
        /// </summary>
        private bool TryCancelIfPendingSkill2WindowExpired(AbilitySystem system, RushRuntimeState state)
        {
            if (system == null || state == null || state.PendingSkill2Spec == null)
                return false;

            if (HasSuccessfulSkill2PreserveKill(state.PendingSkill2Spec))
            {
                state.PendingSkill2Spec = null;
                state.PendingSkill2ExpireTime = 0f;
                return false;
            }

            if (Time.time < state.PendingSkill2ExpireTime)
                return false;

            state.PendingSkill2Spec = null;
            state.PendingSkill2ExpireTime = 0f;
            state.ShouldApplyHandoff = true;
            system.CancelExecution(force: true);
            return true;
        }

        /// <summary>
        /// 책임 :
        /// - Skill2 spec이 최근 실행에서 Rush 유지 조건을 만족하는 킬을 만들었는지 확인한다.
        /// - 최근 킬 시각과 pending 상태를 함께 읽어 즉시 킬/지연 킬 모두 같은 규칙으로 판정한다.
        /// </summary>
        private bool HasSuccessfulSkill2PreserveKill(AbilitySpec skill2Spec)
        {
            if (skill2Spec == null || data == null)
                return false;

            if (skill2Spec.GetBool(AbilityLogic_RealWeaponSkill2SpeedStrike.KeyPendingRushPreserve, false))
                return false;

            float lastKillTime = skill2Spec.GetFloat(AbilityLogic_RealWeaponSkill2SpeedStrike.KeyLastRushPreserveKillTime, -999f);
            float maxAge = Mathf.Max(0.01f, data.skill2KillConfirmWindowSeconds);
            return Time.time - lastKillTime <= maxAge;
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
            state.PendingSkill2Spec = null;
            state.PendingSkill2ExpireTime = 0f;

            runtimeStates.Remove(spec);
        }
    }
}

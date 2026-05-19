using System.Collections;
using UnityEngine;
using UnityGAS;
using CapstoneAudio;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 3연속 콤보 공격의 상태 진행을 담당한다.
    /// - 콤보 인덱스 결정, 애니메이션/돌진 처리, 히트 이벤트 대기 후 MeleeHitboxActor를 생성한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_SwordCombo2D", menuName = "GAS/Samples/AbilityLogic/Sword Combo 2D")]
    public class AbilityLogic_SwordCombo2D : AbilityLogic
    {
        private const string KEY_COMBO_INDEX = "Sword.ComboIndex";
        private const string KEY_COMBO_EXPIRE = "Sword.ComboExpire";

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec == null || spec.Definition == null) yield break;
            if (system.AttributeSet == null) yield break;

            var data = spec.Definition.sourceObject as SwordCombo2DData;
            if (data == null)
            {
                Debug.LogError("[SwordCombo2D] AbilityDefinition.sourceObject must be SwordCombo2DData.");
                yield break;
            }

            Vector2 attackDir = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
            Vector2 lungeDir = AbilityMoveDirectionResolver2D.ResolveMoveThenAim(system.gameObject, attackDir);
            float finalAttackSpeed = AbilityAttackSpeedResolver.ResolveFinalAttackSpeed(system);

            int comboIndex = ResolveComboIndex(spec, data);
            var step = data.GetRuntimeStep(comboIndex, finalAttackSpeed);
            if (ResolveHitboxPrefab(data, step) == null)
            {
                Debug.LogError("[SwordCombo2D] combo step hitboxPrefab is null.");
                yield break;
            }

            spec.SetInt(KEY_COMBO_INDEX, comboIndex);
            spec.SetFloat(KEY_COMBO_EXPIRE, Time.time + data.comboResetTime);
            system.SetNextActivationDelay(spec, step.nextAttackDelay);

            ApplyWeaponVisualSideSign(system, step.sideSign);
            TryPlayAnim(system, step, spec.Definition);
            PlayStepSound(system, step);

            yield return WaitForHitTimingDuringLunge(
                system,
                spec,
                data,
                step,
                lungeDir,
                step.lungeDistance,
                step.lungeDuration);

            float rec = step.recoveryDuration > 0f
                ? step.recoveryDuration
                : Mathf.Max(0.02f, spec.Definition.recoveryTime / finalAttackSpeed);
            spec.SetFloat("RecoveryOverride", rec);

            SpawnHitbox(system, spec, data, step, attackDir);
        }

        private int ResolveComboIndex(AbilitySpec spec, SwordCombo2DData data)
        {
            float expire = spec.GetFloat(KEY_COMBO_EXPIRE, -1f);
            int current = spec.GetInt(KEY_COMBO_INDEX, -1);
            int comboCount = data != null ? Mathf.Max(1, data.GetStepCount()) : 3;

            if (expire > 0f && Time.time <= expire && current >= 0)
                return (current + 1) % comboCount;

            return 0;
        }

        private void TryPlayAnim(AbilitySystem system, SwordCombo2DData.RuntimeSwordComboStepData step, AbilityDefinition definition)
        {
            string trig = step.animationTrigger;
            if (string.IsNullOrEmpty(trig)) return;
            system.TryPlayAnimationTriggerHash(Animator.StringToHash(trig), definition);
        }

        /// <summary>
        /// 책임 :
        /// - 콤보 단계가 시작될 때 step 전용 공격 사운드를 1회 재생한다.
        /// - ability 공용 오디오와 분리해 각 타마다 다른 키를 데이터에서 authoring 할 수 있게 만든다.
        /// </summary>
        private void PlayStepSound(AbilitySystem system, SwordCombo2DData.RuntimeSwordComboStepData step)
        {
            if (system == null || !step.attackSound.IsSet)
                return;

            SoundManager.EnsureInstance().Play(step.attackSound, new SoundPlaybackContext
            {
                Instigator = system.gameObject,
                Causer = system.gameObject,
                Target = system.gameObject,
                Position = system.transform.position,
                SourceObject = this
            });
        }

        /// <summary>
        /// 책임 :
        /// - 콤보 단계의 sideSign을 현재 장착 무기 비주얼에 전달한다.
        /// - 히트박스와 같은 단계 데이터를 무기 표현 계층에도 공유해 공격 방향 피드백을 일치시킨다.
        /// </summary>
        private void ApplyWeaponVisualSideSign(AbilitySystem system, int sideSign)
        {
            if (system == null)
                return;

            WeaponEquipController equipController = system.GetComponentInChildren<WeaponEquipController>();
            if (equipController == null)
                return;

            equipController.SetAttackVisualSideSign(sideSign);
        }

        /// <summary>
        /// 책임 :
        /// - 콤보 lunge와 애니메이션 hit event 대기를 같은 구간에서 함께 처리한다.
        /// - hit event 등록을 먼저 열어 이벤트 miss를 막고, 이벤트가 오면 lunge 종료를 기다리지 않고 바로 다음 단계로 진행시킨다.
        /// </summary>
        private IEnumerator WaitForHitTimingDuringLunge(
            AbilitySystem system,
            AbilitySpec spec,
            SwordCombo2DData data,
            SwordCombo2DData.RuntimeSwordComboStepData step,
            Vector2 dir,
            float distance,
            float duration)
        {
            var motion = system.GetComponent<AbilityMotionController2D>();
            if (motion == null)
            {
                Debug.LogError("[SwordCombo2D] AbilityMotionController2D가 필요합니다.");
                yield break;
            }

            GameplayEventWaiter waiter = null;
            float eventDeadline = data != null && data.hitEventTimeout > 0f
                ? Time.time + data.hitEventTimeout
                : float.PositiveInfinity;

            if (data != null && data.hitEventTag != null)
                waiter = system.WaitGameplayEvent(data.hitEventTag, spec);

            if (distance > 0f && duration > 0f)
            {
                Vector2 start = system.transform.position;
                motion.StartLunge(start, dir, distance, duration);
            }

            float elapsed = 0f;
            while (true)
            {
                if (spec?.Token != null && spec.Token.IsCancelled)
                {
                    waiter?.Cancel();
                    motion.CancelMotion();
                    yield break;
                }

                bool lungeCompleted = duration <= 0f || elapsed >= duration;
                bool eventCompleted = waiter == null || waiter.Done;
                bool eventTimedOut = waiter != null && Time.time >= eventDeadline;

                if (eventCompleted || eventTimedOut || lungeCompleted)
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            waiter?.Cancel();
        }

        /// <summary>
        /// 책임 :
        /// - 콤보 단계별 중심점, 피해량, 넉백, 원소 누적량을 계산한다.
        /// - 계산 결과를 payload/context로 묶어 짧게 유지되는 근접 히트박스를 생성한다.
        /// </summary>
        private void SpawnHitbox(
            AbilitySystem system,
            AbilitySpec abilitySpec,
            SwordCombo2DData data,
            SwordCombo2DData.RuntimeSwordComboStepData step,
            Vector2 dir)
        {
            if (data.damageEffect == null) return;
            if (system.AttributeSet == null) return;

            var cfg = data.DamageConfig;
            bool includeStagger = (cfg != null) && cfg.includeStaggerBuildUp;

            Vector2 perp = new Vector2(-dir.y, dir.x);
            int sideSign = step.sideSign;

            Vector2 center = (Vector2)system.transform.position
                             + dir * step.forwardOffset
                             + perp * (step.sideOffset * sideSign);

#if UNITY_EDITOR
            if (system.TryGetComponent<UnityGAS.Sample.RealtimeHitboxGizmo2D>(out var gizmo))
            {
                int stepIndex = abilitySpec.GetInt(KEY_COMBO_INDEX, 0);
                var col = (stepIndex == 0) ? Color.green : (stepIndex == 1 ? Color.yellow : Color.cyan);
                gizmo.RecordBox(center, step.hitboxSize, 0f, 0.15f, col);
            }
#endif

            IStatProvider statProvider = AbilityStatProviderFactory.Create(system);

            float legacyBaseHp = step.legacyDamage;
            float baseHp = legacyBaseHp;
            if (step.damageFormula != null)
            {
                baseHp = step.damageFormula.Evaluate(
                    system.AttributeSet,
                    statProvider,
                    defaultIfEmpty: legacyBaseHp);
            }

            float baseStagger = (cfg != null && cfg.includeStaggerBuildUp && cfg.staggerFormula != null)
                ? cfg.staggerFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
                : step.legacyStaggerDamage;

            float baseKnockback = 0f;
            if (step.knockbackFormula != null)
            {
                baseKnockback = step.knockbackFormula.Evaluate(
                    system.AttributeSet,
                    statProvider,
                    defaultIfEmpty: 0f);
            }

            var snapshot = DamageSnapshotBuilder.BuildFromBaseValues(
                statProvider: statProvider,
                config: cfg,
                baseHp: baseHp,
                baseStagger: includeStagger ? baseStagger : 0f,
                baseKnockback: baseKnockback,
                elementSource: system.gameObject
            );

            if (snapshot.FinalHpDamage <= 0f)
                return;

            // 책임 :
            // - 콤보 단계별 최종 피해량을 공용 CombatHitPayload 규약으로 고정한다.
            // - 이후 콤보 히트박스도 다른 공격체와 같은 방식으로 피해를 적용한다.
            var payload = CombatHitPayload.FromSnapshot(
                sourceSystem: system,
                sourceSpec: abilitySpec,
                damageEffect: data.damageEffect,
                knockbackEffect: data.knockbackEffect,
                snapshot: snapshot,
                hitConfirmedTag: data.hitConfirmedTag,
                causer: system.gameObject);

            MeleeHitboxActor hitboxPrefab = ResolveHitboxPrefab(data, step);
            var hitbox = Object.Instantiate(hitboxPrefab, center, Quaternion.identity);
            if (hitbox == null)
                return;

            var context = new MeleeHitboxSpawnContext
            {
                ownerSystem = system,
                sourceSpec = abilitySpec,
                causer = system.gameObject,
                ignoreTarget = system.gameObject,
                lifetime = step.activeTime,
                wallLayers = 0,
                damageLayers = data.hitLayers,
                hitPayload = payload,
                worldPosition = center,
                hitboxSize = step.hitboxSize,
                hitOncePerTarget = true,
                destroyOnFirstHit = false,
                direction = dir,
                flipVisualX = step.sideSign < 0,
                visualMirrorMode = step.visualMirrorMode
            };

            hitbox.Setup(context);
        }

        /// <summary>
        /// 책임 :
        /// - 콤보 단계 전용 히트박스 프리팹을 우선 사용하고, 비어 있으면 데이터 공용 프리팹으로 안전하게 대체한다.
        /// - 기존 공용 프리팹 기반 데이터와 새 step별 프리팹 데이터를 모두 호환한다.
        /// </summary>
        private MeleeHitboxActor ResolveHitboxPrefab(
            SwordCombo2DData data,
            SwordCombo2DData.RuntimeSwordComboStepData step)
        {
            if (step.hitboxPrefab != null)
                return step.hitboxPrefab;

            return data.hitboxPrefab;
        }
    }
}

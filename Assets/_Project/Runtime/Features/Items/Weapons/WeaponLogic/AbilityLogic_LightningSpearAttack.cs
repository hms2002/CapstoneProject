using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 번개 창 기본공격 AD의 전용 콤보 진행, 이동, 히트박스 생성, 피해 적용을 실행할 책임을 가집니다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_LightningSpearAttack", menuName = "GAS/Weapon/Lightning Spear/Logic Attack")]
    public sealed class AbilityLogic_LightningSpearAttack : AbilityLogic
    {
        private const string KeyComboIndex = "LightningSpearAttack.ComboIndex";
        private const string KeyComboExpire = "LightningSpearAttack.ComboExpire";

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec?.Definition == null)
                yield break;

            LightningSpearAttackData data = spec.Definition.sourceObject as LightningSpearAttackData;
            if (data == null || data.Combo == null)
            {
                Debug.LogError("[LightningSpearAttack] AbilityDefinition.sourceObject must be LightningSpearAttackData.");
                yield break;
            }

            LightningSpearAttackComboConfig combo = data.Combo;
            Vector2 attackDir = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
            Vector2 lungeDir = AbilityMoveDirectionResolver2D.ResolveMoveThenAim(system.gameObject, attackDir);
            float finalAttackSpeed = AbilityAttackSpeedResolver.ResolveFinalAttackSpeed(system);

            int comboIndex = ResolveComboIndex(spec, combo);
            RuntimeLightningSpearAttackStep step = combo.GetRuntimeStep(comboIndex, finalAttackSpeed);
            MeleeHitboxActor hitboxPrefab = ResolveHitboxPrefab(combo, step);
            if (hitboxPrefab == null)
            {
                Debug.LogError("[LightningSpearAttack] combo step hitboxPrefab is null.");
                yield break;
            }

            spec.SetInt(KeyComboIndex, comboIndex);
            spec.SetFloat(KeyComboExpire, Time.time + combo.ComboResetTime);
            system.SetNextActivationDelay(spec, step.nextAttackDelay);

            ApplyWeaponVisualSideSign(system, step.sideSign);
            TryPlayAnim(system, step.animationTrigger, spec.Definition);
            PlayStepSound(system, step.attackSound);

            yield return WaitForHitTimingDuringLunge(
                system,
                spec,
                combo,
                lungeDir,
                step.lungeDistance,
                step.lungeDuration);

            float recovery = step.recoveryDuration > 0f
                ? step.recoveryDuration
                : Mathf.Max(0.02f, spec.Definition.recoveryTime / Mathf.Max(0.0001f, finalAttackSpeed));
            spec.SetFloat("RecoveryOverride", recovery);

            SpawnHitboxAndEffect(system, spec, combo, step, comboIndex, attackDir, hitboxPrefab);
            OnStepCompleted(comboIndex);
        }

        private static int ResolveComboIndex(AbilitySpec spec, LightningSpearAttackComboConfig combo)
        {
            float expire = spec.GetFloat(KeyComboExpire, -1f);
            int current = spec.GetInt(KeyComboIndex, -1);
            int comboCount = combo != null ? Mathf.Max(1, combo.GetStepCount()) : 1;

            if (expire > 0f && Time.time <= expire && current >= 0)
                return (current + 1) % comboCount;

            return 0;
        }

        private static void TryPlayAnim(AbilitySystem system, string animationTrigger, AbilityDefinition definition)
        {
            if (system == null || string.IsNullOrWhiteSpace(animationTrigger))
                return;

            system.TryPlayAnimationTriggerHash(Animator.StringToHash(animationTrigger), definition);
        }

        private void PlayStepSound(AbilitySystem system, SoundRef attackSound)
        {
            if (system == null || !attackSound.IsSet)
                return;

            SoundManager.EnsureInstance().Play(attackSound, new SoundPlaybackContext
            {
                Instigator = system.gameObject,
                Causer = system.gameObject,
                Target = system.gameObject,
                Position = system.transform.position,
                SourceObject = this
            });
        }

        private static void ApplyWeaponVisualSideSign(AbilitySystem system, int sideSign)
        {
            if (system == null)
                return;

            WeaponEquipController equipController = system.GetComponentInChildren<WeaponEquipController>();
            if (equipController == null)
                return;

            equipController.SetAttackVisualSideSign(sideSign);
        }

        private static IEnumerator WaitForHitTimingDuringLunge(
            AbilitySystem system,
            AbilitySpec spec,
            LightningSpearAttackComboConfig combo,
            Vector2 direction,
            float distance,
            float duration)
        {
            AbilityMotionController2D motion = system.GetComponent<AbilityMotionController2D>();
            if (motion == null)
            {
                Debug.LogError("[LightningSpearAttack] AbilityMotionController2D is required.");
                yield break;
            }

            GameplayEventWaiter waiter = null;
            float eventDeadline = combo != null && combo.HitEventTimeout > 0f
                ? Time.time + combo.HitEventTimeout
                : float.PositiveInfinity;

            if (combo != null && combo.HitEventTag != null)
                waiter = system.WaitGameplayEvent(combo.HitEventTag, spec);

            if (distance > 0f && duration > 0f)
            {
                Vector2 start = system.transform.position;
                motion.StartLunge(start, direction, distance, duration);
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

        private static void SpawnHitboxAndEffect(
            AbilitySystem system,
            AbilitySpec spec,
            LightningSpearAttackComboConfig combo,
            RuntimeLightningSpearAttackStep step,
            int comboIndex,
            Vector2 attackDirection,
            MeleeHitboxActor hitboxPrefab)
        {
            if (system == null || spec == null || combo == null || step.attackPrefab == null)
                return;

            CombatHitPayload payload = BuildPayload(
                system,
                spec,
                combo.DamageConfig,
                combo.DamageEffect,
                combo.KnockbackEffect,
                step.damageFormula,
                step.knockbackFormula,
                step.legacyDamage,
                step.legacyStaggerDamage,
                1f,
                combo.HitConfirmedTag,
                step.hitImpactCueKind);

            if (payload == null)
                return;

            Vector2 direction = attackDirection.sqrMagnitude > 0.0001f
                ? attackDirection.normalized
                : Vector2.right;
            Vector2 perp = new Vector2(-direction.y, direction.x);
            int sideSign = step.sideSign < 0 ? -1 : 1;

            Vector2 center = (Vector2)system.transform.position
                             + direction * step.forwardOffset
                             + perp * (step.sideOffset * sideSign);

#if UNITY_EDITOR
            if (system.TryGetComponent<UnityGAS.Sample.RealtimeHitboxGizmo2D>(out var gizmo))
            {
                Color color = comboIndex == 0 ? Color.green : comboIndex == 1 ? Color.yellow : Color.cyan;
                gizmo.RecordBox(center, step.attackPrefab.HitboxSize, 0f, 0.15f, color);
            }
#endif

            MeleeHitboxActor hitbox = UnityEngine.Object.Instantiate(hitboxPrefab, center, Quaternion.identity);
            if (hitbox == null)
                return;

            hitbox.Setup(new MeleeHitboxSpawnContext
            {
                ownerSystem = system,
                sourceSpec = spec,
                causer = system.gameObject,
                ignoreTarget = system.gameObject,
                lifetime = step.attackPrefab.ActiveTime,
                wallLayers = step.attackPrefab.WallLayers,
                damageLayers = combo.HitLayers,
                hitPayload = payload,
                worldPosition = center,
                hitboxSize = step.attackPrefab.HitboxSize,
                hitboxScaleMultiplier = step.attackPrefab.HitboxScaleMultiplier,
                overrideSizingMode = step.attackPrefab.OverrideSizingMode,
                sizingMode = step.attackPrefab.SizingMode,
                hitOncePerTarget = step.attackPrefab.HitOncePerTarget,
                destroyOnFirstHit = step.attackPrefab.DestroyOnFirstHit,
                direction = direction,
                flipVisualX = direction.x < 0f,
                visualMirrorMode = MeleeHitboxVisualMirrorMode.FlipLocalYWhenRequested
            });
        }

        private static CombatHitPayload BuildPayload(
            AbilitySystem system,
            AbilitySpec spec,
            DamagePayloadConfig config,
            GameplayEffect damageEffect,
            GE_Knockback_Spec knockbackEffect,
            ScaledStatFormula damageFormula,
            ScaledStatFormula knockbackFormula,
            float legacyDamage,
            float legacyStaggerDamage,
            float damageScale,
            GameplayTag hitConfirmedTag,
            HitImpactCueKind hitImpactCueKind)
        {
            if (system == null || system.AttributeSet == null || damageEffect == null)
                return null;

            IStatProvider statProvider = AbilityStatProviderFactory.Create(system);
            float safeScale = Mathf.Max(0f, damageScale);

            float baseHp = damageFormula != null
                ? damageFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: legacyDamage)
                : legacyDamage;
            baseHp *= safeScale;

            float baseKnockback = knockbackFormula != null
                ? knockbackFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
                : 0f;
            baseKnockback *= safeScale;

            float baseStagger = config != null && config.includeStaggerBuildUp && config.staggerFormula != null
                ? config.staggerFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
                : legacyStaggerDamage;
            baseStagger *= safeScale;

            CombatDamageSnapshot snapshot = DamageSnapshotBuilder.BuildFromBaseValues(
                statProvider: statProvider,
                config: config,
                baseHp: baseHp,
                baseStagger: config != null && config.includeStaggerBuildUp ? baseStagger : 0f,
                baseKnockback: baseKnockback,
                elementSource: system.gameObject);

            if (snapshot.FinalHpDamage <= 0f)
                return null;

            return CombatHitPayload.FromSnapshot(
                sourceSystem: system,
                sourceSpec: spec,
                damageEffect: damageEffect,
                knockbackEffect: knockbackEffect,
                snapshot: snapshot,
                hitConfirmedTag: hitConfirmedTag,
                causer: system.gameObject,
                hitImpactCueKind: hitImpactCueKind);
        }

        private static MeleeHitboxActor ResolveHitboxPrefab(
            LightningSpearAttackComboConfig combo,
            RuntimeLightningSpearAttackStep step)
        {
            if (step.attackPrefab != null && step.attackPrefab.HitboxPrefab != null)
                return step.attackPrefab.HitboxPrefab;

            return combo != null ? combo.DefaultHitboxPrefab : null;
        }

        private void OnStepCompleted(int comboIndex)
        {
            // Extension point for future Lightning Spear upgrades, e.g. third-hit mark creation.
        }
    }
}

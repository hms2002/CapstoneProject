using System;
using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    public sealed class WeaponComboAttackExecutionContext
    {
        public AbilitySystem system;
        public AbilitySpec spec;
        public WeaponComboAttack2DConfig config;
        public RuntimeWeaponComboAttackStep2D step;
        public int stepIndex;
        public Vector2 attackDirection;
        public Vector2 lungeDirection;
        public Vector2 hitboxCenter;
        public MeleeHitboxActor hitboxInstance;
    }

    public sealed class WeaponComboAttackCallbacks
    {
        public Action<WeaponComboAttackExecutionContext> onStepStarted;
        public Action<WeaponComboAttackExecutionContext> onHitboxSpawned;
        public Action<WeaponComboAttackExecutionContext> onStepCompleted;
    }

    public static class WeaponComboAttack2DRunner
    {
        private const string KeyComboIndex = "WeaponCombo2D.ComboIndex";
        private const string KeyComboExpire = "WeaponCombo2D.ComboExpire";

        public static IEnumerator Execute(
            AbilitySystem system,
            AbilitySpec spec,
            WeaponComboAttack2DConfig config,
            UnityEngine.Object sourceObject,
            WeaponComboAttackCallbacks callbacks = null)
        {
            if (system == null || spec?.Definition == null || config == null || system.AttributeSet == null)
                yield break;

            Vector2 attackDir = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
            Vector2 lungeDir = AbilityMoveDirectionResolver2D.ResolveMoveThenAim(system.gameObject, attackDir);
            float finalAttackSpeed = AbilityAttackSpeedResolver.ResolveFinalAttackSpeed(system);

            int comboIndex = ResolveComboIndex(spec, config);
            RuntimeWeaponComboAttackStep2D step = config.GetRuntimeStep(comboIndex, finalAttackSpeed);
            MeleeHitboxActor hitboxPrefab = ResolveHitboxPrefab(config, step);
            if (hitboxPrefab == null)
            {
                Debug.LogError("[WeaponComboAttack2D] combo step hitboxPrefab is null.");
                yield break;
            }

            var executionContext = new WeaponComboAttackExecutionContext
            {
                system = system,
                spec = spec,
                config = config,
                step = step,
                stepIndex = comboIndex,
                attackDirection = attackDir,
                lungeDirection = lungeDir
            };

            spec.SetInt(KeyComboIndex, comboIndex);
            spec.SetFloat(KeyComboExpire, Time.time + config.ComboResetTime);
            system.SetNextActivationDelay(spec, step.nextAttackDelay);

            ApplyWeaponVisualSideSign(system, step.sideSign);
            TryPlayAnim(system, step.animationTrigger, spec.Definition);
            PlayStepSound(system, step.attackSound, sourceObject);
            callbacks?.onStepStarted?.Invoke(executionContext);

            yield return WaitForHitTimingDuringLunge(
                system,
                spec,
                config,
                lungeDir,
                step.lungeDistance,
                step.lungeDuration);

            float recovery = step.recoveryDuration > 0f
                ? step.recoveryDuration
                : Mathf.Max(0.02f, spec.Definition.recoveryTime / Mathf.Max(0.0001f, finalAttackSpeed));
            spec.SetFloat("RecoveryOverride", recovery);

            SpawnHitboxAndEffect(executionContext, hitboxPrefab);
            callbacks?.onHitboxSpawned?.Invoke(executionContext);
            callbacks?.onStepCompleted?.Invoke(executionContext);
        }

        private static int ResolveComboIndex(AbilitySpec spec, WeaponComboAttack2DConfig config)
        {
            float expire = spec.GetFloat(KeyComboExpire, -1f);
            int current = spec.GetInt(KeyComboIndex, -1);
            int comboCount = config != null ? Mathf.Max(1, config.GetStepCount()) : 1;

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

        private static void PlayStepSound(AbilitySystem system, SoundRef attackSound, UnityEngine.Object sourceObject)
        {
            if (system == null || !attackSound.IsSet)
                return;

            SoundManager.EnsureInstance().Play(attackSound, new SoundPlaybackContext
            {
                Instigator = system.gameObject,
                Causer = system.gameObject,
                Target = system.gameObject,
                Position = system.transform.position,
                SourceObject = sourceObject
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
            WeaponComboAttack2DConfig config,
            Vector2 direction,
            float distance,
            float duration)
        {
            AbilityMotionController2D motion = system.GetComponent<AbilityMotionController2D>();
            if (motion == null)
            {
                Debug.LogError("[WeaponComboAttack2D] AbilityMotionController2D is required.");
                yield break;
            }

            GameplayEventWaiter waiter = null;
            float eventDeadline = config != null && config.HitEventTimeout > 0f
                ? Time.time + config.HitEventTimeout
                : float.PositiveInfinity;

            if (config != null && config.HitEventTag != null)
                waiter = system.WaitGameplayEvent(config.HitEventTag, spec);

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
            WeaponComboAttackExecutionContext context,
            MeleeHitboxActor hitboxPrefab)
        {
            if (context == null || context.system == null || context.spec == null || context.config == null)
                return;

            RuntimeWeaponComboAttackStep2D step = context.step;
            WeaponAttackPrefabConfig attackPrefabConfig = step.attackPrefab;
            if (attackPrefabConfig == null)
                return;

            CombatHitPayload payload = WeaponDamagePayloadUtility.BuildPayload(
                context.system,
                context.spec,
                context.config.DamageConfig,
                context.config.DamageEffect,
                context.config.KnockbackEffect,
                step.damageFormula,
                step.knockbackFormula,
                step.legacyDamage,
                step.legacyStaggerDamage,
                1f,
                context.config.HitConfirmedTag,
                step.elementDamages);

            if (payload == null)
                return;

            Vector2 direction = context.attackDirection.sqrMagnitude > 0.0001f
                ? context.attackDirection.normalized
                : Vector2.right;
            Vector2 perp = new Vector2(-direction.y, direction.x);
            int sideSign = step.sideSign < 0 ? -1 : 1;

            Vector2 center = (Vector2)context.system.transform.position
                             + direction * step.forwardOffset
                             + perp * (step.sideOffset * sideSign);
            context.hitboxCenter = center;

#if UNITY_EDITOR
            if (context.system.TryGetComponent<UnityGAS.Sample.RealtimeHitboxGizmo2D>(out var gizmo))
            {
                Color color = context.stepIndex == 0 ? Color.green : context.stepIndex == 1 ? Color.yellow : Color.cyan;
                gizmo.RecordBox(center, attackPrefabConfig.HitboxSize, 0f, 0.15f, color);
            }
#endif

            MeleeHitboxActor hitbox = UnityEngine.Object.Instantiate(hitboxPrefab, center, Quaternion.identity);
            if (hitbox != null)
            {
                hitbox.Setup(new MeleeHitboxSpawnContext
                {
                    ownerSystem = context.system,
                    sourceSpec = context.spec,
                    causer = context.system.gameObject,
                    ignoreTarget = context.system.gameObject,
                    lifetime = attackPrefabConfig.ActiveTime,
                    wallLayers = attackPrefabConfig.WallLayers,
                    damageLayers = context.config.HitLayers,
                    hitPayload = payload,
                    worldPosition = center,
                    hitboxSize = attackPrefabConfig.HitboxSize,
                    hitboxScaleMultiplier = attackPrefabConfig.HitboxScaleMultiplier,
                    overrideSizingMode = attackPrefabConfig.OverrideSizingMode,
                    sizingMode = attackPrefabConfig.SizingMode,
                    hitOncePerTarget = attackPrefabConfig.HitOncePerTarget,
                    destroyOnFirstHit = attackPrefabConfig.DestroyOnFirstHit,
                    direction = direction,
                    flipVisualX = sideSign < 0,
                    visualMirrorMode = MeleeHitboxVisualMirrorMode.DoNotMirror
                });

                context.hitboxInstance = hitbox;
            }

        }

        private static MeleeHitboxActor ResolveHitboxPrefab(
            WeaponComboAttack2DConfig config,
            RuntimeWeaponComboAttackStep2D step)
        {
            if (step.attackPrefab != null && step.attackPrefab.HitboxPrefab != null)
                return step.attackPrefab.HitboxPrefab;

            return config != null ? config.DefaultHitboxPrefab : null;
        }
    }
}

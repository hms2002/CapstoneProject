using System.Collections;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_ApprenticeHeroSwordAttack", menuName = "GAS/Weapon/Apprentice Hero Sword/Logic Attack")]
// 책임: 수습 용사 검 기본 공격 콤보 입력 상태, 히트박스, 연출 타이밍을 실행한다.
public sealed class AbilityLogic_ApprenticeHeroSwordAttack : AbilityLogic
{
    private const string KeyComboIndex = "ApprenticeHeroSword.Attack.ComboIndex";
    private const string KeyComboExpire = "ApprenticeHeroSword.Attack.ComboExpire";

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null || spec?.Definition == null)
            yield break;

        ApprenticeHeroSwordAttackData data = spec.Definition.sourceObject as ApprenticeHeroSwordAttackData;
        if (data == null || data.Combo == null)
        {
            Debug.LogError("[ApprenticeHeroSwordAttack] AbilityDefinition.sourceObject must be ApprenticeHeroSwordAttackData.");
            yield break;
        }

        AbilityMotionController2D motion = system.GetComponent<AbilityMotionController2D>();
        if (motion == null)
        {
            Debug.LogError("[ApprenticeHeroSwordAttack] AbilityMotionController2D is required.");
            yield break;
        }

        ApprenticeHeroSwordAttackComboConfig combo = data.Combo;
        Vector2 attackDir = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
        Vector2 lungeDir = AbilityMoveDirectionResolver2D.ResolveMoveThenAim(system.gameObject, attackDir);
        float finalAttackSpeed = AbilityAttackSpeedResolver.ResolveFinalAttackSpeed(system);

        int comboIndex = ResolveComboIndex(spec, combo);
        RuntimeApprenticeHeroSwordAttackStep step = combo.GetRuntimeStep(comboIndex, finalAttackSpeed);
        if (step.hitbox == null || step.hitbox.HitboxPrefab == null)
        {
            Debug.LogError("[ApprenticeHeroSwordAttack] combo step hitboxPrefab is null.");
            yield break;
        }

        spec.SetInt(KeyComboIndex, comboIndex);
        spec.SetFloat(KeyComboExpire, Time.time + combo.ComboResetTime);
        system.SetNextActivationDelay(spec, step.nextAttackDelay);

        ApplyWeaponVisualSideSign(system, step.sideSign);
        TryPlayAnim(system, step.animationTrigger, spec.Definition);
        AbilityAudioRouter.PlayOneShot(step.attackSound, system, spec, sourceObjectOverride: data);

        yield return WaitForHitTimingDuringLunge(
            motion,
            system,
            spec,
            combo,
            lungeDir,
            step.lungeDistance,
            step.lungeDuration);

        if (IsAbilityCancelled(spec))
            yield break;

        float recovery = step.recoveryDuration > 0f
            ? step.recoveryDuration
            : Mathf.Max(0.02f, spec.Definition.recoveryTime / Mathf.Max(0.0001f, finalAttackSpeed));
        spec.SetFloat("RecoveryOverride", recovery);

        CombatHitPayload payload = ApprenticeHeroSwordHitUtility.BuildPayload(system, spec, step.damage, 1f);
        if (payload == null)
            yield break;

        Vector2 direction = attackDir.sqrMagnitude > 0.0001f ? attackDir.normalized : Vector2.right;
        Vector2 perp = new(-direction.y, direction.x);
        int sideSign = step.sideSign < 0 ? -1 : 1;
        Vector2 center = (Vector2)system.transform.position
                         + direction * step.forwardOffset
                         + perp * (step.sideOffset * sideSign);

#if UNITY_EDITOR
        if (system.TryGetComponent<IRealtimeHitboxGizmo2D>(out var gizmo))
        {
            Color color = comboIndex == 0 ? Color.green : comboIndex == 1 ? Color.yellow : Color.cyan;
            gizmo.RecordBox(center, step.hitbox.HitboxSize, 0f, 0.15f, color);
        }
#endif

        ApprenticeHeroSwordHitUtility.SpawnHitbox(
            system,
            spec,
            step.hitbox,
            combo.HitLayers,
            payload,
            center,
            direction,
            step.sideSign < 0);
    }

    private static int ResolveComboIndex(AbilitySpec spec, ApprenticeHeroSwordAttackComboConfig combo)
    {
        float expire = spec.GetFloat(KeyComboExpire, -1f);
        int current = spec.GetInt(KeyComboIndex, -1);
        int comboCount = combo != null ? Mathf.Max(1, combo.GetStepCount()) : 1;

        if (expire > 0f && Time.time <= expire && current >= 0)
            return (current + 1) % comboCount;

        return 0;
    }

    private static IEnumerator WaitForHitTimingDuringLunge(
        AbilityMotionController2D motion,
        AbilitySystem system,
        AbilitySpec spec,
        ApprenticeHeroSwordAttackComboConfig combo,
        Vector2 direction,
        float distance,
        float duration)
    {
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
            if (IsAbilityCancelled(spec))
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

    private static void TryPlayAnim(AbilitySystem system, string animationTrigger, AbilityDefinition definition)
    {
        if (system == null || string.IsNullOrWhiteSpace(animationTrigger))
            return;

        system.TryPlayAnimationTriggerHash(Animator.StringToHash(animationTrigger), definition);
    }

    private static void ApplyWeaponVisualSideSign(AbilitySystem system, int sideSign)
    {
        WeaponEquipController equipController = system != null
            ? system.GetComponentInChildren<WeaponEquipController>()
            : null;
        equipController?.SetAttackVisualSideSign(sideSign);
    }
}

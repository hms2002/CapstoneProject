using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// 취룡 보스의 흡수 패턴을 실행하며, 플레이어 흡입과 모든 활성 장판의 흡수 탄막화를 조율한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_DragonAbsorbPuddles", menuName = "GAS/Ability Logic/Dragon/AL_DragonAbsorbPuddles")]
public sealed class AbilityLogic_DragonAbsorbPuddles : AbilityLogic
{
    private readonly HashSet<PuddleAreaBase> activeAbsorbProjectiles = new();

    [Header("Absorb")]
    [SerializeField, Min(0.01f)] private float alcoholAbsorbSpeed = 3.5f;
    [SerializeField, Min(0.01f)] private float fireAbsorbSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float alcoholStaggerRecoveryMaxRatio = 0.1f;
    [SerializeField, Range(0f, 1f)] private float fireStaggerBuildUpMaxRatio = 0.1f;
    [SerializeField, Min(0.1f)] private float maxAbsorbSeconds = 12f;
    [SerializeField] private bool logAbsorbResult = true;

    [Header("Presentation")]
    [SerializeField] private WorldPresentationHook inhalePresentation;
    [SerializeField] private SoundRef alcoholOnlyDrinkSound;

    [Header("Player Pull")]
    [SerializeField] private bool pullTargetDuringAbsorb = true;
    [SerializeField, Min(0f)] private float pullSpeed = 1.6f;
    [SerializeField, Min(0.01f)] private float pullVelocityRefreshSeconds = 0.08f;
    [SerializeField, Min(0.01f)] private float pullVelocityDurationSeconds = 0.12f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        DragonController dragon = system != null ? system.GetComponent<DragonController>() : null;
        if (dragon == null)
            yield break;

        dragon.PushFaceTargetLock();

        try
        {
            if (IsAbilityCancelled(spec))
                yield break;

            dragon.SpeakSituation(BossSpeechSituationEnum.AbsorbStart);
            dragon.PlayPatternTrigger(DragonAnimationKeys.Inhale);
            PlayInhalePresentation(dragon);
            yield return RunAbsorb(dragon, spec);
        }
        finally
        {
            ClearTrackedPuddles(restoreActiveProjectiles: IsAbilityCancelled(spec));
            RemoveTargetPull(dragon);
            dragon.PopFaceTargetLock();
            dragon.PlayPatternTrigger(DragonAnimationKeys.Idle);
        }
    }

    /// <summary>
    /// 책임:
    /// 흡입 패턴의 시작 연출을 기존 브레스 입 소켓 위치와 바라보는 방향을 기준으로 재생한다.
    /// </summary>
    private void PlayInhalePresentation(DragonController dragon)
    {
        if (dragon == null || !inhalePresentation.HasAnyContent)
            return;

        Vector2 direction = dragon.GetDirectionToTargetOrFacing();
        dragon.FacePatternDirection(direction);

        Vector2 origin = dragon.ResolveFireBreathMouthPosition(direction, fallbackForwardOffset: 0f);
        float angleDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        WorldPresentationPlayback.Play(
            inhalePresentation,
            WorldPresentationContext.AtWorld(
                instigator: dragon.gameObject,
                position: origin,
                fallbackDirection: direction,
                target: dragon.CurrentTarget != null ? dragon.CurrentTarget.gameObject : null,
                sourceObject: this,
                rotation: Quaternion.Euler(0f, 0f, angleDeg)));
    }

    private IEnumerator RunAbsorb(DragonController dragon, AbilitySpec spec)
    {
        dragon.BeginAbsorbPatternTracking();
        ConvertAllGroundPuddles(dragon);

        float elapsed = 0f;
        float nextPullRefreshTime = 0f;
        while (activeAbsorbProjectiles.Count > 0 && elapsed < maxAbsorbSeconds)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            if (pullTargetDuringAbsorb && Time.time >= nextPullRefreshTime)
            {
                ApplyTargetPull(dragon);
                nextPullRefreshTime = Time.time + pullVelocityRefreshSeconds;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!IsAbilityCancelled(spec))
        {
            RemoveTargetPull(dragon);
            SpeakAbsorbResult(dragon);
            LogAbsorbResult(dragon);
        }
    }

    /// <summary>
    /// 책임:
    /// 흡수 패턴 결과에 따라 취룡 전용 상황 대사를 선택해 출력한다.
    /// </summary>
    private void SpeakAbsorbResult(DragonController dragon)
    {
        if (dragon == null)
            return;

        DragonRuntimeData data = dragon.RuntimeData;
        if (data.AbsorbedFireProjectileCount > 0)
        {
            dragon.SpeakSituation(BossSpeechSituationEnum.AbsorbFireAny);
            return;
        }

        if (data.AbsorbedAlcoholProjectileCount > 0)
        {
            PlayAlcoholOnlyDrinkSound(dragon);
            dragon.SpeakSituation(BossSpeechSituationEnum.AbsorbAlcoholOnly);
        }
    }

    /// <summary>
    /// 책임:
    /// 술 장판만 흡수해 취룡이 만족하는 결과가 났을 때 전용 만족 사운드를 재생한다.
    /// </summary>
    private void PlayAlcoholOnlyDrinkSound(DragonController dragon)
    {
        if (dragon == null)
            return;

        SoundPlaybackUtility.Play(
            alcoholOnlyDrinkSound,
            instigator: dragon.gameObject,
            causer: dragon.gameObject,
            target: dragon.CurrentTarget != null ? dragon.CurrentTarget.gameObject : null,
            position: dragon.transform.position,
            sourceObject: this);
    }

    private void ConvertAllGroundPuddles(DragonController dragon)
    {
        activeAbsorbProjectiles.Clear();

        PuddleManager manager = PuddleManager.ResolveForScene();
        IReadOnlyList<PuddleAreaBase> puddles = manager != null ? manager.Puddles : null;
        if (puddles == null)
            return;

        List<PuddleAreaBase> snapshot = new(puddles);
        for (int i = 0; i < snapshot.Count; i++)
        {
            PuddleAreaBase puddle = snapshot[i];
            if (puddle == null || !puddle.IsGroundActive)
                continue;

            activeAbsorbProjectiles.Add(puddle);
            puddle.Consumed += HandleTrackedPuddleConsumed;
            puddle.EnterAbsorbProjectile(
                dragon.transform,
                ResolveAbsorbSpeed(puddle),
                absorbed => HandlePuddleArrivedAtBoss(dragon, absorbed));
        }
    }

    private float ResolveAbsorbSpeed(PuddleAreaBase puddle)
    {
        return puddle != null && puddle.ElementType == PuddleElementType.Alcohol
            ? alcoholAbsorbSpeed
            : fireAbsorbSpeed;
    }

    private void HandlePuddleArrivedAtBoss(DragonController dragon, PuddleAreaBase puddle)
    {
        if (dragon == null || puddle == null)
            return;

        dragon.RecordAbsorbedPuddleProjectile(puddle.ElementType);
        float staggerRecoveryRatio = 0f;
        float staggerBuildUpRatio = 0f;
        float reducedStaggerBuildUp = 0f;
        float addedStaggerBuildUp = 0f;

        if (puddle.ElementType == PuddleElementType.Alcohol)
        {
            staggerRecoveryRatio = alcoholStaggerRecoveryMaxRatio;
            reducedStaggerBuildUp = dragon.RecoverStaggerBuildUpByMaxRatio(staggerRecoveryRatio);
        }
        else if (puddle.ElementType == PuddleElementType.Fire)
        {
            staggerBuildUpRatio = fireStaggerBuildUpMaxRatio;
            addedStaggerBuildUp = dragon.AddStaggerBuildUpByMaxRatio(staggerBuildUpRatio);
        }

        LogAbsorbedPuddleArrival(dragon, puddle, staggerRecoveryRatio, reducedStaggerBuildUp, staggerBuildUpRatio, addedStaggerBuildUp);
        puddle.MarkConsumed();
        puddle.gameObject.SetActive(false);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void LogAbsorbedPuddleArrival(
        DragonController dragon,
        PuddleAreaBase puddle,
        float staggerRecoveryRatio,
        float reducedStaggerBuildUp,
        float staggerBuildUpRatio,
        float addedStaggerBuildUp)
    {
        if (!logAbsorbResult || dragon == null || puddle == null)
            return;

        CapstoneDiagnostics.EditorOnlyLog.Log(
            $"[DragonAbsorb] absorbed {puddle.ElementType} projectile. " +
            $"recoveryRatio={staggerRecoveryRatio:0.###}, reducedStagger={reducedStaggerBuildUp:0.###}, " +
            $"buildUpRatio={staggerBuildUpRatio:0.###}, addedStagger={addedStaggerBuildUp:0.###}",
            dragon);
    }

    private void HandleTrackedPuddleConsumed(PuddleAreaBase puddle)
    {
        if (puddle == null)
            return;

        puddle.Consumed -= HandleTrackedPuddleConsumed;
        activeAbsorbProjectiles.Remove(puddle);
    }

    private void ApplyTargetPull(DragonController dragon)
    {
        if (dragon == null || dragon.CurrentTarget == null || pullSpeed <= 0f)
            return;

        ExternalMovementController2D externalMovement =
            dragon.CurrentTarget.GetComponent<ExternalMovementController2D>() ??
            dragon.CurrentTarget.GetComponentInParent<ExternalMovementController2D>();
        if (externalMovement == null)
            return;

        Vector2 toBoss = (Vector2)(dragon.transform.position - dragon.CurrentTarget.position);
        if (toBoss.sqrMagnitude <= 0.0001f)
            return;

        externalMovement.RemoveTimedVelocitiesFromSource(this);
        externalMovement.AddTimedVelocity(
            toBoss.normalized * pullSpeed,
            pullVelocityDurationSeconds,
            source: this);
    }

    private void RemoveTargetPull(DragonController dragon)
    {
        if (dragon == null || dragon.CurrentTarget == null)
            return;

        ExternalMovementController2D externalMovement =
            dragon.CurrentTarget.GetComponent<ExternalMovementController2D>() ??
            dragon.CurrentTarget.GetComponentInParent<ExternalMovementController2D>();

        externalMovement?.RemoveTimedVelocitiesFromSource(this);
    }

    private void ClearTrackedPuddles(bool restoreActiveProjectiles = false)
    {
        List<PuddleAreaBase> snapshot = new(activeAbsorbProjectiles);
        for (int i = 0; i < snapshot.Count; i++)
        {
            PuddleAreaBase puddle = snapshot[i];
            if (puddle != null)
            {
                puddle.Consumed -= HandleTrackedPuddleConsumed;

                if (restoreActiveProjectiles)
                    puddle.CancelAbsorbToGround();
            }
        }

        activeAbsorbProjectiles.Clear();
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void LogAbsorbResult(DragonController dragon)
    {
        if (!logAbsorbResult || dragon == null)
            return;

        DragonRuntimeData data = dragon.RuntimeData;
        CapstoneDiagnostics.EditorOnlyLog.Log(
            $"[DragonAbsorb] result alcohol={data.AbsorbedAlcoholProjectileCount}, fire={data.AbsorbedFireProjectileCount}",
            dragon);
    }
}

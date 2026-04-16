using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

public sealed class WitchLightAllCandlesPatternExecutor : MonoBehaviour
{
    // 이 클래스의 책임:
    // 마녀 보스의 50% 패턴 1회 실행에서 이동, 보호막, 차지 오브, 실패/성공 분기, 루프 사운드 정리를 전담한다.

    private readonly Dictionary<int, AudioHandle> activeChargeLoopHandles = new();
    private Witch owner;

    private void Awake()
    {
        owner = GetComponent<Witch>();
    }

    /// <summary>50% 패턴 전체 시퀀스를 실행합니다.</summary>
    public IEnumerator RunPattern(AbilityLogic_WitchLightAllCandles logic, GameObject initialTarget)
    {
        if (owner == null || logic == null)
            yield break;

        owner.PlayPatternAttackMotion();
        owner.MoveToPhaseTransitionCenter(logic.MoveToCenterDuration);
        owner.ActivateShield();
        owner.EnableStaggerImmuneDuringPhaseTransition();

        CameraPresentationDirector phaseCameraDirector = owner.GetCameraPresentationDirector();
        bool shouldReturnPhaseCamera = false;
        if (phaseCameraDirector != null)
        {
            phaseCameraDirector.BeginBossFocusWithPhaseLens();
            shouldReturnPhaseCamera = true;
        }

        yield return new WaitForSeconds(logic.MoveToCenterDuration);

        Vector3 center = owner.GetPhaseTransitionCenter();
        owner.SpeakSituation(BossSpeechSituationEnum.UltimateWarning);
        owner.ShowMapWideWarning(center, logic.RelightDeadlineSeconds, logic.MapWideWarningStyleAsset);
        owner.SealAllCandles();

        GameObject chargeOrbInstance = SpawnChargeOrb(logic);
        BeginChargeLoopSound(logic);
        float chargeStartTime = Time.time;
        float deadlineTime = chargeStartTime + logic.RelightDeadlineSeconds;
        bool allCandlesRelit = false;

        while (Time.time < deadlineTime)
        {
            float chargeProgress = Mathf.InverseLerp(chargeStartTime, deadlineTime, Time.time);
            UpdateChargeOrb(logic, chargeOrbInstance, chargeProgress);
            WorldPresentationRuntime.PlaySignalOnly(
                logic.ChargePulsePresentation,
                WorldPresentationContext.AtWorld(
                    instigator: owner.gameObject,
                    position: owner.transform.position,
                    fallbackDirection: Vector3.up,
                    target: null,
                    sourceObject: this,
                    rotation: Quaternion.identity,
                    causer: owner.gameObject));

            if (!owner.HasAnySealedCandles())
            {
                allCandlesRelit = true;
                break;
            }

            yield return null;
        }

        if (allCandlesRelit)
        {
            StopChargeLoopSound(logic);
            CleanupChargeOrb(chargeOrbInstance);

            float shieldBreakDeadline = Time.time + logic.ShieldBreakWaitGraceSeconds;
            while (owner.ShieldController != null && owner.ShieldController.HasShield && Time.time < shieldBreakDeadline)
                yield return null;

            if (owner.ShieldController != null && owner.ShieldController.HasShield)
                owner.BreakShield();

            owner.DisableStaggerImmuneDuringPhaseTransition();
            owner.HideMapWideWarning();
            owner.ApplyGroggyStatus(logic.GroggyStatusEffect);

            if (shouldReturnPhaseCamera)
                yield return phaseCameraDirector.ReturnToPlayerRoutine();

            yield break;
        }

        if (owner.HasAnySealedCandles())
        {
            StopChargeLoopSound(logic);
            yield return PlayFailurePresentation(logic, chargeOrbInstance);
            owner.DisableStaggerImmuneDuringPhaseTransition();
            owner.ClearShield();
            owner.HideMapWideWarning();
            owner.ApplyMapWideDamage(logic.MapWideDamageEffect, logic.MapWideDamageAmount, initialTarget);

            if (shouldReturnPhaseCamera)
                yield return phaseCameraDirector.ReturnToPlayerRoutine();

            yield break;
        }

        StopChargeLoopSound(logic);
        CleanupChargeOrb(chargeOrbInstance);
        owner.HideMapWideWarning();

        if (shouldReturnPhaseCamera)
            yield return phaseCameraDirector.ReturnToPlayerRoutine();
    }

    /// <summary>활성 차지 루프 사운드를 중지합니다.</summary>
    public void StopChargeLoopFor(AbilityLogic_WitchLightAllCandles logic)
    {
        StopChargeLoopSound(logic);
    }

    /// <summary>씬 전환 시 남아 있을 수 있는 패턴 연출을 정리합니다.</summary>
    public void CleanupForSceneTransition(AbilityLogic_WitchLightAllCandles logic)
    {
        StopChargeLoopSound(logic);
        owner?.HideMapWideWarning();
    }

    /// <summary>차지 오브를 생성합니다.</summary>
    private GameObject SpawnChargeOrb(AbilityLogic_WitchLightAllCandles logic)
    {
        if (owner == null || logic == null || logic.ChargeOrbPrefab == null)
            return null;

        Vector3 spawnPosition = owner.transform.TransformPoint(logic.ChargeOrbLocalOffset);
        GameObject instance = Instantiate(logic.ChargeOrbPrefab, spawnPosition, logic.ChargeOrbPrefab.transform.rotation);
        if (instance == null)
            return null;

        instance.transform.localScale = logic.ChargeOrbStartScale;
        WorldPresentationRuntime.InitializeSpawnedPresentation(instance, useUnscaledTime: false);
        return instance;
    }

    /// <summary>차지 진행도에 맞춰 차지 오브 위치와 배율을 갱신합니다.</summary>
    private void UpdateChargeOrb(AbilityLogic_WitchLightAllCandles logic, GameObject chargeOrbInstance, float chargeProgress)
    {
        if (owner == null || logic == null || chargeOrbInstance == null)
            return;

        if (logic.FollowWitchDuringCharge)
            chargeOrbInstance.transform.position = owner.transform.TransformPoint(logic.ChargeOrbLocalOffset);

        chargeOrbInstance.transform.localScale = Vector3.LerpUnclamped(
            logic.ChargeOrbStartScale,
            logic.ChargeOrbEndScale,
            Mathf.Clamp01(chargeProgress));
    }

    /// <summary>실패 시 차지 오브 낙하와 충돌 연출을 재생합니다.</summary>
    private IEnumerator PlayFailurePresentation(AbilityLogic_WitchLightAllCandles logic, GameObject chargeOrbInstance)
    {
        if (logic == null)
            yield break;

        Vector3 impactPosition = owner != null
            ? owner.transform.TransformPoint(logic.ChargeOrbImpactLocalOffset)
            : logic.ChargeOrbImpactLocalOffset;

        WorldPresentationRuntime.PlaySignalOnly(
            logic.OrbLaunchPresentation,
            WorldPresentationContext.AtWorld(
                instigator: owner != null ? owner.gameObject : null,
                position: chargeOrbInstance != null ? chargeOrbInstance.transform.position : impactPosition,
                fallbackDirection: Vector3.down,
                target: null,
                sourceObject: this,
                rotation: Quaternion.identity,
                causer: owner != null ? owner.gameObject : null));

        if (chargeOrbInstance != null)
        {
            Vector3 startPosition = chargeOrbInstance.transform.position;
            Vector3 startScale = chargeOrbInstance.transform.localScale;
            float elapsed = 0f;

            while (elapsed < logic.ChargeOrbFailureDropDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / logic.ChargeOrbFailureDropDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                chargeOrbInstance.transform.position = Vector3.Lerp(startPosition, impactPosition, eased);
                chargeOrbInstance.transform.localScale = Vector3.LerpUnclamped(startScale, logic.ChargeOrbEndScale, eased);
                yield return null;
            }
        }

        CleanupChargeOrb(chargeOrbInstance);

        WorldPresentationRuntime.Play(
            logic.FailureImpactPresentation,
            WorldPresentationContext.AtWorld(
                instigator: owner != null ? owner.gameObject : null,
                position: impactPosition,
                fallbackDirection: Vector3.down,
                target: null,
                sourceObject: this,
                rotation: Quaternion.identity,
                causer: owner != null ? owner.gameObject : null));
    }

    /// <summary>차지 루프 사운드를 시작합니다.</summary>
    private void BeginChargeLoopSound(AbilityLogic_WitchLightAllCandles logic)
    {
        if (owner == null || logic == null || !logic.ChargeLoopSound.IsSet)
            return;

        int key = owner.GetInstanceID();
        StopChargeLoopSound(logic);
        activeChargeLoopHandles[key] = SoundPlaybackUtility.Play(
            logic.ChargeLoopSound,
            instigator: owner.gameObject,
            causer: owner.gameObject,
            position: owner.transform.position,
            sourceObject: this);
    }

    /// <summary>차지 루프 사운드를 정지합니다.</summary>
    private void StopChargeLoopSound(AbilityLogic_WitchLightAllCandles logic)
    {
        if (owner == null || logic == null)
            return;

        int key = owner.GetInstanceID();
        if (!activeChargeLoopHandles.TryGetValue(key, out AudioHandle handle))
            return;

        activeChargeLoopHandles.Remove(key);
        SoundPlaybackUtility.Stop(handle, logic.ChargeLoopFadeOutSeconds);
    }

    /// <summary>차지 오브를 안전하게 제거합니다.</summary>
    private static void CleanupChargeOrb(GameObject chargeOrbInstance)
    {
        if (chargeOrbInstance != null)
            Destroy(chargeOrbInstance);
    }
}

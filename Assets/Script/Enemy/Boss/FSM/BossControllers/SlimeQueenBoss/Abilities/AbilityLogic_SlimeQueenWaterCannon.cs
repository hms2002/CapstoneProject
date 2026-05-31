using System;
using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 원거리 슬라임 여왕의 물대포 경고/발사 패턴과 발사 사운드 실행을 담당한다.
/// </summary>
public sealed class AbilityLogic_SlimeQueenWaterCannon : AbilityLogic
{
    [Header("Sound")]
    [SerializeField] private SoundRef shotWaterSound = SoundRef.FromKey("sound_slimeQueen_ShotWater");

    /// <summary>원거리 슬라임 여왕이 제한 회전 조준으로 짧은 물대포 레이저를 반복 발사합니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        SlimeQueenP2Long slimeQueen = system != null ? system.GetComponent<SlimeQueenP2Long>() : null;
        if (slimeQueen == null)
            yield break;

        try
        {
            slimeQueen.FaceCurrentTarget();
            if (!slimeQueen.BeginWaterCannonBurstAim(initialTarget))
                yield break;

            slimeQueen.BeginWaterCannonAnimation();

            if (!slimeQueen.TryBuildNextWaterCannonShot(initialTarget, out SlimeQueenP2Long.WaterCannonLine nextLine))
                yield break;

            yield return RunWaterCannonOpeningWarning(slimeQueen, spec, nextLine);
            if (IsAbilityCancelled(spec))
                yield break;

            int activeShotSequenceCount = 0;
            float elapsedSeconds = 0f;
            while (elapsedSeconds < slimeQueen.WaterCannonLimitSeconds)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                slimeQueen.FaceCurrentTarget();
                activeShotSequenceCount++;
                slimeQueen.StartCoroutine(RunWaterCannonShotFireSequence(
                    slimeQueen,
                    system,
                    spec,
                    initialTarget,
                    nextLine,
                    () => activeShotSequenceCount--));

                float intervalElapsedSeconds = 0f;
                while (intervalElapsedSeconds < slimeQueen.WaterCannonShotIntervalSeconds &&
                       elapsedSeconds < slimeQueen.WaterCannonLimitSeconds)
                {
                    if (IsAbilityCancelled(spec))
                        yield break;

                    float deltaTime = Time.deltaTime;
                    intervalElapsedSeconds += deltaTime;
                    elapsedSeconds += deltaTime;
                    yield return null;
                }

                if (elapsedSeconds < slimeQueen.WaterCannonLimitSeconds &&
                    !slimeQueen.TryBuildNextWaterCannonShot(initialTarget, out nextLine))
                {
                    break;
                }
            }

            while (activeShotSequenceCount > 0 || slimeQueen.HasActiveWaterCannonLaserVfx())
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                yield return null;
            }
        }
        finally
        {
            slimeQueen.EndWaterCannonAnimation();
            // 정상 종료도 남은 참조 목록은 정리하되, 2초 제한 시점에 진행 중인 레이저를 강제로 끊지는 않는다.
            slimeQueen.CleanupWaterCannonPresentation();
        }
    }

    /// <summary>물대포 연발 시작 전에 최초 1회 경고만 표시합니다.</summary>
    private IEnumerator RunWaterCannonOpeningWarning(
        SlimeQueenP2Long slimeQueen,
        AbilitySpec spec,
        SlimeQueenP2Long.WaterCannonLine line)
    {
        AttackTelegraphView warningView = slimeQueen.ShowWaterCannonShotWarning(line);

        float warningElapsedSeconds = 0f;
        while (warningElapsedSeconds < slimeQueen.WaterCannonShotWarningSeconds)
        {
            if (IsAbilityCancelled(spec))
                yield break;

            warningElapsedSeconds += Time.deltaTime;
            yield return null;
        }

        slimeQueen.ClearWaterCannonShotWarning(warningView);
    }

    /// <summary>물대포 샷 하나의 발사 연출/판정을 처리합니다. 경고는 패턴 시작 시 최초 1회만 담당합니다.</summary>
    private IEnumerator RunWaterCannonShotFireSequence(
        SlimeQueenP2Long slimeQueen,
        AbilitySystem system,
        AbilitySpec spec,
        GameObject initialTarget,
        SlimeQueenP2Long.WaterCannonLine line,
        Action onComplete)
    {
        try
        {
            if (IsAbilityCancelled(spec))
                yield break;

            bool shotStarted = slimeQueen.StartWaterCannonShotVisual(line, out WaterZetLaserVfx laserVfx);
            if (shotStarted)
            {
                SlimeQueenPresentationAudioUtility.PlaySound(
                    shotWaterSound,
                    slimeQueen.gameObject,
                    slimeQueen.transform.position,
                    this,
                    initialTarget);
            }

            yield return WaitForWaterCannonDamageFrame(slimeQueen, spec, laserVfx);

            if (!IsAbilityCancelled(spec) && shotStarted)
                slimeQueen.PlayWaterCannonWallHitEffect(line);

            float activeElapsedSeconds = 0f;
            float nextDamageTime = 0f;
            while (activeElapsedSeconds < slimeQueen.WaterCannonShotActiveSeconds)
            {
                if (IsAbilityCancelled(spec))
                    yield break;

                bool canApplyDamage = laserVfx == null || laserVfx.DamageActive;
                if (shotStarted && canApplyDamage && Time.time >= nextDamageTime)
                {
                    if (slimeQueen.TryDamagePlayerInWaterCannonShot(system, spec, line))
                        nextDamageTime = Time.time + slimeQueen.WaterCannonDamageIntervalSeconds;
                }

                activeElapsedSeconds += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// 책임:
    /// - 물대포 피해 판정이 DemonKing 레이저 VFX의 실제 damage-active 프레임과 동기화되도록 대기한다.
    /// - VFX가 없는 legacy 표시 경로에서는 한 프레임 늦춰 렌더 생성 직후 즉시 판정되는 부자연스러움을 줄인다.
    /// </summary>
    private IEnumerator WaitForWaterCannonDamageFrame(
        SlimeQueenP2Long slimeQueen,
        AbilitySpec spec,
        WaterZetLaserVfx laserVfx)
    {
        if (laserVfx == null)
        {
            yield return null;
            yield break;
        }

        while (laserVfx != null && laserVfx.IsPlaying && !laserVfx.DamageActive)
        {
            if (slimeQueen == null || IsAbilityCancelled(spec))
                yield break;

            yield return null;
        }
    }

    /// <summary>씬 전환이나 강제 정리 시 원거리 슬라임 여왕의 물대포 표시를 제거합니다.</summary>
    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        SlimeQueenP2Long slimeQueen = system != null ? system.GetComponent<SlimeQueenP2Long>() : null;
        if (slimeQueen == null)
            return;

        slimeQueen.CleanupWaterCannonPresentation();
    }
}

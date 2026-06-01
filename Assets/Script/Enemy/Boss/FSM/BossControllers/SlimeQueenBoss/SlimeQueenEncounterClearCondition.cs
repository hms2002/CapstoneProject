using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;

/// <summary>
/// 책임:
/// - 슬라임 여왕 1페이즈 분열 이후 생성된 2페이즈 개체들이 모두 사망했는지 추적한다.
/// - 슬라임 여왕 보스전의 최종 보상 기준을 개별 보스 사망이 아니라 2페이즈 전원 사망으로 고정한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Capstone/Boss/Encounter Conditions/Slime Queen Clear Condition")]
public sealed class SlimeQueenEncounterClearCondition : BossEncounterClearCondition, IBossEncounterFinalePresentationProvider
{
    private const float FinaleIntroDurationSeconds = 0.45f;
    private const float FinaleOutroDurationSeconds = 0.35f;
    private const float FinaleLetterboxScreenHeightRatio = 0.14f;
    private const float FinaleUiTargetAlpha = 0f;
    private const float FinalePreSpeechDelaySeconds = 0.1f;
    private const float FinaleSpeechDurationSeconds = 2.5f;
    private const float FinalePostVanishHoldSeconds = 0.35f;
    private const float FinaleFocusMoveSeconds = 0.55f;

    private static readonly Vector3 FinaleSpeechBubbleOffsetDelta = new(0f, -1f, 0f);

    [SerializeField] private SlimeQueen phaseOneBoss;

    private readonly HashSet<SlimeQueenPhaseTwoBase> observedPhaseTwoBosses = new();
    private SlimeQueenPhaseTwoBase latestObservedPhaseTwoBoss;
    private Vector3 latestObservedPhaseTwoPosition;
    private bool hasObservedPhaseTwo;
    private bool hasObservedPhaseTwoShort;
    private bool hasObservedPhaseTwoLong;
    private CinematicLetterboxOverlay finaleOverlay;
    private Coroutine finaleOverlayIntroRoutine;
    private Coroutine finaleOverlayOutroRoutine;
    private CameraPresentationDirector finaleCameraDirector;
    private GameObject finaleFocusAnchorObject;
    private GameFlowInputBlocker finaleInputBlocker;
    private PlayerCinematicProtection finalePlayerProtection;

    public override bool IsCleared
    {
        get
        {
            ObserveActivePhaseTwoBosses();
            if (!hasObservedPhaseTwoShort || !hasObservedPhaseTwoLong)
                return false;

            foreach (SlimeQueenPhaseTwoBase boss in observedPhaseTwoBosses)
            {
                if (!IsPhaseTwoBossDefeated(boss))
                    return false;
            }

            return true;
        }
    }

    public override BossControllerBase RewardBoss
    {
        get
        {
            if (latestObservedPhaseTwoBoss != null)
                return latestObservedPhaseTwoBoss;

            foreach (SlimeQueenPhaseTwoBase boss in observedPhaseTwoBosses)
            {
                if (boss != null)
                    return boss;
            }

            return phaseOneBoss;
        }
    }

    public override Vector3 RewardOrigin
    {
        get
        {
            BossControllerBase rewardBoss = RewardBoss;
            if (rewardBoss != null)
                return rewardBoss.transform.position;

            if (phaseOneBoss != null)
                return phaseOneBoss.transform.position;

            if (hasObservedPhaseTwo)
                return latestObservedPhaseTwoPosition;

            return transform.position;
        }
    }

    public override bool ControlsBoss(BossControllerBase boss)
    {
        if (boss == null)
            return false;

        return boss is SlimeQueenPhaseTwoBase ||
               (phaseOneBoss != null && ReferenceEquals(boss, phaseOneBoss));
    }

    public bool TryCreateFinalePresentationRoutine(BossEncounterEndDirector director, out IEnumerator routine)
    {
        ObserveActivePhaseTwoBosses();
        if (!TryResolveFinalePair(out SlimeQueenP2Short shortQueen, out SlimeQueenP2Long longQueen))
        {
            routine = null;
            return false;
        }

        routine = RunPhaseTwoFinaleRoutine(shortQueen, longQueen);
        return true;
    }

    private void OnDisable()
    {
        CleanupFinaleRuntimeState();
    }

    private void ObserveActivePhaseTwoBosses()
    {
        SlimeQueenPhaseTwoBase[] bosses = FindObjectsByType<SlimeQueenPhaseTwoBase>(FindObjectsInactive.Exclude);
        for (int i = 0; i < bosses.Length; i++)
        {
            SlimeQueenPhaseTwoBase boss = bosses[i];
            if (boss == null)
                continue;

            hasObservedPhaseTwo = true;
            latestObservedPhaseTwoBoss = boss;
            latestObservedPhaseTwoPosition = boss.transform.position;
            observedPhaseTwoBosses.Add(boss);

            if (boss is SlimeQueenP2Short)
                hasObservedPhaseTwoShort = true;
            else if (boss is SlimeQueenP2Long)
                hasObservedPhaseTwoLong = true;
        }
    }

    private static bool IsPhaseTwoBossDefeated(SlimeQueenPhaseTwoBase boss)
    {
        return boss == null ||
               !boss.gameObject.activeInHierarchy ||
               boss.IsDead ||
               boss.HasDeadTag() ||
               boss.CurrentHealthValue <= 0f;
    }

    private bool TryResolveFinalePair(out SlimeQueenP2Short shortQueen, out SlimeQueenP2Long longQueen)
    {
        shortQueen = null;
        longQueen = null;

        foreach (SlimeQueenPhaseTwoBase boss in observedPhaseTwoBosses)
        {
            if (boss == null)
                continue;

            if (shortQueen == null && boss is SlimeQueenP2Short observedShort)
                shortQueen = observedShort;
            else if (longQueen == null && boss is SlimeQueenP2Long observedLong)
                longQueen = observedLong;
        }

        shortQueen ??= FindActivePhaseTwo<SlimeQueenP2Short>();
        longQueen ??= FindActivePhaseTwo<SlimeQueenP2Long>();

        return IsFinaleTargetAvailable(shortQueen) &&
               IsFinaleTargetAvailable(longQueen);
    }

    private IEnumerator RunPhaseTwoFinaleRoutine(SlimeQueenP2Short shortQueen, SlimeQueenP2Long longQueen)
    {
        SoundManager.EnsureInstance().StopMusic();

        CleanupFinaleRuntimeState();
        finalePlayerProtection = AcquirePlayerProtection();
        finaleInputBlocker = GameFlowInputBlocker.GetOrAdd(this);
        finaleInputBlocker?.Acquire();

        finaleOverlay = new CinematicLetterboxOverlay();
        finaleCameraDirector = FindAnyObjectByType<CameraPresentationDirector>();
        finaleFocusAnchorObject = new GameObject("SlimeQueenPhaseTwoFinaleCameraTarget");
        finaleFocusAnchorObject.hideFlags = HideFlags.DontSave;
        Transform focusAnchor = finaleFocusAnchorObject.transform;
        focusAnchor.position = ResolveFinaleTargetPosition(shortQueen);

        try
        {
            finaleOverlayIntroRoutine = StartCoroutine(finaleOverlay.PlayIn(
                FinaleIntroDurationSeconds,
                FinaleLetterboxScreenHeightRatio,
                FinaleUiTargetAlpha));

            if (finaleCameraDirector != null)
                yield return finaleCameraDirector.FocusTargetWithDeathLensRoutine(focusAnchor);

            if (finaleOverlayIntroRoutine != null)
            {
                yield return finaleOverlayIntroRoutine;
                finaleOverlayIntroRoutine = null;
            }

            yield return WaitForPresentationSeconds(FinalePreSpeechDelaySeconds);
            yield return PlayFinaleSpeechAndWait(
                shortQueen,
                BossSpeechSituationEnum.SlimeQueenFinaleShort,
                FinaleSpeechDurationSeconds);

            if (shortQueen != null)
                shortQueen.PlayFinaleVanishAndDestroy();

            yield return WaitForPresentationSeconds(FinalePostVanishHoldSeconds);

            if (longQueen != null && focusAnchor != null)
                yield return MoveFocusAnchorRoutine(focusAnchor, ResolveFinaleTargetPosition(longQueen), FinaleFocusMoveSeconds);

            yield return WaitForPresentationSeconds(FinalePreSpeechDelaySeconds);
            yield return PlayFinaleSpeechAndWait(
                longQueen,
                BossSpeechSituationEnum.SlimeQueenFinaleLong,
                FinaleSpeechDurationSeconds);

            if (longQueen != null)
                longQueen.PlayFinaleVanishAndDestroy();

            yield return WaitForPresentationSeconds(FinalePostVanishHoldSeconds);

            finaleOverlayOutroRoutine = StartCoroutine(finaleOverlay.PlayOut(FinaleOutroDurationSeconds));

            if (finaleCameraDirector != null)
                yield return finaleCameraDirector.ReturnToPlayerRoutine();

            if (finaleOverlayOutroRoutine != null)
            {
                yield return finaleOverlayOutroRoutine;
                finaleOverlayOutroRoutine = null;
            }
        }
        finally
        {
            CleanupFinaleRuntimeState();
        }
    }

    private void CleanupFinaleRuntimeState()
    {
        StopFinaleOverlayRoutine(ref finaleOverlayIntroRoutine);
        StopFinaleOverlayRoutine(ref finaleOverlayOutroRoutine);

        if (finaleCameraDirector != null)
        {
            finaleCameraDirector.RestoreDefaultState();
            finaleCameraDirector = null;
        }

        if (finaleOverlay != null)
        {
            finaleOverlay.Dispose();
            finaleOverlay = null;
        }

        if (finaleFocusAnchorObject != null)
        {
            Destroy(finaleFocusAnchorObject);
            finaleFocusAnchorObject = null;
        }

        finaleInputBlocker?.Release();
        finaleInputBlocker = null;

        finalePlayerProtection?.Release(this);
        finalePlayerProtection = null;
    }

    private void StopFinaleOverlayRoutine(ref Coroutine routine)
    {
        if (routine == null)
            return;

        StopCoroutine(routine);
        routine = null;
    }

    private IEnumerator PlayFinaleSpeechAndWait(
        SlimeQueenPhaseTwoBase boss,
        BossSpeechSituationEnum situation,
        float duration)
    {
        float resolvedDuration = Mathf.Max(0.1f, duration);
        if (boss == null)
        {
            yield return WaitForPresentationSeconds(resolvedDuration);
            yield break;
        }

        bool bubbleHidden = false;
        bool started = boss.TryShowPhaseTwoSpeech(
            situation,
            resolvedDuration,
            () => bubbleHidden = true,
            FinaleSpeechBubbleOffsetDelta);
        if (!started)
        {
            yield return WaitForPresentationSeconds(resolvedDuration);
            yield break;
        }

        float timeout = Mathf.Max(0.5f, resolvedDuration + 1f);
        float elapsed = 0f;
        while (!bubbleHidden && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator MoveFocusAnchorRoutine(Transform focusAnchor, Vector3 targetPosition, float duration)
    {
        if (focusAnchor == null)
            yield break;

        Vector3 startPosition = focusAnchor.position;
        float resolvedDuration = Mathf.Max(0f, duration);
        if (resolvedDuration <= 0f)
        {
            focusAnchor.position = targetPosition;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < resolvedDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / resolvedDuration);
            focusAnchor.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        focusAnchor.position = targetPosition;
    }

    private IEnumerator WaitForPresentationSeconds(float seconds)
    {
        float duration = Mathf.Max(0f, seconds);
        if (duration <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private PlayerCinematicProtection AcquirePlayerProtection()
    {
        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null)
            return null;

        PlayerCinematicProtection protection = playerTransform.GetComponent<PlayerCinematicProtection>();
        if (protection == null)
            protection = playerTransform.gameObject.AddComponent<PlayerCinematicProtection>();

        protection.Acquire(this);
        return protection;
    }

    private Vector3 ResolveFinaleTargetPosition(SlimeQueenPhaseTwoBase boss)
    {
        return boss != null ? boss.transform.position : latestObservedPhaseTwoPosition;
    }

    private static bool IsFinaleTargetAvailable(SlimeQueenPhaseTwoBase boss)
    {
        return boss != null &&
               boss.gameObject.activeInHierarchy;
    }

    private static T FindActivePhaseTwo<T>() where T : SlimeQueenPhaseTwoBase
    {
        T[] bosses = FindObjectsByType<T>(FindObjectsInactive.Exclude);
        for (int i = 0; i < bosses.Length; i++)
        {
            T boss = bosses[i];
            if (boss != null && boss.gameObject.activeInHierarchy)
                return boss;
        }

        return null;
    }
}

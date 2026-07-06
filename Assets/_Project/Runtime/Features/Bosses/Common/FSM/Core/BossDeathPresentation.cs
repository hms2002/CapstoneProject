using System;
using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 보스 처치 시네마틱의 시작/종료를 조율하고 연출 순서를 실행한다.
/// - 플레이어 보호 자체는 공용 PlayerCinematicProtection에 위임하고, 자신은 연출 시퀀스 오케스트레이션에 집중한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossDeathPresentation : MonoBehaviour
{
    [Serializable]
    private sealed class AnimationStepSettings
    {
        [Tooltip("트리거 기반으로 애니메이션을 재생하고 싶을 때 사용합니다.")]
        public string triggerName;

        [Tooltip("상태명 기반으로 직접 재생하거나, 트리거 사용 시 완료 대기 기준으로 사용할 상태명입니다.")]
        public string stateName;

        [Min(0f)]
        public float crossFadeDuration = 0.05f;

        [Tooltip("상태 진입을 확인할 수 없을 때 사용할 마지막 대기 시간입니다. 0이면 클립 길이를 자동 탐색합니다.")]
        [Min(0f)]
        public float fallbackDuration;

        public bool HasPlayableConfiguration =>
            !string.IsNullOrWhiteSpace(triggerName) ||
            !string.IsNullOrWhiteSpace(stateName) ||
            fallbackDuration > 0f;
    }

    [Header("Timing")]
    [SerializeField] private bool useDeathPresentation = true;
    [SerializeField, Min(0f)] private float deathCinematicIntroDuration = 0.45f;
    [SerializeField, Range(0f, 0.45f)] private float deathLetterboxScreenHeightRatio = 0.14f;
    [SerializeField, Range(0f, 1f)] private float deathUiTargetAlpha = 0f;
    [SerializeField, Min(0f)] private float deathPreSpeechDelaySeconds = 0.1f;
    [SerializeField, Min(0f)] private float deathSpeechDuration = 2.5f;
    [SerializeField, Min(0f)] private float deathPostVanishHoldSeconds = 0.35f;
    [SerializeField, Min(0f)] private float deathCinematicOutroDuration = 0.35f;

    [Header("References")]
    [SerializeField] private MonoBehaviour deathCameraDirector;
    [SerializeField] private Animator deathAnimator;
    [SerializeField] private Transform deathEffectAnchor;
    [SerializeField] private MonoBehaviour speechController;

    [Header("Animation")]
    [SerializeField] private AnimationStepSettings breakdownAnimation = new AnimationStepSettings
    {
        stateName = "Boss1BreakdownAnim",
        crossFadeDuration = 0.05f
    };
    [SerializeField] private AnimationStepSettings deathAnimation = new AnimationStepSettings
    {
        stateName = "BossDeathAnim",
        crossFadeDuration = 0.05f
    };

    [Header("Visuals")]
    [SerializeField] private GameObject deathVanishEffectPrefab;
    [SerializeField] private Vector3 deathVanishEffectOffset;

    [Header("Speech")]
    [SerializeField] private BossSpeechSituationEnum deathSpeechSituation = BossSpeechSituationEnum.Death;

    [Header("Audio")]
#pragma warning disable 0414
    [HideInInspector]
    [SerializeField, Min(0f)] private float deathBgmFadeOutDuration = 0f;
#pragma warning restore 0414

    [Header("Terminal Ending")]
    [SerializeField] private BossDefeatEndingSequence terminalEndingSequence;

    private BossControllerBase owner;
    private Coroutine runningSequence;
    private ICinematicLetterboxOverlayHandle overlay;
    private readonly List<Renderer> cachedDeathRenderers = new();

    private GameFlowInputBlocker inputBlocker;
    private PlayerCinematicProtection lockedPlayerProtection;
    private bool shouldNotifyRewardsForRunningSequence = true;
    private bool completedViaTerminalEnding;
    private bool hasTerminalNonDialogueUiSuppression;

    public bool HandlesDeathFlow => useDeathPresentation && isActiveAndEnabled;
    public bool ShouldDeferDeathAnimation => HandlesDeathFlow;
    public bool IsRunning => runningSequence != null;
    public bool CompletedViaTerminalEnding => completedViaTerminalEnding;
    private ICameraPresentationDirector DeathCameraDirector =>
        CameraPresentationPlayback.FromBehaviour(deathCameraDirector);

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        bool stoppedRunningSequence = runningSequence != null;
        if (runningSequence != null)
        {
            StopCoroutine(runningSequence);
            runningSequence = null;
        }

        if (stoppedRunningSequence)
            RestoreCameraAfterIncompleteTerminalEnding();

        if (stoppedRunningSequence || hasTerminalNonDialogueUiSuppression)
            ReleaseTerminalNonDialogueUiSuppression(restoreIfIncomplete: !completedViaTerminalEnding);

        CleanupPresentationArtifacts();
        UnlockPlayerControls();
        ReleaseInputBlocker();
    }

    public void Bind(BossControllerBase ownerController)
    {
        owner = ownerController;
        ResolveReferences();
    }

    public void NotifyDeathStarted()
    {
        ResolveReferences();
    }

    public bool TryBeginDeathSequence()
    {
        return TryBeginDeathSequence(true);
    }

    public bool TryBeginDeathSequence(bool notifyRewardsReady)
    {
        if (!HandlesDeathFlow)
            return false;

        if (runningSequence != null)
            return true;

        shouldNotifyRewardsForRunningSequence = notifyRewardsReady;
        completedViaTerminalEnding = false;
        AcquireInputBlocker();
        LockPlayerControls();
        runningSequence = StartCoroutine(RunDeathPresentationRoutine());
        return true;
    }

    private IEnumerator RunDeathPresentationRoutine()
    {
        SoundPlaybackUtility.StopMusic();
        LockPlayerControls();
        overlay = CinematicLetterboxPlayback.CreateOverlay();
        bool hasTerminalEnding = TryGetTerminalEndingSequence(out BossDefeatEndingSequence endingSequence);
        if (hasTerminalEnding)
            AcquireTerminalNonDialogueUiSuppression();

        Coroutine overlayIntroRoutine = hasTerminalEnding
            ? StartCoroutine(overlay.PlayIn(
                deathCinematicIntroDuration,
                deathLetterboxScreenHeightRatio,
                deathUiTargetAlpha,
                captureGlobalUiLayers: false))
            : StartCoroutine(overlay.PlayIn(
                deathCinematicIntroDuration,
                deathLetterboxScreenHeightRatio,
                deathUiTargetAlpha));

        ICameraPresentationDirector resolvedDeathCameraDirector = DeathCameraDirector;
        if (resolvedDeathCameraDirector != null)
            yield return resolvedDeathCameraDirector.FocusBossWithDeathLensRoutine();

        yield return overlayIntroRoutine;
        yield return WaitForPresentationSeconds(deathPreSpeechDelaySeconds);
        yield return PlayAnimationAndWait(breakdownAnimation);
        yield return PlayDeathSpeechAndWait(hasTerminalEnding);

        if (hasTerminalEnding && endingSequence != null)
        {
            yield return endingSequence.RunRoutine(owner, PlayTerminalEndingDialoguePreludeRoutine);
            completedViaTerminalEnding = endingSequence.CompletedViaTerminalEnding;
            if (!completedViaTerminalEnding)
            {
                yield return RestoreCameraAfterIncompleteTerminalEndingRoutine();
                ReleaseTerminalNonDialogueUiSuppression(restoreIfIncomplete: true);
            }
            else
            {
                bool transitionStarted = SceneTransitionPlayback.IsTransitionActive;
                ReleaseTerminalNonDialogueUiSuppression(restoreIfIncomplete: !transitionStarted);
            }

            UnlockPlayerControls();
            CleanupPresentationArtifacts();
            ReleaseInputBlocker();
            runningSequence = null;
            yield break;
        }

        yield return PlayAnimationAndWait(deathAnimation);

        HideBossVisuals();
        SpawnDeathVanishEffect();
        if (shouldNotifyRewardsForRunningSequence)
            NotifyRewardsReady();

        yield return WaitForPresentationSeconds(deathPostVanishHoldSeconds);

        Coroutine overlayOutroRoutine = StartCoroutine(overlay.PlayOut(deathCinematicOutroDuration));

        resolvedDeathCameraDirector = DeathCameraDirector;
        if (resolvedDeathCameraDirector != null)
            yield return resolvedDeathCameraDirector.ReturnToPlayerRoutine();

        yield return overlayOutroRoutine;

        UnlockPlayerControls();
        CleanupPresentationArtifacts();
        ReleaseInputBlocker();
        runningSequence = null;
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

    private void ResolveReferences()
    {
        if (DeathCameraDirector == null)
            deathCameraDirector = CameraPresentationPlayback.Get(this) as MonoBehaviour;

        if (DeathCameraDirector == null)
            deathCameraDirector = CameraPresentationPlayback.FindAny() as MonoBehaviour;

        if (deathAnimator == null)
            deathAnimator = GetComponent<Animator>();

        if (speechController == null || speechController is not IBossSpeechPlayback)
            speechController = ResolveSpeechPlaybackBehaviour();

        if (terminalEndingSequence == null)
            terminalEndingSequence = GetComponent<BossDefeatEndingSequence>();

        if (deathEffectAnchor == null)
            deathEffectAnchor = transform;
    }

    private void AcquireInputBlocker()
    {
        if (inputBlocker != null && inputBlocker.IsBlocking)
            return;

        inputBlocker = GameFlowInputBlocker.GetOrAdd(this);
        inputBlocker?.Acquire();
    }

    private void ReleaseInputBlocker()
    {
        inputBlocker?.Release();
        inputBlocker = null;
    }

    private void LockPlayerControls()
    {
        if (lockedPlayerProtection != null)
            return;

        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null)
            return;

        lockedPlayerProtection = playerTransform.GetComponent<PlayerCinematicProtection>();
        if (lockedPlayerProtection == null)
            lockedPlayerProtection = playerTransform.gameObject.AddComponent<PlayerCinematicProtection>();
        lockedPlayerProtection?.Acquire(this);
    }

    private void UnlockPlayerControls()
    {
        if (lockedPlayerProtection != null)
            lockedPlayerProtection.Release(this);

        lockedPlayerProtection = null;
    }

    private void AcquireTerminalNonDialogueUiSuppression()
    {
        if (hasTerminalNonDialogueUiSuppression)
            return;

        if (!DialoguePlayback.IsAvailable)
            return;

        DialoguePlayback.AcquireNonDialogueUiSuppression(this);
        hasTerminalNonDialogueUiSuppression = true;
    }

    private void ReleaseTerminalNonDialogueUiSuppression(bool restoreIfIncomplete)
    {
        if (!hasTerminalNonDialogueUiSuppression)
            return;

        if (DialoguePlayback.IsAvailable)
        {
            if (restoreIfIncomplete)
                DialoguePlayback.ReleaseNonDialogueUiSuppression(this);
            else
                DialoguePlayback.ReleaseNonDialogueUiSuppressionWithoutRestore(this);
        }

        hasTerminalNonDialogueUiSuppression = false;
    }

    private void HideBossVisuals()
    {
        CacheDeathRenderers();

        for (int i = 0; i < cachedDeathRenderers.Count; i++)
        {
            Renderer renderer = cachedDeathRenderers[i];
            if (renderer != null && !ShouldPreserveRenderer(renderer))
                renderer.enabled = false;
        }

        if (deathAnimator != null)
            deathAnimator.enabled = false;
    }

    private IEnumerator PlayDeathSpeechAndWait(bool useTerminalEndingSpeechTiming)
    {
        IBossSpeechPlayback speechPlayback = speechController as IBossSpeechPlayback;
        if (speechPlayback == null)
        {
            Debug.LogWarning("[BossDeathPresentation] Boss speech playback is missing.", this);
            yield break;
        }

        bool bubbleHidden = false;
        bool started = useTerminalEndingSpeechTiming
            ? speechPlayback.TrySpeakSituationAnimated(
                deathSpeechSituation,
                deathSpeechDuration,
                DialogueAnimType.Slow,
                () => bubbleHidden = true,
                ApplyTerminalDeathSpeechMotion)
            : speechPlayback.TrySpeakSituation(
                deathSpeechSituation,
                deathSpeechDuration,
                () => bubbleHidden = true);

        if (!started)
            yield break;

        float timeout = Mathf.Max(0.5f, deathSpeechDuration + 1f);
        float elapsed = 0f;

        while (!bubbleHidden && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static string ApplyTerminalDeathSpeechMotion(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return line;

        return $"[tremble]{line}[/tremble]";
    }

    private IEnumerator PlayAnimationAndWait(AnimationStepSettings settings)
    {
        if (settings == null || !settings.HasPlayableConfiguration)
            yield break;

        float resolvedDuration = ResolveAnimationDuration(settings);

        if (deathAnimator == null)
        {
            yield return WaitForPresentationSeconds(resolvedDuration);
            yield break;
        }

        deathAnimator.enabled = true;
        StartConfiguredAnimation(settings);

        if (!string.IsNullOrWhiteSpace(settings.stateName))
        {
            yield return WaitForAnimatorStateCompletion(settings.stateName, resolvedDuration);
            yield break;
        }

        yield return WaitForPresentationSeconds(resolvedDuration);
    }

    private void StartConfiguredAnimation(AnimationStepSettings settings)
    {
        if (deathAnimator == null || settings == null)
            return;

        if (!string.IsNullOrWhiteSpace(settings.triggerName))
        {
            deathAnimator.ResetTrigger(settings.triggerName);
            deathAnimator.SetTrigger(settings.triggerName);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.stateName))
            return;

        if (settings.crossFadeDuration > 0f)
            deathAnimator.CrossFadeInFixedTime(settings.stateName, settings.crossFadeDuration, 0);
        else
            deathAnimator.Play(settings.stateName, 0, 0f);
    }

    private IEnumerator WaitForAnimatorStateCompletion(string stateName, float fallbackDuration)
    {
        if (deathAnimator == null || string.IsNullOrWhiteSpace(stateName))
        {
            yield return WaitForPresentationSeconds(fallbackDuration);
            yield break;
        }

        int shortNameHash = Animator.StringToHash(stateName);
        float enterTimeout = Mathf.Max(0.1f, fallbackDuration + 0.5f);
        float elapsed = 0f;
        bool enteredState = false;

        while (elapsed < enterTimeout)
        {
            AnimatorStateInfo stateInfo = deathAnimator.GetCurrentAnimatorStateInfo(0);
            if (AnimatorStateMatches(stateInfo, stateName, shortNameHash))
            {
                enteredState = true;
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!enteredState)
        {
            yield return WaitForPresentationSeconds(fallbackDuration);
            yield break;
        }

        float completionTimeout = Mathf.Max(0.1f, fallbackDuration + 2f);
        elapsed = 0f;

        while (elapsed < completionTimeout)
        {
            AnimatorStateInfo stateInfo = deathAnimator.GetCurrentAnimatorStateInfo(0);
            bool isExpectedState = AnimatorStateMatches(stateInfo, stateName, shortNameHash);

            if (!isExpectedState)
                yield break;

            if (!deathAnimator.IsInTransition(0) && stateInfo.normalizedTime >= 1f)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private float ResolveAnimationDuration(AnimationStepSettings settings)
    {
        if (settings == null)
            return 0f;

        if (settings.fallbackDuration > 0f)
            return settings.fallbackDuration;

        if (deathAnimator == null || deathAnimator.runtimeAnimatorController == null)
            return 0f;

        string preferredClipName = !string.IsNullOrWhiteSpace(settings.stateName)
            ? settings.stateName
            : settings.triggerName;

        if (string.IsNullOrWhiteSpace(preferredClipName))
            return 0f;

        AnimationClip[] clips = deathAnimator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0)
            return 0f;

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name == preferredClipName)
                return clip.length;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
                continue;

            if (clip.name.IndexOf(preferredClipName, StringComparison.OrdinalIgnoreCase) >= 0)
                return clip.length;
        }

        return 0f;
    }

    private static bool AnimatorStateMatches(AnimatorStateInfo stateInfo, string stateName, int shortNameHash)
    {
        return stateInfo.shortNameHash == shortNameHash || stateInfo.IsName(stateName);
    }

    private void CacheDeathRenderers()
    {
        if (cachedDeathRenderers.Count > 0)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null)
            return;

        cachedDeathRenderers.AddRange(renderers);
    }

    private bool ShouldPreserveRenderer(Renderer renderer)
    {
        return false;
    }

    private void NotifyRewardsReady()
    {
        if (completedViaTerminalEnding)
            return;

        if (BossEncounterEndDirector.SuppressesAutomaticRewardReady(owner))
            return;

        if (owner != null)
        {
            RunProgressPlayback.NotifyBossRewardsReady(owner);
            return;
        }

        Debug.LogWarning("[BossDeathPresentation] Cannot notify boss rewards without a BossControllerBase owner.", this);
    }

    private void SpawnDeathVanishEffect()
    {
        if (deathVanishEffectPrefab == null)
            return;

        Transform anchor = deathEffectAnchor != null ? deathEffectAnchor : transform;
        Vector3 spawnPosition = anchor.position + deathVanishEffectOffset;
        GameObject resolvedPrefab = PresentationAssetPlayback.ResolvePrefab(deathVanishEffectPrefab);
        GameObject effectInstance = Instantiate(resolvedPrefab, spawnPosition, Quaternion.identity);
        float destroyDelay = ResolveEffectCleanupDelay(effectInstance);
        if (destroyDelay > 0f)
            Destroy(effectInstance, destroyDelay);
    }

    private static float ResolveEffectCleanupDelay(GameObject effectInstance)
    {
        if (effectInstance == null)
            return 0f;

        ParticleSystem[] particleSystems = effectInstance.GetComponentsInChildren<ParticleSystem>(true);
        float maxLifetime = 0f;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            float duration = main.duration;

            if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
                duration += main.startLifetime.constantMax;
            else
                duration += main.startLifetime.constant;

            maxLifetime = Mathf.Max(maxLifetime, duration);
        }

        return maxLifetime > 0f ? maxLifetime + 0.25f : 5f;
    }

    private void CleanupPresentationArtifacts()
    {
        if (overlay != null)
        {
            overlay.Dispose();
            overlay = null;
        }
    }

    private bool TryGetTerminalEndingSequence(out BossDefeatEndingSequence endingSequence)
    {
        ResolveReferences();
        endingSequence = terminalEndingSequence;
        return endingSequence != null && endingSequence.CanRunForBoss(owner);
    }

    private IEnumerator PlayTerminalEndingDialoguePreludeRoutine()
    {
        Coroutine overlayOutroRoutine = null;
        if (overlay != null)
            overlayOutroRoutine = StartCoroutine(overlay.PlayOut(deathCinematicOutroDuration));

        if (overlayOutroRoutine != null)
            yield return overlayOutroRoutine;

        CleanupPresentationArtifacts();
    }

    private IEnumerator RestoreCameraAfterIncompleteTerminalEndingRoutine()
    {
        ICameraPresentationDirector resolvedDeathCameraDirector = DeathCameraDirector;
        if (resolvedDeathCameraDirector == null)
            yield break;

        yield return resolvedDeathCameraDirector.ReturnToPlayerRoutine();
    }

    private void RestoreCameraAfterIncompleteTerminalEnding()
    {
        ICameraPresentationDirector resolvedDeathCameraDirector = DeathCameraDirector;
        if (completedViaTerminalEnding || resolvedDeathCameraDirector == null)
            return;

        resolvedDeathCameraDirector.RestoreDefaultState();
    }

    private MonoBehaviour ResolveSpeechPlaybackBehaviour()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour is IBossSpeechPlayback)
                return behaviour;
        }

        return null;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

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
    [SerializeField] private CameraPresentationDirector deathCameraDirector;
    [SerializeField] private Animator deathAnimator;
    [SerializeField] private Transform deathEffectAnchor;
    [SerializeField] private BossSpeechController speechController;

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
    [SerializeField, Min(0f)] private float deathBgmFadeOutDuration = 0f;

    private BossControllerBase owner;
    private BossDrop bossDrop;
    private Coroutine runningSequence;
    private BossDeathCinematicOverlay overlay;
    private readonly List<Renderer> cachedDeathRenderers = new();
    private readonly List<ManagedBehaviourState> lockedPlayerBehaviourStates = new();

    private PlayerInteractor2D lockedPlayer;
    private MovementMotor2D lockedPlayerMovement;
    private Rigidbody2D lockedPlayerBody;
    private InteractState previousLockedPlayerState = InteractState.Idle;

    private readonly struct ManagedBehaviourState
    {
        public ManagedBehaviourState(Behaviour behaviour, bool wasEnabled)
        {
            Behaviour = behaviour;
            WasEnabled = wasEnabled;
        }

        public Behaviour Behaviour { get; }
        public bool WasEnabled { get; }
    }

    public bool HandlesDeathFlow => useDeathPresentation && isActiveAndEnabled;
    public bool ShouldDeferDeathAnimation => HandlesDeathFlow;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        if (runningSequence != null)
        {
            StopCoroutine(runningSequence);
            runningSequence = null;
        }

        CleanupPresentationArtifacts();
        UnlockPlayerControls();
    }

    public void Bind(BossControllerBase ownerController, BossDrop configuredBossDrop)
    {
        owner = ownerController;
        bossDrop = configuredBossDrop;
        ResolveReferences();
    }

    public void NotifyDeathStarted()
    {
        ResolveReferences();
    }

    public bool TryBeginDeathSequence()
    {
        if (!HandlesDeathFlow)
            return false;

        if (runningSequence != null)
            return true;

        runningSequence = StartCoroutine(RunDeathPresentationRoutine());
        return true;
    }

    private IEnumerator RunDeathPresentationRoutine()
    {
        SoundManager.EnsureInstance().StopMusic(deathBgmFadeOutDuration);
        LockPlayerControls();
        overlay = new BossDeathCinematicOverlay();

        Coroutine overlayIntroRoutine = StartCoroutine(
            overlay.PlayIn(
                deathCinematicIntroDuration,
                deathLetterboxScreenHeightRatio,
                deathUiTargetAlpha));

        if (deathCameraDirector != null)
            yield return deathCameraDirector.FocusBossRoutine();

        yield return overlayIntroRoutine;
        yield return WaitForPresentationSeconds(deathPreSpeechDelaySeconds);
        yield return PlayAnimationAndWait(breakdownAnimation);
        yield return PlayDeathSpeechAndWait();
        yield return PlayAnimationAndWait(deathAnimation);

        HideBossVisuals();
        SpawnDeathVanishEffect();
        bossDrop?.OnBossDead();

        yield return WaitForPresentationSeconds(deathPostVanishHoldSeconds);

        Coroutine overlayOutroRoutine = StartCoroutine(overlay.PlayOut(deathCinematicOutroDuration));

        if (deathCameraDirector != null)
            yield return deathCameraDirector.ReturnToPlayerRoutine();

        yield return overlayOutroRoutine;

        UnlockPlayerControls();
        CleanupPresentationArtifacts();
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
        if (deathCameraDirector == null)
            deathCameraDirector = FindAnyObjectByType<CameraPresentationDirector>();

        if (deathAnimator == null)
            deathAnimator = GetComponent<Animator>();

        if (speechController == null)
            speechController = GetComponent<BossSpeechController>();

        if (deathEffectAnchor == null)
            deathEffectAnchor = transform;
    }

    private void LockPlayerControls()
    {
        if (lockedPlayer != null)
            return;

        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null)
            return;

        lockedPlayer = playerTransform.GetComponent<PlayerInteractor2D>();
        lockedPlayerMovement = playerTransform.GetComponent<MovementMotor2D>();
        lockedPlayerBody = playerTransform.GetComponent<Rigidbody2D>();

        if (lockedPlayer != null)
        {
            previousLockedPlayerState = NormalizePlayerRestoreState(lockedPlayer.CurrentState);
            lockedPlayer.SetInteractState(InteractState.None);
        }

        CacheAndDisablePlayerBehaviour(playerTransform.GetComponent<PlayerIntentInput2D>());
        CacheAndDisablePlayerBehaviour(playerTransform.GetComponent<PlayerCombatInput2D>());
        CacheAndDisablePlayerBehaviour(playerTransform.GetComponent<PlayerAim2D>());
        CacheAndDisablePlayerBehaviour(playerTransform.GetComponent<PlayerConsumableInput2D>());

        lockedPlayerMovement?.StopAllMotion();

        if (lockedPlayerBody != null)
        {
            lockedPlayerBody.linearVelocity = Vector2.zero;
            lockedPlayerBody.angularVelocity = 0f;
        }
    }

    private void UnlockPlayerControls()
    {
        for (int i = lockedPlayerBehaviourStates.Count - 1; i >= 0; i--)
        {
            ManagedBehaviourState state = lockedPlayerBehaviourStates[i];
            if (state.Behaviour != null)
                state.Behaviour.enabled = state.WasEnabled;
        }

        lockedPlayerBehaviourStates.Clear();

        if (lockedPlayer != null)
            lockedPlayer.SetInteractState(previousLockedPlayerState);

        lockedPlayer = null;
        lockedPlayerMovement = null;
        lockedPlayerBody = null;
        previousLockedPlayerState = InteractState.Idle;
    }

    private void CacheAndDisablePlayerBehaviour(Behaviour behaviour)
    {
        if (behaviour == null)
            return;

        lockedPlayerBehaviourStates.Add(new ManagedBehaviourState(behaviour, behaviour.enabled));
        behaviour.enabled = false;
    }

    private static InteractState NormalizePlayerRestoreState(InteractState state)
    {
        return state == InteractState.None ? InteractState.Idle : state;
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

    private IEnumerator PlayDeathSpeechAndWait()
    {
        if (speechController == null)
        {
            Debug.LogWarning("[BossDeathPresentation] BossSpeechController is missing.", this);
            yield break;
        }

        bool bubbleHidden = false;
        bool started = speechController.TrySpeakSituation(
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
        if (renderer == null || bossDrop == null || bossDrop.portalObj == null)
            return false;

        Transform portalTransform = bossDrop.portalObj.transform;
        return renderer.transform == portalTransform || renderer.transform.IsChildOf(portalTransform);
    }

    private void SpawnDeathVanishEffect()
    {
        if (deathVanishEffectPrefab == null)
            return;

        Transform anchor = deathEffectAnchor != null ? deathEffectAnchor : transform;
        Vector3 spawnPosition = anchor.position + deathVanishEffectOffset;
        GameObject effectInstance = Instantiate(deathVanishEffectPrefab, spawnPosition, Quaternion.identity);
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
}

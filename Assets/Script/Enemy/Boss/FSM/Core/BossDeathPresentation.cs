using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class BossDeathPresentation : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private bool useDeathPresentation = true;
    [SerializeField, Min(0f)] private float deathCinematicIntroDuration = 0.45f;
    [SerializeField, Range(0f, 0.45f)] private float deathLetterboxScreenHeightRatio = 0.14f;
    [SerializeField, Range(0f, 1f)] private float deathUiTargetAlpha = 0f;
    [SerializeField, Min(0f)] private float deathPreSpeechDelaySeconds = 0.1f;
    [SerializeField, Min(0f)] private float deathSpeechDuration = 2.5f;
    [SerializeField, Min(0f)] private float deathPostSpeechHoldSeconds = 0.15f;
    [SerializeField, Min(0f)] private float deathPostVanishHoldSeconds = 0.35f;
    [SerializeField, Min(0f)] private float deathCinematicOutroDuration = 0.35f;

    [Header("References")]
    [SerializeField] private CameraPresentationDirector deathCameraDirector;
    [SerializeField] private SpriteRenderer deathSpriteRenderer;
    [SerializeField] private Animator deathAnimator;
    [SerializeField] private Transform deathEffectAnchor;
    [SerializeField] private BossSpeechController speechController;

    [Header("Visuals")]
    [SerializeField] private Sprite deathPoseSprite;
    [SerializeField, Min(0f)] private float deathPoseSpriteSwapDelaySeconds;
    [SerializeField] private bool disableAnimatorWhenShowingDeathPoseSprite = true;
    [SerializeField] private GameObject deathVanishEffectPrefab;
    [SerializeField] private Vector3 deathVanishEffectOffset;

    [Header("Speech")]
    [SerializeField] private BossSpeechSituationEnum deathSpeechSituation = BossSpeechSituationEnum.Death;

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
    private bool hasPlayedDeferredDeathAnimation;

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

    public void PlayDeferredDeathAnimation()
    {
        if (hasPlayedDeferredDeathAnimation || owner == null)
            return;

        hasPlayedDeferredDeathAnimation = true;
        owner.PlayDeferredDeathAnimationFromPresentation();
    }

    private IEnumerator RunDeathPresentationRoutine()
    {
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

        PlayDeferredDeathAnimation();

        if (deathPoseSprite != null)
        {
            if (deathPoseSpriteSwapDelaySeconds <= 0f)
                ApplyDeathPoseSprite();
            else
                StartCoroutine(ApplyDeathPoseSpriteAfterDelay());
        }

        if (PlayDeathSpeech())
            yield return WaitForPresentationSeconds(deathSpeechDuration);

        yield return WaitForPresentationSeconds(deathPostSpeechHoldSeconds);

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

    private IEnumerator ApplyDeathPoseSpriteAfterDelay()
    {
        yield return WaitForPresentationSeconds(deathPoseSpriteSwapDelaySeconds);
        ApplyDeathPoseSprite();
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

        if (deathSpriteRenderer == null)
            deathSpriteRenderer = GetComponent<SpriteRenderer>();

        if (deathAnimator == null)
            deathAnimator = GetComponent<Animator>();

        if (speechController == null)
            speechController = GetComponent<BossSpeechController>();

        if (deathEffectAnchor == null)
            deathEffectAnchor = transform;
    }

    private void ApplyDeathPoseSprite()
    {
        if (deathSpriteRenderer == null || deathPoseSprite == null)
            return;

        if (disableAnimatorWhenShowingDeathPoseSprite && deathAnimator != null)
            deathAnimator.enabled = false;

        deathSpriteRenderer.sprite = deathPoseSprite;
    }

    private bool PlayDeathSpeech()
    {
        if (speechController == null)
        {
            Debug.LogWarning("[BossDeathPresentation] BossSpeechController is missing.", this);
            return false;
        }

        return speechController.TrySpeakSituation(deathSpeechSituation, deathSpeechDuration);
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

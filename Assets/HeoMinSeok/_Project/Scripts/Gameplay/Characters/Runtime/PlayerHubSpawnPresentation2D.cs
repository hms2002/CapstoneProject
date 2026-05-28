using System;
using System.Collections;
using System.Collections.Generic;
using Cainos.PixelArtTopDown_Basic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class PlayerHubSpawnPresentation2D : MonoBehaviour
{
    // 이 클래스의 책임:
    // 허브 진입 시 플레이어 낙하/기상 연출을 재생하고, 연출 중 플레이어 표현·입력·카메라 상태를 안전하게 전환/복구한다.

    private static readonly InputActionId[] WakeInputActions =
    {
        InputActionId.Interact,
        InputActionId.PrimaryAttack,
        InputActionId.Dash,
        InputActionId.Skill1,
        InputActionId.Skill2,
        InputActionId.SwapWeapon,
        InputActionId.ConsumableSlot1,
        InputActionId.ConsumableSlot2,
        InputActionId.ConsumableSlot3,
        InputActionId.ConsumableSlot4,
        InputActionId.InventoryToggle,
    };

    private enum HubSpawnCameraMode
    {
        LockToLandingPoint = 0,
        FollowFallingPlayer = 1
    }

    private enum HubSpawnPlayCondition
    {
        AnyHubSpawn = 0,
        AfterDarkLordTutorialUntilSeen = 1
    }

    private const string DefaultHubSceneName = "ProtoTypeHub";
    private const string DefaultShadowChildName = "Shadow";
    [Header("Scene")]
    [SerializeField] private string hubSceneName = DefaultHubSceneName;
    [SerializeField] private bool playOnHubSpawn = true;

    [Header("Intro Gate")]
    [SerializeField] private HubSpawnPlayCondition playCondition = HubSpawnPlayCondition.AnyHubSpawn;
    [SerializeField] private string darkLordTutorialCompletionId = HubIntroProgressGate.DefaultDarkLordTutorialCompletionId;
    [SerializeField] private string hubIntroSeenId = HubIntroProgressGate.DefaultHubIntroSeenId;
    [SerializeField] private bool allowEditorBypassTutorialCompletion;

    [Header("Fall")]
    [SerializeField, Min(0.1f)] private float fallDuration = 0.85f;
    [SerializeField, Min(0f)] private float minimumStartHeight = 8f;
    [SerializeField, Min(0f)] private float offscreenPadding = 2f;
    [SerializeField, Min(0f)] private float landingLockSeconds = 2f;
    [SerializeField] private AnimationCurve fallCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 1.4f),
        new Keyframe(1f, 1f, 2.4f, 0f));

    [Header("Rotation")]
    [SerializeField, Min(0)] private int fullSpinsBeforeLanding = 2;
    [SerializeField] private float landingRotationZ = 90f;
    [SerializeField] private float spinDirection = 1f;
    [SerializeField] private AnimationCurve rotationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Shadow")]
    [SerializeField] private string shadowChildName = DefaultShadowChildName;
    [SerializeField] private Transform shadowTransform;
    [SerializeField] private SpriteRenderer shadowSpriteRenderer;
    [SerializeField, Min(1f)] private float shadowStartScaleMultiplier = 3f;
    [SerializeField] private AnimationCurve shadowScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Presentation")]
    [SerializeField] private Animator presentationAnimator;
    [SerializeField] private SpriteRenderer presentationSpriteRenderer;
    [SerializeField] private bool disableAnimatorDuringPresentation = true;

    [Header("Post Landing")]
    [SerializeField, Min(0f)] private float sleepAfterIdleSeconds = 10f;
    [SerializeField, Min(0f)] private float sleepWakeDelaySeconds = 1f;
    [SerializeField] private bool autoWakeWithoutInput;
    [SerializeField, Min(0f)] private float autoWakeDelaySeconds = 2f;

    [Header("Lying Shadow")]
    [SerializeField] private bool useLyingShadowOverride;
    [SerializeField] private Transform lyingShadowPoseReference;
    [SerializeField] private Sprite lyingShadowSprite;
    [SerializeField] private Vector3 lyingShadowLocalOffset = new Vector3(-0.02f, 0.1f, 0.1f);
    [SerializeField] private float lyingShadowLocalRotationZ;
    [SerializeField] private Vector3 lyingShadowScaleMultiplier = Vector3.one;

    [Header("Sleep Presentation")]
    [SerializeField] private Sprite awakeIdleSpriteOverride;
    [SerializeField] private Sprite sleepSprite;
    [SerializeField] private Transform sleepEffectAnchor;
    [SerializeField] private GameObject sleepEffectPrefab;
    [SerializeField] private Vector3 sleepEffectLocalOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private bool useUnscaledSleepEffectTime;

    [Header("Camera")]
    [SerializeField] private HubSpawnCameraMode cameraMode = HubSpawnCameraMode.LockToLandingPoint;

    [Header("Gameplay Presentation (Optional)")]
    [SerializeField] private GameplayPresentationDefinition gameplayPresentation;

    private PlayerInteractor2D interactor;
    private PlayerIntentInput2D intentInput;
    private PlayerCombatInput2D combatInput;
    private PlayerAim2D aimInput;
    private PlayerConsumableInput2D consumableInput;
    private MovementMotor2D movementMotor;
    private AbilitySystem abilitySystem;

    private readonly List<ManagedBehaviourState> managedBehaviourStates = new();
    private readonly List<ColliderState> colliderStates = new();
    private readonly List<RigidbodyState> rigidbodyStates = new();

    private Coroutine sequenceRoutine;
    private Transform cameraAnchor;

    private Transform originalShadowParent;
    private Vector3 originalShadowLocalPosition;
    private Quaternion originalShadowLocalRotation;
    private Vector3 originalShadowLocalScale;
    private Vector3 detachedShadowBaseScale = Vector3.one;
    private int originalShadowSiblingIndex;
    private bool shadowDetached;

    private bool presentationPrepared;
    private bool hasPlayedThisScene;
    private bool fadeUnlockBlocked;
    private Vector3 landingPosition;
    private GameplayPresentationRuntime presentationRuntime;
    private Sprite capturedAwakeSprite;
    private Sprite capturedAwakeShadowSprite;
    private GameObject activeSleepEffectInstance;

    public bool IsPlaying => sequenceRoutine != null;
    public event Action<PlayerHubSpawnPresentation2D> PresentationCompleted;

    private void Awake()
    {
        CacheReferences();
        presentationRuntime = new GameplayPresentationRuntime(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        if (presentationPrepared)
            ForceRestorePresentationState(allowHierarchyMutation: gameObject.activeInHierarchy);
        else
            SetFadeTransitionUnlockBlocked(false);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        hasPlayedThisScene = false;
    }

    public void TryPlayIfEligible()
    {
        if (!playOnHubSpawn || hasPlayedThisScene || sequenceRoutine != null)
            return;

        if (!IsHubScene())
            return;

        if (!ShouldPlayForCondition())
            return;

        CacheReferences();
        sequenceRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        hasPlayedThisScene = true;
        landingPosition = transform.position;
        presentationPrepared = true;

        SetFadeTransitionUnlockBlocked(true);
        PrepareForPresentation();
        DetachShadow();

        Vector3 startPosition = ResolveStartPosition(landingPosition);
        transform.position = startPosition;
        transform.rotation = Quaternion.identity;
        ApplyShadowScale(0f);
        ApplyCameraPresentationMode(landingPosition);
        presentationRuntime?.Start(gameplayPresentation, BuildPresentationParams(startPosition, hasExplicitPosition: true));

        float elapsed = 0f;
        float directionSign = Mathf.Approximately(spinDirection, 0f) ? 1f : Mathf.Sign(spinDirection);
        float totalSpinDegrees = directionSign * ((fullSpinsBeforeLanding * 360f) + landingRotationZ);

        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / fallDuration);

            float positionT = Mathf.Clamp01(EvaluateCurve(fallCurve, normalizedTime));
            float rotationT = Mathf.Clamp01(EvaluateCurve(rotationCurve, normalizedTime));
            float shadowT = Mathf.Clamp01(EvaluateCurve(shadowScaleCurve, normalizedTime));

            transform.position = Vector3.LerpUnclamped(startPosition, landingPosition, positionT);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(0f, totalSpinDegrees, rotationT));
            ApplyShadowScale(shadowT);

            yield return null;
        }

        transform.position = landingPosition;
        transform.rotation = Quaternion.Euler(0f, 0f, landingRotationZ);
        ApplyShadowScale(1f);
        ApplyLyingShadowPresentation();
        presentationRuntime?.Stop(gameplayPresentation, BuildPresentationParams(landingPosition, hasExplicitPosition: true), playRemove: true);

        RestorePhysicsParticipation();
        movementMotor?.StopAllMotion();
        ZeroAllRigidbodies();
        RestoreCameraBindingToPlayer();

        yield return new WaitForSeconds(landingLockSeconds);

        bool completedWake = false;
        if (autoWakeWithoutInput)
        {
            yield return WaitForWakeInputOrAutoWakeRoutine();
            completedWake = true;
        }
        else
        {
            float idleElapsed = 0f;
            while (idleElapsed < sleepAfterIdleSeconds)
            {
                if (HasWakeInput())
                {
                    WakeIntoGameplay();
                    completedWake = true;
                    break;
                }

                idleElapsed += Time.deltaTime;
                yield return null;
            }

            if (!completedWake)
            {
                ApplySleepSprite();
                SpawnSleepEffect();

                while (true)
                {
                    if (HasWakeInput())
                    {
                        StopAndClearSleepEffects();
                        ApplyAwakeIdleSprite();

                        if (sleepWakeDelaySeconds > 0f)
                            yield return new WaitForSeconds(sleepWakeDelaySeconds);

                        WakeIntoGameplay();
                        completedWake = true;
                        break;
                    }

                    yield return null;
                }
            }
        }

        if (!completedWake)
            WakeIntoGameplay();

        presentationPrepared = false;
        SetFadeTransitionUnlockBlocked(false);
        sequenceRoutine = null;
        InvokePresentationCompleted();
    }

    private IEnumerator WaitForWakeInputOrAutoWakeRoutine()
    {
        float elapsed = 0f;
        float delaySeconds = Mathf.Max(0f, autoWakeDelaySeconds);
        while (elapsed < delaySeconds)
        {
            if (HasWakeInput())
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        WakeIntoGameplay();
    }

    private void PrepareForPresentation()
    {
        CacheReferences();
        CaptureManagedBehaviourStates();
        CapturePhysicsStates();

        abilitySystem?.ResetTransientRuntimeState();
        movementMotor?.StopAllMotion();
        ZeroAllRigidbodies();
        CaptureAwakeSprite();
        CaptureAwakeShadowSprite();

        if (interactor != null)
            interactor.SetInteractState(InteractState.None);

        ApplyManagedBehavioursEnabled(false);
        DisablePhysicsParticipation();
    }

    private void RestoreGameplayControl()
    {
        ApplyManagedBehavioursEnabled(true);

        if (interactor != null)
            interactor.SetInteractState(InteractState.Idle);
    }

    private void ForceRestorePresentationState(bool allowHierarchyMutation = true)
    {
        transform.position = landingPosition;
        transform.rotation = Quaternion.identity;

        StopAndClearSleepEffects();
        ApplyAwakeIdleSprite();
        RestoreAwakeShadowPresentation();
        RestorePhysicsParticipation();
        if (allowHierarchyMutation)
        {
            ReattachShadow();
            RestoreCameraBindingToPlayer();
            CleanupCameraAnchor();
        }

        RestoreGameplayControl();
        SetFadeTransitionUnlockBlocked(false);
        ZeroAllRigidbodies();
        presentationRuntime?.Stop(gameplayPresentation, BuildPresentationParams(landingPosition, hasExplicitPosition: true), playRemove: false);

        presentationPrepared = false;
    }

    private void CacheReferences()
    {
        if (interactor == null)
            interactor = GetComponent<PlayerInteractor2D>();
        if (intentInput == null)
            intentInput = GetComponent<PlayerIntentInput2D>();
        if (combatInput == null)
            combatInput = GetComponent<PlayerCombatInput2D>();
        if (aimInput == null)
            aimInput = GetComponent<PlayerAim2D>();
        if (consumableInput == null)
            consumableInput = GetComponent<PlayerConsumableInput2D>();
        if (movementMotor == null)
            movementMotor = GetComponent<MovementMotor2D>();
        if (abilitySystem == null)
            abilitySystem = GetComponent<AbilitySystem>();
        if (presentationAnimator == null)
        {
            Transform playerRender = transform.Find("PlayerRender");
            presentationAnimator = playerRender != null
                ? playerRender.GetComponent<Animator>()
                : GetComponentInChildren<Animator>(true);
        }
        if (presentationSpriteRenderer == null)
        {
            Transform playerRender = transform.Find("PlayerRender");
            presentationSpriteRenderer = playerRender != null
                ? playerRender.GetComponent<SpriteRenderer>()
                : GetComponentInChildren<SpriteRenderer>(true);
        }
        if (shadowTransform == null)
            shadowTransform = transform.Find(shadowChildName);
        if (shadowSpriteRenderer == null && shadowTransform != null)
            shadowSpriteRenderer = shadowTransform.GetComponent<SpriteRenderer>();
        if (sleepEffectAnchor == null)
            sleepEffectAnchor = presentationSpriteRenderer != null ? presentationSpriteRenderer.transform : transform;
    }

    private void CaptureManagedBehaviourStates()
    {
        managedBehaviourStates.Clear();

        AddManagedBehaviour(intentInput);
        AddManagedBehaviour(combatInput);
        AddManagedBehaviour(aimInput);
        AddManagedBehaviour(consumableInput);

        if (disableAnimatorDuringPresentation)
            AddManagedBehaviour(presentationAnimator);
    }

    private void AddManagedBehaviour(Behaviour behaviour)
    {
        if (behaviour == null)
            return;

        managedBehaviourStates.Add(new ManagedBehaviourState(behaviour, behaviour.enabled));
    }

    private void ApplyManagedBehavioursEnabled(bool enabled)
    {
        for (int i = 0; i < managedBehaviourStates.Count; i++)
        {
            ManagedBehaviourState state = managedBehaviourStates[i];
            if (state.behaviour == null)
                continue;

            state.behaviour.enabled = enabled ? state.wasEnabled : false;
        }
    }

    private void CapturePhysicsStates()
    {
        colliderStates.Clear();
        rigidbodyStates.Clear();

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null)
                continue;

            colliderStates.Add(new ColliderState(collider, collider.enabled));
        }

        Rigidbody2D[] rigidbodies = GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody2D body = rigidbodies[i];
            if (body == null)
                continue;

            rigidbodyStates.Add(new RigidbodyState(body, body.simulated));
        }
    }

    private void DisablePhysicsParticipation()
    {
        for (int i = 0; i < colliderStates.Count; i++)
        {
            ColliderState state = colliderStates[i];
            if (state.collider != null)
                state.collider.enabled = false;
        }

        for (int i = 0; i < rigidbodyStates.Count; i++)
        {
            RigidbodyState state = rigidbodyStates[i];
            if (state.body == null)
                continue;

            state.body.linearVelocity = Vector2.zero;
            state.body.angularVelocity = 0f;
            state.body.simulated = false;
        }
    }

    private void RestorePhysicsParticipation()
    {
        for (int i = 0; i < rigidbodyStates.Count; i++)
        {
            RigidbodyState state = rigidbodyStates[i];
            if (state.body == null)
                continue;

            state.body.simulated = state.wasSimulated;
            state.body.linearVelocity = Vector2.zero;
            state.body.angularVelocity = 0f;
        }

        for (int i = 0; i < colliderStates.Count; i++)
        {
            ColliderState state = colliderStates[i];
            if (state.collider != null)
                state.collider.enabled = state.wasEnabled;
        }
    }

    private void ZeroAllRigidbodies()
    {
        for (int i = 0; i < rigidbodyStates.Count; i++)
        {
            RigidbodyState state = rigidbodyStates[i];
            if (state.body == null)
                continue;

            state.body.linearVelocity = Vector2.zero;
            state.body.angularVelocity = 0f;
        }
    }

    private void DetachShadow()
    {
        if (shadowTransform == null || shadowDetached)
            return;

        originalShadowParent = shadowTransform.parent;
        originalShadowLocalPosition = shadowTransform.localPosition;
        originalShadowLocalRotation = shadowTransform.localRotation;
        originalShadowLocalScale = shadowTransform.localScale;
        originalShadowSiblingIndex = shadowTransform.GetSiblingIndex();

        shadowTransform.SetParent(null, true);
        shadowTransform.rotation = Quaternion.identity;
        detachedShadowBaseScale = shadowTransform.localScale;
        shadowDetached = true;
    }

    private void ReattachShadow()
    {
        if (shadowTransform == null || !shadowDetached)
            return;

        Transform targetParent = originalShadowParent != null ? originalShadowParent : transform;
        shadowTransform.SetParent(targetParent, false);

        if (shadowTransform.parent != null)
        {
            int maxSiblingIndex = shadowTransform.parent.childCount - 1;
            shadowTransform.SetSiblingIndex(Mathf.Clamp(originalShadowSiblingIndex, 0, maxSiblingIndex));
        }

        shadowTransform.localPosition = originalShadowLocalPosition;
        shadowTransform.localRotation = originalShadowLocalRotation;
        shadowTransform.localScale = originalShadowLocalScale;
        shadowDetached = false;
    }

    private void ApplyShadowScale(float normalizedTime)
    {
        if (shadowTransform == null || !shadowDetached)
            return;

        float scaleMultiplier = Mathf.LerpUnclamped(shadowStartScaleMultiplier, 1f, normalizedTime);
        shadowTransform.localScale = detachedShadowBaseScale * scaleMultiplier;
    }

    private void CaptureAwakeSprite()
    {
        if (presentationSpriteRenderer == null)
            return;

        capturedAwakeSprite = presentationSpriteRenderer.sprite;
    }

    private void CaptureAwakeShadowSprite()
    {
        if (shadowSpriteRenderer == null)
            return;

        capturedAwakeShadowSprite = shadowSpriteRenderer.sprite;
    }

    private void ApplyAwakeIdleSprite()
    {
        if (presentationSpriteRenderer == null)
            return;

        Sprite spriteToApply = awakeIdleSpriteOverride != null ? awakeIdleSpriteOverride : capturedAwakeSprite;
        if (spriteToApply != null)
            presentationSpriteRenderer.sprite = spriteToApply;
    }

    private void ApplySleepSprite()
    {
        if (presentationSpriteRenderer == null || sleepSprite == null)
            return;

        presentationSpriteRenderer.sprite = sleepSprite;
    }

    private void ApplyLyingShadowPresentation()
    {
        if (!useLyingShadowOverride)
            return;

        ApplyShadowPresentation(
            poseReference: lyingShadowPoseReference,
            overrideSprite: ResolveLyingShadowSprite(),
            localOffset: lyingShadowLocalOffset,
            localRotationZ: lyingShadowLocalRotationZ,
            scaleMultiplier: lyingShadowScaleMultiplier);
    }

    private void ApplyShadowPresentation(
        Transform poseReference,
        Sprite overrideSprite,
        Vector3 localOffset,
        float localRotationZ,
        Vector3 scaleMultiplier)
    {
        if (shadowTransform == null || !shadowDetached)
            return;

        if (shadowSpriteRenderer != null && overrideSprite != null)
            shadowSpriteRenderer.sprite = overrideSprite;

        if (poseReference != null)
        {
            shadowTransform.position = poseReference.position;
            shadowTransform.rotation = poseReference.rotation;
            shadowTransform.localScale = poseReference.lossyScale;
            return;
        }

        shadowTransform.position = transform.TransformPoint(localOffset);
        shadowTransform.rotation = transform.rotation * Quaternion.Euler(0f, 0f, localRotationZ);
        shadowTransform.localScale = new Vector3(
            detachedShadowBaseScale.x * scaleMultiplier.x,
            detachedShadowBaseScale.y * scaleMultiplier.y,
            detachedShadowBaseScale.z * scaleMultiplier.z);
    }

    private void RestoreAwakeShadowPresentation()
    {
        if (shadowSpriteRenderer != null && capturedAwakeShadowSprite != null)
            shadowSpriteRenderer.sprite = capturedAwakeShadowSprite;
    }

    private Sprite ResolveLyingShadowSprite()
    {
        if (lyingShadowSprite != null)
            return lyingShadowSprite;

        if (lyingShadowPoseReference == null)
            return null;

        SpriteRenderer referenceRenderer = lyingShadowPoseReference.GetComponent<SpriteRenderer>();
        return referenceRenderer != null ? referenceRenderer.sprite : null;
    }

    private void WakeIntoGameplay()
    {
        StopAndClearSleepEffects();
        ApplyAwakeIdleSprite();
        transform.rotation = Quaternion.identity;
        ReattachShadow();
        RestoreCameraBindingToPlayer();
        RestoreGameplayControl();
        SetFadeTransitionUnlockBlocked(false);
        CleanupCameraAnchor();
    }

    private bool HasWakeInput()
    {
        InputBindingService input = InputBindingService.EnsureInstance();
        if (input == null)
            return false;

        if (input.GetMoveVectorRaw().sqrMagnitude > 0.0001f)
            return true;

        for (int i = 0; i < WakeInputActions.Length; i++)
        {
            if (input.IsPressed(WakeInputActions[i]))
                return true;
        }

        return false;
    }

    private void SpawnSleepEffect()
    {
        if (sleepEffectPrefab == null || activeSleepEffectInstance != null)
            return;

        Transform anchor = sleepEffectAnchor != null ? sleepEffectAnchor : transform;
        Vector3 spawnPosition = anchor.TransformPoint(sleepEffectLocalOffset);
        Quaternion spawnRotation = anchor.rotation;

        GameObject instance = Instantiate(sleepEffectPrefab, spawnPosition, spawnRotation);
        if (instance == null)
            return;

        instance.SetActive(true);
        ConfigureSleepEffectParticles(instance);
        activeSleepEffectInstance = instance;
    }

    private void ConfigureSleepEffectParticles(GameObject instance)
    {
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            if (useUnscaledSleepEffectTime)
            {
                var main = particleSystem.main;
                main.useUnscaledTime = true;
            }

            particleSystem.Play(withChildren: true);
        }
    }

    private void BindCameraToLandingAnchor(Vector3 targetPosition)
    {
        CleanupCameraAnchor();

        GameObject anchorObject = new GameObject($"{name}_HubSpawnCameraAnchor");
        cameraAnchor = anchorObject.transform;
        cameraAnchor.position = targetPosition;

        CameraFollow legacyFollow = CameraBootstrap.GetLegacyFollow();
        if (legacyFollow != null)
        {
            legacyFollow.BindTarget(cameraAnchor, true);
            legacyFollow.SnapToTarget();
            return;
        }

        var playerCamera = CameraBootstrap.GetPlayerCamera();
        if (playerCamera != null)
        {
            playerCamera.Follow = cameraAnchor;
            playerCamera.LookAt = cameraAnchor;
        }
    }

    private void BindCameraToPlayer(bool snap)
    {
        CleanupCameraAnchor();

        CameraFollow legacyFollow = CameraBootstrap.GetLegacyFollow();
        if (legacyFollow != null)
        {
            legacyFollow.BindTarget(transform, snap);
            if (snap)
                legacyFollow.SnapToTarget();
            return;
        }

        var playerCamera = CameraBootstrap.GetPlayerCamera();
        if (playerCamera != null)
        {
            playerCamera.Follow = transform;
            playerCamera.LookAt = transform;
        }
    }

    private void ApplyCameraPresentationMode(Vector3 targetPosition)
    {
        switch (cameraMode)
        {
            case HubSpawnCameraMode.FollowFallingPlayer:
                BindCameraToPlayer(true);
                break;

            case HubSpawnCameraMode.LockToLandingPoint:
            default:
                BindCameraToLandingAnchor(targetPosition);
                break;
        }
    }

    private void RestoreCameraBindingToPlayer()
    {
        BindCameraToPlayer(true);
    }

    private void CleanupCameraAnchor()
    {
        if (cameraAnchor == null)
            return;

        Destroy(cameraAnchor.gameObject);
        cameraAnchor = null;
    }

    private Vector3 ResolveStartPosition(Vector3 targetPosition)
    {
        float startY = targetPosition.y + minimumStartHeight;

        Camera mainCamera = CameraBootstrap.GetMainCamera();
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera != null)
        {
            float cameraTopY = mainCamera.orthographic
                ? mainCamera.transform.position.y + mainCamera.orthographicSize
                : mainCamera.ViewportToWorldPoint(new Vector3(0.5f, 1f, Mathf.Abs(mainCamera.transform.position.z - targetPosition.z))).y;

            startY = Mathf.Max(startY, cameraTopY + offscreenPadding);
        }

        return new Vector3(targetPosition.x, startY, targetPosition.z);
    }

    private bool IsHubScene()
    {
        Scene ownerScene = gameObject.scene;
        if (!ownerScene.IsValid())
            ownerScene = SceneManager.GetActiveScene();

        return string.Equals(ownerScene.name, hubSceneName, System.StringComparison.Ordinal);
    }

    private bool ShouldPlayForCondition()
    {
        return playCondition switch
        {
            HubSpawnPlayCondition.AfterDarkLordTutorialUntilSeen =>
                HubIntroProgressGate.ShouldPlayAfterDarkLordTutorial(
                    darkLordTutorialCompletionId,
                    hubIntroSeenId,
                    allowEditorBypassTutorialCompletion),
            _ => true,
        };
    }

    private void InvokePresentationCompleted()
    {
        PresentationCompleted?.Invoke(this);
    }

    private GameplayCueParams BuildPresentationParams(Vector3 position, bool hasExplicitPosition)
    {
        return presentationRuntime.BuildParams(
            target: gameObject,
            sourceObject: this,
            explicitPosition: position,
            hasExplicitPosition: hasExplicitPosition);
    }

    private static float EvaluateCurve(AnimationCurve curve, float time)
    {
        if (curve == null || curve.length == 0)
            return time;

        return curve.Evaluate(time);
    }

    private void StopAndClearSleepEffects()
    {
        if (activeSleepEffectInstance != null)
            Destroy(activeSleepEffectInstance);

        activeSleepEffectInstance = null;
    }

    private void SetFadeTransitionUnlockBlocked(bool blocked)
    {
        if (fadeUnlockBlocked == blocked)
            return;

        fadeUnlockBlocked = blocked;
        SceneFadeTransitionService.Instance?.SetPlayerUnlockBlocked(this, blocked);
    }

    private readonly struct ManagedBehaviourState
    {
        public readonly Behaviour behaviour;
        public readonly bool wasEnabled;

        public ManagedBehaviourState(Behaviour behaviour, bool wasEnabled)
        {
            this.behaviour = behaviour;
            this.wasEnabled = wasEnabled;
        }
    }

    private readonly struct ColliderState
    {
        public readonly Collider2D collider;
        public readonly bool wasEnabled;

        public ColliderState(Collider2D collider, bool wasEnabled)
        {
            this.collider = collider;
            this.wasEnabled = wasEnabled;
        }
    }

    private readonly struct RigidbodyState
    {
        public readonly Rigidbody2D body;
        public readonly bool wasSimulated;

        public RigidbodyState(Rigidbody2D body, bool wasSimulated)
        {
            this.body = body;
            this.wasSimulated = wasSimulated;
        }
    }
}

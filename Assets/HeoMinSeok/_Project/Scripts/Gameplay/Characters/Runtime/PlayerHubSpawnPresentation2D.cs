using System.Collections;
using System.Collections.Generic;
using Cainos.PixelArtTopDown_Basic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class PlayerHubSpawnPresentation2D : MonoBehaviour
{
    private enum HubSpawnCameraMode
    {
        LockToLandingPoint = 0,
        FollowFallingPlayer = 1
    }

    private const string DefaultHubSceneName = "ProtoTypeHub";
    private const string DefaultShadowChildName = "Shadow";

    [Header("Scene")]
    [SerializeField] private string hubSceneName = DefaultHubSceneName;
    [SerializeField] private bool playOnHubSpawn = true;

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
    [SerializeField, Min(1f)] private float shadowStartScaleMultiplier = 3f;
    [SerializeField] private AnimationCurve shadowScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Presentation")]
    [SerializeField] private Animator presentationAnimator;
    [SerializeField] private bool disableAnimatorDuringPresentation = true;

    [Header("Camera")]
    [SerializeField] private HubSpawnCameraMode cameraMode = HubSpawnCameraMode.LockToLandingPoint;

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
    private Vector3 landingPosition;

    private void Awake()
    {
        CacheReferences();
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
            ForceRestorePresentationState();
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

        CacheReferences();
        sequenceRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        hasPlayedThisScene = true;
        landingPosition = transform.position;
        presentationPrepared = true;

        PrepareForPresentation();
        DetachShadow();

        Vector3 startPosition = ResolveStartPosition(landingPosition);
        transform.position = startPosition;
        transform.rotation = Quaternion.identity;
        ApplyShadowScale(0f);
        ApplyCameraPresentationMode(landingPosition);

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

        RestorePhysicsParticipation();
        movementMotor?.StopAllMotion();
        ZeroAllRigidbodies();

        yield return new WaitForSeconds(landingLockSeconds);

        transform.rotation = Quaternion.identity;
        ReattachShadow();
        RestoreGameplayControl();
        RestoreCameraBindingToPlayer();
        CleanupCameraAnchor();

        presentationPrepared = false;
        sequenceRoutine = null;
    }

    private void PrepareForPresentation()
    {
        CacheReferences();
        CaptureManagedBehaviourStates();
        CapturePhysicsStates();

        abilitySystem?.ResetTransientRuntimeState();
        movementMotor?.StopAllMotion();
        ZeroAllRigidbodies();

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

    private void ForceRestorePresentationState()
    {
        transform.position = landingPosition;
        transform.rotation = Quaternion.identity;

        RestorePhysicsParticipation();
        ReattachShadow();
        RestoreGameplayControl();
        RestoreCameraBindingToPlayer();
        CleanupCameraAnchor();
        ZeroAllRigidbodies();

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
        if (shadowTransform == null)
            shadowTransform = transform.Find(shadowChildName);
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

    private static float EvaluateCurve(AnimationCurve curve, float time)
    {
        if (curve == null || curve.length == 0)
            return time;

        return curve.Evaluate(time);
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

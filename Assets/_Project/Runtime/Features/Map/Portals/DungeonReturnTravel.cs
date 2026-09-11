using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>Owns one generated dungeon's same-scene return sequence and releases all temporary player, camera and timer state.</summary>
[DisallowMultipleComponent]
public sealed class DungeonReturnTravel : MonoBehaviour
{
    [SerializeField] private Transform landingPoint;
    [SerializeField] private DungeonReturnPortalView arrivalPortal;
    [SerializeField] private RoomSocketDirection arrivalDirection = RoomSocketDirection.Down;
    [SerializeField, Min(0f)] private float fadeSeconds = 0.15f;
    [SerializeField, Min(0f)] private float fallHeight = 3f;
    [SerializeField, Min(0.01f)] private float fallSeconds = 0.45f;
    [SerializeField, Min(0f)] private float landingHoldSeconds = 0.1f;
    private readonly Dictionary<Collider2D, bool> colliderStates = new();
    private Func<Vector3, float, Transform, bool> landingValidator;
    private DungeonMapRuntimeController map;
    private int startRoomId;
    private Coroutine sequence;
    private Transform player;
    private PlayerPortalArrivalVisual2D visual;
    private PlayerCinematicProtection protection;
    private PlayerTargetabilityBlocker targetability;
    private RunTimeLimitSystem timer;
    private IGameplayCameraFocusSession cameraSession;
    private ISceneFadeTransitionHandle fade;
    private bool ownsFade;
    public bool IsTravelling { get; private set; }
    public Transform LandingPoint => landingPoint;

    public void Configure(Vector3 position, int roomId, DungeonMapRuntimeController mapRuntime,
        Func<Vector3, float, Transform, bool> validator)
    {
        landingPoint.position = position;
        startRoomId = roomId;
        map = mapRuntime;
        landingValidator = validator;
        arrivalPortal?.SelectDirection(arrivalDirection);
    }

    public bool CanTravel(IPlayerInteractor interactor)
    {
        if (!isActiveAndEnabled || IsTravelling || landingPoint == null || interactor?.Transform == null ||
            interactor.CurrentState != InteractState.Idle || Time.timeScale <= 0f ||
            (SceneFadeTransitionPlayback.Instance?.IsTransitionActive ?? false)) return false;
        Transform target = interactor.Transform;
        AbilitySystem abilities = target.GetComponent<AbilitySystem>();
        PlayerPortalArrivalVisual2D playerVisual = target.GetComponent<PlayerPortalArrivalVisual2D>();
        return (abilities == null || !abilities.IsBusy) && playerVisual != null && playerVisual.IsConfigured &&
            target.GetComponent<MovementMotor2D>() != null &&
            landingValidator != null && landingValidator(landingPoint.position, ResolveClearance(target), target);
    }

    public bool TryTravel(DungeonReturnPortal portal, IPlayerInteractor interactor)
    {
        if (portal == null || !portal.CanInteract(interactor) || !CanTravel(interactor)) return false;
        player = interactor.Transform;
        IsTravelling = true;
        sequence = StartCoroutine(Run());
        return true;
    }

    private IEnumerator Run()
    {
        try
        {
            visual = player.GetComponent<PlayerPortalArrivalVisual2D>();
            if (visual == null || !visual.Begin()) yield break;
            protection = player.GetComponent<PlayerCinematicProtection>();
            if (protection == null) protection = player.gameObject.AddComponent<PlayerCinematicProtection>();
            protection.Acquire(this);
            targetability = PlayerTargetabilityBlocker.GetOrAdd(player);
            targetability.Acquire(this);
            timer = RunTimeLimitSystem.Instance;
            timer?.SetExternalPause(this, true);
            cameraSession = GameplayCameraFocusPlayback.Capture(this);
            fade = SceneFadeTransitionPlayback.Instance;
            ownsFade = fade != null && fade.TryBeginOverlayFadeSession();
            if (ownsFade) yield return fade.FadeOutAsync(fadeSeconds);
            if (player == null || !landingValidator(landingPoint.position, ResolveClearance(player), player)) yield break;

            visual.SetVisible(false);
            // Body stays at the landing floor; only the authored render roots fall from above it.
            foreach (Collider2D collider in player.GetComponentsInChildren<Collider2D>(true))
            {
                colliderStates[collider] = collider.enabled;
                collider.enabled = false;
            }
            MovementMotor2D motor = player.GetComponent<MovementMotor2D>();
            motor.StopAllMotion();
            motor.WarpTo(landingPoint.position);
            yield return new WaitForFixedUpdate();
            if (player == null) yield break;
            cameraSession?.SetTarget(landingPoint);
            cameraSession?.SnapToTarget(landingPoint);
            visual.SetHeight(fallHeight, 1f);
            if (arrivalPortal != null)
            {
                arrivalPortal.transform.position = landingPoint.position + Vector3.up * fallHeight;
                arrivalPortal.Open();
            }
            if (ownsFade) yield return fade.FadeInAsync(fadeSeconds);
            if (arrivalPortal != null) yield return new WaitForSecondsRealtime(arrivalPortal.OpenSeconds);
            visual.SetVisible(true);
            float elapsed = 0f;
            while (elapsed < Mathf.Max(0.01f, fallSeconds) && player != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fallSeconds));
                float remaining = 1f - t * t;
                visual.SetHeight(fallHeight * remaining, remaining);
                yield return null;
            }
            visual?.SetHeight(0f, 0f);
            map?.NotifyPlayerEnteredRoom(startRoomId);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, landingHoldSeconds));
            if (arrivalPortal != null)
            {
                arrivalPortal.Close();
                yield return new WaitForSecondsRealtime(arrivalPortal.CloseSeconds);
            }
        }
        finally { Restore(); sequence = null; }
    }

    private static float ResolveClearance(Transform target)
    {
        PlayerInteractor2D interactor = target.GetComponent<PlayerInteractor2D>();
        Collider2D body = interactor != null ? interactor.BodyCollider : target.GetComponentInChildren<Collider2D>();
        return body != null && body.enabled ? Mathf.Max(0.35f, body.bounds.extents.magnitude +
            Vector2.Distance(body.bounds.center, target.position)) : 0.35f;
    }

    public void Cancel()
    {
        if (sequence != null) StopCoroutine(sequence);
        sequence = null;
        Restore();
    }

    private void Restore()
    {
        visual?.Restore();
        foreach (var pair in colliderStates)
            if (pair.Key != null) pair.Key.enabled = pair.Value;
        colliderStates.Clear();
        arrivalPortal?.HideImmediate();
        cameraSession?.Restore(player);
        cameraSession = null;
        if (ownsFade) fade?.EndOverlayFadeSession();
        ownsFade = false;
        fade = null;
        if (timer != null) timer.SetExternalPause(this, false);
        if (targetability != null) targetability.Release(this);
        if (protection != null) protection.Release(this);
        timer = null; targetability = null; protection = null; visual = null; player = null;
        IsTravelling = false;
    }

    private void OnEnable() => RunSessionStore.OnRunEnded += HandleRunEnded;
    private void OnDisable() { RunSessionStore.OnRunEnded -= HandleRunEnded; Cancel(); }
    private void HandleRunEnded(RunEndReason reason) => Cancel();
#if UNITY_EDITOR
    public void EditorConfigure(Transform landing, DungeonReturnPortalView portal) { landingPoint = landing; arrivalPortal = portal; }
#endif
}

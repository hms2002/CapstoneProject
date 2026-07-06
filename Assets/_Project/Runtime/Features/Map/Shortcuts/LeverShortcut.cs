using System.Collections;
using CapstoneAudio;
using UnityEngine;

/// <summary>
/// 책임 : 영구 레버 숏컷의 활성화 상태와 문 공개 컷씬 연출을 관리한다.
/// </summary>
public class LeverShortcut : PermanentShortcut
{
    private static readonly SoundRef SwitchSound = SoundRef.FromKey("sound_lever_Switch");

    [Header("\uD504\uB86C\uD504\uD2B8")]
    [SerializeField] private string interactPromptText = "\uC791\uB3D9\uD558\uAE30";

    [Header("\uBE44\uC8FC\uC5BC")]
    [SerializeField] private SpriteRenderer leverRenderer;
    [SerializeField] private Sprite activatedSprite;

    [Header("Door Reveal Cinematic")]
    [SerializeField] private Transform doorFocusTarget;
    [SerializeField, Min(0f)] private float cinematicIntroDuration = 0.35f;
    [SerializeField, Min(0f)] private float cameraMoveStartDelaySeconds = 0.15f;
    [SerializeField, Min(0f)] private float cameraFocusWaitSeconds = 0.55f;
    [SerializeField, Min(0.01f)] private float cameraMoveMaxSpeed = 8f;
    [SerializeField, Min(0f)] private float cameraMoveArrivalDistance = 0.05f;
    [SerializeField, Min(0f)] private float postOpenWaitSeconds = 0.8f;
    [SerializeField, Min(0f)] private float cameraReturnWaitSeconds = 0.45f;
    [SerializeField, Min(0f)] private float cinematicOutroDuration = 0.25f;
    [SerializeField, Range(0f, 0.45f)] private float letterboxScreenHeightRatio = 0.14f;
    [SerializeField, Range(0f, 1f)] private float uiTargetAlpha = 0f;
    [SerializeField] private bool pauseWorldTimeDuringCinematic;

    private Sprite defaultSprite;
    private Coroutine cinematicRoutine;
    private ICinematicLetterboxOverlayHandle overlay;
    private GameFlowInputBlocker inputBlocker;
    private PlayerCinematicProtection lockedPlayerProtection;
    private IGameplayCameraFocusSession cameraFocusSession;
    private Transform cameraMoveTarget;
    private bool isCinematicPlaying;
    private bool holdsTimeScalePause;
    private bool holdsRunTimerPause;

    protected override void Awake()
    {
        base.Awake();

        if (leverRenderer != null)
            defaultSprite = leverRenderer.sprite;
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return !isCinematicPlaying && base.CanInteract(player);
    }

    protected override bool CheckCondition(IPlayerInteractor player) => true;

    protected override void OnSuccess()
    {
        SetActivatedVisual();
        SoundPlaybackUtility.Play(SwitchSound, causer: gameObject, position: transform.position, sourceObject: this);

        if (targetDoor == null)
            return;

        if (cinematicRoutine != null)
        {
            StopCoroutine(cinematicRoutine);
            cinematicRoutine = null;
            CleanupCinematicState();
        }

        isCinematicPlaying = true;
        cinematicRoutine = StartCoroutine(PlayDoorRevealCinematicRoutine());
    }

    protected override void SetActivatedVisual()
    {
        if (leverRenderer == null)
            return;

        if (activatedSprite != null)
            leverRenderer.sprite = activatedSprite;
    }

    public void SetDeactivatedVisual()
    {
        if (leverRenderer == null)
            return;

        if (defaultSprite != null)
            leverRenderer.sprite = defaultSprite;
    }

    public override string GetInteractDescription() => isCinematicPlaying ? string.Empty : interactPromptText;

    private void OnDisable()
    {
        if (cinematicRoutine != null)
        {
            StopCoroutine(cinematicRoutine);
            cinematicRoutine = null;
        }

        CleanupCinematicState();
        isCinematicPlaying = false;
    }

    private IEnumerator PlayDoorRevealCinematicRoutine()
    {
        AcquireCinematicTimePause();
        AcquireInputBlocker();
        LockPlayerControls();

        overlay = CinematicLetterboxPlayback.CreateOverlay();
        Coroutine overlayInRoutine = StartCoroutine(
            overlay.PlayIn(cinematicIntroDuration, letterboxScreenHeightRatio, uiTargetAlpha));

        yield return WaitForPresentationSeconds(cameraMoveStartDelaySeconds);

        Coroutine cameraInRoutine = StartCoroutine(FocusDoorCameraRoutine());
        yield return cameraInRoutine;
        yield return overlayInRoutine;

        if (targetDoor != null)
        {
            targetDoor.ForceOpen(
                immediate: false,
                save: true,
                instigator: gameObject);
        }

        yield return WaitForPresentationSeconds(postOpenWaitSeconds);

        Coroutine cameraOutRoutine = StartCoroutine(ReturnCameraRoutine());
        Coroutine overlayOutRoutine = StartCoroutine(overlay.PlayOut(cinematicOutroDuration));

        yield return cameraOutRoutine;
        yield return overlayOutRoutine;

        cinematicRoutine = null;
        CleanupCinematicState();
        isCinematicPlaying = false;
    }

    private IEnumerator FocusDoorCameraRoutine()
    {
        Transform focusTarget = ResolveDoorFocusTarget();
        if (focusTarget == null)
            yield break;

        CacheCameraState();
        Transform moveTarget = EnsureCameraMoveTarget();
        SetCameraTarget(moveTarget);
        yield return MoveCameraTargetToRoutine(moveTarget, focusTarget);
        yield return WaitForPresentationSeconds(cameraFocusWaitSeconds);
        if (cameraFocusSession != null)
            yield return cameraFocusSession.WaitForSettle(focusTarget);
    }

    private IEnumerator ReturnCameraRoutine()
    {
        if (cameraFocusSession == null)
            yield break;

        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        Transform restoreTarget = playerTransform != null ? playerTransform : cameraFocusSession.CachedFollow;
        Transform moveTarget = EnsureCameraMoveTarget();
        SetCameraTarget(moveTarget != null ? moveTarget : restoreTarget);
        yield return MoveCameraTargetToRoutine(moveTarget, restoreTarget);
        yield return WaitForPresentationSeconds(cameraReturnWaitSeconds);
        yield return cameraFocusSession.WaitForSettle(restoreTarget);
        RestoreCameraState(restoreTarget);
    }

    private IEnumerator MoveCameraTargetToRoutine(Transform movingTarget, Transform destination)
    {
        if (movingTarget == null || destination == null)
            yield break;

        float speed = Mathf.Max(0.01f, cameraMoveMaxSpeed);
        float arrivalDistance = Mathf.Max(0f, cameraMoveArrivalDistance);

        while (Vector2.Distance(movingTarget.position, destination.position) > arrivalDistance)
        {
            Vector3 currentPosition = movingTarget.position;
            Vector3 targetPosition = destination.position;
            targetPosition.z = currentPosition.z;

            movingTarget.position = Vector3.MoveTowards(
                currentPosition,
                targetPosition,
                speed * Time.unscaledDeltaTime);

            yield return null;
        }

        Vector3 finalPosition = destination.position;
        finalPosition.z = movingTarget.position.z;
        movingTarget.position = finalPosition;
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

    private Transform ResolveDoorFocusTarget()
    {
        if (doorFocusTarget != null)
            return doorFocusTarget;

        return targetDoor != null ? targetDoor.transform : null;
    }

    private Transform EnsureCameraMoveTarget()
    {
        if (cameraMoveTarget != null)
            return cameraMoveTarget;

        GameObject targetObject = new GameObject($"{name}_DoorRevealCameraTarget");
        targetObject.hideFlags = HideFlags.HideAndDontSave;
        cameraMoveTarget = targetObject.transform;
        cameraMoveTarget.position = ResolveCameraCenterPosition();
        return cameraMoveTarget;
    }

    private Vector3 ResolveCameraCenterPosition()
    {
        IGameplayCameraFocusSession session = cameraFocusSession ?? GameplayCameraFocusPlayback.Capture(this);
        if (session != null)
            return session.CurrentCenter;

        return transform.position;
    }

    private void CacheCameraState()
    {
        if (cameraFocusSession != null)
            return;

        cameraFocusSession = GameplayCameraFocusPlayback.Capture(this);
    }

    private void SetCameraTarget(Transform target)
    {
        cameraFocusSession?.SetTarget(target);
    }

    private void RestoreCameraState(Transform preferredTarget)
    {
        if (cameraFocusSession == null)
            return;

        cameraFocusSession.Restore(preferredTarget);
        cameraFocusSession = null;
    }

    private void DestroyCameraMoveTarget()
    {
        if (cameraMoveTarget == null)
            return;

        if (Application.isPlaying)
            Destroy(cameraMoveTarget.gameObject);
        else
            DestroyImmediate(cameraMoveTarget.gameObject);

        cameraMoveTarget = null;
    }

    private void AcquireInputBlocker()
    {
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
        if (playerTransform == null && PlayerInteractor2D.Instance != null)
            playerTransform = PlayerInteractor2D.Instance.transform;

        if (playerTransform == null)
            return;

        lockedPlayerProtection = playerTransform.GetComponent<PlayerCinematicProtection>();
        if (lockedPlayerProtection == null)
            lockedPlayerProtection = playerTransform.gameObject.AddComponent<PlayerCinematicProtection>();

        lockedPlayerProtection.Acquire(this);
    }

    private void UnlockPlayerControls()
    {
        if (lockedPlayerProtection != null)
            lockedPlayerProtection.Release(this);

        lockedPlayerProtection = null;
    }

    private void CleanupCinematicState()
    {
        if (overlay != null)
        {
            overlay.Dispose();
            overlay = null;
        }

        RestoreCameraState(PlayerRuntimeRegistry.GetPlayerTransform());
        DestroyCameraMoveTarget();
        UnlockPlayerControls();
        ReleaseInputBlocker();
        ReleaseCinematicTimePause();
    }

    /// <summary>
    /// 책임 : 레버 문 개방 시네마틱 동안 런 타이머를 멈추고, 옵션에 따라 전투 월드 시간도 정지한다.
    /// </summary>
    private void AcquireCinematicTimePause()
    {
        if (pauseWorldTimeDuringCinematic && !holdsTimeScalePause)
        {
            TimeScalePausePlayback.Acquire(this);
            holdsTimeScalePause = true;
        }

        if (!holdsRunTimerPause && RunTimeLimitSystem.Instance != null)
        {
            RunTimeLimitSystem.Instance.SetExternalPause(this, true);
            holdsRunTimerPause = true;
        }
    }

    /// <summary>
    /// 책임 : 레버 문 개방 시네마틱 종료/중단 시 보유한 시간 정지 요청을 반드시 반환한다.
    /// </summary>
    private void ReleaseCinematicTimePause()
    {
        if (holdsRunTimerPause && RunTimeLimitSystem.Instance != null)
            RunTimeLimitSystem.Instance.SetExternalPause(this, false);
        holdsRunTimerPause = false;

        if (holdsTimeScalePause)
            TimeScalePausePlayback.Release(this);
        holdsTimeScalePause = false;
    }
}

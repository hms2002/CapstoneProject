using System.Collections;
using Cainos.PixelArtTopDown_Basic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class MerchantActivationCinematic : MonoBehaviour
{
    [Header("Shop Roots")]
    [SerializeField, FormerlySerializedAs("brokenShopRoot")] private GameObject brokenGroup;
    [SerializeField, FormerlySerializedAs("activeShopRoot")] private GameObject activeGroup;

    [Header("Merchant Focus")]
    [SerializeField] private Transform merchantFocusTarget;
    [SerializeField] private SpeechBubbleComponent merchantSpeechBubble;
    [SerializeField] private string merchantSpeechText = "\uC6B0\uB9AC \uAC00\uAC8C \uC815\uC0C1\uC601\uC5C5 \uD569\uB2C8\uB2E4~";

    [Header("Focus Camera")]
    [SerializeField] private CinemachineCamera merchantFocusCamera;
    [SerializeField] private int merchantFocusCameraPriority = 10000;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float cinematicIntroDuration = 0.45f;
    [SerializeField, Min(0f)] private float cameraMoveStartDelaySeconds = 0.35f;
    [SerializeField, Min(0f)] private float cameraFocusWaitSeconds = 0.8f;
    [SerializeField, Min(0f)] private float speechDuration = 2.5f;
    [SerializeField, Min(0f)] private float cameraReturnWaitSeconds = 0.6f;
    [SerializeField, Min(0f)] private float cinematicOutroDuration = 0.35f;

    [Header("Overlay")]
    [SerializeField, Range(0f, 0.45f)] private float letterboxScreenHeightRatio = 0.14f;
    [SerializeField, Range(0f, 1f)] private float uiTargetAlpha = 0f;

    [Header("Camera Zoom")]
    [SerializeField] private bool zoomCameraDuringFocus = true;
    [SerializeField, Min(0.01f)] private float merchantFocusOrthographicSize = 3.5f;
    [SerializeField, Min(0f)] private float zoomInDuration = 0.45f;
    [SerializeField, Min(0f)] private float zoomOutDuration = 0.35f;

    private Coroutine pendingRoutine;
    private CinematicLetterboxOverlay overlay;
    private GameFlowInputBlocker inputBlocker;
    private PlayerCinematicProtection lockedPlayerProtection;

    private CinemachineCamera gameplayCamera;
    private CinemachineBrain cameraBrain;
    private CameraFollow legacyFollowCamera;
    private Transform cachedCameraFollow;
    private Transform cachedCameraLookAt;
    private int cachedCameraPriority;
    private float cachedCameraOrthographicSize;
    private Transform cachedFocusCameraFollow;
    private Transform cachedFocusCameraLookAt;
    private int cachedFocusCameraPriority;
    private float cachedFocusCameraOrthographicSize;
    private bool cachedFocusCameraActiveSelf;
    private bool cachedLegacyFollowEnabled;
    private bool cachedBrainIgnoreTimeScale;
    private bool hasCachedCameraState;
    private bool hasCachedCameraLens;
    private bool hasCachedFocusCameraState;
    private bool hasCachedFocusCameraLens;
    private bool isUsingFocusCamera;
    private bool hasLoggedMissingFocusTarget;
    private bool hasLoggedInvalidFocusCamera;
    private bool isSubscribedToUpgradeManager;
    private bool isSubscribedToRunModifierService;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToServices();
        ApplyShopActivationState();
    }

    private void Start()
    {
        SubscribeToServices();
        ApplyShopActivationState();
    }

    private void Update()
    {
        if (!isSubscribedToUpgradeManager || !isSubscribedToRunModifierService)
            SubscribeToServices();
    }

    private void OnDisable()
    {
        UnsubscribeFromServices();

        if (pendingRoutine != null)
        {
            StopCoroutine(pendingRoutine);
            pendingRoutine = null;
        }

        CleanupCinematicState();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void HandleUpgradeUiClosed()
    {
        if (pendingRoutine != null)
            return;

        pendingRoutine = StartCoroutine(PlayPendingCinematicsAfterPopupsRoutine());
    }

    private void HandleModifiersChanged()
    {
        ApplyShopActivationState();
    }

    private IEnumerator PlayPendingCinematicsAfterPopupsRoutine()
    {
        yield return null;
        yield return WaitForPopupsClosed();

        while (UpgradeManager.Instance != null &&
               UpgradeManager.Instance.TryDequeuePendingCinematic(out UpgradeCinematicRequest request))
        {
            if (request.Type == UpgradeCinematicType.ShopActivated)
                yield return PlayShopActivatedRoutine();

            yield return WaitForPopupsClosed();
        }

        pendingRoutine = null;
    }

    private IEnumerator PlayShopActivatedRoutine()
    {
        ResolveReferences();
        ApplyShopActivationState(true);

        if (merchantFocusTarget == null)
        {
            WarnMissingFocusTargetOnce();
            yield break;
        }

        AcquireInputBlocker();
        LockPlayerControls();
        overlay = new CinematicLetterboxOverlay();

        Coroutine overlayInRoutine = StartCoroutine(
            overlay.PlayIn(cinematicIntroDuration, letterboxScreenHeightRatio, uiTargetAlpha));

        yield return WaitForPresentationSeconds(cameraMoveStartDelaySeconds);

        Coroutine cameraInRoutine = StartCoroutine(FocusCameraRoutine());

        yield return cameraInRoutine;
        yield return overlayInRoutine;

        PlayMerchantSpeech();
        yield return WaitForPresentationSeconds(speechDuration);

        Coroutine overlayOutRoutine = StartCoroutine(overlay.PlayOut(cinematicOutroDuration));
        Coroutine cameraOutRoutine = StartCoroutine(ReturnCameraRoutine());

        yield return cameraOutRoutine;
        yield return overlayOutRoutine;

        CleanupCinematicState();
    }

    private IEnumerator FocusCameraRoutine()
    {
        CacheCameraState();
        if (TryBeginMerchantFocusCameraBlend())
        {
            yield return WaitForPresentationSeconds(cameraFocusWaitSeconds);
            yield break;
        }

        SetCameraTarget(merchantFocusTarget);
        yield return ZoomCameraWhileWaitingRoutine(
            merchantFocusOrthographicSize,
            zoomInDuration,
            cameraFocusWaitSeconds);
    }

    private IEnumerator ReturnCameraRoutine()
    {
        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        Transform restoreTarget = playerTransform != null ? playerTransform : cachedCameraFollow;

        SetCameraTarget(restoreTarget);

        if (isUsingFocusCamera && merchantFocusCamera != null)
        {
            LowerMerchantFocusCameraPriorityForReturn();
            yield return WaitForPresentationSeconds(cameraReturnWaitSeconds);
            RestoreCameraState(restoreTarget);
            yield break;
        }

        float restoreOrthographicSize = hasCachedCameraLens
            ? cachedCameraOrthographicSize
            : GetCameraOrthographicSize();

        yield return ZoomCameraWhileWaitingRoutine(
            restoreOrthographicSize,
            zoomOutDuration,
            cameraReturnWaitSeconds);

        RestoreCameraState(restoreTarget);
    }

    private IEnumerator WaitForPopupsClosed()
    {
        while (UIManager.Instance != null && UIManager.Instance.HasActivePopup())
            yield return null;
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
        if (merchantSpeechBubble == null)
        {
            if (activeGroup != null)
                merchantSpeechBubble = activeGroup.GetComponentInChildren<SpeechBubbleComponent>(true);
            else
                merchantSpeechBubble = GetComponentInChildren<SpeechBubbleComponent>(true);
        }

        if (merchantFocusTarget == null && merchantSpeechBubble != null)
            merchantFocusTarget = merchantSpeechBubble.transform;
    }

    private void ApplyShopActivationState()
    {
        ApplyShopActivationState(RunModifierService.CurrentRewardSnapshot.ShopModifiers.shopEnabled);
    }

    private void ApplyShopActivationState(bool isShopEnabled)
    {
        if (brokenGroup != null)
            brokenGroup.SetActive(!isShopEnabled);

        if (activeGroup != null)
            activeGroup.SetActive(isShopEnabled);
    }

    private void SubscribeToServices()
    {
        if (!isSubscribedToUpgradeManager && UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUIClosed += HandleUpgradeUiClosed;
            isSubscribedToUpgradeManager = true;
        }

        if (!isSubscribedToRunModifierService && RunModifierService.Instance != null)
        {
            RunModifierService.Instance.OnModifiersChanged += HandleModifiersChanged;
            isSubscribedToRunModifierService = true;
        }
    }

    private void UnsubscribeFromServices()
    {
        if (isSubscribedToUpgradeManager && UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUIClosed -= HandleUpgradeUiClosed;

        if (isSubscribedToRunModifierService && RunModifierService.Instance != null)
            RunModifierService.Instance.OnModifiersChanged -= HandleModifiersChanged;

        isSubscribedToUpgradeManager = false;
        isSubscribedToRunModifierService = false;
    }

    private void PlayMerchantSpeech()
    {
        if (merchantSpeechBubble == null)
        {
            Debug.LogWarning("[MerchantActivationCinematic] Merchant SpeechBubbleComponent is missing.", this);
            return;
        }

        merchantSpeechBubble.Speak(merchantSpeechText, speechDuration);
    }

    private void CacheCameraState()
    {
        CameraBootstrap.EnsureRuntimeRigForCurrentScene();

        gameplayCamera = CameraBootstrap.GetPlayerCamera();
        cameraBrain = CameraBootstrap.GetBrain();
        legacyFollowCamera = CameraBootstrap.GetLegacyFollow();

        if (gameplayCamera != null)
        {
            cachedCameraFollow = gameplayCamera.Follow;
            cachedCameraLookAt = gameplayCamera.LookAt;
            cachedCameraPriority = gameplayCamera.Priority;
            cachedCameraOrthographicSize = GetCameraOrthographicSize();
            hasCachedCameraLens = true;
        }

        if (legacyFollowCamera != null)
            cachedLegacyFollowEnabled = legacyFollowCamera.enabled;

        if (cameraBrain != null)
            cachedBrainIgnoreTimeScale = cameraBrain.IgnoreTimeScale;

        hasCachedCameraState = true;
    }

    private void SetCameraTarget(Transform target)
    {
        if (target == null)
            return;

        if (cameraBrain != null)
            cameraBrain.IgnoreTimeScale = true;

        if (legacyFollowCamera != null)
            legacyFollowCamera.enabled = false;

        if (gameplayCamera == null)
            return;

        gameplayCamera.Follow = target;
        gameplayCamera.LookAt = target;
    }

    private void RestoreCameraState(Transform preferredTarget)
    {
        if (!hasCachedCameraState)
            return;

        Transform restoreFollow = preferredTarget != null ? preferredTarget : cachedCameraFollow;
        Transform restoreLookAt = preferredTarget != null ? preferredTarget : cachedCameraLookAt;

        if (gameplayCamera != null)
        {
            gameplayCamera.Follow = restoreFollow;
            gameplayCamera.LookAt = restoreLookAt;
            gameplayCamera.Priority = cachedCameraPriority;

            if (hasCachedCameraLens)
                SetCameraOrthographicSize(cachedCameraOrthographicSize);
        }

        if (legacyFollowCamera != null)
        {
            if (restoreFollow != null)
                legacyFollowCamera.BindTarget(restoreFollow, snap: false);

            legacyFollowCamera.enabled = cachedLegacyFollowEnabled;
        }

        if (cameraBrain != null)
            cameraBrain.IgnoreTimeScale = cachedBrainIgnoreTimeScale;

        RestoreMerchantFocusCameraState();

        hasCachedCameraState = false;
        hasCachedCameraLens = false;
    }

    private bool TryBeginMerchantFocusCameraBlend()
    {
        if (merchantFocusCamera == null)
            return false;

        if (gameplayCamera != null && merchantFocusCamera == gameplayCamera)
        {
            WarnInvalidFocusCameraOnce();
            return false;
        }

        CacheMerchantFocusCameraState();

        if (cameraBrain != null)
            cameraBrain.IgnoreTimeScale = true;

        if (legacyFollowCamera != null)
            legacyFollowCamera.enabled = false;

        if (!merchantFocusCamera.gameObject.activeSelf)
            merchantFocusCamera.gameObject.SetActive(true);

        merchantFocusCamera.Follow = merchantFocusTarget;
        merchantFocusCamera.LookAt = merchantFocusTarget;
        SetCameraOrthographicSize(merchantFocusCamera, merchantFocusOrthographicSize);

        int gameplayPriority = gameplayCamera != null ? gameplayCamera.Priority : cachedCameraPriority;
        merchantFocusCamera.Priority = Mathf.Max(merchantFocusCameraPriority, gameplayPriority + 1);
        isUsingFocusCamera = true;
        return true;
    }

    private void CacheMerchantFocusCameraState()
    {
        if (merchantFocusCamera == null || hasCachedFocusCameraState)
            return;

        cachedFocusCameraActiveSelf = merchantFocusCamera.gameObject.activeSelf;
        cachedFocusCameraFollow = merchantFocusCamera.Follow;
        cachedFocusCameraLookAt = merchantFocusCamera.LookAt;
        cachedFocusCameraPriority = merchantFocusCamera.Priority;
        cachedFocusCameraOrthographicSize = GetCameraOrthographicSize(
            merchantFocusCamera,
            merchantFocusOrthographicSize);
        hasCachedFocusCameraLens = true;
        hasCachedFocusCameraState = true;
    }

    private void LowerMerchantFocusCameraPriorityForReturn()
    {
        if (merchantFocusCamera == null || !hasCachedFocusCameraState)
            return;

        int gameplayPriority = gameplayCamera != null ? gameplayCamera.Priority : cachedCameraPriority;
        merchantFocusCamera.Priority = gameplayPriority - 1;
    }

    private void RestoreMerchantFocusCameraState()
    {
        if (merchantFocusCamera != null && hasCachedFocusCameraState)
        {
            merchantFocusCamera.Follow = cachedFocusCameraFollow;
            merchantFocusCamera.LookAt = cachedFocusCameraLookAt;
            merchantFocusCamera.Priority = cachedFocusCameraPriority;

            if (hasCachedFocusCameraLens)
                SetCameraOrthographicSize(merchantFocusCamera, cachedFocusCameraOrthographicSize);

            merchantFocusCamera.gameObject.SetActive(cachedFocusCameraActiveSelf);
        }

        cachedFocusCameraFollow = null;
        cachedFocusCameraLookAt = null;
        hasCachedFocusCameraState = false;
        hasCachedFocusCameraLens = false;
        isUsingFocusCamera = false;
    }

    private IEnumerator ZoomCameraWhileWaitingRoutine(float targetOrthographicSize, float zoomDuration, float minimumWaitSeconds)
    {
        float waitDuration = Mathf.Max(0f, minimumWaitSeconds);
        if (!zoomCameraDuringFocus || gameplayCamera == null)
        {
            yield return WaitForPresentationSeconds(waitDuration);
            yield break;
        }

        float startOrthographicSize = GetCameraOrthographicSize();
        float clampedTargetSize = Mathf.Max(0.01f, targetOrthographicSize);
        float clampedZoomDuration = Mathf.Max(0f, zoomDuration);
        float totalDuration = Mathf.Max(waitDuration, clampedZoomDuration);

        if (clampedZoomDuration <= 0f)
            SetCameraOrthographicSize(clampedTargetSize);

        if (totalDuration <= 0f)
        {
            SetCameraOrthographicSize(clampedTargetSize);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (clampedZoomDuration > 0f)
            {
                float t = Mathf.Clamp01(elapsed / clampedZoomDuration);
                float easedT = Mathf.SmoothStep(0f, 1f, t);
                SetCameraOrthographicSize(Mathf.Lerp(startOrthographicSize, clampedTargetSize, easedT));
            }

            yield return null;
        }

        SetCameraOrthographicSize(clampedTargetSize);
    }

    private float GetCameraOrthographicSize()
    {
        return GetCameraOrthographicSize(gameplayCamera, merchantFocusOrthographicSize);
    }

    private static float GetCameraOrthographicSize(CinemachineCamera camera, float fallbackOrthographicSize)
    {
        if (camera == null)
            return Mathf.Max(0.01f, fallbackOrthographicSize);

        var lens = camera.Lens;
        return Mathf.Max(0.01f, lens.OrthographicSize);
    }

    private void SetCameraOrthographicSize(float orthographicSize)
    {
        SetCameraOrthographicSize(gameplayCamera, orthographicSize);
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

    private static void SetCameraOrthographicSize(CinemachineCamera camera, float orthographicSize)
    {
        if (camera == null)
            return;

        var lens = camera.Lens;
        lens.OrthographicSize = Mathf.Max(0.01f, orthographicSize);
        camera.Lens = lens;
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
        UnlockPlayerControls();
        ReleaseInputBlocker();
    }

    private void WarnMissingFocusTargetOnce()
    {
        if (hasLoggedMissingFocusTarget)
            return;

        hasLoggedMissingFocusTarget = true;
        Debug.LogWarning("[MerchantActivationCinematic] Merchant focus target is missing. Shop activation cinematic skipped.", this);
    }

    private void WarnInvalidFocusCameraOnce()
    {
        if (hasLoggedInvalidFocusCamera)
            return;

        hasLoggedInvalidFocusCamera = true;
        Debug.LogWarning(
            "[MerchantActivationCinematic] Merchant focus camera is the gameplay camera. Falling back to direct gameplay camera focus.",
            this);
    }
}

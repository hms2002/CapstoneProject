using System.Collections;
using Cainos.PixelArtTopDown_Basic;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임 : 보스/컷씬 카메라 우선순위, 타겟, 렌즈 확대 연출을 실제 Cinemachine rig에 적용한다.
/// </summary>
public class CameraPresentationDirector : MonoBehaviour, ICameraPresentationDirector, ICameraPresentationSettingsReceiver
{
    [Header("Camera")]
    [SerializeField] private CinemachineCamera playerCam;
    [SerializeField] private CinemachineCamera bossCam;

    [Header("Priority")]
    [SerializeField] private int normalPriority = 10;
    [SerializeField] private int focusPriority = 100;

    [Header("Sequence")]
    [SerializeField] private bool disableLegacyFollowWhileSequence = true;
    [SerializeField] private float blendWaitFallbackSeconds = 2f;

    [Header("Boss Lens")]
    [SerializeField] private bool useBossLensPresentation = true;
    [SerializeField] [Min(0.01f)] private float deathLensScale = 0.82f;
    [SerializeField] [Min(0f)] private float deathLensBlendDuration = 0.35f;
    [SerializeField] [Min(0.01f)] private float phaseLensScale = 1.18f;
    [SerializeField] [Min(0f)] private float phaseLensBlendDuration = 0.35f;

    private CinemachineBrain brain;
    private CameraFollow legacyFollowCamera;
    private bool previousBrainIgnoreTimeScale;
    private bool hasStoredBrainIgnoreTimeScale;
    private bool hasCachedBossLensDefaults;
    private bool cachedBossLensIsOrthographic;
    private float cachedBossLensOrthographicSize;
    private float cachedBossLensFieldOfView;
    private Coroutine activeBossLensRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterFactoryBackend()
    {
        CameraPresentationPlayback.RegisterFactoryBackend(CameraPresentationDirectorFactoryBackend.Instance);
    }

    private void Awake()
    {
        ResolveRuntimeReferences();
        RestoreDefaultState();
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ResolveRuntimeReferences();
        BindPlayerCameraToCurrentPlayer();
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered -= HandlePlayerUnregistered;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        StopBossLensAnimation();
        RestoreBrainIgnoreTimeScale();
        SetLegacyFollowEnabled(true);
    }

    public void BindPlayerCameraToCurrentPlayer()
    {
        BindPlayerCamera(PlayerRuntimeRegistry.GetPlayerTransform());
    }

    public void ApplyPresentationSettings(
        Component playerCamera,
        Component bossCamera,
        int defaultPriority,
        int highlightedPriority,
        bool disableLegacyFollow,
        float blendFallbackSeconds)
    {
        ApplyPresentationSettings(
            playerCamera as CinemachineCamera,
            bossCamera as CinemachineCamera,
            defaultPriority,
            highlightedPriority,
            disableLegacyFollow,
            blendFallbackSeconds);
    }

    public void ApplyPresentationSettings(
        CinemachineCamera playerCamera,
        CinemachineCamera bossCamera,
        int defaultPriority,
        int highlightedPriority,
        bool disableLegacyFollow,
        float blendFallbackSeconds)
    {
        playerCam = ResolvePlayerCameraReference(playerCamera);
        bossCam = ResolveBossCameraReference(bossCamera);
        normalPriority = defaultPriority;
        focusPriority = highlightedPriority;
        disableLegacyFollowWhileSequence = disableLegacyFollow;
        blendWaitFallbackSeconds = blendFallbackSeconds;

        ResolveRuntimeReferences();
        RestoreDefaultState();
    }

    public IEnumerator FocusBossRoutine()
    {
        BeginBossFocus();
        yield return WaitForBlendEnd(ResolveBossFocusTarget());
    }

    public IEnumerator FocusBossWithDeathLensRoutine()
    {
        BeginBossFocusWithDeathLens();
        yield return WaitForBlendEnd(ResolveBossFocusTarget());
    }

    public IEnumerator FocusTargetWithDeathLensRoutine(Transform target)
    {
        BindBossCameraTarget(target);
        yield return FocusBossWithDeathLensRoutine();
    }

    public IEnumerator FocusBossWithPhaseLensRoutine()
    {
        BeginBossFocusWithPhaseLens();
        yield return WaitForBlendEnd(ResolveBossFocusTarget());
    }

    public void BindBossCameraTarget(Transform target)
    {
        ResolveRuntimeReferences();

        if (bossCam == null || target == null)
            return;

        bossCam.Follow = target;
        bossCam.LookAt = target;
    }

    public IEnumerator ReturnToPlayerRoutine()
    {
        StopBossLensAnimation();
        BindPlayerCameraToCurrentPlayer();

        if (bossCam != null)
            bossCam.Priority = GetNormalPriority();

        if (playerCam != null)
            playerCam.Priority = GetPlayerFocusPriority();

        yield return WaitForBlendEnd(PlayerRuntimeRegistry.GetPlayerTransform());

        RestoreBossLensImmediate();
        RestoreBrainIgnoreTimeScale();
        SetLegacyFollowEnabled(true);

        if (legacyFollowCamera != null)
            legacyFollowCamera.SnapToTarget();
    }

    public void RestoreDefaultState()
    {
        StopBossLensAnimation();
        BindPlayerCameraToCurrentPlayer();

        if (bossCam != null)
            bossCam.Priority = GetNormalPriority();

        if (playerCam != null)
            playerCam.Priority = GetPlayerFocusPriority();

        RestoreBossLensImmediate();
        RestoreBrainIgnoreTimeScale();
        SetLegacyFollowEnabled(true);
    }

    public void BeginBossFocusWithDeathLens()
    {
        BeginBossFocusWithLens(deathLensScale, deathLensBlendDuration);
    }

    public void BeginBossFocusWithPhaseLens()
    {
        BeginBossFocusWithLens(phaseLensScale, phaseLensBlendDuration);
    }

    public IEnumerator PlayBossPhasePresentationRoutine(float holdSeconds)
    {
        if (bossCam == null)
            yield break;

        yield return FocusBossWithPhaseLensRoutine();

        float duration = Mathf.Max(0f, holdSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return ReturnToPlayerRoutine();
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        if (player == null)
            return;

        BindPlayerCamera(player.transform);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveRuntimeReferences();
        RestoreDefaultState();
    }

    private void HandlePlayerUnregistered(PlayerInteractor2D player)
    {
        if (playerCam == null || player == null)
            return;

        if (playerCam.Follow == player.transform)
            playerCam.Follow = null;

        if (playerCam.LookAt == player.transform)
            playerCam.LookAt = null;
    }

    private void BindPlayerCamera(Transform playerTransform)
    {
        if (playerCam == null || playerTransform == null)
            return;

        EnsureImpulseListener(playerCam);
        playerCam.Follow = playerTransform;
        playerCam.LookAt = playerTransform;
    }

    private void ResolveRuntimeReferences()
    {
        CameraBootstrap.EnsureRuntimeRigForCurrentScene();

        playerCam = ResolvePlayerCameraReference(playerCam);
        bossCam = ResolveBossCameraReference(bossCam);
        brain = CameraBootstrap.GetBrain();
        legacyFollowCamera = CameraBootstrap.GetLegacyFollow();
        CacheBossLensDefaults();

        EnsureImpulseListener(playerCam);
        EnsureImpulseListener(bossCam);

        if (legacyFollowCamera != null && playerCam != null)
            legacyFollowCamera.SetControlledCamera(playerCam, rebindCurrentTarget: true);
    }

    private void ValidateBossCameraAvailability()
    {
        if (!CameraBootstrap.IsBossScene(gameObject.scene))
            return;

        if (bossCam == null)
            Debug.LogError("[CameraPresentationDirector] Boss scene is missing BossCam. Boss presentation integrity is not guaranteed.", this);
    }

    private CinemachineCamera ResolvePlayerCameraReference(CinemachineCamera fallbackCamera)
    {
        CinemachineCamera bootstrapPlayerCamera = CameraBootstrap.GetPlayerCamera();
        return bootstrapPlayerCamera != null ? bootstrapPlayerCamera : fallbackCamera;
    }

    private CinemachineCamera ResolveBossCameraReference(CinemachineCamera fallbackCamera)
    {
        Scene ownerScene = gameObject.scene;
        if (!CameraBootstrap.IsBossScene(ownerScene))
            return null;

        if (fallbackCamera != null && fallbackCamera.gameObject.scene == ownerScene)
            return fallbackCamera;

        return CameraBootstrap.FindSceneBossCamera(ownerScene);
    }

    private static void EnsureImpulseListener(CinemachineCamera camera)
    {
        if (camera == null)
            return;

        CameraBootstrap.EnsureImpulseListener(camera.gameObject);
    }

    private IEnumerator WaitForBlendEnd(Transform settleTarget)
    {
        yield return null;

        if (brain == null)
            yield break;

        float fallbackDuration = Mathf.Max(0f, GetBlendWaitFallbackSeconds());
        float elapsed = 0f;
        bool sawBlend = brain.IsBlending;

        while (elapsed < fallbackDuration)
        {
            if (brain == null)
                yield break;

            if (brain.IsBlending)
            {
                sawBlend = true;
            }
            else if (sawBlend)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return CameraCinematicWaitUtility.WaitForCameraSettle(brain, null, settleTarget);
    }

    private float GetBlendWaitFallbackSeconds()
    {
        if (brain != null)
        {
            float defaultBlendTime = brain.DefaultBlend.Time;
            if (defaultBlendTime > 0f)
                return defaultBlendTime + 0.1f;
        }

        return blendWaitFallbackSeconds;
    }

    private void EnableUnscaledCameraBlend()
    {
        if (brain == null)
            return;

        if (!hasStoredBrainIgnoreTimeScale)
        {
            previousBrainIgnoreTimeScale = brain.IgnoreTimeScale;
            hasStoredBrainIgnoreTimeScale = true;
        }

        brain.IgnoreTimeScale = true;
    }

    private void RestoreBrainIgnoreTimeScale()
    {
        if (brain == null || !hasStoredBrainIgnoreTimeScale)
            return;

        brain.IgnoreTimeScale = previousBrainIgnoreTimeScale;
        hasStoredBrainIgnoreTimeScale = false;
    }

    private void SetLegacyFollowEnabled(bool enabled)
    {
        if (!disableLegacyFollowWhileSequence)
            return;

        if (legacyFollowCamera != null)
            legacyFollowCamera.enabled = enabled;
    }

    private void BeginBossFocus()
    {
        EnableUnscaledCameraBlend();
        ValidateBossCameraAvailability();
        BindPlayerCameraToCurrentPlayer();
        StopBossLensAnimation();
        RestoreBossLensImmediate();
        SetLegacyFollowEnabled(false);

        if (playerCam != null)
            playerCam.Priority = GetNormalPriority();

        if (bossCam != null)
            bossCam.Priority = GetBossFocusPriority();
    }

    private Transform ResolveBossFocusTarget()
    {
        if (bossCam == null)
            return null;

        return bossCam.Follow != null ? bossCam.Follow : bossCam.LookAt;
    }

    private void BeginBossFocusWithLens(float lensScale, float lensDuration)
    {
        BeginBossFocus();

        if (bossCam != null && useBossLensPresentation)
            activeBossLensRoutine = StartCoroutine(AnimateBossLensScaleRoutine(lensScale, lensDuration));
    }

    private void CacheBossLensDefaults()
    {
        if (bossCam == null)
        {
            hasCachedBossLensDefaults = false;
            return;
        }

        cachedBossLensIsOrthographic = bossCam.Lens.Orthographic;
        cachedBossLensOrthographicSize = bossCam.Lens.OrthographicSize;
        cachedBossLensFieldOfView = bossCam.Lens.FieldOfView;
        hasCachedBossLensDefaults = true;
    }

    private IEnumerator AnimateBossLensScaleRoutine(float scale, float duration)
    {
        if (!useBossLensPresentation || bossCam == null)
            yield break;

        if (!hasCachedBossLensDefaults)
            CacheBossLensDefaults();

        if (!hasCachedBossLensDefaults)
            yield break;

        float resolvedScale = Mathf.Max(0.01f, scale);

        if (bossCam.Lens.Orthographic)
        {
            float targetSize = cachedBossLensOrthographicSize * resolvedScale;
            yield return AnimateBossOrthographicSizeRoutine(targetSize, duration);
            activeBossLensRoutine = null;
            yield break;
        }

        float targetFieldOfView = cachedBossLensFieldOfView * resolvedScale;
        yield return AnimateBossFieldOfViewRoutine(targetFieldOfView, duration);
        activeBossLensRoutine = null;
    }

    private IEnumerator AnimateBossOrthographicSizeRoutine(float targetSize, float duration)
    {
        float startSize = bossCam.Lens.OrthographicSize;
        float elapsed = 0f;
        float resolvedDuration = Mathf.Max(0f, duration);

        if (resolvedDuration <= 0f)
        {
            bossCam.Lens.OrthographicSize = targetSize;
            yield break;
        }

        while (elapsed < resolvedDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / resolvedDuration);
            bossCam.Lens.OrthographicSize = Mathf.Lerp(startSize, targetSize, t);
            yield return null;
        }

        bossCam.Lens.OrthographicSize = targetSize;
    }

    private IEnumerator AnimateBossFieldOfViewRoutine(float targetFieldOfView, float duration)
    {
        float startFieldOfView = bossCam.Lens.FieldOfView;
        float elapsed = 0f;
        float resolvedDuration = Mathf.Max(0f, duration);

        if (resolvedDuration <= 0f)
        {
            bossCam.Lens.FieldOfView = targetFieldOfView;
            yield break;
        }

        while (elapsed < resolvedDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / resolvedDuration);
            bossCam.Lens.FieldOfView = Mathf.Lerp(startFieldOfView, targetFieldOfView, t);
            yield return null;
        }

        bossCam.Lens.FieldOfView = targetFieldOfView;
    }

    private void RestoreBossLensImmediate()
    {
        if (!useBossLensPresentation || bossCam == null)
            return;

        if (!hasCachedBossLensDefaults)
            CacheBossLensDefaults();

        if (!hasCachedBossLensDefaults)
            return;

        if (cachedBossLensIsOrthographic)
            bossCam.Lens.OrthographicSize = cachedBossLensOrthographicSize;
        else
            bossCam.Lens.FieldOfView = cachedBossLensFieldOfView;
    }

    private void StopBossLensAnimation()
    {
        if (activeBossLensRoutine == null)
            return;

        StopCoroutine(activeBossLensRoutine);
        activeBossLensRoutine = null;
    }

    private int GetNormalPriority()
    {
        return normalPriority;
    }

    private int GetPlayerFocusPriority()
    {
        if (playerCam == null)
            return focusPriority;

        return Mathf.Max(focusPriority, playerCam.Priority, normalPriority + 1);
    }

    private int GetBossFocusPriority()
    {
        int playerPriority = GetPlayerFocusPriority();
        return Mathf.Max(focusPriority, playerPriority + 1);
    }

    /// <summary>
    /// 책임 : Core의 카메라 연출 생성 요청을 실제 CameraPresentationDirector 컴포넌트 생성으로 변환한다.
    /// </summary>
    private sealed class CameraPresentationDirectorFactoryBackend : ICameraPresentationFactoryBackend
    {
        public static readonly CameraPresentationDirectorFactoryBackend Instance = new();

        public ICameraPresentationDirector GetOrAdd(GameObject host)
        {
            if (host == null)
                return null;

            CameraPresentationDirector director = host.GetComponent<CameraPresentationDirector>();
            if (director == null)
                director = host.AddComponent<CameraPresentationDirector>();

            return director;
        }
    }
}

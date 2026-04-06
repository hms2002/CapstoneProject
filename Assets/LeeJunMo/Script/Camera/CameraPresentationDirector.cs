using System.Collections;
using Cainos.PixelArtTopDown_Basic;
using UnityEngine;
using Unity.Cinemachine;

public class CameraPresentationDirector : MonoBehaviour
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

    private CinemachineBrain brain;
    private CameraFollow legacyFollowCamera;
    private bool previousBrainIgnoreTimeScale;
    private bool hasStoredBrainIgnoreTimeScale;

    private void Awake()
    {
        if (Camera.main != null)
        {
            brain = Camera.main.GetComponent<CinemachineBrain>();
            legacyFollowCamera = Camera.main.GetComponent<CameraFollow>();
        }

        EnsureImpulseListener(playerCam);
        EnsureImpulseListener(bossCam);
        RestoreDefaultState();
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;
        BindPlayerCameraToCurrentPlayer();
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered -= HandlePlayerUnregistered;
        RestoreBrainIgnoreTimeScale();
        SetLegacyFollowEnabled(true);
    }

    public void BindPlayerCameraToCurrentPlayer()
    {
        BindPlayerCamera(PlayerRuntimeRegistry.GetPlayerTransform());
    }

    public void ApplyPresentationSettings(
        CinemachineCamera playerCamera,
        CinemachineCamera bossCamera,
        int defaultPriority,
        int highlightedPriority,
        bool disableLegacyFollow,
        float blendFallbackSeconds)
    {
        playerCam = playerCamera;
        bossCam = bossCamera;
        normalPriority = defaultPriority;
        focusPriority = highlightedPriority;
        disableLegacyFollowWhileSequence = disableLegacyFollow;
        blendWaitFallbackSeconds = blendFallbackSeconds;

        EnsureImpulseListener(playerCam);
        EnsureImpulseListener(bossCam);
        RestoreDefaultState();
    }

    public IEnumerator FocusBossRoutine()
    {
        EnableUnscaledCameraBlend();
        BindPlayerCameraToCurrentPlayer();
        SetLegacyFollowEnabled(false);

        if (playerCam != null)
            playerCam.Priority = normalPriority;

        if (bossCam != null)
            bossCam.Priority = focusPriority;

        yield return WaitForBlendEnd();
    }

    public IEnumerator ReturnToPlayerRoutine()
    {
        BindPlayerCameraToCurrentPlayer();

        if (bossCam != null)
            bossCam.Priority = normalPriority;

        if (playerCam != null)
            playerCam.Priority = focusPriority;

        yield return WaitForBlendEnd();

        RestoreBrainIgnoreTimeScale();
        SetLegacyFollowEnabled(true);

        if (legacyFollowCamera != null)
            legacyFollowCamera.SnapToTarget();
    }

    public void RestoreDefaultState()
    {
        BindPlayerCameraToCurrentPlayer();

        if (bossCam != null)
            bossCam.Priority = normalPriority;

        if (playerCam != null)
            playerCam.Priority = focusPriority;

        RestoreBrainIgnoreTimeScale();
        SetLegacyFollowEnabled(true);
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        if (player == null)
            return;

        BindPlayerCamera(player.transform);
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

    private static void EnsureImpulseListener(CinemachineCamera camera)
    {
        if (camera == null)
            return;

        var listener = camera.GetComponent<CinemachineImpulseListener>();
        if (listener != null)
            return;

        listener = camera.gameObject.AddComponent<CinemachineImpulseListener>();
        listener.ChannelMask = 1;
        listener.Gain = 1f;
        listener.Use2DDistance = true;
    }

    private IEnumerator WaitForBlendEnd()
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
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
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
}

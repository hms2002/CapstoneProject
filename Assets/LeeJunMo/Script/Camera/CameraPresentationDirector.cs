using System.Collections;
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

    private CinemachineBrain brain;

    private void Awake()
    {
        if (Camera.main != null)
            brain = Camera.main.GetComponent<CinemachineBrain>();

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
    }

    public void BindPlayerCameraToCurrentPlayer()
    {
        BindPlayerCamera(PlayerRuntimeRegistry.GetPlayerTransform());
    }

    public IEnumerator FocusBossRoutine()
    {
        BindPlayerCameraToCurrentPlayer();

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
    }

    public void RestoreDefaultState()
    {
        BindPlayerCameraToCurrentPlayer();

        if (bossCam != null)
            bossCam.Priority = normalPriority;

        if (playerCam != null)
            playerCam.Priority = focusPriority;
    }

    private void HandlePlayerRegistered(SampleTopDownPlayer player)
    {
        if (player == null)
            return;

        BindPlayerCamera(player.transform);
    }

    private void HandlePlayerUnregistered(SampleTopDownPlayer player)
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

        if (brain != null)
            yield return new WaitUntil(() => !brain.IsBlending);
    }
}

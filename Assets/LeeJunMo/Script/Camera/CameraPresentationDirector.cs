using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using Cainos.PixelArtTopDown_Basic;

public class CameraPresentationDirector : MonoBehaviour
{
    [Header("카메라")]
    [SerializeField] private CinemachineCamera playerCam;
    [SerializeField] private CinemachineCamera bossCam;

    [Header("우선순위")]
    [SerializeField] private int normalPriority = 10;
    [SerializeField] private int focusPriority = 100;

    [Header("옵션")]
    [SerializeField] private bool disableLegacyFollowWhilePresenting = true;

    private CinemachineBrain brain;
    private CameraFollow legacyFollowCamera;

    private void Awake()
    {
        if (Camera.main != null)
        {
            brain = Camera.main.GetComponent<CinemachineBrain>();
            legacyFollowCamera = Camera.main.GetComponent<CameraFollow>();
        }

        RestoreDefaultState();
    }

    public void BindPlayerCameraToCurrentPlayer()
    {
        if (playerCam == null)
            return;

        var playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null)
            return;

        playerCam.Follow = playerTransform;
        playerCam.LookAt = playerTransform;
    }

    public IEnumerator FocusBossRoutine()
    {
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

        SetLegacyFollowEnabled(true);

        if (legacyFollowCamera != null)
            legacyFollowCamera.SnapToTarget();
    }

    public void RestoreDefaultState()
    {
        if (bossCam != null)
            bossCam.Priority = normalPriority;

        if (playerCam != null)
            playerCam.Priority = focusPriority;

        SetLegacyFollowEnabled(true);
    }

    private void SetLegacyFollowEnabled(bool enabled)
    {
        if (!disableLegacyFollowWhilePresenting)
            return;

        if (legacyFollowCamera != null)
            legacyFollowCamera.enabled = enabled;
    }

    private IEnumerator WaitForBlendEnd()
    {
        yield return null;

        if (brain != null)
            yield return new WaitUntil(() => !brain.IsBlending);
    }
}
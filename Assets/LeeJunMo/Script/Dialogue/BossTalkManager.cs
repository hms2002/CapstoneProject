using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using Cainos.PixelArtTopDown_Basic;

public class BossTalkManager : MonoBehaviour
{
    [Header("데이터 설정")]
    [SerializeField] private TextAsset inkJSON;
    [SerializeField] private NPCData npcData;

    [Header("카메라 설정")]
    [SerializeField] private CinemachineCamera playerCam;
    [SerializeField] private CinemachineCamera bossCam;
    [SerializeField] private int normalPriority = 10;
    [SerializeField] private int focusPriority = 100;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool disableLegacyFollowWhileSequence = true;

    [Header("후처리")]
    [SerializeField] private BossDrop bossDrop;
     
    private CinemachineBrain brain;
    private CameraFollow legacyFollowCamera;
    private Coroutine runningSequence;
    private PlayerInteractor2D cachedPlayer;
    private InteractState previousPlayerState = InteractState.Idle;

    private void Awake()
    {
        if (Camera.main != null)
        {
            brain = Camera.main.GetComponent<CinemachineBrain>();
            legacyFollowCamera = Camera.main.GetComponent<CameraFollow>();
        }

        ApplyDefaultCameraState();
    }

    private void Start()
    {
        if (playOnStart)
            BeginEncounterSequence();
    }

    private void OnDisable()
    {
        if (runningSequence != null)
        {
            StopCoroutine(runningSequence);
            runningSequence = null;
        }

        RestorePlayerState();
        RestoreDefaultCameraState();
        SetLegacyFollowEnabled(true);

        if (legacyFollowCamera != null)
            legacyFollowCamera.SnapToTarget();
    }

    public void BeginEncounterSequence()
    {
        if (runningSequence != null)
            return;

        runningSequence = StartCoroutine(EncounterSequence());
    }

    private IEnumerator EncounterSequence()
    {
        if (!ValidateSetup())
        {
            runningSequence = null;
            yield break;
        }

        cachedPlayer = ResolvePlayer();

        if (cachedPlayer != null)
        {
            previousPlayerState = cachedPlayer.CurrentState;
            cachedPlayer.SetInteractState(InteractState.Talking);
        }

        BindPlayerCameraToCurrentPlayer();

        yield return FocusBossCameraRoutine();
        yield return PlayDialogueRoutine();
        yield return ReturnToPlayerCameraRoutine();

        RestorePlayerState();

        if (bossDrop != null)
            bossDrop.OnBossDead();

        runningSequence = null;
    }

    private bool ValidateSetup()
    {
        if (brain == null)
        {
            Debug.LogError("[BossTalkManager] Main Camera에 CinemachineBrain이 없다.");
            return false;
        }

        if (playerCam == null)
        {
            Debug.LogError("[BossTalkManager] playerCam이 비어 있다.");
            return false;
        }

        if (bossCam == null)
        {
            Debug.LogError("[BossTalkManager] bossCam이 비어 있다.");
            return false;
        }

        if (inkJSON == null)
        {
            Debug.LogError("[BossTalkManager] inkJSON이 비어 있다.");
            return false;
        }

        if (npcData == null)
        {
            Debug.LogError("[BossTalkManager] npcData가 비어 있다.");
            return false;
        }

        if (DialogueService.Instance == null)
        {
            Debug.LogError("[BossTalkManager] DialogueController 인스턴스를 찾을 수 없다.");
            return false;
        }

        return true;
    }

    private PlayerInteractor2D ResolvePlayer()
    {
        var playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null)
            return null;

        return playerTransform.GetComponent<PlayerInteractor2D>();
    }

    private void BindPlayerCameraToCurrentPlayer()
    {
        var playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null || playerCam == null)
            return;

        // 네 Cinemachine 버전에서 Follow / LookAt 이름이 다르면 이 부분만 맞춰주면 된다.
        playerCam.Follow = playerTransform;
        playerCam.LookAt = playerTransform;
    }

    private IEnumerator FocusBossCameraRoutine()
    {
        SetLegacyFollowEnabled(false);

        if (playerCam != null)
            playerCam.Priority = normalPriority;

        if (bossCam != null)
            bossCam.Priority = focusPriority;

        yield return WaitForBlendEnd();
    }

    private IEnumerator ReturnToPlayerCameraRoutine()
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

    private IEnumerator PlayDialogueRoutine()
    {
        List<NPCData> participants = new List<NPCData> { npcData };
        if (!DialogueService.Instance.TryStartDialogue(inkJSON, participants))
            yield break;

        yield return new WaitUntil(() =>
            DialogueService.Instance == null || !DialogueService.Instance.IsPlaying);
    }

    private IEnumerator WaitForBlendEnd()
    {
        yield return null;

        if (brain != null)
            yield return new WaitUntil(() => !brain.IsBlending);
    }

    private void RestorePlayerState()
    {
        if (cachedPlayer == null)
            return;

        cachedPlayer.SetInteractState(previousPlayerState);
        cachedPlayer = null;
    }

    private void SetLegacyFollowEnabled(bool enabled)
    {
        if (!disableLegacyFollowWhileSequence)
            return;

        if (legacyFollowCamera != null)
            legacyFollowCamera.enabled = enabled;
    }

    private void ApplyDefaultCameraState()
    {
        if (bossCam != null)
            bossCam.Priority = normalPriority;

        if (playerCam != null)
            playerCam.Priority = focusPriority;
    }

    private void RestoreDefaultCameraState()
    {
        ApplyDefaultCameraState();
    }
}

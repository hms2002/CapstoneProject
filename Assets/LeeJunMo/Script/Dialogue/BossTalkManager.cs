using System.Collections;
using System.Collections.Generic;
using Cainos.PixelArtTopDown_Basic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;

public class BossTalkManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private NPCData npcData;
    [FormerlySerializedAs("inkJSON")]
    [SerializeField, HideInInspector] private TextAsset legacyInkJSON;

    [Header("Camera Settings")]
    [SerializeField] private CinemachineCamera playerCam;
    [SerializeField] private CinemachineCamera bossCam;
    [SerializeField] private int normalPriority = 10;
    [SerializeField] private int focusPriority = 100;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool disableLegacyFollowWhileSequence = true;
    [SerializeField] private float blendWaitFallbackSeconds = 2f;

    [Header("Post Sequence")]
    [SerializeField] private BossDrop bossDrop;

    private CinemachineBrain brain;
    private CameraFollow legacyFollowCamera;
    private Coroutine runningSequence;
    private PlayerInteractor2D cachedPlayer;
    private InteractState previousPlayerState = InteractState.Idle;
    private bool previousBrainIgnoreTimeScale;
    private bool hasStoredBrainIgnoreTimeScale;

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
        RestoreBrainIgnoreTimeScale();
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
        EnableUnscaledCameraBlend();

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
        RestoreBrainIgnoreTimeScale();

        if (bossDrop != null)
            bossDrop.OnBossDead();

        runningSequence = null;
    }

    private bool ValidateSetup()
    {
        if (brain == null)
        {
            Debug.LogError("[BossTalkManager] Main Camera is missing CinemachineBrain.", this);
            return false;
        }

        if (playerCam == null)
        {
            Debug.LogError("[BossTalkManager] playerCam is missing.", this);
            return false;
        }

        if (bossCam == null)
        {
            Debug.LogError("[BossTalkManager] bossCam is missing.", this);
            return false;
        }

        if (npcData == null)
        {
            Debug.LogError("[BossTalkManager] npcData is missing.", this);
            return false;
        }

        if (ResolveDialogueInk() == null)
        {
            Debug.LogError("[BossTalkManager] No dialogue ink is assigned on NPCData.", this);
            return false;
        }

        if (DialogueService.Instance == null)
        {
            Debug.LogError("[BossTalkManager] DialogueService instance was not found.", this);
            return false;
        }

        return true;
    }

    private PlayerInteractor2D ResolvePlayer()
    {
        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null)
            return null;

        return playerTransform.GetComponent<PlayerInteractor2D>();
    }

    private void BindPlayerCameraToCurrentPlayer()
    {
        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null || playerCam == null)
            return;

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
        TextAsset dialogueInk = ResolveDialogueInk();
        if (dialogueInk == null)
            yield break;

        List<NPCData> participants = new List<NPCData> { npcData };
        if (!DialogueService.Instance.TryStartDialogue(dialogueInk, participants))
            yield break;

        yield return new WaitUntil(() =>
            DialogueService.Instance == null || !DialogueService.Instance.IsPlaying);
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

    private TextAsset ResolveDialogueInk()
    {
        if (npcData != null)
        {
            if (npcData.PrimaryInk != null)
                return npcData.PrimaryInk;

            if (legacyInkJSON != null)
            {
                npcData.AssignPrimaryInkIfEmpty(legacyInkJSON);
                if (npcData.PrimaryInk != null)
                    return npcData.PrimaryInk;
            }
        }

        return legacyInkJSON;
    }
}

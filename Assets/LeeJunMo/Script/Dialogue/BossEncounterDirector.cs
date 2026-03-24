using System.Collections;
using UnityEngine;

public class BossEncounterDirector : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private CameraPresentationDirector cameraDirector;
    [SerializeField] private BossDialogueRunner dialogueRunner;
    [SerializeField] private BossDrop bossDrop;

    [Header("실행")]
    [SerializeField] private bool autoPlayWhenPlayerSpawned = true;
    [SerializeField] private bool playOnlyOnce = true;

    private Coroutine runningSequence;
    private SampleTopDownPlayer cachedPlayer;
    private InteractState previousPlayerState = InteractState.Idle;
    private bool hasPlayed;

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;

        // 이미 플레이어가 등록된 뒤에 이 오브젝트가 켜졌을 수도 있으니 즉시 체크
        TryBeginIfPlayerAlreadyExists();
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;

        if (runningSequence != null)
        {
            StopCoroutine(runningSequence);
            runningSequence = null;
        }

        RestorePlayerState();

        if (cameraDirector != null)
            cameraDirector.RestoreDefaultState();
    }

    private void TryBeginIfPlayerAlreadyExists()
    {
        if (!autoPlayWhenPlayerSpawned)
            return;

        var playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null)
            return;

        var player = playerTransform.GetComponent<SampleTopDownPlayer>();
        if (player != null)
            HandlePlayerRegistered(player);
    }

    private void HandlePlayerRegistered(SampleTopDownPlayer player)
    {
        if (!autoPlayWhenPlayerSpawned)
            return;

        if (player == null)
            return;

        if (playOnlyOnce && hasPlayed)
            return;

        BeginSequence();
    }

    public void BeginSequence()
    {
        if (runningSequence != null)
            return;

        if (playOnlyOnce && hasPlayed)
            return;

        runningSequence = StartCoroutine(SequenceRoutine());
    }

    private IEnumerator SequenceRoutine()
    {
        if (cameraDirector == null)
        {
            Debug.LogError("[BossEncounterDirector] cameraDirector가 비어 있다.");
            runningSequence = null;
            yield break;
        }

        if (dialogueRunner == null)
        {
            Debug.LogError("[BossEncounterDirector] dialogueRunner가 비어 있다.");
            runningSequence = null;
            yield break;
        }

        // 플레이어 스폰/등록, 카메라 바인딩, 시네머신 초기화 한 프레임 여유
        yield return null;
        yield return new WaitUntil(() => PlayerRuntimeRegistry.GetPlayerTransform() != null);

        CacheAndLockPlayer();

        yield return cameraDirector.FocusBossRoutine();
        yield return dialogueRunner.PlayDialogueRoutine();
        yield return cameraDirector.ReturnToPlayerRoutine();

        RestorePlayerState();

        if (bossDrop != null)
            bossDrop.OnBossDead();

        hasPlayed = true;
        runningSequence = null;
    }

    private void CacheAndLockPlayer()
    {
        var playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        cachedPlayer = playerTransform != null ? playerTransform.GetComponent<SampleTopDownPlayer>() : null;

        if (cachedPlayer == null)
            return;

        previousPlayerState = cachedPlayer.CurrentState;
        cachedPlayer.SetInteractState(InteractState.Talking);
    }

    private void RestorePlayerState()
    {
        if (cachedPlayer == null)
            return;

        cachedPlayer.SetInteractState(previousPlayerState);
        cachedPlayer = null;
    }
}
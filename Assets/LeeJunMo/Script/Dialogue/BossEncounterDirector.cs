using System.Collections;
using UnityEngine;

public class BossEncounterDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraPresentationDirector cameraDirector;
    [SerializeField] private BossDialogueRunner dialogueRunner;
    [SerializeField] private BossControllerBase bossController;

    [Header("Playback")]
    [SerializeField] private bool autoPlayWhenPlayerSpawned = true;
    [SerializeField] private bool playOnlyOnce = true;
    [SerializeField] private bool startBossCombatAfterDialogue = true;

    private Coroutine runningSequence;
    private PlayerInteractor2D cachedPlayer;
    private InteractState previousPlayerState = InteractState.Idle;
    private bool hasPlayed;
    private bool holdsTransitionPlayerLock;
    private bool holdsRunTimerPause;

    public bool IsSequenceRunning => runningSequence != null;
    public bool HasPlayed => hasPlayed;

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PrepareBossForEncounter();
        TryBeginIfPlayerAlreadyExists();
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        ReleaseTransitionPlayerLock();
        ReleaseRunTimerPause();

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

        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null)
            return;

        PlayerInteractor2D player = playerTransform.GetComponent<PlayerInteractor2D>();
        if (player != null)
            HandlePlayerRegistered(player);
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        if (!autoPlayWhenPlayerSpawned)
            return;

        if (player == null)
            return;

        if (playOnlyOnce && hasPlayed)
            return;

        AcquireTransitionPlayerLock();
        AcquireRunTimerPause();
        TryCacheAndLockPlayer(player);
        BeginSequence();
    }

    public void BeginSequence()
    {
        if (runningSequence != null)
            return;

        if (playOnlyOnce && hasPlayed)
            return;

        AcquireTransitionPlayerLock();
        AcquireRunTimerPause();
        runningSequence = StartCoroutine(SequenceRoutine());
    }

    private IEnumerator SequenceRoutine()
    {
        if (cameraDirector == null)
        {
            Debug.LogError("[BossEncounterDirector] cameraDirector is missing.", this);
            ReleaseTransitionPlayerLock();
            ReleaseRunTimerPause();
            runningSequence = null;
            yield break;
        }

        if (dialogueRunner == null)
        {
            Debug.LogError("[BossEncounterDirector] dialogueRunner is missing.", this);
            ReleaseTransitionPlayerLock();
            ReleaseRunTimerPause();
            runningSequence = null;
            yield break;
        }

        yield return new WaitUntil(() => PlayerRuntimeRegistry.GetPlayerTransform() != null);
        TryCacheAndLockPlayer();
        yield return new WaitUntil(IsSceneTransitionReady);
        ReleaseTransitionPlayerLock();

        yield return cameraDirector.FocusBossRoutine();
        yield return dialogueRunner.PlayDialogueRoutine();
        yield return cameraDirector.ReturnToPlayerRoutine();

        RestorePlayerState();
        StartBossCombat();
        ReleaseRunTimerPause();

        hasPlayed = true;
        runningSequence = null;
    }

    private void TryCacheAndLockPlayer(PlayerInteractor2D player = null)
    {
        if (cachedPlayer != null)
            return;

        if (player == null)
        {
            Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
            player = playerTransform != null ? playerTransform.GetComponent<PlayerInteractor2D>() : null;
        }

        cachedPlayer = player;

        if (cachedPlayer == null)
            return;

        previousPlayerState = NormalizeRestoredPlayerState(cachedPlayer.CurrentState);
        cachedPlayer.SetInteractState(InteractState.Talking);
    }

    private void RestorePlayerState()
    {
        if (cachedPlayer == null)
            return;

        cachedPlayer.SetInteractState(previousPlayerState);
        cachedPlayer = null;
    }

    private static bool IsSceneTransitionReady()
    {
        SceneFadeTransitionService transitionService = SceneFadeTransitionService.EnsureInstance();
        return transitionService == null || !transitionService.IsTransitionActive;
    }

    private void AcquireTransitionPlayerLock()
    {
        if (holdsTransitionPlayerLock)
            return;

        SceneFadeTransitionService transitionService = SceneFadeTransitionService.EnsureInstance();
        if (transitionService == null)
            return;

        transitionService.SetPlayerUnlockBlocked(this, true);
        holdsTransitionPlayerLock = true;
    }

    private void ReleaseTransitionPlayerLock()
    {
        if (!holdsTransitionPlayerLock)
            return;

        SceneFadeTransitionService transitionService = SceneFadeTransitionService.Instance;
        if (transitionService != null)
            transitionService.SetPlayerUnlockBlocked(this, false);

        holdsTransitionPlayerLock = false;
    }

    /// <summary>
    /// 책임 :
    /// - 보스 등장 연출 시퀀스 전체 동안 런 제한 시간 감소를 멈춘다.
    /// - 대화 본문뿐 아니라 카메라 연출/대기 구간까지 포함해 시간 손실이 생기지 않도록 보호한다.
    /// </summary>
    private void AcquireRunTimerPause()
    {
        if (holdsRunTimerPause || RunTimeLimitSystem.Instance == null)
            return;

        RunTimeLimitSystem.Instance.SetExternalPause(this, true);
        holdsRunTimerPause = true;
    }

    /// <summary>
    /// 책임 :
    /// - 보스 등장 연출 시퀀스가 끝나거나 중단될 때 런 제한 시간 pause를 해제한다.
    /// - 중도 종료/비활성화가 발생해도 타이머가 영구 정지되지 않도록 정리 책임을 진다.
    /// </summary>
    private void ReleaseRunTimerPause()
    {
        if (!holdsRunTimerPause || RunTimeLimitSystem.Instance == null)
            return;

        RunTimeLimitSystem.Instance.SetExternalPause(this, false);
        holdsRunTimerPause = false;
    }

    private static InteractState NormalizeRestoredPlayerState(InteractState state)
    {
        return state == InteractState.None || state == InteractState.Talking
            ? InteractState.Idle
            : state;
    }

    private void PrepareBossForEncounter()
    {
        ResolveBossController();

        if (bossController != null)
            bossController.SetCombatActive(false);
    }

    private void StartBossCombat()
    {
        if (!startBossCombatAfterDialogue)
            return;

        ResolveBossController();

        if (bossController == null)
        {
            Debug.LogWarning("[BossEncounterDirector] No BossControllerBase found to start combat.", this);
            return;
        }

        bossController.BeginCombatEncounter(PlayerRuntimeRegistry.GetPlayerTransform());
    }

    private void ResolveBossController()
    {
        if (bossController != null)
            return;

        bossController = FindAnyObjectByType<BossControllerBase>();
    }
}

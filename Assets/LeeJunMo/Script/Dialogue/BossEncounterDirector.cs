using System.Collections;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 보스 조우 시작 연출의 카메라, 대화, 전투 시작 순서를 조율한다.
/// - 연출 중 플레이어 조작 보호는 PlayerCinematicProtection에 위임해 보스별 구현 차이를 줄인다.
/// </summary>
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
    private PlayerCinematicProtection lockedPlayerProtection;
    private InteractState previousPlayerState = InteractState.Idle;
    private bool hasPlayed;
    private bool shouldHoldPlayerLock;
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

    private void LateUpdate()
    {
        if (shouldHoldPlayerLock)
            MaintainPlayerLock();
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

        shouldHoldPlayerLock = false;
        RestorePlayerState();
        ReleasePlayerCinematicProtection();

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
        shouldHoldPlayerLock = true;
        TryCacheAndLockPlayer(player);
        AcquirePlayerCinematicProtection(player);
        MaintainPlayerLock();
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
        shouldHoldPlayerLock = true;
        MaintainPlayerLock();
        runningSequence = StartCoroutine(SequenceRoutine());
    }

    private IEnumerator SequenceRoutine()
    {
        if (cameraDirector == null)
        {
            Debug.LogError("[BossEncounterDirector] cameraDirector is missing.", this);
            ReleaseTransitionPlayerLock();
            shouldHoldPlayerLock = false;
            RestorePlayerState();
            ReleasePlayerCinematicProtection();
            ReleaseRunTimerPause();
            runningSequence = null;
            yield break;
        }

        if (dialogueRunner == null)
        {
            Debug.LogError("[BossEncounterDirector] dialogueRunner is missing.", this);
            ReleaseTransitionPlayerLock();
            shouldHoldPlayerLock = false;
            RestorePlayerState();
            ReleasePlayerCinematicProtection();
            ReleaseRunTimerPause();
            runningSequence = null;
            yield break;
        }

        yield return new WaitUntil(() => PlayerRuntimeRegistry.GetPlayerTransform() != null);
        TryCacheAndLockPlayer();
        AcquirePlayerCinematicProtection();
        MaintainPlayerLock();
        yield return new WaitUntil(IsSceneTransitionReady);
        ReleaseTransitionPlayerLock();

        yield return cameraDirector.FocusBossRoutine();
        yield return dialogueRunner.PlayDialogueRoutine();
        yield return cameraDirector.ReturnToPlayerRoutine();

        shouldHoldPlayerLock = false;
        RestorePlayerState();
        ReleasePlayerCinematicProtection();
        StartBossCombat();
        ReleaseRunTimerPause();

        hasPlayed = true;
        runningSequence = null;
    }

    /// <summary>
    /// 책임 :
    /// - 마녀 보스에서 검증된 방식처럼 조우 연출 동안 PlayerInteractor 상태를 Talking으로 고정한다.
    /// - 이동 모터가 비활성화된 입력 컴포넌트의 이전 의도를 읽더라도 GetIntent 단계에서 None을 반환하게 만든다.
    /// </summary>
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

    /// <summary>
    /// 책임 :
    /// - 조우 연출 종료/중단 시 PlayerInteractor 상태를 연출 진입 전 상태로 되돌린다.
    /// - None/Talking 같은 연출 전용 상태는 일반 Idle로 정규화해 플레이어가 영구 잠기지 않도록 한다.
    /// </summary>
    private void RestorePlayerState()
    {
        if (cachedPlayer == null)
            return;

        cachedPlayer.SetInteractState(previousPlayerState);
        cachedPlayer = null;
    }

    /// <summary>
    /// 책임 :
    /// - 보스 조우 연출 동안 플레이어 입력/전투 조작/상호작용을 공용 보호 규칙으로 잠근다.
    /// - 플레이어 프리팹에 보호 컴포넌트가 없더라도 런타임에 보강해 연출 경로가 깨지지 않게 한다.
    /// </summary>
    private void AcquirePlayerCinematicProtection(PlayerInteractor2D player = null)
    {
        if (lockedPlayerProtection != null)
            return;

        Transform playerTransform = player != null ? player.transform : PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null)
            return;

        lockedPlayerProtection = playerTransform.GetComponent<PlayerCinematicProtection>();
        if (lockedPlayerProtection == null)
            lockedPlayerProtection = playerTransform.gameObject.AddComponent<PlayerCinematicProtection>();
        lockedPlayerProtection.Acquire(this);
    }

    /// <summary>
    /// 책임 :
    /// - 보스 조우 연출이 정상 종료되거나 중단될 때 플레이어 보호 토큰을 회수한다.
    /// - 중복 잠금 환경에서도 다른 연출의 보호 요청을 침범하지 않도록 자신의 토큰만 해제한다.
    /// </summary>
    private void ReleasePlayerCinematicProtection()
    {
        if (lockedPlayerProtection == null)
            return;

        lockedPlayerProtection.Release(this);
        lockedPlayerProtection = null;
    }

    /// <summary>
    /// 책임 :
    /// - 씬 전환/스폰/대화 준비 과정에서 다른 시스템이 플레이어 상태를 되돌려도 조우 연출 잠금을 다시 적용한다.
    /// - 카메라가 보스로 이동하는 대화 전 구간까지 이동/입력 잠금이 끊기지 않게 유지한다.
    /// </summary>
    private void MaintainPlayerLock()
    {
        if (cachedPlayer == null)
            TryCacheAndLockPlayer();

        if (lockedPlayerProtection == null)
            AcquirePlayerCinematicProtection();

        if (cachedPlayer == null)
            return;

        if (cachedPlayer.CurrentState == InteractState.Talking)
            return;

        cachedPlayer.SetInteractState(InteractState.Talking);
    }

    private static InteractState NormalizeRestoredPlayerState(InteractState state)
    {
        return state == InteractState.None || state == InteractState.Talking
            ? InteractState.Idle
            : state;
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
        RunRouteBgmService.EnsureInstance()?.NotifyBossCombatStarted();
    }

    private void ResolveBossController()
    {
        if (bossController != null)
            return;

        bossController = FindAnyObjectByType<BossControllerBase>();
    }
}

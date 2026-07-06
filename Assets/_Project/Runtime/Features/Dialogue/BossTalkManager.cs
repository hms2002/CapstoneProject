using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 책임 : 레거시 보스 대화 조우에서 카메라 포커스, 대화 재생, 전투 시작 전환을 순서대로 실행한다.
/// </summary>
public class BossTalkManager : MonoBehaviour
{
    [Header("Legacy Data")]
    [SerializeField] private NPCData npcData;
    [FormerlySerializedAs("inkJSON")]
    [SerializeField, HideInInspector] private TextAsset legacyInkJSON;

    [Header("Legacy Camera Settings")]
    [SerializeField] private Component playerCam;
    [SerializeField] private Component bossCam;
    [SerializeField] private int normalPriority = 10;
    [SerializeField] private int focusPriority = 100;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool disableLegacyFollowWhileSequence = true;
    [SerializeField] private float blendWaitFallbackSeconds = 2f;

    [Header("Sequence")]
    [SerializeField] private MonoBehaviour cameraDirector;
    [SerializeField] private BossDialogueRunner dialogueRunner;
    [SerializeField] private BossControllerBase bossController;
    [SerializeField] private bool startBossCombatAfterDialogue = true;

    private Coroutine runningSequence;
    private PlayerInteractor2D cachedPlayer;
    private PlayerCinematicProtection lockedPlayerProtection;
    private PlayerAnimatorController2D lockedPlayerAnimator;
    private WeaponPresentationRig2D lockedWeaponPresentationRig;
    private GameFlowInputBlocker encounterInputBlocker;
    private InteractState previousPlayerState = InteractState.Idle;
    private bool holdsTransitionPlayerLock;
    private bool holdsRunTimerPause;

    public bool IsSequenceRunning => runningSequence != null;
    private ICameraPresentationDirector CameraDirector => CameraPresentationPlayback.FromBehaviour(cameraDirector);

    private void Awake()
    {
        CacheDependencies();
        ConfigureLegacyAdapters();
        PrepareBossForEncounter();
    }

    private void Start()
    {
        if (playOnStart)
            BeginEncounterSequence();
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        ReleaseTransitionPlayerLock();
        ReleaseEncounterInputBlock();
        ReleaseRunTimerPause();

        if (runningSequence != null)
        {
            StopCoroutine(runningSequence);
            runningSequence = null;
        }

        ReleasePlayerCinematicProtection();
        RestorePlayerState();

        CameraDirector?.RestoreDefaultState();
    }

    public void BeginEncounterSequence()
    {
        if (runningSequence != null)
            return;

        AcquireTransitionPlayerLock();
        AcquireEncounterInputBlock();
        AcquireRunTimerPause();
        AcquirePlayerCinematicProtection();
        TryCacheAndLockPlayer();
        runningSequence = StartCoroutine(EncounterSequence());
    }

    private IEnumerator EncounterSequence()
    {
        if (!ValidateSetup())
        {
            ReleaseTransitionPlayerLock();
            ReleaseEncounterInputBlock();
            ReleasePlayerCinematicProtection();
            RestorePlayerState();
            ReleaseRunTimerPause();
            runningSequence = null;
            yield break;
        }

        yield return new WaitUntil(() => PlayerRuntimeRegistry.GetPlayerTransform() != null);
        AcquirePlayerCinematicProtection();
        TryCacheAndLockPlayer();
        yield return new WaitUntil(IsSceneTransitionReady);
        ReleaseTransitionPlayerLock();
        ICameraPresentationDirector resolvedCameraDirector = CameraDirector;
        yield return resolvedCameraDirector.FocusBossRoutine();
        yield return dialogueRunner.PlayDialogueRoutine();
        yield return resolvedCameraDirector.ReturnToPlayerRoutine();

        ReleasePlayerCinematicProtection();
        RestorePlayerState();
        ReleaseEncounterInputBlock();
        StartBossCombat();
        ReleaseRunTimerPause();
        runningSequence = null;
    }

    private bool ValidateSetup()
    {
        if (CameraDirector == null)
        {
            Debug.LogError("[BossTalkManager] cameraDirector is missing.", this);
            return false;
        }

        if (dialogueRunner == null)
        {
            Debug.LogError("[BossTalkManager] dialogueRunner is missing.", this);
            return false;
        }

        return true;
    }

    private void CacheDependencies()
    {
        if (CameraDirector == null)
            cameraDirector = CameraPresentationPlayback.GetOrAdd(gameObject) as MonoBehaviour;

        if (dialogueRunner == null)
            dialogueRunner = GetComponent<BossDialogueRunner>();

        if (dialogueRunner == null)
            dialogueRunner = gameObject.AddComponent<BossDialogueRunner>();
    }

    private void ConfigureLegacyAdapters()
    {
        ICameraPresentationSettingsReceiver cameraSettingsReceiver =
            CameraPresentationPlayback.AsSettingsReceiver(cameraDirector);
        if (cameraSettingsReceiver != null)
        {
            cameraSettingsReceiver.ApplyPresentationSettings(
                playerCam,
                bossCam,
                normalPriority,
                focusPriority,
                disableLegacyFollowWhileSequence,
                blendWaitFallbackSeconds);
        }

        if (dialogueRunner != null)
            dialogueRunner.ApplyLegacyDialogueData(npcData, legacyInkJSON);
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

    private void AcquirePlayerCinematicProtection(PlayerInteractor2D player = null)
    {
        if (lockedPlayerProtection != null)
        {
            AcquirePlayerPresentationLock(lockedPlayerProtection.transform);
            return;
        }

        Transform playerTransform = player != null ? player.transform : PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null)
            return;

        lockedPlayerProtection = playerTransform.GetComponent<PlayerCinematicProtection>();
        if (lockedPlayerProtection == null)
            lockedPlayerProtection = playerTransform.gameObject.AddComponent<PlayerCinematicProtection>();

        lockedPlayerProtection.Acquire(this);
        AcquirePlayerPresentationLock(playerTransform);
    }

    private void ReleasePlayerCinematicProtection()
    {
        if (lockedPlayerProtection != null)
        {
            lockedPlayerProtection.Release(this);
            lockedPlayerProtection = null;
        }

        ReleasePlayerPresentationLock();
    }

    private void AcquirePlayerPresentationLock(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        if (lockedPlayerAnimator == null)
            lockedPlayerAnimator = playerTransform.GetComponent<PlayerAnimatorController2D>();

        lockedPlayerAnimator?.AcquireCinematicFacingLock(this);

        if (lockedWeaponPresentationRig == null)
            lockedWeaponPresentationRig = playerTransform.GetComponentInChildren<WeaponPresentationRig2D>(true);

        lockedWeaponPresentationRig?.AcquireCinematicPresentationLock(this);
    }

    private void ReleasePlayerPresentationLock()
    {
        lockedPlayerAnimator?.ReleaseCinematicFacingLock(this);
        lockedPlayerAnimator = null;

        lockedWeaponPresentationRig?.ReleaseCinematicPresentationLock(this);
        lockedWeaponPresentationRig = null;
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
        ISceneFadeTransitionHandle transitionService = SceneFadeTransitionPlayback.EnsureInstance();
        return transitionService == null || !transitionService.IsTransitionActive;
    }

    private void AcquireTransitionPlayerLock()
    {
        if (holdsTransitionPlayerLock)
            return;

        ISceneFadeTransitionHandle transitionService = SceneFadeTransitionPlayback.EnsureInstance();
        if (transitionService == null)
            return;

        transitionService.SetPlayerUnlockBlocked(this, true);
        holdsTransitionPlayerLock = true;
    }

    private void ReleaseTransitionPlayerLock()
    {
        if (!holdsTransitionPlayerLock)
            return;

        ISceneFadeTransitionHandle transitionService = SceneFadeTransitionPlayback.Instance;
        if (transitionService != null)
            transitionService.SetPlayerUnlockBlocked(this, false);

        holdsTransitionPlayerLock = false;
    }

    private void AcquireEncounterInputBlock()
    {
        encounterInputBlocker = GameFlowInputBlocker.GetOrAdd(this);
        encounterInputBlocker?.Acquire();
    }

    private void ReleaseEncounterInputBlock()
    {
        encounterInputBlocker?.Release();
    }

    /// <summary>
    /// 책임 :
    /// - 보스 연출과 대화 시퀀스 전체 동안 런 제한 시간 감소를 멈춘다.
    /// - 대화 본문이 시작되기 전 카메라 연출/대기 시간도 공정하게 보호한다.
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
    /// - 보스 연출 시퀀스가 끝나거나 중도 취소될 때 런 제한 시간 pause를 해제한다.
    /// - 레거시 시퀀스 경로에서도 타이머 정리 누락이 발생하지 않게 보장한다.
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
            Debug.LogWarning("[BossTalkManager] No BossControllerBase found to start combat.", this);
            return;
        }

        bossController.BeginCombatEncounter(PlayerRuntimeRegistry.GetPlayerTransform());
        RunRouteBgmPlayback.NotifyBossCombatStarted();
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        if (runningSequence == null || player == null)
            return;

        AcquirePlayerCinematicProtection(player);
        TryCacheAndLockPlayer(player);
    }

    private void ResolveBossController()
    {
        if (bossController != null)
            return;

        bossController = FindAnyObjectByType<BossControllerBase>();
    }
}

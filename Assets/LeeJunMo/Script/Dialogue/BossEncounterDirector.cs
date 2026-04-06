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

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PrepareBossForEncounter();
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
            Debug.LogError("[BossEncounterDirector] cameraDirector is missing.", this);
            runningSequence = null;
            yield break;
        }

        if (dialogueRunner == null)
        {
            Debug.LogError("[BossEncounterDirector] dialogueRunner is missing.", this);
            runningSequence = null;
            yield break;
        }

        yield return null;
        yield return new WaitUntil(() => PlayerRuntimeRegistry.GetPlayerTransform() != null);

        CacheAndLockPlayer();

        yield return cameraDirector.FocusBossRoutine();
        yield return dialogueRunner.PlayDialogueRoutine();
        yield return cameraDirector.ReturnToPlayerRoutine();

        RestorePlayerState();
        StartBossCombat();

        hasPlayed = true;
        runningSequence = null;
    }

    private void CacheAndLockPlayer()
    {
        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        cachedPlayer = playerTransform != null ? playerTransform.GetComponent<PlayerInteractor2D>() : null;

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

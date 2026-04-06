using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;

public class BossTalkManager : MonoBehaviour
{
    [Header("Legacy Data")]
    [SerializeField] private NPCData npcData;
    [FormerlySerializedAs("inkJSON")]
    [SerializeField, HideInInspector] private TextAsset legacyInkJSON;

    [Header("Legacy Camera Settings")]
    [SerializeField] private CinemachineCamera playerCam;
    [SerializeField] private CinemachineCamera bossCam;
    [SerializeField] private int normalPriority = 10;
    [SerializeField] private int focusPriority = 100;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool disableLegacyFollowWhileSequence = true;
    [SerializeField] private float blendWaitFallbackSeconds = 2f;

    [Header("Sequence")]
    [SerializeField] private CameraPresentationDirector cameraDirector;
    [SerializeField] private BossDialogueRunner dialogueRunner;
    [SerializeField] private BossControllerBase bossController;
    [SerializeField] private bool startBossCombatAfterDialogue = true;

    [Header("Legacy Post Sequence")]
    [SerializeField] private BossDrop bossDrop;

    private Coroutine runningSequence;
    private PlayerInteractor2D cachedPlayer;
    private InteractState previousPlayerState = InteractState.Idle;

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

    private void OnDisable()
    {
        if (runningSequence != null)
        {
            StopCoroutine(runningSequence);
            runningSequence = null;
        }

        RestorePlayerState();

        if (cameraDirector != null)
            cameraDirector.RestoreDefaultState();
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

        yield return null;
        yield return new WaitUntil(() => PlayerRuntimeRegistry.GetPlayerTransform() != null);

        CacheAndLockPlayer();
        yield return cameraDirector.FocusBossRoutine();
        yield return dialogueRunner.PlayDialogueRoutine();
        yield return cameraDirector.ReturnToPlayerRoutine();

        RestorePlayerState();
        StartBossCombat();
        runningSequence = null;
    }

    private bool ValidateSetup()
    {
        if (cameraDirector == null)
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
        if (cameraDirector == null)
            cameraDirector = GetComponent<CameraPresentationDirector>();

        if (cameraDirector == null)
            cameraDirector = gameObject.AddComponent<CameraPresentationDirector>();

        if (dialogueRunner == null)
            dialogueRunner = GetComponent<BossDialogueRunner>();

        if (dialogueRunner == null)
            dialogueRunner = gameObject.AddComponent<BossDialogueRunner>();
    }

    private void ConfigureLegacyAdapters()
    {
        if (cameraDirector != null)
        {
            cameraDirector.ApplyPresentationSettings(
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
            Debug.LogWarning("[BossTalkManager] No BossControllerBase found to start combat.", this);
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

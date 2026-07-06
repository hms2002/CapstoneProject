using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Capstone/Boss/Boss Defeat Ending Sequence")]
// 책임: 보스 처치 후 대화, 엔딩 아웃트로, 승리 종료 또는 씬 이동 흐름을 순서대로 진행한다.
public sealed class BossDefeatEndingSequence : MonoBehaviour
{
    private enum TerminalCompletionMode
    {
        VictoryGameOver,
        LoadTargetScene
    }

    [Header("Boss")]
    [SerializeField] private BossControllerBase targetBoss;

    [Header("Dialogue")]
    [SerializeField] private NPCData dialogueNpcData;
    [SerializeField] private TextAsset dialogueInk;
    [SerializeField] private string dialogueStartPath;

    [Header("Outro")]
    [SerializeField] private EndingOutroPlayer outroPlayer;
    [SerializeField] private bool keepOutroVisibleUntilSceneTransition = true;

    [Header("Completion")]
    [SerializeField] private TerminalCompletionMode completionMode = TerminalCompletionMode.VictoryGameOver;
    [SerializeField] private string targetSceneName = "TitleScene";
    [SerializeField] private RunEndReason endRunReason = RunEndReason.Victory;
    [SerializeField, Min(0f)] private float titleSceneFadeOutDuration = 1.5f;
    [SerializeField, Min(0f)] private float titleSceneFadeInDuration = 1f;

    private GameFlowInputBlocker inputBlocker;
    private PlayerCinematicProtection lockedPlayerProtection;
    private bool isRunning;
    private bool completedViaTerminalEnding;
    private bool keepTerminalStateUntilDisable;
    private bool hasWarnedMultipleOutroPlayers;

    public bool IsRunning => isRunning;
    public bool CompletedViaTerminalEnding => completedViaTerminalEnding;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        ReleaseTerminalState();
    }

    public bool CanRunForBoss(BossControllerBase boss)
    {
        ResolveReferences();

        if (!isActiveAndEnabled || boss == null || targetBoss == null)
            return false;

        return ReferenceEquals(targetBoss, boss);
    }

    public IEnumerator RunRoutine(
        BossControllerBase boss,
        Func<IEnumerator> beforeDialogue = null)
    {
        if (!CanRunForBoss(boss))
            yield break;

        if (isRunning)
        {
            while (isRunning)
                yield return null;

            yield break;
        }

        isRunning = true;
        completedViaTerminalEnding = false;
        keepTerminalStateUntilDisable = false;
        AcquireTerminalState();

        try
        {
            if (beforeDialogue != null)
            {
                IEnumerator beforeDialogueRoutine = beforeDialogue();
                if (beforeDialogueRoutine != null)
                    yield return beforeDialogueRoutine;
            }

            yield return PlayDialogueRoutine();

            yield return PlayOutroRoutine();

            completedViaTerminalEnding = true;
            keepTerminalStateUntilDisable = CompleteTerminalEnding();
        }
        finally
        {
            isRunning = false;
            if (!keepTerminalStateUntilDisable)
                ReleaseTerminalState();
        }
    }

    private IEnumerator PlayDialogueRoutine()
    {
        if (dialogueInk == null)
            yield break;

        if (dialogueNpcData == null)
        {
            Debug.LogWarning("[BossDefeatEndingSequence] Dialogue NPCData is missing.", this);
            yield break;
        }

        if (!DialoguePlayback.IsAvailable)
        {
            Debug.LogWarning("[BossDefeatEndingSequence] Dialogue playback backend was not found.", this);
            yield break;
        }

        List<DialogueStorySegment> segments = new()
        {
            new DialogueStorySegment(dialogueInk, dialogueStartPath)
        };
        List<NPCData> participants = new() { dialogueNpcData };

        if (!DialoguePlayback.TryStartDialogueSequence(segments, participants))
            yield break;

        yield return new WaitUntil(() => !DialoguePlayback.IsPlaying);
    }

    private IEnumerator PlayOutroRoutine()
    {
        outroPlayer = ResolveOutroPlayerForPlayback();
        if (outroPlayer == null)
        {
            Debug.LogWarning("[BossDefeatEndingSequence] EndingOutroPlayer is missing.", this);
            yield break;
        }

        bool completed = false;
        bool keepVisibleOnCompleted = keepOutroVisibleUntilSceneTransition &&
                                      completionMode == TerminalCompletionMode.LoadTargetScene;
        if (!outroPlayer.TryPlay(
                () => completed = true,
                keepVisibleOnCompleted))
        {
            yield break;
        }

        yield return new WaitUntil(() => completed || outroPlayer == null || !outroPlayer.IsPlaying);

        if (completionMode == TerminalCompletionMode.VictoryGameOver)
            HideOutroViewIfAlive();
    }

    private bool CompleteTerminalEnding()
    {
        if (completionMode == TerminalCompletionMode.LoadTargetScene)
            return CompleteRunAndLoadTargetScene();

        if (CompleteRunWithVictoryGameOver())
            return true;

        return CompleteRunAndLoadTargetScene();
    }

    private bool CompleteRunWithVictoryGameOver()
    {
        int magicStoneRewardAmount = Mathf.Max(0, RunSessionStore.GetPendingRunMagicStoneDelta());

        GameOverPresentationRequest request = GameOverPresentationRequest.Victory(
            PlayerRuntimeRegistry.GetPlayerTransform(),
            magicStoneRewardAmount,
            null,
            useSceneTransitionService: true);

        ReleaseTerminalState();

        if (GameOverPresentationPlayback.TryShow(request))
            return true;

        Debug.LogWarning(
            "[BossDefeatEndingSequence] Victory game-over presentation was not accepted. Falling back to target scene transition.",
            this);
        return false;
    }

    private bool CompleteRunAndLoadTargetScene()
    {
        RunSessionStore.EndRun(endRunReason);

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[BossDefeatEndingSequence] Target scene name is empty.", this);
            return false;
        }

        ISceneTransitionHandle coordinator = SceneTransitionPlayback.EnsureInstance();
        if (coordinator == null)
        {
            Debug.LogWarning("[BossDefeatEndingSequence] Scene transition service could not be resolved.", this);
            return false;
        }

        if (!coordinator.TryLoadScene(
                targetSceneName,
                titleSceneFadeOutDuration,
                titleSceneFadeInDuration))
        {
            Debug.LogWarning(
                $"[BossDefeatEndingSequence] Scene transition to '{targetSceneName}' was not accepted.",
                this);
            return false;
        }

        return true;
    }

    private void AcquireTerminalState()
    {
        inputBlocker = GameFlowInputBlocker.GetOrAdd(this);
        inputBlocker?.Acquire();
        AcquirePlayerCinematicProtection();
    }

    private void ReleaseTerminalState()
    {
        keepTerminalStateUntilDisable = false;
        inputBlocker?.Release();
        inputBlocker = null;
        ReleasePlayerCinematicProtection();
    }

    private void AcquirePlayerCinematicProtection()
    {
        if (lockedPlayerProtection != null)
            return;

        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null)
            return;

        lockedPlayerProtection = playerTransform.GetComponent<PlayerCinematicProtection>();
        if (lockedPlayerProtection == null)
            lockedPlayerProtection = playerTransform.gameObject.AddComponent<PlayerCinematicProtection>();

        lockedPlayerProtection?.Acquire(this);
    }

    private void ReleasePlayerCinematicProtection()
    {
        if (lockedPlayerProtection != null)
            lockedPlayerProtection.Release(this);

        lockedPlayerProtection = null;
    }

    private void ResolveReferences()
    {
        if (targetBoss == null)
            targetBoss = GetComponentInParent<BossControllerBase>();

        if (outroPlayer == null)
            outroPlayer = GetComponentInChildren<EndingOutroPlayer>(includeInactive: true);

        if (outroPlayer == null)
            outroPlayer = FindSingleSceneOutroPlayer();
    }

    private EndingOutroPlayer ResolveOutroPlayerForPlayback()
    {
        ResolveReferences();

        if (outroPlayer != null && outroPlayer.CanPlay)
            return outroPlayer;

        EndingOutroPlayer playablePlayer = FindSinglePlayableSceneOutroPlayer();
        if (playablePlayer != null)
        {
            outroPlayer = playablePlayer;
            return outroPlayer;
        }

        return outroPlayer != null && outroPlayer.CanPlay ? outroPlayer : null;
    }

    private void HideOutroViewIfAlive()
    {
        if (outroPlayer != null)
            outroPlayer.HideViewImmediate();
    }

    private EndingOutroPlayer FindSingleSceneOutroPlayer()
    {
        EndingOutroPlayer[] players = FindObjectsByType<EndingOutroPlayer>(FindObjectsInactive.Include);

        EndingOutroPlayer found = null;
        int foundCount = 0;
        for (int i = 0; i < players.Length; i++)
        {
            EndingOutroPlayer player = players[i];
            if (player == null)
                continue;

            found = player;
            foundCount++;
        }

        if (foundCount == 1)
            return found;

        if (foundCount > 1 && !hasWarnedMultipleOutroPlayers)
        {
            hasWarnedMultipleOutroPlayers = true;
            Debug.LogWarning(
                "[BossDefeatEndingSequence] Multiple EndingOutroPlayer instances were found. Assign one explicitly.",
                this);
        }

        return null;
    }

    private EndingOutroPlayer FindSinglePlayableSceneOutroPlayer()
    {
        EndingOutroPlayer[] players = FindObjectsByType<EndingOutroPlayer>(FindObjectsInactive.Include);

        EndingOutroPlayer found = null;
        int foundCount = 0;
        for (int i = 0; i < players.Length; i++)
        {
            EndingOutroPlayer player = players[i];
            if (player == null || !player.CanPlay)
                continue;

            found = player;
            foundCount++;
        }

        if (foundCount == 1)
            return found;

        if (foundCount > 1 && !hasWarnedMultipleOutroPlayers)
        {
            hasWarnedMultipleOutroPlayers = true;
            Debug.LogWarning(
                "[BossDefeatEndingSequence] Multiple playable EndingOutroPlayer instances were found. Assign one explicitly.",
                this);
        }

        return null;
    }
}

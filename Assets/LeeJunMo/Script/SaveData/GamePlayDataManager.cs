using UnityEngine;

public sealed class GamePlayDataManager : MonoBehaviour
{
    public static GamePlayDataManager Instance { get; private set; }

    public GamePlayData Data { get; private set; } = new GamePlayData();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null)
            return;

        var go = new GameObject("GamePlayDataManager");
        go.AddComponent<GamePlayDataManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartRun()
    {
        Data.runCount++;
        Data.ResetForRunStart();
    }

    public void TickRun(float deltaTime)
    {
        if (!Data.isRunActive)
            return;

        Data.runElapsedSeconds += Mathf.Max(0f, deltaTime);
    }

    public void EndRun(RunEndReason reason, int defeatedBossId = -1, string defeatReason = null)
    {
        Data.lastRunEndReason = reason;
        Data.lastDefeatedBossId = defeatedBossId;
        Data.lastDefeatReason = defeatReason;
        Data.ClearRunState();
    }

    public void PrepareTransition(SceneTransitionContext context)
    {
        Data.pendingTransition = context;
    }

    public SceneTransitionContext ConsumePendingTransition()
    {
        var context = Data.pendingTransition;
        Data.pendingTransition = null;
        return context;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}

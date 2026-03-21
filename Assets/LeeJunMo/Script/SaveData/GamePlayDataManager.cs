using UnityEngine;

public sealed class GamePlayDataManager : MonoBehaviour
{
    public static GamePlayDataManager Instance { get; private set; }

    public GamePlayData Data { get; private set; } = new GamePlayData();

    private static bool s_isQuitting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting) return;
        if (Instance != null) return;

        var go = new GameObject(nameof(GamePlayDataManager));
        go.AddComponent<GamePlayDataManager>();
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

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void StartRun()
    {
        Data.isRunActive = true;
        Data.runElapsedSeconds = 0f;
    }

    public void EndRun(RunEndReason reason)
    {
        Data.lastRunEndReason = reason;
        Data.isRunActive = false;
        Data.pendingTransition = null;
        Data.pendingPlayerState = null;
    }

    public void TickRunTimer(float deltaTime)
    {
        if (!Data.isRunActive)
            return;

        Data.runElapsedSeconds += Mathf.Max(0f, deltaTime);
    }

    public void PrepareTransition(SceneTransitionContext context)
    {
        Data.pendingTransition = context;
    }

    public SceneTransitionContext PeekPendingTransition()
    {
        return Data.pendingTransition;
    }

    public SceneTransitionContext ConsumePendingTransition()
    {
        var ctx = Data.pendingTransition;
        Data.pendingTransition = null;
        return ctx;
    }
}
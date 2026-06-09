using UnityEngine;

public class GameDataSaveCoordinator : MonoBehaviour
{
    public static GameDataSaveCoordinator Instance { get; private set; }

    private static bool s_isQuitting;

    [SerializeField] private float deferredSaveDelay = 0.25f;

    private bool saveQueued;
    private bool immediateSaveQueued;
    private float nextDeferredSaveTime = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        var go = new GameObject(nameof(GameDataSaveCoordinator));
        go.AddComponent<GameDataSaveCoordinator>();
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

    private void Update()
    {
        if (!saveQueued || immediateSaveQueued || nextDeferredSaveTime < 0f)
            return;

        if (Time.unscaledTime >= nextDeferredSaveTime)
            FlushSave();
    }

    private void LateUpdate()
    {
        if (!saveQueued || !immediateSaveQueued)
            return;

        FlushSave();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    public static void RequestImmediateSave(Object requester = null)
    {
        Instance?.QueueImmediateSave();
    }

    public static void RequestDeferredSave(Object requester = null)
    {
        Instance?.QueueDeferredSave();
    }

    public static void FlushNow(Object requester = null)
    {
        Instance?.FlushSave();
    }

    private void QueueImmediateSave()
    {
        saveQueued = true;
        immediateSaveQueued = true;
        nextDeferredSaveTime = -1f;
    }

    private void QueueDeferredSave()
    {
        saveQueued = true;
        if (immediateSaveQueued)
            return;

        float targetTime = Time.unscaledTime + deferredSaveDelay;
        if (nextDeferredSaveTime < 0f || targetTime < nextDeferredSaveTime)
            nextDeferredSaveTime = targetTime;
    }

    private void FlushSave()
    {
        if (GameDataManager.Instance == null)
            return;

        GameDataManager.Instance.SaveData();
        saveQueued = false;
        immediateSaveQueued = false;
        nextDeferredSaveTime = -1f;
    }
}

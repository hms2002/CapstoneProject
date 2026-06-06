using UnityEngine;

public class NPCManager : MonoBehaviour
{
    public static NPCManager Instance { get; private set; }

    private static bool s_isQuitting;

    [Header("NPC Database")]
    [SerializeField] private NPCDatabase database;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        var go = new GameObject(nameof(NPCManager));
        go.AddComponent<NPCManager>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            RunModifierService.Instance?.RebuildFromPurchasedUpgrades();
            return;
        }

        Instance.TryAdoptDatabase(database);
        Destroy(gameObject);
    }

    public NPCData GetNPCData(int id)
    {
        return database != null ? database.GetNPC(id) : null;
    }

    private void TryAdoptDatabase(NPCDatabase incomingDatabase)
    {
        if (incomingDatabase == null)
            return;

        if (database != null)
        {
            if (database != incomingDatabase)
            {
                Debug.LogWarning("[NPCManager] Different NPCDatabase was supplied by a scene instance. Keeping the existing database.", this);
            }

            return;
        }

        database = incomingDatabase;
        RunModifierService.Instance?.RebuildFromPurchasedUpgrades();
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }
}

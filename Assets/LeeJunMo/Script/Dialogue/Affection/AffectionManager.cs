using System;
using UnityEngine;

public class AffectionManager : MonoBehaviour
{
    public static AffectionManager Instance { get; private set; }

    private static bool s_isQuitting;

    private readonly AffectionProgressStore progressStore = new AffectionProgressStore();
    private readonly AffectionRewardProcessor rewardProcessor = new AffectionRewardProcessor();

    private int currentNpcId;
    private AffectionUI linkedUI;

    public event Action<int, int> OnAffectionChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        GameObject go = new GameObject(nameof(AffectionManager));
        go.AddComponent<AffectionManager>();
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

    private void Start()
    {
        LoadAffectionData();
    }

    public void SetLinkedUI(AffectionUI ui)
    {
        linkedUI = ui;
        linkedUI?.Setup(GetAffection(currentNpcId));
    }

    public void SetCurrentNPC(int npcId)
    {
        currentNpcId = npcId;
        linkedUI?.Setup(GetAffection(npcId));
    }

    public int GetAffection()
    {
        return GetAffection(currentNpcId);
    }

    public int GetAffection(int npcId)
    {
        return progressStore.GetAffection(npcId);
    }

    public bool AddAffection(NPCData data, int amount, Action onComplete = null)
    {
        if (data == null)
        {
            Debug.LogError("[AffectionManager] NPC data is null.");
            onComplete?.Invoke();
            return false;
        }

        AffectionChangeResult change = progressStore.AddAffection(GameDataManager.Instance?.Data, data.id, amount);

        Debug.Log($"<color=cyan>[AffectionManager] {data.npcName}(ID:{data.id}) affection {change.PreviousAmount} -> {change.NewAmount} (delta {change.Delta})</color>");

        bool hasReward = rewardProcessor.HasRewardsInRange(data, change.PreviousAmount, change.NewAmount);
        if (hasReward)
            Debug.Log($"<color=green>[AffectionManager] Rewards unlocked in affection range {change.PreviousAmount} -> {change.NewAmount}</color>");

        RunAffectionPresentation(data, change, hasReward, onComplete);

        OnAffectionChanged?.Invoke(data.id, change.NewAmount);
        return true;
    }

    public void SetAffection(int npcId, int value)
    {
        progressStore.SetAffection(GameDataManager.Instance?.Data, npcId, value);
    }

    private void LoadAffectionData()
    {
        progressStore.Load(GameDataManager.Instance?.Data);
        int recordCount = GameDataManager.Instance?.Data?.affectionData?.affectionRecords?.Count ?? 0;
        Debug.Log($"[AffectionManager] Loaded affection data. NPC count: {recordCount}");
    }

    private void RunAffectionPresentation(NPCData data, AffectionChangeResult change, bool hasReward, Action onComplete)
    {
        if (linkedUI != null && linkedUI.gameObject.activeInHierarchy)
        {
            linkedUI.PlayGainAnimation(change.PreviousAmount, change.NewAmount, () =>
            {
                CompleteRewardFlow(data, change, hasReward, onComplete);
            });
            return;
        }

        CompleteRewardFlow(data, change, hasReward, onComplete);
    }

    private void CompleteRewardFlow(NPCData data, AffectionChangeResult change, bool hasReward, Action onComplete)
    {
        if (hasReward)
        {
            rewardProcessor.GrantRewards(data, change.PreviousAmount, change.NewAmount, onComplete);
            return;
        }

        onComplete?.Invoke();
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
}

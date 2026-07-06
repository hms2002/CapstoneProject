using System;
using UnityEngine;

/// <summary>
/// 책임 : NPC 호감도 저장값을 변경하고 보상/표시/피드백 흐름을 조율한다.
/// </summary>
public class AffectionManager : MonoBehaviour
{
    public static AffectionManager Instance { get; private set; }

    private static bool s_isQuitting;

    private readonly AffectionProgressStore progressStore = new AffectionProgressStore();
    private readonly AffectionRewardProcessor rewardProcessor = new AffectionRewardProcessor();

    private int currentNpcId;
    private IAffectionPresentationView linkedUI;
    private bool isSubscribedToGameDataStore;

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

    private void OnEnable()
    {
        TrySubscribeToGameDataStore();
    }

    private void OnDisable()
    {
        if (isSubscribedToGameDataStore)
            GameDataStore.OnDataLoaded -= HandleGameDataLoaded;

        isSubscribedToGameDataStore = false;
    }

    private void Start()
    {
        TrySubscribeToGameDataStore();
        LoadAffectionData();
    }

    public void SetLinkedUI(IAffectionPresentationView ui)
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

        bool isRunActive = IsRunActive();
        AffectionChangeResult change = progressStore.AddAffection(
            GameDataStore.Data,
            data.id,
            amount,
            syncToGameData: !isRunActive);

        Debug.Log($"<color=cyan>[AffectionManager] {data.npcName}(ID:{data.id}) affection {change.PreviousAmount} -> {change.NewAmount} (delta {change.Delta})</color>");

        if (change.NewAmount != change.PreviousAmount)
        {
            PlayAffectionChangeSound(change.Delta);

            if (isRunActive)
                RunSessionStore.AddPendingAffectionDelta(data.id, change.Delta);
            else
                GameDataStore.RequestImmediateSave(this);
        }

        bool hasReward = rewardProcessor.HasRewardsInRange(data, change.PreviousAmount, change.NewAmount);
        if (hasReward)
            Debug.Log($"<color=green>[AffectionManager] Rewards unlocked in affection range {change.PreviousAmount} -> {change.NewAmount}</color>");

        RunAffectionPresentation(data, change, hasReward, onComplete);

        OnAffectionChanged?.Invoke(data.id, change.NewAmount);
        return true;
    }

    public void SetAffection(int npcId, int value)
    {
        int previousValue = progressStore.GetAffection(npcId);
        bool isRunActive = IsRunActive();
        progressStore.SetAffection(GameDataStore.Data, npcId, value, syncToGameData: !isRunActive);
        if (previousValue != value)
        {
            PlayAffectionChangeSound(value - previousValue);

            if (isRunActive)
                RunSessionStore.AddPendingAffectionDelta(npcId, value - previousValue);
            else
                GameDataStore.RequestImmediateSave(this);

            if (currentNpcId == npcId)
                linkedUI?.Setup(value);

            OnAffectionChanged?.Invoke(npcId, value);
        }
    }

    /// <summary>호감도 증감 방향에 맞는 UI 피드백 사운드를 재생합니다.</summary>
    private static void PlayAffectionChangeSound(int delta)
    {
        if (delta == 0)
            return;

        AffectionFeedbackSoundPlayer.PlayChange(delta);
    }

    private void LoadAffectionData()
    {
        progressStore.Load(GameDataStore.Data);
        int recordCount = GameDataStore.Data?.affectionData?.affectionRecords?.Count ?? 0;
        Debug.Log($"[AffectionManager] Loaded affection data. NPC count: {recordCount}");
    }

    /// <summary>GameData 저장소 생성 순서와 무관하게 호감도 캐시 갱신 이벤트를 한 번만 구독합니다.</summary>
    private void TrySubscribeToGameDataStore()
    {
        if (isSubscribedToGameDataStore || !GameDataStore.IsAvailable)
            return;

        GameDataStore.OnDataLoaded += HandleGameDataLoaded;
        isSubscribedToGameDataStore = true;
    }

    /// <summary>슬롯 변경/삭제 후 새 저장 데이터 기준으로 호감도 캐시와 UI를 다시 맞춥니다.</summary>
    private void HandleGameDataLoaded(GameData data, int slotIndex)
    {
        progressStore.Load(data);
        linkedUI?.Setup(GetAffection(currentNpcId));
        int recordCount = data?.affectionData?.affectionRecords?.Count ?? 0;
        Debug.Log($"[AffectionManager] Reloaded affection data for slot {slotIndex + 1}. NPC count: {recordCount}");
    }

    private void RunAffectionPresentation(NPCData data, AffectionChangeResult change, bool hasReward, Action onComplete)
    {
        if (linkedUI != null && linkedUI.IsPresentationActive)
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

    private static bool IsRunActive()
    {
        return RunSessionStore.IsRunActive;
    }
}

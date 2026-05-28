using System;
using CapstoneAudio;
using UnityEngine;

public class AffectionManager : MonoBehaviour
{
    private static readonly SoundRef AffectionUpSound = SoundRef.FromKey("sound_ui_AffectionUp");
    private static readonly SoundRef AffectionDownSound = SoundRef.FromKey("sound_ui_AffectionDown");

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

        bool isRunActive = IsRunActive();
        AffectionChangeResult change = progressStore.AddAffection(
            GameDataManager.Instance?.Data,
            data.id,
            amount,
            syncToGameData: !isRunActive);

        Debug.Log($"<color=cyan>[AffectionManager] {data.npcName}(ID:{data.id}) affection {change.PreviousAmount} -> {change.NewAmount} (delta {change.Delta})</color>");

        if (change.NewAmount != change.PreviousAmount)
        {
            PlayAffectionChangeSound(change.Delta);

            if (isRunActive)
                GamePlayDataManager.Instance?.AddPendingAffectionDelta(data.id, change.Delta);
            else
                GameDataSaveCoordinator.RequestImmediateSave(this);
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
        progressStore.SetAffection(GameDataManager.Instance?.Data, npcId, value, syncToGameData: !isRunActive);
        if (previousValue != value)
        {
            PlayAffectionChangeSound(value - previousValue);

            if (isRunActive)
                GamePlayDataManager.Instance?.AddPendingAffectionDelta(npcId, value - previousValue);
            else
                GameDataSaveCoordinator.RequestImmediateSave(this);
        }
    }

    /// <summary>호감도 증감 방향에 맞는 UI 피드백 사운드를 재생합니다.</summary>
    private static void PlayAffectionChangeSound(int delta)
    {
        if (delta == 0)
            return;

        SoundPlaybackUtility.Play(delta > 0 ? AffectionUpSound : AffectionDownSound);
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

    private static bool IsRunActive()
    {
        return GamePlayDataManager.Instance != null
            && GamePlayDataManager.Instance.Data != null
            && GamePlayDataManager.Instance.Data.isRunActive;
    }
}

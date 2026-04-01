using System;
using System.Collections.Generic;
using UnityEngine;

public class RewardDisplayService : MonoBehaviour
{
    public static RewardDisplayService Instance { get; private set; }

    private static bool s_isQuitting;

    private readonly Queue<PendingRewardRequest> pendingRequests = new Queue<PendingRewardRequest>();

    private RewardDisplayUI currentView;
    private bool isShowingReward;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        var go = new GameObject(nameof(RewardDisplayService));
        go.AddComponent<RewardDisplayService>();
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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    public void RegisterView(RewardDisplayUI view)
    {
        if (view == null)
            return;

        currentView = view;
        isShowingReward = view.IsActive;
        TryPresentNext();
    }

    public void UnregisterView(RewardDisplayUI view)
    {
        if (currentView != view)
            return;

        currentView = null;
        isShowingReward = false;
    }

    public void ShowReward(List<UpgradeEffectSO> upgradeEffects = null, List<AffectionEffect> affectionEffects = null, Action callback = null)
    {
        pendingRequests.Enqueue(new PendingRewardRequest(
            upgradeEffects != null ? new List<UpgradeEffectSO>(upgradeEffects) : null,
            affectionEffects != null ? new List<AffectionEffect>(affectionEffects) : null,
            callback));

        if (currentView == null)
            Debug.LogWarning("[RewardDisplayService] RewardDisplayUI view is not registered yet. Reward display request will be queued.", this);

        TryPresentNext();
    }

    public void NotifyClosed(RewardDisplayUI view)
    {
        if (currentView != view)
            return;

        isShowingReward = false;
        TryPresentNext();
    }

    private void TryPresentNext()
    {
        if (currentView == null || isShowingReward || pendingRequests.Count == 0)
            return;

        PendingRewardRequest request = pendingRequests.Dequeue();
        isShowingReward = true;
        currentView.ShowReward(request.upgradeEffects, request.affectionEffects, request.callback);
    }

    private readonly struct PendingRewardRequest
    {
        public readonly List<UpgradeEffectSO> upgradeEffects;
        public readonly List<AffectionEffect> affectionEffects;
        public readonly Action callback;

        public PendingRewardRequest(List<UpgradeEffectSO> upgradeEffects, List<AffectionEffect> affectionEffects, Action callback)
        {
            this.upgradeEffects = upgradeEffects;
            this.affectionEffects = affectionEffects;
            this.callback = callback;
        }
    }
}

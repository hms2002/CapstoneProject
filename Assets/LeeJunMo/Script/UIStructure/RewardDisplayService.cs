using System;
using System.Collections.Generic;
using UnityEngine;

public class RewardDisplayService : MonoBehaviour
{
    public static RewardDisplayService Instance { get; private set; }

    private static bool s_isQuitting;

    private RewardDisplayUI currentView;
    private PendingRewardRequest? pendingRequest;

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
        TryFlushPending();
    }

    public void UnregisterView(RewardDisplayUI view)
    {
        if (currentView == view)
            currentView = null;
    }

    public void ShowReward(List<UpgradeEffectSO> upgradeEffects = null, List<AffectionEffect> affectionEffects = null, Action callback = null)
    {
        if (currentView != null)
        {
            currentView.ShowReward(upgradeEffects, affectionEffects, callback);
            return;
        }

        pendingRequest = new PendingRewardRequest(
            upgradeEffects != null ? new List<UpgradeEffectSO>(upgradeEffects) : null,
            affectionEffects != null ? new List<AffectionEffect>(affectionEffects) : null,
            callback);

        Debug.LogWarning("[RewardDisplayService] RewardDisplayUI view가 아직 등록되지 않아 보상 표시를 대기합니다.", this);
    }

    private void TryFlushPending()
    {
        if (currentView == null || pendingRequest == null)
            return;

        PendingRewardRequest request = pendingRequest.Value;
        pendingRequest = null;
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

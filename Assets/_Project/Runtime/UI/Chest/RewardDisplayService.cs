using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RewardDisplayService : MonoBehaviour
{
    public static RewardDisplayService Instance { get; private set; }

    private static bool s_isQuitting;

    private readonly Queue<PendingRewardRequest> pendingRequests = new Queue<PendingRewardRequest>();

    private RewardDisplayUI currentView;
    private Coroutine presentationRetryRoutine;
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
        StopPresentationRetry();
    }

    public void ShowReward(List<UpgradeEffectSO> upgradeEffects = null, List<AffectionEffect> affectionEffects = null, Action callback = null)
    {
        EnqueueRewardRequest(upgradeEffects, affectionEffects, callback, false, null);
    }

    public void ShowUpgradeReward(UpgradeNodeSO upgradeNode, Action callback = null)
    {
        if (upgradeNode == null)
        {
            callback?.Invoke();
            return;
        }

        EnqueueRewardRequest(upgradeNode.effects, null, callback, false, upgradeNode);
    }

    public void ShowFlowOwnedReward(List<UpgradeEffectSO> upgradeEffects = null, List<AffectionEffect> affectionEffects = null, Action callback = null)
    {
        if (currentView == null)
        {
            Debug.LogWarning("[RewardDisplayService] RewardDisplayUI view is not registered. Flow-owned reward display will be skipped to keep the owning flow moving.", this);
            callback?.Invoke();
            return;
        }

        EnqueueRewardRequest(upgradeEffects, affectionEffects, callback, true, null);
    }

    private void EnqueueRewardRequest(
        List<UpgradeEffectSO> upgradeEffects,
        List<AffectionEffect> affectionEffects,
        Action callback,
        bool allowDuringExternalUiInputBlock,
        UpgradeNodeSO upgradeNode)
    {
        pendingRequests.Enqueue(new PendingRewardRequest(
            upgradeEffects != null ? new List<UpgradeEffectSO>(upgradeEffects) : null,
            affectionEffects != null ? new List<AffectionEffect>(affectionEffects) : null,
            callback,
            allowDuringExternalUiInputBlock,
            upgradeNode));

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

        PendingRewardRequest request = pendingRequests.Peek();
        if (!CanPresent(request))
        {
            EnsurePresentationRetry();
            return;
        }

        StopPresentationRetry();
        request = pendingRequests.Dequeue();
        isShowingReward = true;
        if (!currentView.ShowReward(
                request.upgradeEffects,
                request.affectionEffects,
                request.callback,
                request.allowDuringExternalUiInputBlock,
                request.upgradeNode))
        {
            isShowingReward = false;
            request.callback?.Invoke();
            TryPresentNext();
        }
    }

    private bool CanPresent(PendingRewardRequest request)
    {
        if (UIManager.Instance == null)
            return true;

        return request.allowDuringExternalUiInputBlock
            ? UIManager.Instance.CanOpenFlowOwnedUI(currentView)
            : UIManager.Instance.CanOpenUI(currentView);
    }

    private void EnsurePresentationRetry()
    {
        if (presentationRetryRoutine != null)
            return;

        presentationRetryRoutine = StartCoroutine(RetryPresentWhenPossible());
    }

    private void StopPresentationRetry()
    {
        if (presentationRetryRoutine == null)
            return;

        StopCoroutine(presentationRetryRoutine);
        presentationRetryRoutine = null;
    }

    private IEnumerator RetryPresentWhenPossible()
    {
        while (currentView != null && !isShowingReward && pendingRequests.Count > 0)
        {
            if (CanPresent(pendingRequests.Peek()))
                break;

            yield return null;
        }

        presentationRetryRoutine = null;
        TryPresentNext();
    }

    private readonly struct PendingRewardRequest
    {
        public readonly List<UpgradeEffectSO> upgradeEffects;
        public readonly List<AffectionEffect> affectionEffects;
        public readonly Action callback;
        public readonly bool allowDuringExternalUiInputBlock;
        public readonly UpgradeNodeSO upgradeNode;

        public PendingRewardRequest(
            List<UpgradeEffectSO> upgradeEffects,
            List<AffectionEffect> affectionEffects,
            Action callback,
            bool allowDuringExternalUiInputBlock,
            UpgradeNodeSO upgradeNode)
        {
            this.upgradeEffects = upgradeEffects;
            this.affectionEffects = affectionEffects;
            this.callback = callback;
            this.allowDuringExternalUiInputBlock = allowDuringExternalUiInputBlock;
            this.upgradeNode = upgradeNode;
        }
    }
}

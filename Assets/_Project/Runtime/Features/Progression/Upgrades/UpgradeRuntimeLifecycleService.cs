using System;
using UnityEngine.SceneManagement;

// 책임: 씬/런 생명주기에 맞춰 업그레이드 해금, 런 시작 효과, 런타임 효과 재적용을 관리한다.
internal sealed class UpgradeRuntimeLifecycleService
{
    private const string TitleSceneName = "TitleScene";
    private const string HubSceneName = "ProtoTypeHub";

    private readonly Action resolveUpgradeTreeUiReference;
    private readonly Action<bool> checkAndUnlockNodes;
    private readonly Action rebuildRunModifiers;
    private readonly Action resetAppliedPlayerEffects;
    private readonly Action reapplyRuntimeEffects;
    private readonly Func<UpgradeRunStartEffectRequest, bool> applyRunStartEffects;
    private readonly Func<bool> isRunActive;

    private bool hasAppliedRunStartEffectsForCurrentRun;
    private bool hasObservedSceneLoadForCurrentRun;
    private bool isSubscribed;

    public UpgradeRuntimeLifecycleService(
        Action resolveUpgradeTreeUiReference,
        Action<bool> checkAndUnlockNodes,
        Action rebuildRunModifiers,
        Action resetAppliedPlayerEffects,
        Action reapplyRuntimeEffects,
        Func<UpgradeRunStartEffectRequest, bool> applyRunStartEffects,
        Func<bool> isRunActive)
    {
        this.resolveUpgradeTreeUiReference = resolveUpgradeTreeUiReference;
        this.checkAndUnlockNodes = checkAndUnlockNodes;
        this.rebuildRunModifiers = rebuildRunModifiers;
        this.resetAppliedPlayerEffects = resetAppliedPlayerEffects;
        this.reapplyRuntimeEffects = reapplyRuntimeEffects;
        this.applyRunStartEffects = applyRunStartEffects;
        this.isRunActive = isRunActive;
    }

    public void Subscribe()
    {
        if (isSubscribed)
            return;

        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        RunSessionStore.OnRunStarted += HandleRunStarted;
        RunSessionStore.OnRunEnded += HandleRunEnded;

        isSubscribed = true;
    }

    public void Unsubscribe()
    {
        if (!isSubscribed)
            return;

        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        RunSessionStore.OnRunStarted -= HandleRunStarted;
        RunSessionStore.OnRunEnded -= HandleRunEnded;

        isSubscribed = false;
    }

    public void RunStartupFlow()
    {
        hasObservedSceneLoadForCurrentRun = IsRunActive();
        checkAndUnlockNodes?.Invoke(true);
        rebuildRunModifiers?.Invoke();
        reapplyRuntimeEffects?.Invoke();
        TryApplyRunStartEffects();
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        reapplyRuntimeEffects?.Invoke();
        TryApplyRunStartEffects();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        resolveUpgradeTreeUiReference?.Invoke();
        checkAndUnlockNodes?.Invoke(false);
        rebuildRunModifiers?.Invoke();
        resetAppliedPlayerEffects?.Invoke();

        if (IsRunActive())
            hasObservedSceneLoadForCurrentRun = true;

        reapplyRuntimeEffects?.Invoke();
        TryApplyRunStartEffects();
    }

    private void HandleRunStarted()
    {
        hasAppliedRunStartEffectsForCurrentRun = false;
        hasObservedSceneLoadForCurrentRun = IsActiveSceneRunContent();
        TryApplyRunStartEffects();
    }

    private void HandleRunEnded(RunEndReason reason)
    {
        hasAppliedRunStartEffectsForCurrentRun = false;
        hasObservedSceneLoadForCurrentRun = false;
    }

    private void TryApplyRunStartEffects()
    {
        bool applied = applyRunStartEffects != null && applyRunStartEffects(new UpgradeRunStartEffectRequest(
            hasAppliedRunStartEffectsForCurrentRun,
            IsRunActive(),
            hasObservedSceneLoadForCurrentRun,
            IsActiveSceneRunContent()));

        if (applied)
            hasAppliedRunStartEffectsForCurrentRun = true;
    }

    private bool IsRunActive()
    {
        return isRunActive != null && isRunActive();
    }

    private static bool IsActiveSceneRunContent()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
            return false;

        string sceneName = activeScene.name;
        return !string.Equals(sceneName, TitleSceneName, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sceneName, HubSceneName, StringComparison.OrdinalIgnoreCase);
    }
}

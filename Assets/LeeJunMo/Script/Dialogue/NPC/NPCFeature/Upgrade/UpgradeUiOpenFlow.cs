using System;
using System.Collections;
using UnityEngine;

internal sealed class UpgradeUiOpenFlow
{
    private readonly MonoBehaviour coroutineOwner;
    private readonly Func<UpgradeTreeUI> resolveUpgradeTreeUi;
    private Coroutine openPresentationRoutine;
    private GameFlowInputBlocker openPresentationInputBlocker;

    public UpgradeUiOpenFlow(
        MonoBehaviour coroutineOwner,
        Func<UpgradeTreeUI> resolveUpgradeTreeUi)
    {
        this.coroutineOwner = coroutineOwner;
        this.resolveUpgradeTreeUi = resolveUpgradeTreeUi;
    }

    public void Open(
        bool useFadePresentationOnOpen,
        float openFadeOutDuration,
        float openFadeInDuration)
    {
        UpgradeTreeUI upgradeTreeUI = ResolveUpgradeTreeUi();
        if (upgradeTreeUI == null || upgradeTreeUI.IsActive)
            return;

        if (UIManager.Instance != null && !UIManager.Instance.CanOpenUI(upgradeTreeUI))
            return;

        if (!useFadePresentationOnOpen)
        {
            OpenImmediate();
            return;
        }

        if (openPresentationRoutine != null || coroutineOwner == null)
            return;

        openPresentationRoutine = coroutineOwner.StartCoroutine(
            OpenWithFadePresentation(openFadeOutDuration, openFadeInDuration));
    }

    public void Close()
    {
        StopOpenPresentation();
        ReleaseOpenPresentationInputBlock();

        UpgradeTreeUI upgradeTreeUI = ResolveUpgradeTreeUi();
        if (upgradeTreeUI == null)
            return;

        if (UIManager.Instance != null)
            UIManager.Instance.PopUI(upgradeTreeUI);
        else
            upgradeTreeUI.CloseUI();
    }

    public void Cleanup()
    {
        StopOpenPresentation();
        ReleaseOpenPresentationInputBlock();
    }

    private IEnumerator OpenWithFadePresentation(
        float openFadeOutDuration,
        float openFadeInDuration)
    {
        AcquireOpenPresentationInputBlock();

        SceneFadeTransitionService fadeService = SceneFadeTransitionService.EnsureInstance(allowRuntimeFallback: true);
        bool hasFadeOverlay = fadeService != null && fadeService.TryBeginOverlayFadeSession(initialAlpha: 0f);

        try
        {
            if (hasFadeOverlay)
                yield return fadeService.FadeOutAsync(openFadeOutDuration);

            bool opened = OpenImmediate(openPresentationInputBlocker);

            if (hasFadeOverlay)
            {
                yield return null;
                yield return fadeService.FadeInAsync(opened ? openFadeInDuration : openFadeOutDuration);
            }
        }
        finally
        {
            if (hasFadeOverlay)
                fadeService.EndOverlayFadeSession();

            ReleaseOpenPresentationInputBlock();
            openPresentationRoutine = null;
        }
    }

    private bool OpenImmediate(GameFlowInputBlocker inputBlocker = null)
    {
        UpgradeTreeUI upgradeTreeUI = ResolveUpgradeTreeUi();
        if (upgradeTreeUI == null)
            return false;

        if (upgradeTreeUI.IsActive)
            return true;

        if (UIManager.Instance != null)
        {
            return inputBlocker != null
                ? inputBlocker.TryPushOwnedUI(upgradeTreeUI)
                : UIManager.Instance.TryPushUI(upgradeTreeUI);
        }

        upgradeTreeUI.OpenUI();
        return true;
    }

    private UpgradeTreeUI ResolveUpgradeTreeUi()
    {
        return resolveUpgradeTreeUi != null ? resolveUpgradeTreeUi() : null;
    }

    private void AcquireOpenPresentationInputBlock()
    {
        if (openPresentationInputBlocker != null && openPresentationInputBlocker.IsBlocking)
            return;

        openPresentationInputBlocker = GameFlowInputBlocker.GetOrAdd(coroutineOwner);
        openPresentationInputBlocker?.Acquire();
    }

    private void ReleaseOpenPresentationInputBlock()
    {
        openPresentationInputBlocker?.Release();
    }

    private void StopOpenPresentation()
    {
        if (openPresentationRoutine == null)
            return;

        if (coroutineOwner != null)
            coroutineOwner.StopCoroutine(openPresentationRoutine);

        openPresentationRoutine = null;
        SceneFadeTransitionService.Instance?.EndOverlayFadeSession();
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardDisplayUI : MonoBehaviour, IStackableUI
{
    public static RewardDisplayUI Instance { get; private set; }

    [Header("UI Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contextText;

    [Header("Presentation")]
    [SerializeField] private Graphic dimPanelGraphic;
    [SerializeField] private UISlideFadePresentation titleTextPresentation;
    [SerializeField] private UISlideFadePresentation rewardContentPresentation;
    [SerializeField] private UISlideFadePresentation contextTextPresentation;
    [SerializeField] private UISlideFadePresentation closeButtonPresentation;
    [SerializeField, Min(0f)] private float dimFadeInDuration = 0.08f;
    [SerializeField, Min(0f)] private float dimFadeOutDuration = 0.08f;

    [Header("Slot Prefabs")]
    [SerializeField] private GameObject unlockSlotPrefab;
    [SerializeField] private GameObject effectSlotPrefab;
    [SerializeField] private Transform slotParent;

    private Action onCloseCallback;
    private Coroutine presentationRoutine;
    private float dimPanelOpenAlpha = 1f;
    private bool hasDimPanelOpenAlpha;
    private bool isClosing;
    private GameFlowInputBlocker openPresentationInputBlocker;

    public bool IsActive => panelRoot != null && panelRoot.activeSelf;
    public bool CanCloseOnEscape => true;
    public UIOpenGroup OpenGroup => UIOpenGroup.Overlay;
    public UIOpenGroup BlockedOpenGroups => UIOpenGroup.None;
    public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.FreezeAndBlockControl;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        GlobalUIRoot.AdoptToCanvas(GlobalCanvasLayer.Reward, transform);
        ResolvePresentationReferences();
        CaptureDimPanelOpenAlpha();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        RewardDisplayService.Instance?.RegisterView(this);
    }

    private void OnDestroy()
    {
        ReleaseOpenPresentationInputBlock();
        RewardDisplayService.Instance?.UnregisterView(this);

        if (Instance == this)
            Instance = null;
    }

    private void OnDisable()
    {
        ReleaseOpenPresentationInputBlock();
    }

    public void OpenUI()
    {
        isClosing = false;
        StopPresentationRoutine();
        ResolvePresentationReferences();

        if (panelRoot != null)
            panelRoot.SetActive(true);

        PrepareContentForOpenPresentation();
        AcquireOpenPresentationInputBlock();
        presentationRoutine = StartCoroutine(PlayOpenPresentation());
    }

    public void CloseUI()
    {
        if (isClosing)
            return;

        ResolvePresentationReferences();
        StopPresentationRoutine();

        if (panelRoot == null || !panelRoot.activeSelf)
        {
            FinishClose();
            return;
        }

        isClosing = true;
        presentationRoutine = StartCoroutine(PlayClosePresentation());
    }

    public bool ShowReward(
        List<UpgradeEffectSO> upgradeEffects = null,
        List<AffectionEffect> affectionEffects = null,
        Action callback = null,
        bool allowDuringExternalUiInputBlock = false)
    {
        onCloseCallback = callback;

        if (slotParent != null)
        {
            foreach (Transform child in slotParent)
                Destroy(child.gameObject);
        }

        string summary = string.Empty;

        if (upgradeEffects != null && upgradeEffects.Count > 0)
        {
            if (titleText != null)
                titleText.text = "업그레이드 완료!";

            foreach (UpgradeEffectSO effect in upgradeEffects)
                ProcessUpgrade(effect, ref summary);
        }
        else if (affectionEffects != null && affectionEffects.Count > 0)
        {
            if (titleText != null)
                titleText.text = "호감도 보상!";

            foreach (AffectionEffect effect in affectionEffects)
                ProcessAffection(effect, ref summary);
        }

        if (contextText != null)
            contextText.text = summary.TrimEnd();

        RebuildRewardLayout();

        bool opened;
        if (UIManager.Instance != null)
        {
            opened = allowDuringExternalUiInputBlock
                ? UIManager.Instance.TryPushFlowOwnedUI(this)
                : UIManager.Instance.TryPushUI(this);
        }
        else
        {
            OpenUI();
            opened = true;
        }

        if (!opened)
            onCloseCallback = null;

        return opened;
    }

    public void Close()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.PopUI(this);
        else
            CloseUI();
    }

    private void ProcessUpgrade(UpgradeEffectSO effect, ref string summary)
    {
        if (effect == null)
            return;

        if (effect is ItemUnlockUpgradeEffectSO unlockEffect)
        {
            if (unlockEffect.Weapons != null)
            {
                foreach (var weapon in unlockEffect.Weapons)
                    CreateUnlockSlot(weapon);
            }

            if (unlockEffect.Relics != null)
            {
                foreach (var relic in unlockEffect.Relics)
                    CreateUnlockSlot(relic);
            }
        }
        else if (effect.rewardIcon != null)
        {
            CreateEffectSlot(effect.rewardIcon);
        }

        if (!string.IsNullOrEmpty(effect.rewardText))
            summary += $"- {effect.rewardText}\n";
    }

    private void ProcessAffection(AffectionEffect effect, ref string summary)
    {
        if (effect == null)
            return;

        if (effect is UnlockItemAffectionEffect unlockEffect)
        {
            foreach (var weapon in unlockEffect.weapons)
                CreateUnlockSlot(weapon);

            foreach (var relic in unlockEffect.relics)
                CreateUnlockSlot(relic);
        }
        else if (effect.rewardIcon != null)
        {
            CreateEffectSlot(effect.rewardIcon);
        }

        if (!string.IsNullOrEmpty(effect.rewardText))
            summary += $"- {effect.rewardText}\n";
    }

    private void CreateUnlockSlot(ScriptableObject definition)
    {
        if (unlockSlotPrefab == null || slotParent == null)
            return;

        Instantiate(unlockSlotPrefab, slotParent).GetComponent<UnlockSlotUI>().Setup(definition);
    }

    private void CreateEffectSlot(Sprite icon)
    {
        if (effectSlotPrefab == null || slotParent == null)
            return;

        Instantiate(effectSlotPrefab, slotParent).GetComponent<RewardEffectSlotUI>().Setup(icon);
    }

    private IEnumerator PlayOpenPresentation()
    {
        try
        {
            SetDimPanelAlpha(0f);
            yield return FadeDimPanel(dimPanelOpenAlpha, dimFadeInDuration, EaseOutCubic);
            yield return PlayContentOpenPresentation();
        }
        finally
        {
            ReleaseOpenPresentationInputBlock();
            presentationRoutine = null;
        }
    }

    private IEnumerator PlayClosePresentation()
    {
        yield return PlayContentClosePresentation();
        yield return FadeDimPanel(0f, dimFadeOutDuration, EaseInCubic);
        presentationRoutine = null;
        FinishClose();
    }

    private IEnumerator PlayContentOpenPresentation()
    {
        int pending = 0;

        if (titleTextPresentation != null)
        {
            pending++;
            titleTextPresentation.PlayOpen(() => pending--);
        }

        if (rewardContentPresentation != null)
        {
            pending++;
            rewardContentPresentation.PlayOpen(() => pending--);
        }

        if (contextTextPresentation != null)
        {
            pending++;
            contextTextPresentation.PlayOpen(() => pending--);
        }

        if (closeButtonPresentation != null)
        {
            pending++;
            closeButtonPresentation.PlayOpen(() => pending--);
        }

        while (pending > 0)
            yield return null;
    }

    private IEnumerator PlayContentClosePresentation()
    {
        int pending = 0;

        if (titleTextPresentation != null)
        {
            pending++;
            titleTextPresentation.PlayClose(() => pending--);
        }

        if (rewardContentPresentation != null)
        {
            pending++;
            rewardContentPresentation.PlayClose(() => pending--);
        }

        if (contextTextPresentation != null)
        {
            pending++;
            contextTextPresentation.PlayClose(() => pending--);
        }

        if (closeButtonPresentation != null)
        {
            pending++;
            closeButtonPresentation.PlayClose(() => pending--);
        }

        while (pending > 0)
            yield return null;
    }

    private void PrepareContentForOpenPresentation()
    {
        RestoreContentOpenPoseForLayout(titleTextPresentation);
        RestoreContentOpenPoseForLayout(rewardContentPresentation);
        RestoreContentOpenPoseForLayout(contextTextPresentation);
        RestoreContentOpenPoseForLayout(closeButtonPresentation);
        RebuildRewardLayout();
        SnapContentClosedForOpen(titleTextPresentation);
        SnapContentClosedForOpen(rewardContentPresentation);
        SnapContentClosedForOpen(contextTextPresentation);
        SnapContentClosedForOpen(closeButtonPresentation);
    }

    private static void RestoreContentOpenPoseForLayout(UISlideFadePresentation presentation)
    {
        if (presentation == null)
            return;

        presentation.SnapOpen();
        presentation.ClearCapturedOpenPosition();
    }

    private static void SnapContentClosedForOpen(UISlideFadePresentation presentation)
    {
        if (presentation == null)
            return;

        presentation.ClearCapturedOpenPosition();
        presentation.SnapClosed(false);
    }

    private IEnumerator FadeDimPanel(float targetAlpha, float duration, Func<float, float> ease)
    {
        if (dimPanelGraphic == null)
            yield break;

        float startAlpha = dimPanelGraphic.color.a;
        if (duration <= 0f)
        {
            SetDimPanelAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = ease != null ? ease(t) : t;
            SetDimPanelAlpha(Mathf.LerpUnclamped(startAlpha, targetAlpha, eased));
            yield return null;
        }

        SetDimPanelAlpha(targetAlpha);
    }

    private void SetDimPanelAlpha(float alpha)
    {
        if (dimPanelGraphic == null)
            return;

        Color color = dimPanelGraphic.color;
        color.a = alpha;
        dimPanelGraphic.color = color;
    }

    private void FinishClose()
    {
        StopPresentationRoutine();
        isClosing = false;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        onCloseCallback?.Invoke();
        onCloseCallback = null;
        RewardDisplayService.Instance?.NotifyClosed(this);
    }

    private void StopPresentationRoutine()
    {
        ReleaseOpenPresentationInputBlock();

        if (presentationRoutine == null)
            return;

        StopCoroutine(presentationRoutine);
        presentationRoutine = null;
    }

    private void AcquireOpenPresentationInputBlock()
    {
        if (openPresentationInputBlocker != null && openPresentationInputBlocker.IsBlocking)
            return;

        openPresentationInputBlocker = GameFlowInputBlocker.GetOrAdd(this);
        openPresentationInputBlocker?.Acquire();
    }

    private void ReleaseOpenPresentationInputBlock()
    {
        openPresentationInputBlocker?.Release();
    }

    private void ResolvePresentationReferences()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (dimPanelGraphic == null && panelRoot != null)
            dimPanelGraphic = panelRoot.GetComponent<Graphic>();

        if (titleTextPresentation == null && titleText != null)
            titleTextPresentation = ResolveSlideFadePresentation(titleText.transform as RectTransform);

        if (rewardContentPresentation == null)
        {
            RectTransform contentRoot = ResolveRewardContentRoot();
            rewardContentPresentation = ResolveSlideFadePresentation(contentRoot);
        }

        if (contextTextPresentation == null && contextText != null)
            contextTextPresentation = ResolveSlideFadePresentation(contextText.transform as RectTransform);

        if (closeButtonPresentation == null)
        {
            RectTransform closeButtonRoot = ResolveCloseButtonRoot();
            closeButtonPresentation = ResolveSlideFadePresentation(closeButtonRoot);
        }
    }

    private UISlideFadePresentation ResolveSlideFadePresentation(RectTransform target)
    {
        if (target == null)
            return null;

        UISlideFadePresentation presentation = target.GetComponent<UISlideFadePresentation>();
        if (presentation == null)
            presentation = target.gameObject.AddComponent<UISlideFadePresentation>();

        presentation.Configure(target);
        presentation.DeactivateAfterClose = false;
        return presentation;
    }

    private RectTransform ResolveRewardContentRoot()
    {
        RectTransform contentRoot = ResolveDirectChildContaining(slotParent);
        if (contentRoot != null)
            return contentRoot;

        if (panelRoot == null)
            return null;

        Transform namedChild = panelRoot.transform.Find("RewardUI");
        return namedChild as RectTransform;
    }

    private RectTransform ResolveCloseButtonRoot()
    {
        if (panelRoot == null)
            return null;

        Button[] buttons = panelRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button != null && button.transform.parent == panelRoot.transform)
                return button.transform as RectTransform;
        }

        return buttons.Length > 0 ? buttons[0].transform as RectTransform : null;
    }

    private RectTransform ResolveDirectChildContaining(Transform child)
    {
        if (child == null || panelRoot == null)
            return null;

        Transform current = child;
        while (current != null && current.parent != panelRoot.transform)
            current = current.parent;

        return current as RectTransform;
    }

    private void CaptureDimPanelOpenAlpha()
    {
        if (hasDimPanelOpenAlpha || dimPanelGraphic == null)
            return;

        dimPanelOpenAlpha = dimPanelGraphic.color.a;
        hasDimPanelOpenAlpha = true;
    }

    private void RebuildRewardLayout()
    {
        Canvas.ForceUpdateCanvases();

        if (slotParent is RectTransform slotRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(slotRect);

        if (panelRoot != null && panelRoot.transform is RectTransform panelRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
    }

    private static float EaseOutCubic(float t)
    {
        t = 1f - Mathf.Clamp01(t);
        return 1f - t * t * t;
    }

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }
}

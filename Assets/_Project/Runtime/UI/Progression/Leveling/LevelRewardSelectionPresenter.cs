using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임: 제작된 레벨 보상 선택창을 세션 컨트롤러 이벤트에 연결하고 1~3개 후보와 UI 명령을 투영한다.
/// 이 컴포넌트는 비활성 panelRoot 바깥의 항상 활성인 UI 호스트에 배치해야 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelRewardSelectionPresenter : MonoBehaviour, IStackableUI, ICloseRequestHandler
{
    private const int MaximumCardCount = 3;
    private const KeyCode RerollKey = KeyCode.R;

    [Serializable]
    private sealed class CardSlotBinding
    {
        [SerializeField] private GameObject slotRoot;
        [SerializeField] private LevelRewardCardView cardView;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private CanvasGroup visualCanvasGroup;
        [SerializeField] private GameObject frontRoot;
        [SerializeField] private GameObject backRoot;

        [NonSerialized] private bool authoredStateCaptured;
        [NonSerialized] private Vector2 authoredPosition;
        [NonSerialized] private Vector3 authoredScale = Vector3.one;

        public GameObject SlotRoot => slotRoot;
        public LevelRewardCardView CardView => cardView;
        public bool IsConfigured => slotRoot != null &&
                                    cardView != null &&
                                    visualRoot != null &&
                                    visualCanvasGroup != null &&
                                    frontRoot != null &&
                                    backRoot != null;
        public bool IsVisible => IsConfigured && slotRoot.activeSelf;

        public void CaptureAuthoredState()
        {
            if (authoredStateCaptured || visualRoot == null)
                return;

            authoredPosition = visualRoot.anchoredPosition;
            authoredScale = visualRoot.localScale;
            authoredStateCaptured = true;
        }

        public void ResetVisual(bool showFront)
        {
            CaptureAuthoredState();
            if (visualRoot != null)
            {
                visualRoot.anchoredPosition = authoredPosition;
                visualRoot.localScale = authoredScale;
            }

            SetAlpha(1f);
            SetFace(showFront);
        }

        public void SetVerticalOffset(float offset)
        {
            CaptureAuthoredState();
            if (visualRoot != null)
                visualRoot.anchoredPosition = authoredPosition + Vector2.up * offset;
        }

        public void SetHorizontalScale(float normalizedScale)
        {
            CaptureAuthoredState();
            if (visualRoot == null)
                return;

            Vector3 scale = authoredScale;
            scale.x *= Mathf.Clamp01(normalizedScale);
            visualRoot.localScale = scale;
        }

        public void SetAlpha(float alpha)
        {
            if (visualCanvasGroup != null)
                visualCanvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        public void SetFace(bool showFront)
        {
            if (frontRoot != null)
                frontRoot.SetActive(showFront);
            if (backRoot != null)
                backRoot.SetActive(!showFront);
        }
    }

    private readonly struct OfferPresentationIdentity : IEquatable<OfferPresentationIdentity>
    {
        public OfferPresentationIdentity(int seed, int sequence, int rerollsUsed)
        {
            Seed = seed;
            Sequence = sequence;
            RerollsUsed = rerollsUsed;
        }

        private int Seed { get; }
        private int Sequence { get; }
        private int RerollsUsed { get; }

        public bool Equals(OfferPresentationIdentity other)
        {
            return Seed == other.Seed &&
                   Sequence == other.Sequence &&
                   RerollsUsed == other.RerollsUsed;
        }
    }

    [Header("Session")]
    [SerializeField] private LevelRewardSessionController sessionController;

    [Header("Window")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private CanvasGroup inputBlockerCanvasGroup;
    [SerializeField] private CanvasGroup presentationCanvasGroup;

    [Header("Cards (1-3)")]
    [SerializeField] private List<CardSlotBinding> cardSlots = new();
    [SerializeField] private Sprite fallbackIcon;

    [Header("Controls")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button rerollButton;
    [SerializeField] private TMP_Text rerollCountText;
    [SerializeField] private TMP_Text pendingRewardCountText;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Card Enter Presentation")]
    [SerializeField, Min(0f)] private float cardEnterDuration = 0.2f;
    [SerializeField, Min(0f)] private float cardEnterStagger = 0.06f;
    [SerializeField, Min(0f)] private float cardEnterDistance = 140f;
    [SerializeField, Min(0f)] private float cardEnterOvershoot = 24f;

    [Header("Card Flip Presentation")]
    [SerializeField, Min(0f)] private float cardFlipHalfDuration = 0.1f;

    [Header("Close Presentation")]
    [SerializeField, Min(0f)] private float cardExitDuration = 0.16f;
    [SerializeField, Min(0f)] private float cardExitDistance = 48f;
    [SerializeField, Min(0f)] private float windowFadeDuration = 0.25f;

    private readonly List<CardSlotBinding> visibleCards = new(MaximumCardCount);
    private Coroutine transitionRoutine;
    private bool isApplyingSelection;
    private bool isTransitioning;
    private bool isClosing;
    private bool bypassCloseRequest;
    private bool hasRevealedOffer;
    private OfferPresentationIdentity lastRevealedOffer;
    private int sessionOpenedFrame = -1;

    public bool IsActive => panelRoot != null && panelRoot.activeSelf;
    public bool CanCloseOnEscape => !isApplyingSelection && !isTransitioning && !isClosing;
    public UIOpenGroup OpenGroup => UIOpenGroup.ExclusiveModal;
    public UIOpenGroup BlockedOpenGroups => UIOpenGroup.ExclusiveModal | UIOpenGroup.Overlay;
    public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.FreezeAndBlockControl;

    private void Awake()
    {
        CaptureCardAuthoredStates();
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        SubscribeSession();
        closeButton?.onClick.AddListener(RequestClose);
        rerollButton?.onClick.AddListener(RequestReroll);
    }

    private void OnDisable()
    {
        UnsubscribeSession();
        closeButton?.onClick.RemoveListener(RequestClose);
        rerollButton?.onClick.RemoveListener(RequestReroll);
        StopTransition(resetVisuals: true);
    }

    private void Update()
    {
        if (!IsActive ||
            sessionController == null ||
            !sessionController.IsSessionOpen ||
            isApplyingSelection ||
            isTransitioning ||
            isClosing ||
            Time.frameCount == sessionOpenedFrame)
        {
            return;
        }

        if (InputActionQuery.WasKeyPressedThisFrame(RerollKey))
        {
            RequestReroll();
            return;
        }

        if (InputActionQuery.WasKeyPressedThisFrame(KeyCode.Alpha1))
            RequestSelectByIndex(0);
        else if (InputActionQuery.WasKeyPressedThisFrame(KeyCode.Alpha2))
            RequestSelectByIndex(1);
        else if (InputActionQuery.WasKeyPressedThisFrame(KeyCode.Alpha3))
            RequestSelectByIndex(2);
    }

    public void OpenUI()
    {
        if (panelRoot == null)
        {
            Debug.LogError("[LevelRewardSelectionPresenter] Panel root is not assigned.", this);
            sessionController?.CloseSession();
            return;
        }

        StopTransition(resetVisuals: true);
        isClosing = false;
        bypassCloseRequest = false;
        panelRoot.SetActive(true);
        ResetWindowVisuals();
        RenderSession();

        OfferPresentationIdentity identity = GetCurrentOfferIdentity();
        bool shouldFlip = !IsOfferRevealed(identity);
        BeginTransition(AnimateOpenSequence(identity, shouldFlip));
    }

    public void CloseUI()
    {
        StopTransition(resetVisuals: true);
        ClearCards();
        ClearFeedback();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (sessionController != null && sessionController.IsSessionOpen)
            sessionController.CloseSession();

        ResetWindowVisuals();
        isApplyingSelection = false;
        isTransitioning = false;
        isClosing = false;
        bypassCloseRequest = false;
    }

    public bool TryHandleCloseRequest()
    {
        if (bypassCloseRequest)
        {
            bypassCloseRequest = false;
            return false;
        }

        if (!IsActive)
            return false;

        if (isApplyingSelection || isTransitioning || isClosing)
            return true;

        BeginFullClose();
        return true;
    }

    private void SubscribeSession()
    {
        if (sessionController == null)
            return;

        sessionController.SessionOpened += HandleSessionOpened;
        sessionController.SessionChanged += HandleSessionChanged;
        sessionController.SessionClosed += HandleSessionClosed;
    }

    private void UnsubscribeSession()
    {
        if (sessionController == null)
            return;

        sessionController.SessionOpened -= HandleSessionOpened;
        sessionController.SessionChanged -= HandleSessionChanged;
        sessionController.SessionClosed -= HandleSessionClosed;
    }

    private void HandleSessionOpened()
    {
        sessionOpenedFrame = Time.frameCount;
        if (!CanPresentCurrentCandidates(out string failureReason))
        {
            Debug.LogError($"[LevelRewardSelectionPresenter] {failureReason}", this);
            sessionController.CloseSession();
            return;
        }

        if (!sessionController.TryPushSessionUI(this))
        {
            Debug.LogError("[LevelRewardSelectionPresenter] Failed to push the selection window to the UI stack.", this);
            sessionController.CloseSession();
        }
    }

    private void HandleSessionChanged()
    {
        if (IsActive && !isTransitioning && !isApplyingSelection && !isClosing)
            RenderSession();
    }

    private void HandleSessionClosed()
    {
        sessionOpenedFrame = -1;
        if (isClosing)
            return;

        StopTransition(resetVisuals: true);
        bypassCloseRequest = true;
        if (UIManager.Instance != null && IsActive)
        {
            UIManager.Instance.PopUI(this);
            if (IsActive)
                CloseUI();
        }
        else
        {
            bypassCloseRequest = false;
            CloseUI();
        }
    }

    private bool CanPresentCurrentCandidates(out string failureReason)
    {
        failureReason = null;
        if (panelRoot == null)
        {
            failureReason = "Panel root is not assigned.";
            return false;
        }

        int candidateCount = sessionController?.Candidates?.Count ?? 0;
        if (candidateCount <= 0 || candidateCount > MaximumCardCount)
        {
            failureReason = $"Candidate count must be between 1 and {MaximumCardCount}. current={candidateCount}";
            return false;
        }

        if (cardSlots == null || cardSlots.Count < candidateCount)
        {
            failureReason = $"At least {candidateCount} authored card slots are required.";
            return false;
        }

        for (int i = 0; i < candidateCount; i++)
        {
            if (cardSlots[i] == null || !cardSlots[i].IsConfigured)
            {
                failureReason = $"Card slot {i + 1} is not fully configured.";
                return false;
            }
        }

        return true;
    }

    private void RenderSession()
    {
        if (sessionController == null)
            return;

        IReadOnlyList<LevelRewardDefinitionSO> candidates = sessionController.Candidates;
        int candidateCount = Mathf.Min(candidates?.Count ?? 0, MaximumCardCount);
        int authoredSlotCount = cardSlots?.Count ?? 0;

        for (int i = 0; i < authoredSlotCount; i++)
        {
            CardSlotBinding slot = cardSlots[i];
            if (slot == null)
                continue;

            bool shouldShow = i < candidateCount && slot.IsConfigured;
            if (slot.SlotRoot != null)
                slot.SlotRoot.SetActive(shouldShow);

            if (shouldShow)
                slot.CardView.Bind(candidates[i], i, fallbackIcon, RequestSelect);
            else
                slot.CardView?.Clear();
        }

        int remainingRerolls = Mathf.Max(0, sessionController.MaxRerolls - sessionController.RerollsUsed);
        if (rerollCountText != null)
            rerollCountText.text = $"[R] 리롤 {remainingRerolls}/{sessionController.MaxRerolls}";
        if (pendingRewardCountText != null)
            pendingRewardCountText.text = Mathf.Max(0, sessionController.PendingRewardCount).ToString();

        RefreshInteractionState();
    }

    private void RequestSelectByIndex(int index)
    {
        IReadOnlyList<LevelRewardDefinitionSO> candidates = sessionController?.Candidates;
        if (candidates == null || index < 0 || index >= candidates.Count || index >= MaximumCardCount)
            return;

        LevelRewardDefinitionSO definition = candidates[index];
        if (definition != null)
            RequestSelect(definition.RewardId);
    }

    private void RequestSelect(string rewardId)
    {
        if (isApplyingSelection || isTransitioning || isClosing ||
            sessionController == null || string.IsNullOrWhiteSpace(rewardId))
        {
            return;
        }

        isApplyingSelection = true;
        SetTransitioning(true);
        ClearFeedback();

        bool selected = sessionController.TrySelectCandidate(rewardId, out string failureReason);
        if (!selected)
        {
            isApplyingSelection = false;
            SetTransitioning(false);
            ShowFeedback(failureReason);
            return;
        }

        if (sessionController.IsSessionOpen && sessionController.PendingRewardCount > 0)
            BeginTransition(AnimateNextOffer());
        else
            BeginFullClose();
    }

    private void RequestReroll()
    {
        if (isApplyingSelection || isTransitioning || isClosing ||
            sessionController == null || !sessionController.IsSessionOpen)
        {
            return;
        }

        if (!sessionController.CanReroll)
        {
            if (!sessionController.TryReroll(out string unavailableReason))
                ShowFeedback(unavailableReason);
            return;
        }

        SetTransitioning(true);
        ClearFeedback();
        BeginTransition(AnimateReroll());
    }

    private void RequestClose()
    {
        if (isApplyingSelection || isTransitioning || isClosing)
            return;

        if (UIManager.Instance != null && IsActive)
            UIManager.Instance.PopUI(this);
        else
            BeginFullClose();
    }

    private IEnumerator AnimateOpenSequence(OfferPresentationIdentity identity, bool shouldFlip)
    {
        PrepareVisibleCardsForEntrance(showFront: !shouldFlip);
        yield return AnimateCardsIn();

        if (shouldFlip)
            yield return AnimateVisibleCardFlips();

        MarkOfferRevealed(identity);
        FinishCardTransition();
    }

    private IEnumerator AnimateNextOffer()
    {
        yield return AnimateCardsOut();
        RenderSession();

        OfferPresentationIdentity identity = GetCurrentOfferIdentity();
        bool shouldFlip = !IsOfferRevealed(identity);
        PrepareVisibleCardsForEntrance(showFront: !shouldFlip);
        yield return AnimateCardsIn();

        if (shouldFlip)
            yield return AnimateVisibleCardFlips();

        MarkOfferRevealed(identity);
        FinishCardTransition();
    }

    private IEnumerator AnimateReroll()
    {
        yield return AnimateCardsOut();

        bool rerolled = sessionController.TryReroll(out string failureReason);
        RenderSession();

        OfferPresentationIdentity identity = GetCurrentOfferIdentity();
        bool shouldFlip = rerolled && !IsOfferRevealed(identity);
        PrepareVisibleCardsForEntrance(showFront: !shouldFlip);
        yield return AnimateCardsIn();

        if (shouldFlip)
            yield return AnimateVisibleCardFlips();

        if (rerolled)
            MarkOfferRevealed(identity);
        else
            ShowFeedback(failureReason);

        FinishCardTransition();
    }

    private IEnumerator AnimateCardsIn()
    {
        CollectVisibleCards();
        int count = visibleCards.Count;
        if (count == 0)
            yield break;

        float enterDuration = Mathf.Max(0f, cardEnterDuration);
        float stagger = Mathf.Max(0f, cardEnterStagger);
        if (enterDuration <= 0f)
        {
            for (int i = 0; i < count; i++)
                visibleCards[i].SetVerticalOffset(0f);
            yield break;
        }

        float totalDuration = enterDuration + stagger * Mathf.Max(0, count - 1);
        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            for (int i = 0; i < count; i++)
            {
                float localTime = elapsed - stagger * i;
                float progress = Mathf.Clamp01(localTime / enterDuration);
                visibleCards[i].SetVerticalOffset(EvaluateCardEnterOffset(progress));
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
            visibleCards[i].SetVerticalOffset(0f);
    }

    private float EvaluateCardEnterOffset(float progress)
    {
        const float risePortion = 0.72f;
        if (progress <= risePortion)
        {
            float riseProgress = progress / risePortion;
            return Mathf.Lerp(-Mathf.Max(0f, cardEnterDistance), Mathf.Max(0f, cardEnterOvershoot), EaseOutCubic(riseProgress));
        }

        float settleProgress = (progress - risePortion) / (1f - risePortion);
        return Mathf.Lerp(Mathf.Max(0f, cardEnterOvershoot), 0f, EaseInOutQuad(settleProgress));
    }

    private IEnumerator AnimateVisibleCardFlips()
    {
        CollectVisibleCards();
        for (int i = 0; i < visibleCards.Count; i++)
            yield return AnimateCardFlip(visibleCards[i]);
    }

    private IEnumerator AnimateCardFlip(CardSlotBinding slot)
    {
        float halfDuration = Mathf.Max(0f, cardFlipHalfDuration);
        if (halfDuration <= 0f)
        {
            slot.SetFace(true);
            slot.SetHorizontalScale(1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / halfDuration);
            slot.SetHorizontalScale(1f - EaseInCubic(progress));
            yield return null;
        }

        slot.SetHorizontalScale(0f);
        slot.SetFace(true);
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / halfDuration);
            slot.SetHorizontalScale(EaseOutCubic(progress));
            yield return null;
        }

        slot.SetHorizontalScale(1f);
    }

    private IEnumerator AnimateCardsOut()
    {
        CollectVisibleCards();
        float duration = Mathf.Max(0f, cardExitDuration);
        if (duration <= 0f)
        {
            for (int i = 0; i < visibleCards.Count; i++)
            {
                visibleCards[i].SetVerticalOffset(-Mathf.Max(0f, cardExitDistance));
                visibleCards[i].SetAlpha(0f);
            }
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = EaseInCubic(progress);
            for (int i = 0; i < visibleCards.Count; i++)
            {
                visibleCards[i].SetVerticalOffset(-Mathf.Max(0f, cardExitDistance) * eased);
                visibleCards[i].SetAlpha(1f - eased);
            }

            yield return null;
        }
    }

    private void BeginFullClose()
    {
        if (isClosing || transitionRoutine != null)
            return;

        isClosing = true;
        SetTransitioning(true);
        BeginTransition(AnimateFullClose());
    }

    private IEnumerator AnimateFullClose()
    {
        CollectVisibleCards();
        float cardsDuration = Mathf.Max(0f, cardExitDuration);
        float fadeDuration = Mathf.Max(0f, windowFadeDuration);
        float totalDuration = Mathf.Max(cardsDuration, fadeDuration);

        if (totalDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float cardProgress = cardsDuration > 0f ? Mathf.Clamp01(elapsed / cardsDuration) : 1f;
                float cardEased = EaseInCubic(cardProgress);
                for (int i = 0; i < visibleCards.Count; i++)
                {
                    visibleCards[i].SetVerticalOffset(-Mathf.Max(0f, cardExitDistance) * cardEased);
                    visibleCards[i].SetAlpha(1f - cardEased);
                }

                float fadeProgress = fadeDuration > 0f ? Mathf.Clamp01(elapsed / fadeDuration) : 1f;
                float fadeAlpha = 1f - EaseInOutQuad(fadeProgress);
                if (presentationCanvasGroup != null)
                    presentationCanvasGroup.alpha = fadeAlpha;
                if (inputBlockerCanvasGroup != null)
                    inputBlockerCanvasGroup.alpha = fadeAlpha;

                yield return null;
            }
        }

        if (presentationCanvasGroup != null)
            presentationCanvasGroup.alpha = 0f;
        if (inputBlockerCanvasGroup != null)
            inputBlockerCanvasGroup.alpha = 0f;

        transitionRoutine = null;
        CompleteAnimatedClose();
    }

    private void CompleteAnimatedClose()
    {
        bypassCloseRequest = true;
        if (UIManager.Instance != null && IsActive)
        {
            UIManager.Instance.PopUI(this);
            if (IsActive)
                CloseUI();
        }
        else
        {
            bypassCloseRequest = false;
            CloseUI();
        }
    }

    private void PrepareVisibleCardsForEntrance(bool showFront)
    {
        CollectVisibleCards();
        for (int i = 0; i < visibleCards.Count; i++)
        {
            visibleCards[i].ResetVisual(showFront);
            visibleCards[i].SetVerticalOffset(-Mathf.Max(0f, cardEnterDistance));
        }
    }

    private void CollectVisibleCards()
    {
        visibleCards.Clear();
        if (cardSlots == null)
            return;

        for (int i = 0; i < cardSlots.Count; i++)
        {
            CardSlotBinding slot = cardSlots[i];
            if (slot != null && slot.IsVisible)
                visibleCards.Add(slot);
        }
    }

    private void CaptureCardAuthoredStates()
    {
        if (cardSlots == null)
            return;

        for (int i = 0; i < cardSlots.Count; i++)
            cardSlots[i]?.CaptureAuthoredState();
    }

    private void ResetWindowVisuals()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;
        }

        if (inputBlockerCanvasGroup != null)
            inputBlockerCanvasGroup.alpha = 1f;
        if (presentationCanvasGroup != null)
            presentationCanvasGroup.alpha = 1f;

        if (cardSlots == null)
            return;

        for (int i = 0; i < cardSlots.Count; i++)
            cardSlots[i]?.ResetVisual(showFront: true);
    }

    private void SetTransitioning(bool transitioning)
    {
        isTransitioning = transitioning;
        RefreshInteractionState();
    }

    private void FinishCardTransition()
    {
        transitionRoutine = null;
        isApplyingSelection = false;
        SetTransitioning(false);
    }

    private void RefreshInteractionState()
    {
        bool interactable = IsActive &&
                            !isApplyingSelection &&
                            !isTransitioning &&
                            !isClosing &&
                            sessionController != null &&
                            sessionController.IsSessionOpen;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = IsActive;
        }

        SetCardInteraction(interactable);
        if (closeButton != null)
            closeButton.interactable = interactable;
        if (rerollButton != null)
            rerollButton.interactable = interactable && sessionController.CanReroll;
    }

    private void SetCardInteraction(bool interactable)
    {
        if (cardSlots == null)
            return;

        for (int i = 0; i < cardSlots.Count; i++)
        {
            CardSlotBinding slot = cardSlots[i];
            if (slot?.SlotRoot != null && slot.SlotRoot.activeSelf)
                slot.CardView?.SetInteractable(interactable);
        }
    }

    private void ClearCards()
    {
        if (cardSlots == null)
            return;

        for (int i = 0; i < cardSlots.Count; i++)
        {
            CardSlotBinding slot = cardSlots[i];
            slot?.CardView?.Clear();
            slot?.ResetVisual(showFront: true);
            if (slot?.SlotRoot != null)
                slot.SlotRoot.SetActive(false);
        }
    }

    private OfferPresentationIdentity GetCurrentOfferIdentity()
    {
        return new OfferPresentationIdentity(
            sessionController?.OfferSeed ?? 0,
            sessionController?.OfferSequence ?? 0,
            sessionController?.RerollsUsed ?? 0);
    }

    private bool IsOfferRevealed(OfferPresentationIdentity identity)
    {
        return hasRevealedOffer && lastRevealedOffer.Equals(identity);
    }

    private void MarkOfferRevealed(OfferPresentationIdentity identity)
    {
        lastRevealedOffer = identity;
        hasRevealedOffer = true;
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = string.IsNullOrWhiteSpace(message) ? "요청을 처리할 수 없습니다." : message;
    }

    private void ClearFeedback()
    {
        if (feedbackText != null)
            feedbackText.text = string.Empty;
    }

    private void BeginTransition(IEnumerator routine)
    {
        if (routine == null)
            return;

        SetTransitioning(true);
        transitionRoutine = StartCoroutine(routine);
    }

    private void StopTransition(bool resetVisuals)
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        isTransitioning = false;
        if (resetVisuals)
            ResetWindowVisuals();
    }

    private static float EaseInCubic(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * value;
    }

    private static float EaseOutCubic(float value)
    {
        value = 1f - Mathf.Clamp01(value);
        return 1f - value * value * value;
    }

    private static float EaseInOutQuad(float value)
    {
        value = Mathf.Clamp01(value);
        return value < 0.5f
            ? 2f * value * value
            : 1f - Mathf.Pow(-2f * value + 2f, 2f) * 0.5f;
    }
}

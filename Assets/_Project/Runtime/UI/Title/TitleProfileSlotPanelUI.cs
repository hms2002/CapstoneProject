using System;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TitleProfileSlotPanelUI : MonoBehaviour, ICloseRequestHandler, IUIView
{
    [Header("Header")]
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Cards")]
    [SerializeField] private List<TitleProfileSlotCardUI> slotCards = new();

    [Header("Buttons")]
    [SerializeField] private Button backButton;

    [Header("Warning")]
    [SerializeField] private GameObject warningRoot;
    [SerializeField] private TMP_Text warningText;
    [SerializeField] private GameObject warningButtonGroup;
    [SerializeField] private Button confirmDeleteButton;
    [SerializeField] private Button cancelDeleteButton;

    [Header("Common")]
    [SerializeField] private UIChainDropPresentation dropPresentation;
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private CanvasGroup interactionCanvasGroup;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.15f;
    [SerializeField] private bool waitForSlotCardCloseAnimations;
    [SerializeField, Min(0f)] private float slotCardCloseLeadTime = 0.08f;

    private Action<int> onSlotSelected;
    private Action onClosed;
    private bool listenersBound;
    private bool isClosing;
    private bool awaitingDeleteConfirmation;
    private int pendingDeleteSlotIndex = -1;
    private Coroutine activeFadeCoroutine;
    private Coroutine activeCloseLeadCoroutine;
    private bool refreshWhenLocalizationReady;

    public bool IsActive => gameObject.activeSelf && !isClosing;

    private void Awake()
    {
        ResolveReferences();
        BindListeners();
        HideWarning();
        SetFadeAlpha(0f);
        SetInteractionEnabled(false);
        SetPanelContentInteractable(false);
    }

    private void OnEnable()
    {
        refreshWhenLocalizationReady = !UnityEngine.Localization.Settings.LocalizationSettings.InitializationOperation.IsDone;
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
    }

    private void Update()
    {
        if (!refreshWhenLocalizationReady || !UnityEngine.Localization.Settings.LocalizationSettings.InitializationOperation.IsDone) return;
        refreshWhenLocalizationReady = false;
        HandleLocaleChanged(UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale);
    }

    private void HandleLocaleChanged(UnityEngine.Localization.Locale locale)
    {
        if (!IsActive) return;
        RefreshTexts();
        var service = TitleProfileSlotService.Instance;
        if (service == null) return;
        for (int index = 0; index < slotCards.Count && index < service.SlotCount; index++)
            if (slotCards[index] != null)
                slotCards[index].RefreshLocalizedText(service.GetSlotSummary(index));
        if (awaitingDeleteConfirmation && pendingDeleteSlotIndex >= 0 && warningText != null)
            warningText.text = BuildDeleteWarningMessage(pendingDeleteSlotIndex);
    }

    private void OnDisable()
    {
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        StopFadeCoroutine();
        StopCloseLeadCoroutine();
        isClosing = false;
        HideWarning();
    }

    public void Open(Action<int> onSlotSelected, Action onClosed = null)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        ResolveReferences();
        BindListeners();

        this.onSlotSelected = onSlotSelected;
        this.onClosed = onClosed;
        isClosing = false;

        RefreshTexts();
        RefreshSlots();
        HideWarning();
        SetInteractionEnabled(true);
        SetPanelContentInteractable(true);

        if (dropPresentation != null)
        {
            SetFadeAlpha(1f);
            dropPresentation.PlayOpen();
            return;
        }

        SetFadeAlpha(0f);
        StartFade(1f, fadeInDuration);
    }

    public void OpenUI()
    {
        Open(onSlotSelected, onClosed);
    }

    public void SetInteractionBlocked(bool blocked)
    {
        if (blocked)
        {
            SetInteractionEnabled(false);
            SetPanelContentInteractable(false);
            return;
        }

        if (!gameObject.activeSelf || isClosing || awaitingDeleteConfirmation)
            return;

        SetInteractionEnabled(true);
        SetPanelContentInteractable(true);
    }

    public void CloseUI()
    {
        if (isClosing)
            return;

        if (!gameObject.activeSelf)
        {
            isClosing = false;
            return;
        }

        isClosing = true;
        SetInteractionEnabled(false);
        SetPanelContentInteractable(false);

        if (waitForSlotCardCloseAnimations)
        {
            PlaySlotCardCloseSequence(BeginPanelClose);
            return;
        }

        TriggerSlotCardClosePresentations();
        StartCloseLead(BeginPanelClose);
    }

    public bool TryHandleCloseRequest()
    {
        if (!gameObject.activeSelf)
            return false;

        if (awaitingDeleteConfirmation)
        {
            CancelDeleteConfirmation();
            return true;
        }

        CloseUI();
        return true;
    }

    private void ResolveReferences()
    {
        if (dropPresentation == null)
            dropPresentation = GetComponent<UIChainDropPresentation>();

        if (dropPresentation == null)
            dropPresentation = GetComponentInChildren<UIChainDropPresentation>(true);

        if (interactionCanvasGroup == null)
            interactionCanvasGroup = fadeCanvasGroup;

        if (interactionCanvasGroup == null)
            interactionCanvasGroup = GetComponent<CanvasGroup>();

        if (interactionCanvasGroup == null)
            interactionCanvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (fadeCanvasGroup == null)
            fadeCanvasGroup = interactionCanvasGroup;
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;

        if (backButton != null)
            backButton.onClick.AddListener(CloseUI);

        if (confirmDeleteButton != null)
            confirmDeleteButton.onClick.AddListener(HandleConfirmDelete);

        if (cancelDeleteButton != null)
            cancelDeleteButton.onClick.AddListener(HandleCancelDelete);

        listenersBound = true;
    }

    private void RefreshTexts()
    {
        if (headerText != null)
            headerText.text = GameText.Get("code.titleprofileslotpanelui.a19583b4e9", "프로필 선택");

        if (descriptionText != null)
            descriptionText.text = GameText.Get("code.titleprofileslotpanelui.f8fc40251a", "진행 중 런이 있으면 이어하기, 없으면 새 런을 시작합니다.");
    }

    private void RefreshSlots()
    {
        TitleProfileSlotService service = TitleProfileSlotService.EnsureInstance();
        if (service == null)
            return;

        for (int i = 0; i < slotCards.Count; i++)
        {
            TitleProfileSlotCardUI card = slotCards[i];
            if (card == null)
                continue;

            if (i >= service.SlotCount)
            {
                card.gameObject.SetActive(false);
                continue;
            }

            card.gameObject.SetActive(true);
            card.Bind(service.GetSlotSummary(i), HandleSlotSelected, HandleDeleteRequested);
        }
    }

    private void HandleSlotSelected(int slotIndex)
    {
        onSlotSelected?.Invoke(slotIndex);
    }

    private void HandleDeleteRequested(int slotIndex)
    {
        TitleProfileSlotService service = TitleProfileSlotService.EnsureInstance();
        if (service == null || !service.CanDeleteSlot(slotIndex))
            return;

        awaitingDeleteConfirmation = true;
        pendingDeleteSlotIndex = slotIndex;
        SetPanelContentInteractable(false);
        ShowWarning(BuildDeleteWarningMessage(slotIndex), showButtons: true);
    }

    private void HandleConfirmDelete()
    {
        if (!awaitingDeleteConfirmation || pendingDeleteSlotIndex < 0)
            return;

        TitleProfileSlotService service = TitleProfileSlotService.EnsureInstance();
        if (service != null && service.DeleteSlot(pendingDeleteSlotIndex))
            RefreshSlots();

        HideWarning();
        SetPanelContentInteractable(true);
    }

    private void HandleCancelDelete()
    {
        CancelDeleteConfirmation();
    }

    private void CancelDeleteConfirmation()
    {
        HideWarning();
        SetPanelContentInteractable(true);
    }

    private void FinalizeClose()
    {
        StopFadeCoroutine();
        StopCloseLeadCoroutine();
        HideWarning();
        gameObject.SetActive(false);
        SetFadeAlpha(0f);
        SetInteractionEnabled(true);
        SetPanelContentInteractable(true);
        isClosing = false;
        onClosed?.Invoke();
    }

    private void BeginPanelClose()
    {
        if (dropPresentation == null)
        {
            StartFade(0f, fadeOutDuration, FinalizeClose);
            return;
        }

        dropPresentation.PlayClose(FinalizeClose);
    }

    private void SetInteractionEnabled(bool enabled)
    {
        if (interactionCanvasGroup == null)
            return;

        interactionCanvasGroup.interactable = enabled;
        interactionCanvasGroup.blocksRaycasts = enabled;
    }

    private void SetPanelContentInteractable(bool enabled)
    {
        for (int i = 0; i < slotCards.Count; i++)
        {
            if (slotCards[i] != null)
                slotCards[i].SetInteractable(enabled);
        }

        if (backButton != null)
            backButton.interactable = enabled;
    }

    private void ShowWarning(string message, bool showButtons)
    {
        if (warningRoot != null)
            warningRoot.SetActive(true);

        if (warningText != null)
            warningText.text = message;

        if (warningButtonGroup != null)
            warningButtonGroup.SetActive(showButtons);
    }

    private void TriggerSlotCardClosePresentations()
    {
        for (int i = 0; i < slotCards.Count; i++)
        {
            TitleProfileSlotCardUI card = slotCards[i];
            if (card == null || !card.gameObject.activeInHierarchy)
                continue;

            card.PlayClosePresentations();
        }
    }

    private void PlaySlotCardCloseSequence(Action onCompleted)
    {
        int activeCardCount = 0;
        for (int i = 0; i < slotCards.Count; i++)
        {
            if (slotCards[i] != null && slotCards[i].gameObject.activeInHierarchy)
                activeCardCount++;
        }

        if (activeCardCount == 0)
        {
            onCompleted?.Invoke();
            return;
        }

        int remainingCallbacks = activeCardCount;
        for (int i = 0; i < slotCards.Count; i++)
        {
            TitleProfileSlotCardUI card = slotCards[i];
            if (card == null || !card.gameObject.activeInHierarchy)
                continue;

            card.PlayClosePresentations(() =>
            {
                remainingCallbacks--;
                if (remainingCallbacks <= 0)
                    onCompleted?.Invoke();
            });
        }
    }

    private void StartCloseLead(Action onCompleted)
    {
        StopCloseLeadCoroutine();

        if (slotCardCloseLeadTime <= 0f)
        {
            onCompleted?.Invoke();
            return;
        }

        activeCloseLeadCoroutine = StartCoroutine(CloseLeadRoutine(onCompleted));
    }

    private IEnumerator CloseLeadRoutine(Action onCompleted)
    {
        float elapsed = 0f;
        while (elapsed < slotCardCloseLeadTime)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        activeCloseLeadCoroutine = null;
        onCompleted?.Invoke();
    }

    private void HideWarning()
    {
        awaitingDeleteConfirmation = false;
        pendingDeleteSlotIndex = -1;

        if (warningRoot != null)
            warningRoot.SetActive(false);

        if (warningButtonGroup != null)
            warningButtonGroup.SetActive(false);
    }

    private static string BuildDeleteWarningMessage(int slotIndex)
    {
        return GameText.Format("title.profile.delete_confirmation", "슬롯 {0}의 프로필 데이터를 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.", slotIndex + 1);
    }

    private void StartFade(float targetAlpha, float duration, Action onCompleted = null)
    {
        if (fadeCanvasGroup == null)
        {
            onCompleted?.Invoke();
            return;
        }

        StopFadeCoroutine();

        if (duration <= 0f)
        {
            SetFadeAlpha(targetAlpha);
            onCompleted?.Invoke();
            return;
        }

        activeFadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, duration, onCompleted));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration, Action onCompleted)
    {
        float startAlpha = fadeCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            SetFadeAlpha(Mathf.Lerp(startAlpha, targetAlpha, normalized));
            yield return null;
        }

        SetFadeAlpha(targetAlpha);
        activeFadeCoroutine = null;
        onCompleted?.Invoke();
    }

    private void StopFadeCoroutine()
    {
        if (activeFadeCoroutine == null)
            return;

        StopCoroutine(activeFadeCoroutine);
        activeFadeCoroutine = null;
    }

    private void StopCloseLeadCoroutine()
    {
        if (activeCloseLeadCoroutine == null)
            return;

        StopCoroutine(activeCloseLeadCoroutine);
        activeCloseLeadCoroutine = null;
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeCanvasGroup == null)
            return;

        fadeCanvasGroup.alpha = alpha;
    }
}

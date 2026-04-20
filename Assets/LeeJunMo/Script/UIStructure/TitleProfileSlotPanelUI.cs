using System;
using System.Collections.Generic;
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

    [Header("Common")]
    [SerializeField] private UIChainDropPresentation dropPresentation;
    [SerializeField] private CanvasGroup interactionCanvasGroup;

    private Action<int> onSlotSelected;
    private Action onClosed;
    private bool listenersBound;
    private bool isClosing;
    private TitleProfileSlotPanelMode currentMode;

    public bool IsActive => gameObject.activeSelf && !isClosing;

    private void Awake()
    {
        ResolveReferences();
        BindListeners();
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        isClosing = false;
    }

    public void Open(
        TitleProfileSlotPanelMode mode,
        Action<int> onSlotSelected,
        Action onClosed = null)
    {
        ResolveReferences();
        BindListeners();

        currentMode = mode;
        this.onSlotSelected = onSlotSelected;
        this.onClosed = onClosed;
        isClosing = false;

        RefreshTexts();
        RefreshSlots();
        SetInteractionEnabled(true);

        gameObject.SetActive(true);
        dropPresentation?.PlayOpen();
    }

    public void OpenUI()
    {
        Open(currentMode, onSlotSelected, onClosed);
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

        if (dropPresentation == null)
        {
            gameObject.SetActive(false);
            SetInteractionEnabled(true);
            onClosed?.Invoke();
            return;
        }

        isClosing = true;
        SetInteractionEnabled(false);
        dropPresentation.PlayClose(FinalizeClose);
    }

    public bool TryHandleCloseRequest()
    {
        if (!gameObject.activeSelf)
            return false;

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
            interactionCanvasGroup = GetComponentInChildren<CanvasGroup>(true);
    }

    private void BindListeners()
    {
        if (listenersBound)
            return;

        if (backButton != null)
            backButton.onClick.AddListener(CloseUI);

        listenersBound = true;
    }

    private void RefreshTexts()
    {
        if (headerText != null)
            headerText.text = currentMode == TitleProfileSlotPanelMode.NewGame ? "새 게임" : "이어하기";

        if (descriptionText != null)
        {
            descriptionText.text = currentMode == TitleProfileSlotPanelMode.NewGame
                ? "새 런을 시작할 프로필 슬롯을 선택하세요."
                : "이어할 런이 있는 프로필 슬롯을 선택하세요.";
        }
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
            card.Bind(service.GetSlotSummary(i), currentMode, HandleSlotSelected);
        }
    }

    private void HandleSlotSelected(int slotIndex)
    {
        onSlotSelected?.Invoke(slotIndex);
    }

    private void FinalizeClose()
    {
        gameObject.SetActive(false);
        SetInteractionEnabled(true);
        isClosing = false;
        onClosed?.Invoke();
    }

    private void SetInteractionEnabled(bool enabled)
    {
        if (interactionCanvasGroup == null)
            return;

        interactionCanvasGroup.interactable = enabled;
        interactionCanvasGroup.blocksRaycasts = enabled;
    }
}

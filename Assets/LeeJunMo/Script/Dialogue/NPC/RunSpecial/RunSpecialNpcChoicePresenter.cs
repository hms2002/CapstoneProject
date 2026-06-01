using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 책임 :
/// - 런 중 등장하는 특수 NPC 선택지 UI를 표시하고 버튼/숫자 입력 선택을 중계한다.
/// - DialogueService를 거치지 않는 선택지 표시 동안 DialogueCanvas raycast가 필요함을 전역으로 알린다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RunSpecialNpcChoicePresenter : MonoBehaviour
{
    [Header("Authored UI References")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private Button[] choiceButtons = Array.Empty<Button>();
    [SerializeField] private TMP_Text[] choiceLabels = Array.Empty<TMP_Text>();
    [SerializeField] private bool hideUnusedButtons = true;
    [SerializeField] private bool allowGlobalLookup;

    private UnityAction[] clickHandlers = Array.Empty<UnityAction>();
    private Action<int> choiceSelected;
    private int activeChoiceCount;
    private float inputEnableTime;
    private RectTransform rootRect;
    private bool isVisible;
    private bool inputEnabled;
    private bool contributesRaycastRequest;
    private static int visibleChoicePresenterCount;

    public static bool HasVisibleChoicePresenter => visibleChoicePresenterCount > 0;

    private void Awake()
    {
        hideUnusedButtons = true;
        ResolveRootGroup();
        Hide();
    }

    private void OnDisable()
    {
        SetRaycastRequestActive(false);
    }

    private void OnDestroy()
    {
        SetRaycastRequestActive(false);
    }

    private void Update()
    {
        if (!isVisible || inputEnabled || Time.unscaledTime < inputEnableTime)
            return;

        inputEnabled = true;
        ApplyButtonInteractableState();
    }

    public void Show(IReadOnlyList<string> labels, Action<int> onChoiceSelected, float inputGuardSeconds)
    {
        hideUnusedButtons = true;
        ResolveRootGroup();
        EnsureButtonHandlers();

        choiceSelected = onChoiceSelected;
        activeChoiceCount = labels != null ? Mathf.Min(labels.Count, choiceButtons.Length) : 0;
        inputEnableTime = Time.unscaledTime + Mathf.Max(0f, inputGuardSeconds);
        isVisible = true;
        inputEnabled = inputGuardSeconds <= 0f;
        SetRaycastRequestActive(activeChoiceCount > 0);

        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = inputEnabled;
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button == null)
                continue;

            bool active = i < activeChoiceCount;
            SetChoiceButtonVisible(button, active);

            button.interactable = active && inputEnabled;

            if (i < choiceLabels.Length && choiceLabels[i] != null)
                choiceLabels[i].text = active ? labels[i] : string.Empty;

            ApplyChoiceKeyGlyph(button, i, active);
        }

        RebuildLayout();
    }

    public void Hide()
    {
        isVisible = false;
        inputEnabled = false;
        choiceSelected = null;
        activeChoiceCount = 0;
        SetRaycastRequestActive(false);

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button == null)
                continue;

            button.interactable = false;
            ApplyChoiceKeyGlyph(button, i, false);
            SetChoiceButtonVisible(button, false);
        }

        RebuildLayout();
    }

    public bool ConfirmChoiceAt(int index)
    {
        if (!isVisible || !inputEnabled || index < 0 || index >= activeChoiceCount)
            return false;

        Action<int> callback = choiceSelected;
        choiceSelected = null;
        callback?.Invoke(index);
        return true;
    }

    public bool CanAcceptInput => isVisible && inputEnabled;
    public bool AllowGlobalLookup => allowGlobalLookup;

    private void ResolveRootGroup()
    {
        if (rootGroup == null)
            rootGroup = GetComponent<CanvasGroup>();

        rootRect = rootGroup != null
            ? rootGroup.transform as RectTransform
            : transform as RectTransform;
    }

    private void SetChoiceButtonVisible(Button button, bool visible)
    {
        if (button == null)
            return;

        bool shouldShow = visible || !hideUnusedButtons;

        if (button.gameObject.activeSelf != shouldShow)
            button.gameObject.SetActive(shouldShow);
    }

    private void RebuildLayout()
    {
        if (rootRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
    }

    private static void ApplyChoiceKeyGlyph(Button button, int index, bool visible)
    {
        if (button == null)
            return;

        DialogueChoiceKeyGlyph keyGlyph = button.GetComponent<DialogueChoiceKeyGlyph>();
        if (keyGlyph == null)
            return;

        if (visible)
            keyGlyph.Bind(index);
        else
            keyGlyph.Hide();
    }

    private void EnsureButtonHandlers()
    {
        if (choiceButtons == null)
            choiceButtons = Array.Empty<Button>();

        if (clickHandlers.Length == choiceButtons.Length)
            return;

        for (int i = 0; i < clickHandlers.Length && i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] != null && clickHandlers[i] != null)
                choiceButtons[i].onClick.RemoveListener(clickHandlers[i]);
        }

        clickHandlers = new UnityAction[choiceButtons.Length];
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int index = i;
            clickHandlers[i] = () => ConfirmChoiceAt(index);
            if (choiceButtons[i] != null)
                choiceButtons[i].onClick.AddListener(clickHandlers[i]);
        }
    }

    private void ApplyButtonInteractableState()
    {
        if (rootGroup != null)
            rootGroup.interactable = true;

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button == null)
                continue;

            button.interactable = i < activeChoiceCount;
        }
    }

    private void SetRaycastRequestActive(bool active)
    {
        if (contributesRaycastRequest == active)
            return;

        contributesRaycastRequest = active;
        visibleChoicePresenterCount += active ? 1 : -1;
        if (visibleChoicePresenterCount < 0)
            visibleChoicePresenterCount = 0;
    }
}

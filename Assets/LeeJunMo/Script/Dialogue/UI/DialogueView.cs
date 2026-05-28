using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CapstoneAudio;

public class DialogueView : MonoBehaviour
{
    private static readonly SoundRef TalkUiIntroSound = SoundRef.FromKey("sound_ui_TalkUIIntro");

    [Header("UI Groups (CanvasGroup required)")]
    [SerializeField] private CanvasGroup textBoxGroup;
    [SerializeField] private CanvasGroup affectionGroup;

    [Header("UI Presentation")]
    [SerializeField] private UISlideFadePresentation textBoxPresentation;
    [SerializeField] private UISlideFadePresentation affectionPresentation;

    [Header("Text Components")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("Continue Icon")]
    [SerializeField] private GameObject continueIcon;

    [Header("Choice UI")]
    [SerializeField] private Transform choiceContainer;
    [SerializeField] private GameObject choiceButtonPrefab;
    [SerializeField] private Color normalChoiceColor = Color.gray;
    [SerializeField] private Color selectedChoiceColor = Color.white;

    [Header("Theme")]
    [SerializeField] private Graphic[] textBoxThemeTargets;
    [SerializeField] private Graphic[] speakerFrameThemeTargets;
    [SerializeField] private Color defaultTextBoxFillColor = new Color(0f, 0f, 0f, 0.85f);
    [SerializeField] private Graphic dimPanelGraphic;
    [SerializeField] private float dimFadeDuration = 0.25f;
    [SerializeField] private float dialogueEffectIntroFallbackDuration = 0.5f;
    [SerializeField] private Animator dialogueEffectAnimator;
    [SerializeField] private string dialogueEffectIdleState = "Idle";
    [SerializeField] private string dialogueEffectIntroState = "Intro";

    [Header("Typing Audio")]
    [SerializeField] private bool playTypingSound = true;
    [SerializeField, Min(0f)] private float typingSoundInterval = 0.035f;

    private Tween typingTween;
    private Tween continueIconTween;
    private RectTransform continueIconRect;
    private Vector2 continueIconBaseAnchoredPosition;
    private bool hasContinueIconBasePosition;
    private readonly List<GameObject> activeChoiceButtons = new List<GameObject>();
    private readonly Dictionary<Graphic, Material> originalThemeMaterials = new Dictionary<Graphic, Material>();
    private readonly Dictionary<Graphic, Color> originalThemeColors = new Dictionary<Graphic, Color>();
    private readonly Dictionary<Outline, Color> originalOutlineColors = new Dictionary<Outline, Color>();
    private readonly Dictionary<Graphic, Material> runtimeThemeMaterials = new Dictionary<Graphic, Material>();

    private DialogueThemeSO currentTheme;
    private DialogueThemeSO currentEffectTheme;
    private RuntimeAnimatorController defaultEffectController;
    private Color defaultNameTextColor;
    private float defaultDimPanelAlpha;
    private bool isUiVisible;
    private bool choiceInputEnabled;
    private int currentChoiceIndex;
    private Action<int> onChoiceSelectedCallback;
    private int lastTypedCharacterCount;
    private float nextTypingSoundTime;

    private void Awake()
    {
        AutoResolveThemeTargets();
        CacheThemeDefaults();
        if (nameText != null)
            defaultNameTextColor = nameText.color;
        if (dimPanelGraphic != null)
        {
            defaultDimPanelAlpha = dimPanelGraphic.color.a;
            SetDimPanelVisible(false, true);
        }

        ResetDialogueEffectToHiddenIdle();
        if (dialogueEffectAnimator != null)
            dialogueEffectAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

        ResolveGroupPresentations();
        SnapGroupClosed(textBoxGroup, textBoxPresentation);
        SnapGroupClosed(affectionGroup, affectionPresentation);

        if (continueIcon != null)
        {
            continueIcon.SetActive(false);
            CacheContinueIconTransform();
        }

        ClearChoices();
        ClearText();
    }

    private void OnEnable()
    {
        StartContinueIconMotion();
    }

    private void OnDisable()
    {
        StopContinueIconMotion(true);
    }

    private void OnDestroy()
    {
        StopRuntimeTweens();

        foreach (Material runtimeMaterial in runtimeThemeMaterials.Values)
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }

        runtimeThemeMaterials.Clear();
    }

    public void ClearText()
    {
        ResetTypingAudioTracking();

        if (nameText != null)
            nameText.text = string.Empty;

        if (dialogueText != null)
            dialogueText.text = string.Empty;
    }

    public void ApplyTheme(DialogueThemeSO theme, bool updateEffectTheme = false)
    {
        AutoResolveThemeTargets();
        CacheThemeDefaults();
        currentTheme = theme;
        if (updateEffectTheme)
            currentEffectTheme = theme;

        RestoreThemeVisuals();

        if (theme == null)
        {
            RefreshDialogueEffectOverride();
            return;
        }

        ApplyThemeToTargets(textBoxThemeTargets, defaultTextBoxFillColor, theme.outlineColor);
        ApplyThemeToTargets(speakerFrameThemeTargets, theme.speakerFrameFillColor, theme.outlineColor);
        if (nameText != null)
            nameText.color = theme.outlineColor;
        RefreshDialogueEffectOverride();
    }

    public void ResetTheme()
    {
        currentTheme = null;
        currentEffectTheme = null;
        RestoreThemeVisuals();
        if (nameText != null)
            nameText.color = defaultNameTextColor;
        ResetDialogueEffectOverride();
        ResetDialogueEffectToHiddenIdle();
    }

    public void ShowUI(bool isBoss, Action onComplete = null)
    {
        isUiVisible = true;
        RefreshThemePresentation(false);
        ResolveGroupPresentations();

        int pendingAnimations = 0;
        bool didComplete = false;
        bool startedAllAnimations = false;

        void RegisterAnimation()
        {
            pendingAnimations++;
        }

        void CompleteAnimation()
        {
            pendingAnimations--;
            if (pendingAnimations > 0 || didComplete || !startedAllAnimations)
                return;

            didComplete = true;
            onComplete?.Invoke();
        }

        if (textBoxGroup != null)
        {
            RegisterAnimation();
            PlayGroupOpen(textBoxGroup, textBoxPresentation, CompleteAnimation);
        }

        if (isBoss && affectionGroup != null)
        {
            RegisterAnimation();
            PlayGroupOpen(affectionGroup, affectionPresentation, CompleteAnimation);
        }
        else
        {
            SnapGroupClosed(affectionGroup, affectionPresentation);
        }

        startedAllAnimations = true;
        if (pendingAnimations == 0 && !didComplete)
        {
            didComplete = true;
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// 책임 : 대화 UI가 실제 텍스트 표시로 넘어가기 전, 진입 연출 시작음을 재생한다.
    /// </summary>
    public void PlayOpeningIntroSound()
    {
        if (isUiVisible)
            return;

        SoundPlaybackUtility.Play(TalkUiIntroSound, sourceObject: this);
    }

    public void PlayBossPrelude(Action onComplete = null)
    {
        RefreshThemePresentation(false);

        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true);
        float effectDuration = GetDialogueEffectIntroDuration();
        if (effectDuration <= 0f)
            effectDuration = dialogueEffectIntroFallbackDuration;

        if (dimPanelGraphic != null)
        {
            SetDimPanelVisible(true, true);
            seq.Append(dimPanelGraphic.DOFade(defaultDimPanelAlpha, dimFadeDuration).SetUpdate(true));
        }

        seq.AppendCallback(() =>
        {
            SetDialogueEffectVisible(true);
            PlayDialogueEffectIntro();
        });

        seq.AppendInterval(effectDuration);
        seq.OnComplete(() => onComplete?.Invoke());
    }

    public void TypeText(string speakerName, string text, Action onComplete = null)
    {
        if (nameText != null)
            nameText.text = speakerName;

        if (dialogueText != null)
            dialogueText.text = string.Empty;

        if (continueIcon != null)
            continueIcon.SetActive(false);

        typingTween?.Kill();
        ResetTypingAudioTracking();

        if (dialogueText != null)
        {
            typingTween = dialogueText.DOText(text, text.Length * 0.05f)
                .SetUpdate(true)
                .SetEase(Ease.Linear)
                .OnUpdate(HandleTypingTweenUpdated)
                .OnComplete(() =>
                {
                    HandleTypingTweenUpdated();

                    if (continueIcon != null)
                        continueIcon.SetActive(true);

                    onComplete?.Invoke();
                });
        }
    }

    public void SkipTyping(string fullText)
    {
        typingTween?.Kill();

        if (dialogueText != null)
            dialogueText.text = fullText;

        lastTypedCharacterCount = string.IsNullOrEmpty(fullText) ? 0 : fullText.Length;

        if (continueIcon != null)
            continueIcon.SetActive(true);
    }

    public bool ShowChoices(List<Ink.Runtime.Choice> choices, Action<int> onChoiceSelected)
    {
        ClearChoices();

        if (continueIcon != null)
            continueIcon.SetActive(false);

        if (choiceContainer == null || choiceButtonPrefab == null)
        {
            Debug.LogError("[DialogueView] choiceContainer or choiceButtonPrefab is missing. Cannot display dialogue choices.", this);
            return false;
        }

        onChoiceSelectedCallback = onChoiceSelected;
        choiceInputEnabled = false;
        currentChoiceIndex = -1;
        EventSystem.current?.SetSelectedGameObject(null);

        foreach (Ink.Runtime.Choice choice in choices)
        {
            GameObject btnObj = Instantiate(choiceButtonPrefab, choiceContainer);
            if (btnObj != null && !btnObj.activeSelf)
                btnObj.SetActive(true);

            activeChoiceButtons.Add(btnObj);
            int listIndex = activeChoiceButtons.Count - 1;

            TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
                btnText.text = choice.text;

            DialogueChoiceHighlightPresentation choiceHighlight = btnObj.GetComponent<DialogueChoiceHighlightPresentation>();
            if (choiceHighlight != null)
                choiceHighlight.SetSelected(false, true);

            DialogueChoiceInputRelay inputRelay = btnObj.GetComponent<DialogueChoiceInputRelay>();
            if (inputRelay != null)
                inputRelay.Bind(this, listIndex);

            DialogueChoiceKeyGlyph keyGlyph = btnObj.GetComponent<DialogueChoiceKeyGlyph>();
            if (keyGlyph != null)
                keyGlyph.Bind(listIndex);

            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick = new Button.ButtonClickedEvent();
                Navigation navigation = btn.navigation;
                navigation.mode = Navigation.Mode.None;
                btn.navigation = navigation;

                int index = choice.index;
                btn.onClick.AddListener(() =>
                {
                    if (!choiceInputEnabled)
                        return;

                    Action<int> callback = onChoiceSelectedCallback;
                    ClearChoices();
                    callback?.Invoke(index);
                });
            }
        }

        HighlightChoice(currentChoiceIndex);
        return true;
    }

    public void ChangeChoiceSelection(int direction)
    {
        if (activeChoiceButtons.Count == 0)
            return;

        if (currentChoiceIndex < 0)
        {
            currentChoiceIndex = direction < 0 ? 0 : Mathf.Min(1, activeChoiceButtons.Count - 1);
            HighlightChoice(currentChoiceIndex);
            return;
        }

        currentChoiceIndex += direction;

        if (currentChoiceIndex < 0)
            currentChoiceIndex = activeChoiceButtons.Count - 1;
        else if (currentChoiceIndex >= activeChoiceButtons.Count)
            currentChoiceIndex = 0;

        HighlightChoice(currentChoiceIndex);
    }

    public void ConfirmChoice()
    {
        if (!choiceInputEnabled || activeChoiceButtons.Count <= 0)
            return;

        if (currentChoiceIndex < 0 || currentChoiceIndex >= activeChoiceButtons.Count)
            return;

        Button selectedBtn = activeChoiceButtons[currentChoiceIndex].GetComponent<Button>();
        selectedBtn?.onClick.Invoke();
    }

    public void ConfirmChoiceAt(int index)
    {
        if (!choiceInputEnabled || index < 0 || index >= activeChoiceButtons.Count)
            return;

        currentChoiceIndex = index;
        HighlightChoice(currentChoiceIndex);

        Button selectedBtn = activeChoiceButtons[currentChoiceIndex].GetComponent<Button>();
        selectedBtn?.onClick.Invoke();
    }

    public void SetChoiceInputEnabled(bool enabled)
    {
        choiceInputEnabled = enabled;
    }

    public void SelectChoiceFromPointer(int index)
    {
        if (!choiceInputEnabled)
            return;

        SelectChoice(index);
    }

    private void SelectChoice(int index)
    {
        if (index < 0 || index >= activeChoiceButtons.Count)
            return;

        currentChoiceIndex = index;
        HighlightChoice(currentChoiceIndex);
    }

    public void ClearChoices()
    {
        choiceInputEnabled = false;
        currentChoiceIndex = -1;

        foreach (GameObject btn in activeChoiceButtons)
        {
            if (btn != null)
            {
                DialogueChoiceHighlightPresentation choiceHighlight =
                    btn.GetComponent<DialogueChoiceHighlightPresentation>();
                if (choiceHighlight != null)
                    choiceHighlight.SetSelected(false, true);

                Destroy(btn);
            }
        }

        activeChoiceButtons.Clear();
        onChoiceSelectedCallback = null;
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private void CacheContinueIconTransform()
    {
        if (continueIcon == null)
            return;

        continueIconRect = continueIcon.GetComponent<RectTransform>();
        if (continueIconRect == null)
            return;

        continueIconBaseAnchoredPosition = continueIconRect.anchoredPosition;
        hasContinueIconBasePosition = true;
    }

    private void StartContinueIconMotion()
    {
        if (continueIconRect == null)
            CacheContinueIconTransform();

        if (continueIconRect == null)
            return;

        StopContinueIconMotion(true);
        continueIconTween = continueIconRect
            .DOAnchorPosY(continueIconBaseAnchoredPosition.y - 10f, 0.5f)
            .SetUpdate(true)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    private void StopContinueIconMotion(bool resetPosition)
    {
        continueIconTween?.Kill();
        continueIconTween = null;

        if (continueIconRect != null)
            continueIconRect.DOKill();

        if (resetPosition && continueIconRect != null && hasContinueIconBasePosition)
            continueIconRect.anchoredPosition = continueIconBaseAnchoredPosition;
    }

    private void StopRuntimeTweens()
    {
        typingTween?.Kill();
        typingTween = null;

        StopContinueIconMotion(true);

        if (dialogueText != null)
            dialogueText.DOKill();

        if (textBoxGroup != null)
            textBoxGroup.DOKill();

        if (affectionGroup != null)
            affectionGroup.DOKill();

        if (dimPanelGraphic != null)
            dimPanelGraphic.DOKill();

        foreach (GameObject choiceButton in activeChoiceButtons)
        {
            if (choiceButton != null)
                choiceButton.transform.DOKill();
        }
    }

    public void HideUI(Action onComplete = null)
    {
        ClearChoices();

        if (continueIcon != null)
            continueIcon.SetActive(false);

        ResolveGroupPresentations();

        int pendingAnimations = 0;
        bool didComplete = false;
        bool startedAllAnimations = false;

        void FinishHide()
        {
            isUiVisible = false;

            SetDimPanelVisible(false, true);
            ResetDialogueEffectToHiddenIdle();
            onComplete?.Invoke();
        }

        void RegisterAnimation()
        {
            pendingAnimations++;
        }

        void CompleteAnimation()
        {
            pendingAnimations--;
            if (pendingAnimations > 0 || didComplete || !startedAllAnimations)
                return;

            didComplete = true;
            FinishHide();
        }

        if (textBoxGroup != null && textBoxGroup.gameObject.activeSelf)
        {
            RegisterAnimation();
            PlayGroupClose(textBoxGroup, textBoxPresentation, CompleteAnimation);
        }

        if (affectionGroup != null && affectionGroup.gameObject.activeSelf)
        {
            RegisterAnimation();
            PlayGroupClose(affectionGroup, affectionPresentation, CompleteAnimation);
        }

        startedAllAnimations = true;
        if (pendingAnimations == 0 && !didComplete)
        {
            didComplete = true;
            FinishHide();
        }
    }

    private void PlayGroupOpen(CanvasGroup group, UISlideFadePresentation presentation, Action onComplete)
    {
        if (presentation != null)
        {
            presentation.PlayOpen(onComplete);
            return;
        }

        if (group == null)
        {
            onComplete?.Invoke();
            return;
        }

        group.DOKill();
        group.gameObject.SetActive(true);
        group.alpha = 0f;
        group.DOFade(1f, 0.25f)
            .SetUpdate(true)
            .OnComplete(() => onComplete?.Invoke());
    }

    private void PlayGroupClose(CanvasGroup group, UISlideFadePresentation presentation, Action onComplete)
    {
        if (presentation != null)
        {
            presentation.PlayClose(onComplete);
            return;
        }

        if (group == null || !group.gameObject.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }

        group.DOKill();
        group.DOFade(0f, 0.25f)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                group.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
    }

    private void SnapGroupClosed(CanvasGroup group, UISlideFadePresentation presentation)
    {
        if (presentation != null)
        {
            presentation.SnapClosed();
            return;
        }

        if (group == null)
            return;

        group.DOKill();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.gameObject.SetActive(false);
    }

    private void ResolveGroupPresentations()
    {
        if (textBoxPresentation == null)
            textBoxPresentation = ResolveGroupPresentation(textBoxGroup);

        if (affectionPresentation == null)
            affectionPresentation = ResolveGroupPresentation(affectionGroup);
    }

    private UISlideFadePresentation ResolveGroupPresentation(CanvasGroup group)
    {
        if (group == null)
            return null;

        return group.GetComponent<UISlideFadePresentation>();
    }

    private void HighlightChoice(int index)
    {
        for (int i = 0; i < activeChoiceButtons.Count; i++)
        {
            GameObject choiceButton = activeChoiceButtons[i];
            if (choiceButton == null)
                continue;

            bool isSelected = index >= 0 && i == index;

            TextMeshProUGUI btnText = choiceButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
                btnText.color = isSelected ? selectedChoiceColor : normalChoiceColor;

            DialogueChoiceHighlightPresentation choiceHighlight =
                choiceButton.GetComponent<DialogueChoiceHighlightPresentation>();
            if (choiceHighlight != null)
                choiceHighlight.SetSelected(isSelected);

            choiceButton.transform.DOScale(isSelected ? 1.05f : 1.0f, 0.1f).SetUpdate(true);
        }
    }

    private void AutoResolveThemeTargets()
    {
        if (dialogueEffectAnimator == null)
        {
            Transform effectTransform = FindChildRecursive("DialogueEffect");
            if (effectTransform != null)
                dialogueEffectAnimator = effectTransform.GetComponent<Animator>();
        }

        if (dialogueEffectAnimator != null && defaultEffectController == null)
            defaultEffectController = dialogueEffectAnimator.runtimeAnimatorController;

        if (dimPanelGraphic == null)
            dimPanelGraphic = FindGraphicByName("DimPanel");

        if (textBoxThemeTargets == null || textBoxThemeTargets.Length == 0)
        {
            Graphic textBoxGraphic = FindGraphicByName("TextBoxGroup");
            if (textBoxGraphic != null)
                textBoxThemeTargets = new[] { textBoxGraphic };
        }

        if (speakerFrameThemeTargets == null || speakerFrameThemeTargets.Length == 0)
        {
            Graphic speakerFrameGraphic = FindGraphicByName("SpeakerFrame");
            if (speakerFrameGraphic != null)
                speakerFrameThemeTargets = new[] { speakerFrameGraphic };
        }
    }

    private void CacheThemeDefaults()
    {
        foreach (Graphic graphic in EnumerateThemeTargets())
        {
            if (graphic == null)
                continue;

            if (!originalThemeMaterials.ContainsKey(graphic))
                originalThemeMaterials[graphic] = graphic.material;

            if (!originalThemeColors.ContainsKey(graphic))
                originalThemeColors[graphic] = graphic.color;

            foreach (Outline outline in graphic.GetComponents<Outline>())
            {
                if (outline != null && !originalOutlineColors.ContainsKey(outline))
                    originalOutlineColors[outline] = outline.effectColor;
            }
        }
    }

    private IEnumerable<Graphic> EnumerateThemeTargets()
    {
        HashSet<Graphic> uniqueTargets = new HashSet<Graphic>();

        if (textBoxThemeTargets != null)
        {
            foreach (Graphic graphic in textBoxThemeTargets)
            {
                if (graphic != null && uniqueTargets.Add(graphic))
                    yield return graphic;
            }
        }

        if (speakerFrameThemeTargets != null)
        {
            foreach (Graphic graphic in speakerFrameThemeTargets)
            {
                if (graphic != null && uniqueTargets.Add(graphic))
                    yield return graphic;
            }
        }
    }

    private void ApplyThemeToTargets(Graphic[] targets, Color fillColor, Color outlineColor)
    {
        if (targets == null)
            return;

        foreach (Graphic graphic in targets)
        {
            if (graphic == null)
                continue;

            Material themedMaterial = GetOrCreateThemeMaterial(graphic);
            if (themedMaterial != null)
            {
                if (themedMaterial.HasProperty("_OutlineColor"))
                    themedMaterial.SetColor("_OutlineColor", outlineColor);

                graphic.material = themedMaterial;
            }

            graphic.color = fillColor;

            foreach (Outline outline in graphic.GetComponents<Outline>())
            {
                if (outline != null)
                    outline.effectColor = outlineColor;
            }
        }
    }

    private Material GetOrCreateThemeMaterial(Graphic graphic)
    {
        if (graphic == null)
            return null;

        if (runtimeThemeMaterials.TryGetValue(graphic, out Material cachedMaterial) && cachedMaterial != null)
            return cachedMaterial;

        originalThemeMaterials.TryGetValue(graphic, out Material originalMaterial);
        Material themeMaterial = null;

        if (originalMaterial != null && originalMaterial.HasProperty("_OutlineColor"))
        {
            themeMaterial = new Material(originalMaterial);
        }
        else
        {
            Shader outlineShader = Shader.Find("UI/Alpha Outline");
            if (outlineShader != null)
                themeMaterial = new Material(outlineShader);
            else if (originalMaterial != null)
                themeMaterial = new Material(originalMaterial);
        }

        if (themeMaterial != null)
            runtimeThemeMaterials[graphic] = themeMaterial;

        return themeMaterial;
    }

    private void RestoreThemeVisuals()
    {
        foreach (Graphic graphic in EnumerateThemeTargets())
        {
            if (graphic == null)
                continue;

            if (originalThemeMaterials.TryGetValue(graphic, out Material originalMaterial))
                graphic.material = originalMaterial;

            if (originalThemeColors.TryGetValue(graphic, out Color originalColor))
                graphic.color = originalColor;

            foreach (Outline outline in graphic.GetComponents<Outline>())
            {
                if (outline != null && originalOutlineColors.TryGetValue(outline, out Color originalOutlineColor))
                    outline.effectColor = originalOutlineColor;
            }
        }
    }

    private void RefreshThemePresentation(bool restartEffect)
    {
        if (currentTheme == null)
        {
            RestoreThemeVisuals();
            if (nameText != null)
                nameText.color = defaultNameTextColor;

            RefreshDialogueEffectOverride();
            if (restartEffect)
                ResetDialogueEffectToHiddenIdle();
            return;
        }

        ApplyThemeToTargets(textBoxThemeTargets, defaultTextBoxFillColor, currentTheme.outlineColor);
        ApplyThemeToTargets(speakerFrameThemeTargets, currentTheme.speakerFrameFillColor, currentTheme.outlineColor);

        if (nameText != null)
            nameText.color = currentTheme.outlineColor;

        RefreshDialogueEffectOverride();

        if (restartEffect)
            PlayDialogueEffectIntro();
    }

    private void ApplyDialogueEffectOverride(AnimatorOverrideController overrideController)
    {
        if (dialogueEffectAnimator == null)
            return;

        if (defaultEffectController == null)
            defaultEffectController = dialogueEffectAnimator.runtimeAnimatorController;

        RuntimeAnimatorController targetController = overrideController != null
            ? overrideController
            : defaultEffectController;

        if (dialogueEffectAnimator.runtimeAnimatorController == targetController)
            return;

        dialogueEffectAnimator.runtimeAnimatorController = targetController;
        dialogueEffectAnimator.Rebind();
        dialogueEffectAnimator.Update(0f);
    }

    private void ResetDialogueEffectOverride()
    {
        if (dialogueEffectAnimator == null || defaultEffectController == null)
            return;

        if (dialogueEffectAnimator.runtimeAnimatorController == defaultEffectController)
            return;

        dialogueEffectAnimator.runtimeAnimatorController = defaultEffectController;
        dialogueEffectAnimator.Rebind();
        dialogueEffectAnimator.Update(0f);
    }

    private void RefreshDialogueEffectOverride()
    {
        if (currentEffectTheme != null)
            ApplyDialogueEffectOverride(currentEffectTheme.effectOverride);
        else
            ResetDialogueEffectOverride();
    }

    private void PlayDialogueEffectIntro()
    {
        PlayDialogueEffectState(dialogueEffectIntroState);
    }

    private void PlayDialogueEffectIdle()
    {
        PlayDialogueEffectState(dialogueEffectIdleState);
    }

    private void ResetDialogueEffectToHiddenIdle()
    {
        if (dialogueEffectAnimator == null)
            return;

        SetDialogueEffectVisible(true);
        PlayDialogueEffectIdle();
        SetDialogueEffectVisible(false);
    }

    private void SetDialogueEffectVisible(bool visible)
    {
        if (dialogueEffectAnimator == null)
            return;

        GameObject effectObject = dialogueEffectAnimator.gameObject;
        if (effectObject != null && effectObject.activeSelf != visible)
            effectObject.SetActive(visible);
    }

    private void PlayDialogueEffectState(string stateName)
    {
        if (dialogueEffectAnimator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        int stateHash = Animator.StringToHash(stateName);
        if (!dialogueEffectAnimator.HasState(0, stateHash))
            return;

        dialogueEffectAnimator.Play(stateHash, 0, 0f);
        dialogueEffectAnimator.Update(0f);
    }

    /// <summary>
    /// 책임 :
    /// - DialogueEffect 인트로 클립 길이를 상태 변경 없이 조회해 프리루드의 대기 시간을 계산한다.
    /// </summary>
    private float GetDialogueEffectIntroDuration()
    {
        if (dialogueEffectAnimator == null || string.IsNullOrWhiteSpace(dialogueEffectIntroState))
            return 0f;

        AnimationClip introClip = ResolveDialogueEffectClip(dialogueEffectIntroState);
        return introClip != null ? introClip.length : 0f;
    }

    private void ResetTypingAudioTracking()
    {
        lastTypedCharacterCount = 0;
        nextTypingSoundTime = 0f;
    }

    private void HandleTypingTweenUpdated()
    {
        if (!playTypingSound || dialogueText == null)
            return;

        string currentText = dialogueText.text;
        int currentCharacterCount = string.IsNullOrEmpty(currentText) ? 0 : currentText.Length;
        if (currentCharacterCount <= lastTypedCharacterCount)
            return;

        if (Time.unscaledTime >= nextTypingSoundTime)
        {
            TypingAudioUtility.PlayBossTalking(this, gameObject);
            nextTypingSoundTime = Time.unscaledTime + typingSoundInterval;
        }

        lastTypedCharacterCount = currentCharacterCount;
    }

    private AnimationClip ResolveDialogueEffectClip(string stateOrClipName)
    {
        RuntimeAnimatorController controller = dialogueEffectAnimator != null
            ? dialogueEffectAnimator.runtimeAnimatorController
            : null;

        if (controller == null)
            return null;

        if (controller is AnimatorOverrideController overrideController)
        {
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);

            foreach (KeyValuePair<AnimationClip, AnimationClip> pair in overrides)
            {
                if (!MatchesDialogueEffectClipHint(pair.Key, stateOrClipName))
                    continue;

                return pair.Value != null ? pair.Value : pair.Key;
            }

            controller = overrideController.runtimeAnimatorController;
        }

        return controller.animationClips
            .FirstOrDefault(clip => MatchesDialogueEffectClipHint(clip, stateOrClipName));
    }

    private static bool MatchesDialogueEffectClipHint(AnimationClip clip, string stateOrClipName)
    {
        if (clip == null || string.IsNullOrWhiteSpace(stateOrClipName))
            return false;

        return string.Equals(clip.name, stateOrClipName, StringComparison.OrdinalIgnoreCase)
               || clip.name.IndexOf(stateOrClipName, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SetDimPanelVisible(bool visible, bool immediate)
    {
        if (dimPanelGraphic == null)
            return;

        dimPanelGraphic.DOKill();
        dimPanelGraphic.gameObject.SetActive(visible);

        Color color = dimPanelGraphic.color;
        color.a = visible ? (immediate ? defaultDimPanelAlpha : color.a) : 0f;
        dimPanelGraphic.color = color;
    }

    private Graphic FindGraphicByName(string targetName)
    {
        return GetComponentsInChildren<Graphic>(true)
            .FirstOrDefault(graphic => string.Equals(graphic.gameObject.name, targetName, StringComparison.Ordinal));
    }

    private Transform FindChildRecursive(string targetName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, targetName, StringComparison.Ordinal))
                return child;
        }

        return null;
    }
}

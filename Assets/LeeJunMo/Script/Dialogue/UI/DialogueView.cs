using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueView : MonoBehaviour
{
    [Header("UI Groups (CanvasGroup required)")]
    [SerializeField] private CanvasGroup textBoxGroup;
    [SerializeField] private CanvasGroup affectionGroup;

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

    private Tween typingTween;
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
    private int currentChoiceIndex;
    private Action<int> onChoiceSelectedCallback;

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

        if (textBoxGroup != null)
        {
            textBoxGroup.alpha = 0f;
            textBoxGroup.gameObject.SetActive(false);
        }

        if (affectionGroup != null)
        {
            affectionGroup.alpha = 0f;
            affectionGroup.gameObject.SetActive(false);
        }

        if (continueIcon != null)
        {
            continueIcon.SetActive(false);
            RectTransform iconRect = continueIcon.GetComponent<RectTransform>();
            if (iconRect != null)
            {
                iconRect.DOAnchorPosY(iconRect.anchoredPosition.y - 10f, 0.5f)
                    .SetUpdate(true)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
        }

        ClearChoices();
        ClearText();
    }

    private void OnDestroy()
    {
        foreach (Material runtimeMaterial in runtimeThemeMaterials.Values)
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }

        runtimeThemeMaterials.Clear();
    }

    public void ClearText()
    {
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

        if (textBoxGroup != null)
        {
            textBoxGroup.gameObject.SetActive(true);

            Sequence seq = DOTween.Sequence();
            seq.SetUpdate(true);
            seq.Append(textBoxGroup.DOFade(1f, 0.25f).SetUpdate(true));

            if (isBoss && affectionGroup != null)
            {
                affectionGroup.gameObject.SetActive(true);
                seq.Join(affectionGroup.DOFade(1f, 0.25f).SetUpdate(true));
            }
            else if (!isBoss && affectionGroup != null)
            {
                affectionGroup.gameObject.SetActive(false);
                affectionGroup.alpha = 0f;
            }

            seq.OnComplete(() => onComplete?.Invoke());
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    public void PlayBossPrelude(Action onComplete = null)
    {
        RefreshThemePresentation(false);

        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true);
        float effectDuration = 0f;

        if (dimPanelGraphic != null)
        {
            SetDimPanelVisible(true, true);
            seq.Append(dimPanelGraphic.DOFade(defaultDimPanelAlpha, dimFadeDuration).SetUpdate(true));
        }

        seq.AppendCallback(() =>
        {
            SetDialogueEffectVisible(true);
            effectDuration = PlayDialogueEffectIntroAndGetDuration();
        });

        if (effectDuration <= 0f)
            effectDuration = dialogueEffectIntroFallbackDuration;

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

        if (dialogueText != null)
        {
            typingTween = dialogueText.DOText(text, text.Length * 0.05f)
                .SetUpdate(true)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
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
        currentChoiceIndex = 0;

        foreach (Ink.Runtime.Choice choice in choices)
        {
            GameObject btnObj = Instantiate(choiceButtonPrefab, choiceContainer);
            activeChoiceButtons.Add(btnObj);

            TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
                btnText.text = choice.text;

            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                int index = choice.index;
                btn.onClick.AddListener(() =>
                {
                    ClearChoices();
                    onChoiceSelectedCallback?.Invoke(index);
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

        currentChoiceIndex += direction;

        if (currentChoiceIndex < 0)
            currentChoiceIndex = activeChoiceButtons.Count - 1;
        else if (currentChoiceIndex >= activeChoiceButtons.Count)
            currentChoiceIndex = 0;

        HighlightChoice(currentChoiceIndex);
    }

    public void ConfirmChoice()
    {
        if (activeChoiceButtons.Count <= 0)
            return;

        Button selectedBtn = activeChoiceButtons[currentChoiceIndex].GetComponent<Button>();
        selectedBtn?.onClick.Invoke();
    }

    public void ClearChoices()
    {
        foreach (GameObject btn in activeChoiceButtons)
        {
            if (btn != null)
                Destroy(btn);
        }

        activeChoiceButtons.Clear();
    }

    public void HideUI(Action onComplete = null)
    {
        ClearChoices();

        if (continueIcon != null)
            continueIcon.SetActive(false);

        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true);

        if (textBoxGroup != null)
            seq.Append(textBoxGroup.DOFade(0f, 0.25f).SetUpdate(true));

        if (affectionGroup != null && affectionGroup.gameObject.activeSelf)
            seq.Join(affectionGroup.DOFade(0f, 0.25f).SetUpdate(true));

        seq.OnComplete(() =>
        {
            isUiVisible = false;

            if (textBoxGroup != null)
                textBoxGroup.gameObject.SetActive(false);

            if (affectionGroup != null)
                affectionGroup.gameObject.SetActive(false);

            SetDimPanelVisible(false, true);
            ResetDialogueEffectToHiddenIdle();
            onComplete?.Invoke();
        });
    }

    private void HighlightChoice(int index)
    {
        for (int i = 0; i < activeChoiceButtons.Count; i++)
        {
            TextMeshProUGUI btnText = activeChoiceButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (btnText == null)
                continue;

            if (i == index)
            {
                btnText.color = selectedChoiceColor;
                activeChoiceButtons[i].transform.DOScale(1.05f, 0.1f).SetUpdate(true);
            }
            else
            {
                btnText.color = normalChoiceColor;
                activeChoiceButtons[i].transform.DOScale(1.0f, 0.1f).SetUpdate(true);
            }
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

        dialogueEffectAnimator.runtimeAnimatorController = overrideController != null
            ? overrideController
            : defaultEffectController;

        dialogueEffectAnimator.Rebind();
        dialogueEffectAnimator.Update(0f);
    }

    private void ResetDialogueEffectOverride()
    {
        if (dialogueEffectAnimator == null || defaultEffectController == null)
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

    private float PlayDialogueEffectIntroAndGetDuration()
    {
        SetDialogueEffectVisible(true);
        PlayDialogueEffectIntro();

        if (dialogueEffectAnimator == null)
            return 0f;

        return dialogueEffectAnimator.GetCurrentAnimatorStateInfo(0).length;
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

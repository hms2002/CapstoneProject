using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class WeaponAbilityBlockView : MonoBehaviour
{
    [Serializable]
    private struct InputHintSpriteEntry
    {
        public string inputHint;
        public Sprite sprite;
    }

    [Header("Header")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image iconImage;

    [Header("Meta")]
    [SerializeField] private Image inputHintImage;
    [SerializeField] private TMP_Text cooldownText;
    [SerializeField] private TMP_Text extraMetaText;
    [SerializeField] private List<InputHintSpriteEntry> inputHintSprites = new();

    [Header("Body")]
    [SerializeField] private GameObject bodyRoot;
    [SerializeField] private TMP_Text bodyText;

    [Header("Variant Switch Guide")]
    [SerializeField] private GameObject variantSwitchGuideRoot;
    [SerializeField] private Image variantSwitchGuideIcon;
    [SerializeField] private TMP_Text variantSwitchGuideText;
    [SerializeField] private string variantSwitchGuideFallbackLabel = "` 모드 전환";

    [Header("Variant Shuffle")]
    [SerializeField] private RectTransform cardMotionRoot;
    [SerializeField] private RectTransform currentContainer;
    [SerializeField] private CanvasGroup currentContainerGroup;
    [SerializeField] private RectTransform nextContainer;
    [SerializeField] private CanvasGroup nextContainerGroup;
    [SerializeField] private TMP_Text nextTitleText;
    [SerializeField] private Image nextIconImage;
    [SerializeField] private Image nextInputHintImage;
    [SerializeField] private TMP_Text nextCooldownText;
    [SerializeField] private TMP_Text nextExtraMetaText;
    [SerializeField] private GameObject nextBodyRoot;
    [SerializeField] private TMP_Text nextBodyText;
    [SerializeField, Min(0f)] private float shuffleDuration = 0.16f;
    [SerializeField] private Vector2 shuffleOffset = new Vector2(36f, 0f);
    [SerializeField] private Vector2 externalPreviewOffset = new Vector2(-18f, 12f);
    [SerializeField] private Vector2 externalShuffleSeparation = new Vector2(0f, 34f);
    [SerializeField, FormerlySerializedAs("externalPreviewAlpha"), Range(0f, 1f)] private float externalPreviewBrightness = 0.55f;
    [SerializeField, Range(0f, 1f)] private float externalPreviewDesaturation = 0.85f;
    [SerializeField] private Vector2 switchingKeyDetachOffset = new Vector2(-10f, 10f);
    [SerializeField, Min(0f)] private float switchingKeyDetachScale = 1.05f;

    private Coroutine variantSwitchRoutine;
    private bool variantGuideVisible;
    private Sprite variantGuideIcon;
    private string variantGuideLabel;
    private WeaponAbilityBlockView externalShuffleNextView;
    private CanvasGroup externalShuffleNextGroup;
    private LayoutElement externalShuffleNextLayout;
    private LayoutElement currentLayoutElement;
    private bool currentLayoutElementAdded;
    private bool currentLayoutIgnoreLayoutBeforeAnimation;
    private GameObject currentLayoutPlaceholder;
    private bool externalPreviewConfigured;
    private bool hasQueuedExternalPreview;
    private ExternalPreviewPayload queuedExternalPreview;
    private readonly List<GraphicColorState> authoredGraphicColors = new();
    private bool graphicColorsCaptured;
    private bool previewMuted;
    private float appliedPreviewBrightness = -1f;
    private float appliedPreviewDesaturation = -1f;
    private RectTransform activeShuffleCurrentMotion;
    private Vector2 activeShuffleCurrentMotionStart;
    private RectTransform activeShuffleNextMotion;
    private Vector2 activeShuffleNextMotionStart;
    private RectTransform activeShuffleKeyRect;
    private Vector2 activeShuffleKeyStart;
    private Vector3 activeShuffleKeyScale;
    private LayoutGroup[] activeShuffleCurrentLayoutGroups;
    private bool[] activeShuffleCurrentLayoutGroupStates;
    private LayoutGroup[] activeShuffleNextLayoutGroups;
    private bool[] activeShuffleNextLayoutGroupStates;
    private LayoutFreezeState activeShuffleCurrentLayoutFreeze;
    private LayoutFreezeState activeShuffleNextLayoutFreeze;

    public bool IsVariantSwitching => variantSwitchRoutine != null;

    public void Set(
        string title,
        Sprite icon,
        string inputHint,
        float cooldownSeconds,
        string extraMeta,
        string body,
        Action<string> onGlossaryClick = null)
    {
        Set(title, icon, inputHint, cooldownSeconds, extraMeta, body, null, onGlossaryClick);
    }

    public void Set(
        string title,
        Sprite icon,
        string inputHint,
        float cooldownSeconds,
        string extraMeta,
        string body,
        InputActionId? inputAction,
        Action<string> onGlossaryClick = null)
    {
        StopVariantSwitchRoutine();
        SetVariantSwitchGuide(false, null, null);
        ApplyContentToCurrent(title, icon, inputHint, cooldownSeconds, extraMeta, body, inputAction, onGlossaryClick);
        ResetNextContainer();
    }

    public void SetExternalShuffleNextView(WeaponAbilityBlockView nextView)
    {
        externalShuffleNextView = nextView != this ? nextView : null;
        externalShuffleNextGroup = null;
        externalShuffleNextLayout = null;

        if (externalShuffleNextView == null)
            return;

        externalShuffleNextGroup = externalShuffleNextView.GetComponent<CanvasGroup>();
        if (externalShuffleNextGroup == null)
            externalShuffleNextGroup = externalShuffleNextView.gameObject.AddComponent<CanvasGroup>();

        externalShuffleNextLayout = externalShuffleNextView.GetComponent<LayoutElement>();
        if (externalShuffleNextLayout == null)
            externalShuffleNextLayout = externalShuffleNextView.gameObject.AddComponent<LayoutElement>();

        externalShuffleNextLayout.ignoreLayout = true;
        externalShuffleNextGroup.alpha = 0f;
        externalShuffleNextGroup.interactable = false;
        externalShuffleNextGroup.blocksRaycasts = false;
        SetExternalPanelDrawOrder(externalInFront: false);
        externalShuffleNextView.SetPreviewMuted(false, externalPreviewBrightness, externalPreviewDesaturation);
        externalPreviewConfigured = false;
        hasQueuedExternalPreview = false;
        externalShuffleNextView.SetVariantSwitchGuide(false, null, null);
        externalShuffleNextView.gameObject.SetActive(false);
    }

    public void SetExternalShufflePreview(
        string title,
        Sprite icon,
        string inputHint,
        float cooldownSeconds,
        string extraMeta,
        string body,
        InputActionId? inputAction,
        Action<string> onGlossaryClick = null)
    {
        if (externalShuffleNextView == null)
            return;

        externalPreviewConfigured = true;
        hasQueuedExternalPreview = false;
        externalShuffleNextView.gameObject.SetActive(true);
        externalShuffleNextView.Set(
            title,
            icon,
            inputHint,
            cooldownSeconds,
            extraMeta,
            body,
            inputAction,
            onGlossaryClick);
        externalShuffleNextView.SetVariantSwitchGuide(false, null, null);
        StyleRestingExternalPreview();
    }

    public void QueueExternalShufflePreview(
        string title,
        Sprite icon,
        string inputHint,
        float cooldownSeconds,
        string extraMeta,
        string body,
        InputActionId? inputAction,
        Action<string> onGlossaryClick = null)
    {
        hasQueuedExternalPreview = true;
        queuedExternalPreview = new ExternalPreviewPayload(
            title,
            icon,
            inputHint,
            cooldownSeconds,
            extraMeta,
            body,
            inputAction,
            onGlossaryClick);
    }

    public void RefreshExternalShufflePreviewLayout()
    {
        if (variantSwitchRoutine == null)
            StyleRestingExternalPreview();
    }

    public void SetVariantSwitchGuide(bool visible, Sprite icon, string label)
    {
        variantGuideVisible = visible;
        variantGuideIcon = icon;
        variantGuideLabel = string.IsNullOrWhiteSpace(label)
            ? variantSwitchGuideFallbackLabel
            : label;

        RefreshVariantSwitchGuide();
    }

    public void SetVariantDisplay(
        string title,
        Sprite icon,
        string inputHint,
        float cooldownSeconds,
        string extraMeta,
        string body,
        InputActionId? inputAction,
        bool animate,
        Action<string> onGlossaryClick = null)
    {
        if (animate && CanPlayExternalPanelShuffle())
        {
            variantSwitchRoutine = StartCoroutine(PlayExternalPanelShuffleRoutine(
                title,
                icon,
                inputHint,
                cooldownSeconds,
                extraMeta,
                body,
                inputAction,
                onGlossaryClick));
            return;
        }

        if (animate && CanPlayShuffle())
        {
            variantSwitchRoutine = StartCoroutine(PlayShuffleRoutine(
                title,
                icon,
                inputHint,
                cooldownSeconds,
                extraMeta,
                body,
                inputAction,
                onGlossaryClick));
            return;
        }

        if (animate && CanPlaySingleContainerShuffle())
        {
            variantSwitchRoutine = StartCoroutine(PlaySingleContainerShuffleRoutine(
                title,
                icon,
                inputHint,
                cooldownSeconds,
                extraMeta,
                body,
                inputAction,
                onGlossaryClick));
            return;
        }

        ApplyContentToCurrent(title, icon, inputHint, cooldownSeconds, extraMeta, body, inputAction, onGlossaryClick);
        ResetNextContainer();
    }

    private IEnumerator PlayExternalPanelShuffleRoutine(
        string title,
        Sprite icon,
        string inputHint,
        float cooldownSeconds,
        string extraMeta,
        string body,
        InputActionId? inputAction,
        Action<string> onGlossaryClick)
    {
        RectTransform currentRect = transform as RectTransform;
        RectTransform nextRect = externalShuffleNextView.transform as RectTransform;
        if (CanUseCardMotionShuffle(currentRect, nextRect, out RectTransform currentMotion, out RectTransform nextMotion))
        {
            yield return PlayExternalCardMotionShuffleRoutine(
                currentRect,
                nextRect,
                currentMotion,
                nextMotion,
                title,
                icon,
                inputHint,
                cooldownSeconds,
                extraMeta,
                body,
                inputAction,
                onGlossaryClick);
            yield break;
        }

        CanvasGroup currentGroup = ResolveCurrentPanelCanvasGroup();
        CanvasGroup nextGroup = ResolveExternalNextCanvasGroup();

        Vector2 start = currentRect.anchoredPosition;
        Vector2 separation = externalShuffleSeparation.sqrMagnitude > 0.0001f
            ? externalShuffleSeparation
            : new Vector2(0f, 34f);
        Vector2 previewStart = start + externalPreviewOffset;
        Vector2 currentApart = start - separation;
        Vector2 nextApart = previewStart + separation;

        PrepareCurrentLayoutAnimation(currentRect);
        currentRect.anchoredPosition = start;
        externalShuffleNextView.gameObject.SetActive(true);
        externalShuffleNextView.Set(
            title,
            icon,
            inputHint,
            cooldownSeconds,
            extraMeta,
            body,
            inputAction,
            onGlossaryClick);
        externalShuffleNextView.SetVariantSwitchGuide(false, null, null);
        SyncExternalNextRect(currentRect, nextRect);
        PlaceExternalNextBehindCurrent(currentRect, nextRect);
        SetExternalPanelDrawOrder(externalInFront: false);
        SetPreviewMuted(false, externalPreviewBrightness, externalPreviewDesaturation);
        externalShuffleNextView.SetPreviewMuted(true, externalPreviewBrightness, externalPreviewDesaturation);

        currentRect.anchoredPosition = start;
        nextRect.anchoredPosition = previewStart;
        SetAlpha(currentGroup, 1f);
        SetAlpha(nextGroup, 1f);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, shuffleDuration);
        float halfDuration = duration * 0.5f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float eased = t * t * (3f - 2f * t);

            currentRect.anchoredPosition = Vector2.Lerp(start, currentApart, eased);
            nextRect.anchoredPosition = Vector2.Lerp(previewStart, nextApart, eased);
            SetAlpha(currentGroup, 1f);
            SetAlpha(nextGroup, 1f);
            yield return null;
        }

        SetExternalPanelDrawOrder(externalInFront: true);
        SetPreviewMuted(true, externalPreviewBrightness, externalPreviewDesaturation);
        externalShuffleNextView.SetPreviewMuted(false, externalPreviewBrightness, externalPreviewDesaturation);

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float eased = t * t * (3f - 2f * t);

            currentRect.anchoredPosition = Vector2.Lerp(currentApart, previewStart, eased);
            nextRect.anchoredPosition = Vector2.Lerp(nextApart, start, eased);
            SetAlpha(currentGroup, 1f);
            SetAlpha(nextGroup, 1f);
            yield return null;
        }

        ApplyContentToCurrent(title, icon, inputHint, cooldownSeconds, extraMeta, body, inputAction, onGlossaryClick);
        currentRect.anchoredPosition = start;
        nextRect.anchoredPosition = start;
        SetAlpha(currentGroup, 1f);
        SetAlpha(nextGroup, 1f);
        SetPreviewMuted(false, externalPreviewBrightness, externalPreviewDesaturation);
        RestoreCurrentLayoutAnimation();
        SetExternalPanelDrawOrder(externalInFront: false);
        ApplyQueuedExternalPreviewOrRestore();
        variantSwitchRoutine = null;
        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator PlayExternalCardMotionShuffleRoutine(
        RectTransform currentRect,
        RectTransform nextRect,
        RectTransform currentMotion,
        RectTransform nextMotion,
        string title,
        Sprite icon,
        string inputHint,
        float cooldownSeconds,
        string extraMeta,
        string body,
        InputActionId? inputAction,
        Action<string> onGlossaryClick)
    {
        CanvasGroup currentGroup = ResolveCurrentPanelCanvasGroup();
        CanvasGroup nextGroup = ResolveExternalNextCanvasGroup();

        Vector2 rootStart = currentRect.anchoredPosition;
        Vector2 previewRootStart = rootStart + externalPreviewOffset;
        Vector2 separation = externalShuffleSeparation.sqrMagnitude > 0.0001f
            ? externalShuffleSeparation
            : new Vector2(0f, 34f);

        Vector2 currentMotionStart = currentMotion.anchoredPosition;
        Vector2 nextMotionStart = currentMotionStart;
        Vector2 currentApart = currentMotionStart - separation;
        Vector2 nextApart = nextMotionStart + separation;
        Vector2 currentPreviewPosition = currentMotionStart + externalPreviewOffset;
        Vector2 nextActivePosition = currentMotionStart;

        RectTransform keyRect = variantSwitchGuideRoot != null
            ? variantSwitchGuideRoot.transform as RectTransform
            : null;
        Vector2 keyStart = keyRect != null ? keyRect.anchoredPosition : Vector2.zero;
        Vector3 keyScale = keyRect != null ? keyRect.localScale : Vector3.one;
        Vector2 keyDetached = keyStart + switchingKeyDetachOffset;
        Vector3 keyDetachedScale = keyScale * Mathf.Max(0f, switchingKeyDetachScale);
        RegisterActiveCardMotionShuffle(
            currentRect,
            currentMotion,
            currentMotionStart,
            nextRect,
            nextMotion,
            nextMotionStart,
            keyRect,
            keyStart,
            keyScale);

        externalShuffleNextView.gameObject.SetActive(true);
        externalShuffleNextView.Set(
            title,
            icon,
            inputHint,
            cooldownSeconds,
            extraMeta,
            body,
            inputAction,
            onGlossaryClick);
        externalShuffleNextView.SetVariantSwitchGuide(false, null, null);
        SyncExternalNextRect(currentRect, nextRect);
        SetExternalPanelDrawOrder(externalInFront: false);

        currentRect.anchoredPosition = rootStart;
        nextRect.anchoredPosition = previewRootStart;
        currentMotion.anchoredPosition = currentMotionStart;
        nextMotion.anchoredPosition = nextMotionStart;
        SetAlpha(currentGroup, 1f);
        SetAlpha(nextGroup, 1f);
        SetPreviewMuted(false, externalPreviewBrightness, externalPreviewDesaturation);
        externalShuffleNextView.SetPreviewMuted(true, externalPreviewBrightness, externalPreviewDesaturation);

        float duration = Mathf.Max(0.01f, shuffleDuration);
        float halfDuration = duration * 0.5f;
        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float eased = SmoothStep(t);

            currentMotion.anchoredPosition = Vector2.Lerp(currentMotionStart, currentApart, eased);
            nextMotion.anchoredPosition = Vector2.Lerp(nextMotionStart, nextApart, eased);
            ApplySwitchingKeyPose(keyRect, keyStart, keyDetached, keyScale, keyDetachedScale, eased);
            yield return null;
        }

        SetExternalPanelDrawOrder(externalInFront: true);
        SetPreviewMuted(true, externalPreviewBrightness, externalPreviewDesaturation);
        externalShuffleNextView.SetPreviewMuted(false, externalPreviewBrightness, externalPreviewDesaturation);

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float eased = SmoothStep(t);

            currentMotion.anchoredPosition = Vector2.Lerp(currentApart, currentPreviewPosition, eased);
            nextMotion.anchoredPosition = Vector2.Lerp(nextApart, nextActivePosition, eased);
            nextRect.anchoredPosition = Vector2.Lerp(previewRootStart, rootStart, eased);
            ApplySwitchingKeyPose(keyRect, keyDetached, keyStart, keyDetachedScale, keyScale, eased);
            yield return null;
        }

        ApplyContentToCurrent(title, icon, inputHint, cooldownSeconds, extraMeta, body, inputAction, onGlossaryClick);
        currentRect.anchoredPosition = rootStart;
        nextRect.anchoredPosition = rootStart;
        currentMotion.anchoredPosition = currentMotionStart;
        nextMotion.anchoredPosition = nextMotionStart;
        ApplySwitchingKeyPose(keyRect, keyStart, keyStart, keyScale, keyScale, 1f);
        SetAlpha(currentGroup, 1f);
        SetAlpha(nextGroup, 1f);
        SetPreviewMuted(false, externalPreviewBrightness, externalPreviewDesaturation);
        SetExternalPanelDrawOrder(externalInFront: false);
        ApplyQueuedExternalPreviewOrRestore();
        RestoreActiveCardMotionShuffleLayouts();
        ClearActiveCardMotionShuffle();
        variantSwitchRoutine = null;
        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator PlaySingleContainerShuffleRoutine(
        string title,
        Sprite icon,
        string inputHint,
        float cooldownSeconds,
        string extraMeta,
        string body,
        InputActionId? inputAction,
        Action<string> onGlossaryClick)
    {
        RectTransform container = ResolveCurrentContainer();
        Vector2 start = container.anchoredPosition;
        Vector2 offset = shuffleOffset.sqrMagnitude > 0.0001f ? shuffleOffset : new Vector2(36f, 0f);
        float halfDuration = Mathf.Max(0.01f, shuffleDuration * 0.5f);

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float eased = t * t * (3f - 2f * t);
            container.anchoredPosition = Vector2.Lerp(start, start - offset, eased);
            SetAlpha(currentContainerGroup, 1f - eased);
            yield return null;
        }

        ApplyContentToCurrent(title, icon, inputHint, cooldownSeconds, extraMeta, body, inputAction, onGlossaryClick);
        container.anchoredPosition = start + offset;
        SetAlpha(currentContainerGroup, 0f);

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / halfDuration);
            float eased = t * t * (3f - 2f * t);
            container.anchoredPosition = Vector2.Lerp(start + offset, start, eased);
            SetAlpha(currentContainerGroup, eased);
            yield return null;
        }

        container.anchoredPosition = start;
        SetAlpha(currentContainerGroup, 1f);
        variantSwitchRoutine = null;
        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator PlayShuffleRoutine(
        string title,
        Sprite icon,
        string inputHint,
        float cooldownSeconds,
        string extraMeta,
        string body,
        InputActionId? inputAction,
        Action<string> onGlossaryClick)
    {
        Vector2 currentStart = currentContainer.anchoredPosition;
        Vector2 nextStart = nextContainer.anchoredPosition;
        Vector2 offset = shuffleOffset.sqrMagnitude > 0.0001f ? shuffleOffset : new Vector2(36f, 0f);

        ApplyContentToNext(title, icon, inputHint, cooldownSeconds, extraMeta, body, inputAction);
        nextContainer.gameObject.SetActive(true);
        currentContainer.anchoredPosition = currentStart;
        nextContainer.anchoredPosition = nextStart + offset;
        SetAlpha(currentContainerGroup, 1f);
        SetAlpha(nextContainerGroup, 0f);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, shuffleDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);

            currentContainer.anchoredPosition = Vector2.Lerp(currentStart, currentStart - offset, eased);
            nextContainer.anchoredPosition = Vector2.Lerp(nextStart + offset, nextStart, eased);
            SetAlpha(currentContainerGroup, 1f - eased);
            SetAlpha(nextContainerGroup, eased);
            yield return null;
        }

        ApplyContentToCurrent(title, icon, inputHint, cooldownSeconds, extraMeta, body, inputAction, onGlossaryClick);
        currentContainer.anchoredPosition = currentStart;
        nextContainer.anchoredPosition = nextStart;
        SetAlpha(currentContainerGroup, 1f);
        ResetNextContainer();
        variantSwitchRoutine = null;
        Canvas.ForceUpdateCanvases();
    }

    private void ApplyContentToCurrent(
        string title,
        Sprite icon,
        string inputHint,
        float cooldownSeconds,
        string extraMeta,
        string body,
        InputActionId? inputAction,
        Action<string> onGlossaryClick)
    {
        ApplyContent(
            titleText,
            iconImage,
            inputHintImage,
            cooldownText,
            extraMetaText,
            bodyRoot,
            bodyText,
            title,
            icon,
            inputHint,
            cooldownSeconds,
            extraMeta,
            body,
            inputAction,
            onGlossaryClick);

        RefreshVariantSwitchGuide();
    }

    private void ApplyContentToNext(
        string title,
        Sprite icon,
        string inputHint,
        float cooldownSeconds,
        string extraMeta,
        string body,
        InputActionId? inputAction)
    {
        ApplyContent(
            nextTitleText,
            nextIconImage,
            nextInputHintImage,
            nextCooldownText,
            nextExtraMetaText,
            nextBodyRoot,
            nextBodyText,
            title,
            icon,
            inputHint,
            cooldownSeconds,
            extraMeta,
            body,
            inputAction,
            null);
    }

    private void ApplyContent(
        TMP_Text targetTitle,
        Image targetIcon,
        Image targetInputHint,
        TMP_Text targetCooldown,
        TMP_Text targetExtraMeta,
        GameObject targetBodyRoot,
        TMP_Text targetBody,
        string title,
        Sprite icon,
        string inputHint,
        float cooldownSeconds,
        string extraMeta,
        string body,
        InputActionId? inputAction,
        Action<string> onGlossaryClick)
    {
        if (targetTitle != null)
            targetTitle.text = title ?? string.Empty;

        if (targetIcon != null)
        {
            targetIcon.sprite = icon;
            targetIcon.enabled = icon != null;
        }

        ApplyInputHintSprite(inputHint, inputAction, targetInputHint);

        if (targetCooldown != null)
            targetCooldown.text = cooldownSeconds > 0f ? $"{cooldownSeconds:0.##}s" : string.Empty;

        if (targetExtraMeta != null)
            targetExtraMeta.text = string.IsNullOrEmpty(extraMeta) ? "-" : extraMeta;

        if (targetBody != null)
        {
            targetBody.text = body ?? string.Empty;

            if (targetBodyRoot != null)
                targetBodyRoot.SetActive(!string.IsNullOrWhiteSpace(targetBody.text));

            if (onGlossaryClick != null)
            {
                TmpLinkClickHandler handler = targetBody.GetComponent<TmpLinkClickHandler>();
                if (handler == null)
                    handler = targetBody.gameObject.AddComponent<TmpLinkClickHandler>();

                handler.onGlossaryKeyClicked = onGlossaryClick;
            }
        }
        else if (targetBodyRoot != null)
        {
            targetBodyRoot.SetActive(false);
        }
    }

    private void ApplyInputHintSprite(string inputHint, InputActionId? inputAction, Image targetImage)
    {
        if (targetImage == null)
            return;

        Sprite resolvedSprite = ResolveInputHintSprite(inputHint, inputAction);
        targetImage.sprite = resolvedSprite;
        targetImage.enabled = resolvedSprite != null;
    }

    private Sprite ResolveInputHintSprite(string inputHint, InputActionId? inputAction)
    {
        if (inputAction.HasValue)
        {
            InputGlyphPresentation glyph = InputBindingService.EnsureInstance().GetBindingGlyph(inputAction.Value);
            Sprite glyphSprite = InputGlyphVisualUtility.ResolveIcon(
                glyph,
                inputHint,
                ResolveMappedInputHintSprite);
            if (glyphSprite != null)
                return glyphSprite;
        }

        return ResolveMappedInputHintSprite(inputHint);
    }

    private Sprite ResolveMappedInputHintSprite(string inputHint)
    {
        if (string.IsNullOrWhiteSpace(inputHint) || inputHintSprites == null)
            return null;

        string normalized = inputHint.Trim();
        for (int i = 0; i < inputHintSprites.Count; i++)
        {
            InputHintSpriteEntry entry = inputHintSprites[i];
            if (string.Equals(entry.inputHint?.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                return entry.sprite;
        }

        return null;
    }

    private void RefreshVariantSwitchGuide()
    {
        if (variantSwitchGuideRoot != null)
            variantSwitchGuideRoot.SetActive(variantGuideVisible);

        if (variantSwitchGuideIcon != null)
        {
            variantSwitchGuideIcon.sprite = variantGuideIcon;
            variantSwitchGuideIcon.enabled = variantGuideVisible && variantGuideIcon != null;
        }

        if (variantSwitchGuideText != null)
            variantSwitchGuideText.text = variantGuideVisible ? variantGuideLabel : string.Empty;
    }

    private bool CanPlayShuffle()
    {
        return currentContainer != null &&
               nextContainer != null &&
               nextTitleText != null &&
               nextBodyText != null;
    }

    private bool CanPlayExternalPanelShuffle()
    {
        return externalShuffleNextView != null &&
               transform is RectTransform &&
               externalShuffleNextView.transform is RectTransform &&
               shuffleDuration > 0f;
    }

    private bool CanPlaySingleContainerShuffle()
    {
        return ResolveCurrentContainer() != null && shuffleDuration > 0f;
    }

    private RectTransform ResolveCurrentContainer()
    {
        return currentContainer != null ? currentContainer : transform as RectTransform;
    }

    private RectTransform ResolveCardMotionRoot()
    {
        return cardMotionRoot != null ? cardMotionRoot : transform as RectTransform;
    }

    private bool CanUseCardMotionShuffle(
        RectTransform currentRect,
        RectTransform nextRect,
        out RectTransform currentMotion,
        out RectTransform nextMotion)
    {
        currentMotion = ResolveCardMotionRoot();
        nextMotion = externalShuffleNextView != null
            ? externalShuffleNextView.ResolveCardMotionRoot()
            : null;

        return currentRect != null &&
               nextRect != null &&
               currentMotion != null &&
               nextMotion != null &&
               currentMotion != currentRect &&
               nextMotion != nextRect;
    }

    private void RegisterActiveCardMotionShuffle(
        RectTransform currentRoot,
        RectTransform currentMotion,
        Vector2 currentMotionStart,
        RectTransform nextRoot,
        RectTransform nextMotion,
        Vector2 nextMotionStart,
        RectTransform keyRect,
        Vector2 keyStart,
        Vector3 keyScale)
    {
        activeShuffleCurrentMotion = currentMotion;
        activeShuffleCurrentMotionStart = currentMotionStart;
        activeShuffleNextMotion = nextMotion;
        activeShuffleNextMotionStart = nextMotionStart;
        activeShuffleKeyRect = keyRect;
        activeShuffleKeyStart = keyStart;
        activeShuffleKeyScale = keyScale;

        activeShuffleCurrentLayoutFreeze = FreezeLayoutElement(currentRoot, forceParticipatesInParentLayout: true);
        activeShuffleNextLayoutFreeze = FreezeLayoutElement(nextRoot, forceParticipatesInParentLayout: false);
        activeShuffleCurrentLayoutGroups = CaptureLayoutGroups(currentRoot, out activeShuffleCurrentLayoutGroupStates);
        activeShuffleNextLayoutGroups = CaptureLayoutGroups(nextRoot, out activeShuffleNextLayoutGroupStates);
        SetLayoutGroupsEnabled(activeShuffleCurrentLayoutGroups, false);
        SetLayoutGroupsEnabled(activeShuffleNextLayoutGroups, false);
    }

    private void RestoreActiveCardMotionShufflePose()
    {
        if (activeShuffleCurrentMotion != null)
            activeShuffleCurrentMotion.anchoredPosition = activeShuffleCurrentMotionStart;

        if (activeShuffleNextMotion != null)
            activeShuffleNextMotion.anchoredPosition = activeShuffleNextMotionStart;

        if (activeShuffleKeyRect != null)
        {
            activeShuffleKeyRect.anchoredPosition = activeShuffleKeyStart;
            activeShuffleKeyRect.localScale = activeShuffleKeyScale;
        }

        RestoreActiveCardMotionShuffleLayouts();
        ClearActiveCardMotionShuffle();
    }

    private void ClearActiveCardMotionShuffle()
    {
        activeShuffleCurrentMotion = null;
        activeShuffleNextMotion = null;
        activeShuffleKeyRect = null;
        activeShuffleCurrentLayoutGroups = null;
        activeShuffleCurrentLayoutGroupStates = null;
        activeShuffleNextLayoutGroups = null;
        activeShuffleNextLayoutGroupStates = null;
        activeShuffleCurrentLayoutFreeze = null;
        activeShuffleNextLayoutFreeze = null;
    }

    private static LayoutFreezeState FreezeLayoutElement(RectTransform rect, bool forceParticipatesInParentLayout)
    {
        if (rect == null)
            return null;

        Rect sourceRect = rect.rect;
        float frozenMinWidth = ResolveLayoutMetric(LayoutUtility.GetMinWidth(rect), sourceRect.width);
        float frozenMinHeight = ResolveLayoutMetric(LayoutUtility.GetMinHeight(rect), sourceRect.height);
        float frozenPreferredWidth = ResolveLayoutMetric(LayoutUtility.GetPreferredWidth(rect), sourceRect.width);
        float frozenPreferredHeight = ResolveLayoutMetric(LayoutUtility.GetPreferredHeight(rect), sourceRect.height);
        float frozenFlexibleWidth = Mathf.Max(0f, LayoutUtility.GetFlexibleWidth(rect));
        float frozenFlexibleHeight = Mathf.Max(0f, LayoutUtility.GetFlexibleHeight(rect));

        LayoutElement element = rect.GetComponent<LayoutElement>();
        bool added = element == null;
        if (element == null)
            element = rect.gameObject.AddComponent<LayoutElement>();

        LayoutFreezeState state = new LayoutFreezeState(element, added);

        element.ignoreLayout = forceParticipatesInParentLayout ? false : state.IgnoreLayout;
        element.minWidth = frozenMinWidth;
        element.minHeight = frozenMinHeight;
        element.preferredWidth = frozenPreferredWidth;
        element.preferredHeight = frozenPreferredHeight;
        element.flexibleWidth = frozenFlexibleWidth;
        element.flexibleHeight = frozenFlexibleHeight;
        element.layoutPriority = Mathf.Max(element.layoutPriority, 100);

        return state;
    }

    private static LayoutGroup[] CaptureLayoutGroups(RectTransform root, out bool[] states)
    {
        if (root == null)
        {
            states = Array.Empty<bool>();
            return Array.Empty<LayoutGroup>();
        }

        LayoutGroup[] groups = root.GetComponents<LayoutGroup>();
        states = new bool[groups.Length];
        for (int i = 0; i < groups.Length; i++)
            states[i] = groups[i] != null && groups[i].enabled;

        return groups;
    }

    private static void SetLayoutGroupsEnabled(LayoutGroup[] groups, bool enabled)
    {
        if (groups == null)
            return;

        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null)
                groups[i].enabled = enabled;
        }
    }

    private void RestoreActiveCardMotionShuffleLayouts()
    {
        RestoreLayoutGroups(activeShuffleCurrentLayoutGroups, activeShuffleCurrentLayoutGroupStates);
        RestoreLayoutGroups(activeShuffleNextLayoutGroups, activeShuffleNextLayoutGroupStates);
        RestoreLayoutFreeze(activeShuffleCurrentLayoutFreeze);
        RestoreLayoutFreeze(activeShuffleNextLayoutFreeze);
    }

    private static void RestoreLayoutGroups(LayoutGroup[] groups, bool[] states)
    {
        if (groups == null || states == null)
            return;

        int count = Mathf.Min(groups.Length, states.Length);
        for (int i = 0; i < count; i++)
        {
            if (groups[i] != null)
                groups[i].enabled = states[i];
        }
    }

    private static void RestoreLayoutFreeze(LayoutFreezeState state)
    {
        if (state == null || state.Element == null)
            return;

        if (state.Added)
        {
            UnityEngine.Object.Destroy(state.Element);
            return;
        }

        state.Restore();
    }

    private void ResetNextContainer()
    {
        if (nextContainer != null)
            nextContainer.gameObject.SetActive(false);

        SetAlpha(nextContainerGroup, 0f);

        if (externalShuffleNextView != null)
        {
            if (externalPreviewConfigured)
                StyleRestingExternalPreview();
            else
            {
                externalShuffleNextView.gameObject.SetActive(false);
                SetAlpha(externalShuffleNextGroup, 0f);
            }
        }
    }

    private void StopVariantSwitchRoutine()
    {
        if (variantSwitchRoutine == null)
            return;

        StopCoroutine(variantSwitchRoutine);
        variantSwitchRoutine = null;

        if (currentContainer != null)
            SetAlpha(currentContainerGroup, 1f);

        SetAlpha(ResolveCurrentPanelCanvasGroup(), 1f);
        RestoreActiveCardMotionShufflePose();
        RestoreCurrentLayoutAnimation();
        SetExternalPanelDrawOrder(externalInFront: false);
        ResetNextContainer();
    }

    private static void SetAlpha(CanvasGroup group, float alpha)
    {
        if (group != null)
            group.alpha = alpha;
    }

    private CanvasGroup ResolveCurrentPanelCanvasGroup()
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        return group != null ? group : gameObject.AddComponent<CanvasGroup>();
    }

    private CanvasGroup ResolveExternalNextCanvasGroup()
    {
        if (externalShuffleNextView == null)
            return null;

        if (externalShuffleNextGroup == null)
        {
            externalShuffleNextGroup = externalShuffleNextView.GetComponent<CanvasGroup>();
            if (externalShuffleNextGroup == null)
                externalShuffleNextGroup = externalShuffleNextView.gameObject.AddComponent<CanvasGroup>();
        }

        return externalShuffleNextGroup;
    }

    private void ApplyQueuedExternalPreviewOrRestore()
    {
        if (!hasQueuedExternalPreview)
        {
            StyleRestingExternalPreview();
            return;
        }

        hasQueuedExternalPreview = false;
        SetExternalShufflePreview(
            queuedExternalPreview.Title,
            queuedExternalPreview.Icon,
            queuedExternalPreview.InputHint,
            queuedExternalPreview.CooldownSeconds,
            queuedExternalPreview.ExtraMeta,
            queuedExternalPreview.Body,
            queuedExternalPreview.InputAction,
            queuedExternalPreview.OnGlossaryClick);
    }

    private void StyleRestingExternalPreview()
    {
        if (externalShuffleNextView == null || !externalPreviewConfigured)
            return;

        RectTransform currentRect = transform as RectTransform;
        RectTransform nextRect = externalShuffleNextView.transform as RectTransform;
        CanvasGroup nextGroup = ResolveExternalNextCanvasGroup();

        if (currentRect == null || nextRect == null)
            return;

        externalShuffleNextView.gameObject.SetActive(true);
        SyncExternalNextRect(currentRect, nextRect);
        PlaceExternalNextBehindCurrent(currentRect, nextRect);
        SetExternalPanelDrawOrder(externalInFront: false);
        if (CanUseCardMotionShuffle(currentRect, nextRect, out RectTransform currentMotion, out RectTransform nextMotion))
        {
            nextRect.anchoredPosition = currentRect.anchoredPosition + externalPreviewOffset;
            nextMotion.anchoredPosition = currentMotion.anchoredPosition;
        }
        else
        {
            nextRect.anchoredPosition = currentRect.anchoredPosition + externalPreviewOffset;
        }

        SetAlpha(nextGroup, 1f);
        externalShuffleNextView.SetPreviewMuted(true, externalPreviewBrightness, externalPreviewDesaturation);

        if (nextGroup != null)
        {
            nextGroup.interactable = false;
            nextGroup.blocksRaycasts = false;
        }
    }

    private static void SyncExternalNextRect(RectTransform currentRect, RectTransform nextRect)
    {
        if (currentRect == null || nextRect == null)
            return;

        nextRect.anchorMin = currentRect.anchorMin;
        nextRect.anchorMax = currentRect.anchorMax;
        nextRect.pivot = currentRect.pivot;
        nextRect.sizeDelta = currentRect.sizeDelta;
        nextRect.localScale = currentRect.localScale;
        nextRect.localRotation = currentRect.localRotation;
    }

    private static void PlaceExternalNextBehindCurrent(RectTransform currentRect, RectTransform nextRect)
    {
        if (currentRect == null || nextRect == null || currentRect.parent != nextRect.parent)
            return;

        int currentIndex = currentRect.GetSiblingIndex();
        int nextIndex = nextRect.GetSiblingIndex();
        if (nextIndex > currentIndex)
        {
            nextRect.SetSiblingIndex(currentIndex);
        }
        else if (nextIndex < currentIndex - 1)
        {
            nextRect.SetSiblingIndex(currentIndex - 1);
        }
    }

    private void PrepareCurrentLayoutAnimation(RectTransform currentRect)
    {
        if (currentRect == null || currentLayoutPlaceholder != null)
            return;

        RectTransform parent = currentRect.parent as RectTransform;
        if (parent == null)
            return;

        currentLayoutElement = GetComponent<LayoutElement>();
        currentLayoutElementAdded = currentLayoutElement == null;
        if (currentLayoutElement == null)
            currentLayoutElement = gameObject.AddComponent<LayoutElement>();

        currentLayoutIgnoreLayoutBeforeAnimation = currentLayoutElement.ignoreLayout;

        currentLayoutPlaceholder = new GameObject($"{name}_LayoutPlaceholder", typeof(RectTransform), typeof(LayoutElement));
        RectTransform placeholderRect = currentLayoutPlaceholder.GetComponent<RectTransform>();
        LayoutElement placeholderLayout = currentLayoutPlaceholder.GetComponent<LayoutElement>();

        placeholderRect.SetParent(parent, false);
        placeholderRect.SetSiblingIndex(currentRect.GetSiblingIndex());
        placeholderRect.anchorMin = currentRect.anchorMin;
        placeholderRect.anchorMax = currentRect.anchorMax;
        placeholderRect.pivot = currentRect.pivot;
        placeholderRect.sizeDelta = currentRect.sizeDelta;
        placeholderRect.localScale = currentRect.localScale;
        placeholderRect.localRotation = currentRect.localRotation;

        Rect rect = currentRect.rect;
        placeholderLayout.minWidth = ResolveLayoutMetric(LayoutUtility.GetMinWidth(currentRect), rect.width);
        placeholderLayout.minHeight = ResolveLayoutMetric(LayoutUtility.GetMinHeight(currentRect), rect.height);
        placeholderLayout.preferredWidth = ResolveLayoutMetric(LayoutUtility.GetPreferredWidth(currentRect), rect.width);
        placeholderLayout.preferredHeight = ResolveLayoutMetric(LayoutUtility.GetPreferredHeight(currentRect), rect.height);
        placeholderLayout.flexibleWidth = Mathf.Max(0f, LayoutUtility.GetFlexibleWidth(currentRect));
        placeholderLayout.flexibleHeight = Mathf.Max(0f, LayoutUtility.GetFlexibleHeight(currentRect));
        placeholderLayout.ignoreLayout = false;

        currentLayoutElement.ignoreLayout = true;
        LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
    }

    private void RestoreCurrentLayoutAnimation()
    {
        RectTransform placeholderParent = null;
        if (currentLayoutPlaceholder != null)
        {
            placeholderParent = currentLayoutPlaceholder.transform.parent as RectTransform;
            LayoutElement placeholderLayout = currentLayoutPlaceholder.GetComponent<LayoutElement>();
            if (placeholderLayout != null)
                placeholderLayout.ignoreLayout = true;

            currentLayoutPlaceholder.SetActive(false);
        }

        if (currentLayoutElement != null)
            currentLayoutElement.ignoreLayout = currentLayoutIgnoreLayoutBeforeAnimation;

        if (placeholderParent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(placeholderParent);

        if (currentLayoutElementAdded && currentLayoutElement != null)
            Destroy(currentLayoutElement);

        currentLayoutElement = null;
        currentLayoutElementAdded = false;

        if (currentLayoutPlaceholder != null)
            Destroy(currentLayoutPlaceholder);

        currentLayoutPlaceholder = null;
    }

    private static float ResolveLayoutMetric(float value, float fallback)
    {
        return value > 0f ? value : Mathf.Max(0f, fallback);
    }

    private void SetExternalPanelDrawOrder(bool externalInFront)
    {
        if (externalShuffleNextView == null)
            return;

        RectTransform currentRect = transform as RectTransform;
        RectTransform nextRect = externalShuffleNextView.transform as RectTransform;
        if (externalInFront)
            PlaceExternalNextInFrontOfCurrent(currentRect, nextRect);
        else
            PlaceExternalNextBehindCurrent(currentRect, nextRect);
    }

    private static void PlaceExternalNextInFrontOfCurrent(RectTransform currentRect, RectTransform nextRect)
    {
        if (currentRect == null || nextRect == null || currentRect.parent != nextRect.parent)
            return;

        int currentIndex = currentRect.GetSiblingIndex();
        int nextIndex = nextRect.GetSiblingIndex();
        if (nextIndex == currentIndex + 1)
            return;

        int targetIndex = nextIndex < currentIndex ? currentIndex : currentIndex + 1;
        nextRect.SetSiblingIndex(targetIndex);
    }

    private void SetPreviewMuted(bool muted, float brightnessOverride, float desaturationOverride)
    {
        CaptureAuthoredGraphicColors();

        float brightness = Mathf.Clamp01(brightnessOverride);
        float desaturation = Mathf.Clamp01(desaturationOverride);
        if (previewMuted == muted &&
            Mathf.Approximately(appliedPreviewBrightness, brightness) &&
            Mathf.Approximately(appliedPreviewDesaturation, desaturation))
        {
            return;
        }

        for (int i = 0; i < authoredGraphicColors.Count; i++)
        {
            Graphic graphic = authoredGraphicColors[i].Graphic;
            if (graphic == null)
                continue;

            graphic.color = muted
                ? BuildMutedColor(authoredGraphicColors[i].AuthoredColor, brightness, desaturation)
                : authoredGraphicColors[i].AuthoredColor;
        }

        previewMuted = muted;
        appliedPreviewBrightness = brightness;
        appliedPreviewDesaturation = desaturation;
    }

    private void CaptureAuthoredGraphicColors()
    {
        if (graphicColorsCaptured)
            return;

        authoredGraphicColors.Clear();
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null && !IsUnderVariantSwitchGuide(graphics[i].transform))
                authoredGraphicColors.Add(new GraphicColorState(graphics[i], graphics[i].color));
        }

        graphicColorsCaptured = true;
    }

    private bool IsUnderVariantSwitchGuide(Transform target)
    {
        if (variantSwitchGuideRoot == null || target == null)
            return false;

        return target == variantSwitchGuideRoot.transform ||
               target.IsChildOf(variantSwitchGuideRoot.transform);
    }

    private static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private static void ApplySwitchingKeyPose(
        RectTransform keyRect,
        Vector2 fromPosition,
        Vector2 toPosition,
        Vector3 fromScale,
        Vector3 toScale,
        float t)
    {
        if (keyRect == null)
            return;

        keyRect.anchoredPosition = Vector2.Lerp(fromPosition, toPosition, t);
        keyRect.localScale = Vector3.Lerp(fromScale, toScale, t);
    }

    private static Color BuildMutedColor(Color source, float brightness, float desaturation)
    {
        float luminance = source.r * 0.2126f + source.g * 0.7152f + source.b * 0.0722f;
        Color grayscale = new Color(luminance, luminance, luminance, source.a);
        Color muted = Color.Lerp(source, grayscale, desaturation);
        muted.r *= brightness;
        muted.g *= brightness;
        muted.b *= brightness;
        muted.a = source.a;
        return muted;
    }

    private readonly struct ExternalPreviewPayload
    {
        public ExternalPreviewPayload(
            string title,
            Sprite icon,
            string inputHint,
            float cooldownSeconds,
            string extraMeta,
            string body,
            InputActionId? inputAction,
            Action<string> onGlossaryClick)
        {
            Title = title;
            Icon = icon;
            InputHint = inputHint;
            CooldownSeconds = cooldownSeconds;
            ExtraMeta = extraMeta;
            Body = body;
            InputAction = inputAction;
            OnGlossaryClick = onGlossaryClick;
        }

        public string Title { get; }
        public Sprite Icon { get; }
        public string InputHint { get; }
        public float CooldownSeconds { get; }
        public string ExtraMeta { get; }
        public string Body { get; }
        public InputActionId? InputAction { get; }
        public Action<string> OnGlossaryClick { get; }
    }

    private readonly struct GraphicColorState
    {
        public GraphicColorState(Graphic graphic, Color authoredColor)
        {
            Graphic = graphic;
            AuthoredColor = authoredColor;
        }

        public Graphic Graphic { get; }
        public Color AuthoredColor { get; }
    }

    private sealed class LayoutFreezeState
    {
        public LayoutFreezeState(LayoutElement element, bool added)
        {
            Element = element;
            Added = added;

            if (element == null)
                return;

            IgnoreLayout = element.ignoreLayout;
            MinWidth = element.minWidth;
            MinHeight = element.minHeight;
            PreferredWidth = element.preferredWidth;
            PreferredHeight = element.preferredHeight;
            FlexibleWidth = element.flexibleWidth;
            FlexibleHeight = element.flexibleHeight;
            LayoutPriority = element.layoutPriority;
        }

        public LayoutElement Element { get; }
        public bool Added { get; }
        public bool IgnoreLayout { get; }
        private float MinWidth { get; }
        private float MinHeight { get; }
        private float PreferredWidth { get; }
        private float PreferredHeight { get; }
        private float FlexibleWidth { get; }
        private float FlexibleHeight { get; }
        private int LayoutPriority { get; }

        public void Restore()
        {
            if (Element == null)
                return;

            Element.ignoreLayout = IgnoreLayout;
            Element.minWidth = MinWidth;
            Element.minHeight = MinHeight;
            Element.preferredWidth = PreferredWidth;
            Element.preferredHeight = PreferredHeight;
            Element.flexibleWidth = FlexibleWidth;
            Element.flexibleHeight = FlexibleHeight;
            Element.layoutPriority = LayoutPriority;
        }
    }
}

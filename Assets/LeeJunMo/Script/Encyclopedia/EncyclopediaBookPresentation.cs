using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EncyclopediaBookPresentation : MonoBehaviour
{
    private const int BaseLayer = 0;
    private const string BaseLayerPrefix = "Base Layer.";

    [Header("References")]
    [SerializeField] private Image bookFrameImage;
    [SerializeField] private CanvasGroup dimPanelGroup;
    [SerializeField] private Graphic dimPanelGraphic;
    [SerializeField] private RectTransform bookMotionRoot;
    [SerializeField] private CanvasGroup pageContentGroup;
    [SerializeField] private GameObject[] pageContentRoots;
    [SerializeField] private Animator bookAnimator;
    [SerializeField] private Image pageCoverImage;
    [SerializeField] private Animator pageCoverAnimator;

    [Header("Book Animator States")]
    [SerializeField] private string openedStateName = "BookIdle";
    [SerializeField] private string closedStateName = "BookIdle";
    [FormerlySerializedAs("openStateName")]
    [SerializeField] private string bookOpenStateName = "BookOpen";
    [FormerlySerializedAs("closeStateName")]
    [SerializeField] private string bookCloseStateName = "BookClose";
    [SerializeField] private string leftPageStateName = "BookLeftPage";
    [SerializeField] private string rightPageStateName = "BookRightPage";
    [SerializeField] private AnimationClip openedClip;
    [SerializeField] private AnimationClip closedClip;
    [FormerlySerializedAs("openClip")]
    [SerializeField] private AnimationClip bookOpenClip;
    [FormerlySerializedAs("closeClip")]
    [SerializeField] private AnimationClip bookCloseClip;
    [SerializeField] private AnimationClip leftPageClip;
    [SerializeField] private AnimationClip rightPageClip;

    [Header("Legacy Content Reveal")]
    [SerializeField] private string contentAppearStateName = "ContentAppear";
    [SerializeField] private AnimationClip contentAppearClip;

    [Header("Dim")]
    [SerializeField, Range(0f, 1f)] private float dimTargetAlpha = 0.65f;
    [SerializeField, Min(0f)] private float dimFadeDuration = 0.12f;
    [SerializeField] private bool deactivateDimPanelWhenTransparent = true;

    [Header("Book Drop")]
    [SerializeField] private Vector2 bookDropOffset = new(0f, 360f);
    [SerializeField, Min(0f)] private float bookDropDuration = 0.16f;
    [SerializeField] private AnimationCurve bookDropCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Timing")]
    [SerializeField] private bool hideContentDuringBookMotion = true;
    [SerializeField] private bool setContentRootsActive = true;
    [SerializeField] private bool useUnscaledTime = true;

    private Coroutine activeRoutine;
    private Vector2 bookBaseAnchoredPosition;
    private bool hasBookBaseAnchoredPosition;
    private bool pageCoverAnimatorDisabledForSampling;
    private bool pageCoverAnimatorRestoreEnabledAfterSampling;
    private bool bookAnimatorDisabledForStaticSample;
    private bool bookAnimatorRestoreEnabledAfterStaticSample;
    private bool warnedMissingBookOpenClip;
    private bool warnedMissingBookCloseClip;

    public bool IsPlaying => activeRoutine != null;
    public bool CanPlayOpen => bookAnimator != null && HasState(bookAnimator, bookOpenStateName);
    public bool CanPlayClose => bookAnimator != null && HasState(bookAnimator, bookCloseStateName);
    public bool CanPlayLeftPageTurn => bookAnimator != null && HasState(bookAnimator, leftPageStateName);
    public bool CanPlayRightPageTurn => bookAnimator != null && HasState(bookAnimator, rightPageStateName);
    public bool CanPlayContentReveal => pageCoverImage != null && contentAppearClip != null;

    private void Awake()
    {
        ResolveReferences();
        ConfigureAnimatorUpdateMode();
        CaptureBookBasePosition();
        HidePageCover();
    }

    private void OnDisable()
    {
        StopActiveRoutine();
        RestoreBookAnimatorAfterStaticSample();
        RestorePageCoverAnimatorAfterSampling();
        SetDimAlpha(0f);
        HidePageCover();
    }

    public void PlayOpen(Action prepareContent, Action onComplete = null)
    {
        StopActiveRoutine();
        ResolveReferences();
        ConfigureAnimatorUpdateMode();
        CaptureBookBasePosition();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (hideContentDuringBookMotion)
            SetContentVisible(false);

        if (!gameObject.activeInHierarchy)
        {
            SnapOpened();
            prepareContent?.Invoke();
            onComplete?.Invoke();
            return;
        }

        activeRoutine = StartCoroutine(PlayOpenRoutine(prepareContent, onComplete));
    }

    public void PlayLeftPageTurn(Action swapContent, Action onComplete = null)
    {
        PlayPageTurn(leftPageStateName, leftPageClip, CanPlayLeftPageTurn, swapContent, onComplete);
    }

    public void PlayRightPageTurn(Action swapContent, Action onComplete = null)
    {
        PlayPageTurn(rightPageStateName, rightPageClip, CanPlayRightPageTurn, swapContent, onComplete);
    }

    public void PlayContentReveal(Action onComplete = null)
    {
        PlayContentReveal(null, onComplete);
    }

    public void PlayContentReveal(Action swapContent, Action onComplete = null)
    {
        StopActiveRoutine();
        ResolveReferences();
        ConfigureAnimatorUpdateMode();
        PlayBookState(openedStateName);
        SetContentHiddenForLayout();
        swapContent?.Invoke();
        SetContentVisible(true);

        if (!CanPlayContentReveal || !gameObject.activeInHierarchy)
        {
            HidePageCover();
            onComplete?.Invoke();
            return;
        }

        activeRoutine = StartCoroutine(PlayContentRevealRoutine(onComplete));
    }

    public void PlayClose(Action onComplete = null)
    {
        StopActiveRoutine();
        ResolveReferences();
        ConfigureAnimatorUpdateMode();
        CaptureBookBasePosition();
        HidePageCover();

        if (hideContentDuringBookMotion)
            SetContentVisible(false);

        if (!gameObject.activeInHierarchy)
        {
            SnapClosed();
            onComplete?.Invoke();
            return;
        }

        activeRoutine = StartCoroutine(PlayCloseRoutine(onComplete));
    }

    public void SnapOpened()
    {
        StopActiveRoutine();
        ResolveReferences();
        ConfigureAnimatorUpdateMode();
        CaptureBookBasePosition();
        SetDimAlpha(dimTargetAlpha);
        SetBookAnchoredPosition(bookBaseAnchoredPosition);
        PlayBookState(openedStateName);
        SetContentVisible(true);
        HidePageCover();
    }

    public void SnapClosed()
    {
        StopActiveRoutine();
        ResolveReferences();
        ConfigureAnimatorUpdateMode();
        CaptureBookBasePosition();
        SetDimAlpha(0f);
        SetBookAnchoredPosition(bookBaseAnchoredPosition);
        PlayBookState(closedStateName);
        SetContentVisible(false);
        HidePageCover();
    }

    public void CancelAndHide()
    {
        StopActiveRoutine();
        HidePageCover();
        SetContentVisible(true);
    }

    private IEnumerator PlayOpenRoutine(Action prepareContent, Action onComplete)
    {
        SetDimAlpha(0f);
        DisableBookAnimatorForStaticSample();
        SampleBookClip(bookOpenClip, bookOpenStateName, 0f);
        SetBookAnchoredPosition(bookBaseAnchoredPosition + bookDropOffset);

        yield return FadeDim(0f, dimTargetAlpha, dimFadeDuration);
        yield return MoveBook(bookBaseAnchoredPosition + bookDropOffset, bookBaseAnchoredPosition, bookDropDuration);

        RestoreBookAnimatorAfterStaticSample();
        PlayBookState(bookOpenStateName);
        yield return WaitForDuration(GetRequiredBookClipLength(bookOpenClip, bookOpenStateName, "BookOpen", ref warnedMissingBookOpenClip));

        PlayBookState(openedStateName);
        SetContentHiddenForLayout();
        prepareContent?.Invoke();
        SetContentVisible(true);
        yield return PlayContentAppearRoutine();

        activeRoutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator PlayCloseRoutine(Action onComplete)
    {
        PlayBookState(bookCloseStateName);
        float closeDuration = GetRequiredBookClipLength(bookCloseClip, bookCloseStateName, "BookClose", ref warnedMissingBookCloseClip);
        yield return WaitForDuration(closeDuration);
        SampleBookClip(bookCloseClip, bookCloseStateName, closeDuration);

        yield return MoveBookAndFadeDim(
            bookBaseAnchoredPosition,
            bookBaseAnchoredPosition + bookDropOffset,
            bookDropDuration,
            GetDimAlpha(),
            0f,
            dimFadeDuration);

        activeRoutine = null;
        onComplete?.Invoke();
    }

    private void PlayPageTurn(string stateName, AnimationClip clip, bool canPlay, Action swapContent, Action onComplete)
    {
        StopActiveRoutine();
        ResolveReferences();
        ConfigureAnimatorUpdateMode();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (!gameObject.activeInHierarchy || !canPlay)
        {
            SetContentHiddenForLayout();
            swapContent?.Invoke();
            SetContentVisible(true);
            onComplete?.Invoke();
            return;
        }

        activeRoutine = StartCoroutine(PlayPageTurnRoutine(stateName, clip, swapContent, onComplete));
    }

    private IEnumerator PlayPageTurnRoutine(string stateName, AnimationClip clip, Action swapContent, Action onComplete)
    {
        yield return PlayContentDisappearRoutine();
        SetContentVisible(false);
        PlayBookState(stateName);
        yield return WaitForDuration(GetClipLength(clip, stateName));
        PlayBookState(openedStateName);
        SetContentHiddenForLayout();
        swapContent?.Invoke();
        SetContentVisible(true);
        yield return PlayContentAppearRoutine();

        activeRoutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator PlayContentRevealRoutine(Action onComplete)
    {
        yield return PlayContentAppearRoutine();
        activeRoutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator PlayContentAppearRoutine()
    {
        yield return PlayPageCoverSampleRoutine(reverse: false);
    }

    private IEnumerator PlayContentDisappearRoutine()
    {
        yield return PlayPageCoverSampleRoutine(reverse: true);
    }

    private IEnumerator PlayPageCoverSampleRoutine(bool reverse)
    {
        if (!CanPlayContentReveal || !gameObject.activeInHierarchy)
        {
            HidePageCover();
            yield break;
        }

        DisablePageCoverAnimatorForSampling();
        ShowPageCover();
        float length = contentAppearClip.length;
        if (length <= 0f)
        {
            SamplePageCover(reverse ? 0f : length);
            HidePageCover();
            RestorePageCoverAnimatorAfterSampling();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < length)
        {
            float normalized = Mathf.Clamp01(elapsed / length);
            float time = reverse
                ? Mathf.Lerp(length, 0f, normalized)
                : Mathf.Lerp(0f, length, normalized);
            SamplePageCover(time);
            elapsed += GetDeltaTime();
            yield return null;
        }

        SamplePageCover(reverse ? 0f : length);

        HidePageCover();
        RestorePageCoverAnimatorAfterSampling();
    }

    private IEnumerator FadeDim(float from, float to, float duration)
    {
        if ((dimPanelGroup == null && dimPanelGraphic == null) || duration <= 0f)
        {
            SetDimAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);
            SetDimAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetDimAlpha(to);
    }

    private IEnumerator MoveBook(Vector2 from, Vector2 to, float duration)
    {
        if (bookMotionRoot == null || duration <= 0f)
        {
            SetBookAnchoredPosition(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = bookDropCurve != null ? bookDropCurve.Evaluate(t) : t;
            SetBookAnchoredPosition(Vector2.LerpUnclamped(from, to, eased));
            yield return null;
        }

        SetBookAnchoredPosition(to);
    }

    private IEnumerator MoveBookAndFadeDim(Vector2 from, Vector2 to, float moveDuration, float dimFrom, float dimTo, float fadeDuration)
    {
        float duration = Mathf.Max(moveDuration, fadeDuration);
        if (duration <= 0f)
        {
            SetBookAnchoredPosition(to);
            SetDimAlpha(dimTo);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();

            if (moveDuration > 0f)
            {
                float moveT = Mathf.Clamp01(elapsed / moveDuration);
                float eased = bookDropCurve != null ? bookDropCurve.Evaluate(moveT) : moveT;
                SetBookAnchoredPosition(Vector2.LerpUnclamped(from, to, eased));
            }
            else
            {
                SetBookAnchoredPosition(to);
            }

            if (fadeDuration > 0f)
            {
                float fadeT = Mathf.Clamp01(elapsed / fadeDuration);
                SetDimAlpha(Mathf.Lerp(dimFrom, dimTo, fadeT));
            }
            else
            {
                SetDimAlpha(dimTo);
            }

            yield return null;
        }

        SetBookAnchoredPosition(to);
        SetDimAlpha(dimTo);
    }

    private bool PlayBookState(string stateName)
    {
        return PlayAnimatorState(bookAnimator, stateName);
    }

    private bool PlayAnimatorState(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        if (!HasState(animator, stateName))
            return false;

        animator.Play(stateName, BaseLayer, 0f);
        animator.Update(0f);
        return true;
    }

    private void SamplePageCover(float time)
    {
        if (pageCoverImage == null || contentAppearClip == null)
            return;

        contentAppearClip.SampleAnimation(pageCoverImage.gameObject, Mathf.Clamp(time, 0f, contentAppearClip.length));
    }

    private IEnumerator WaitForDuration(float duration)
    {
        if (duration <= 0f)
            yield break;

        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(duration);
        else
            yield return new WaitForSeconds(duration);
    }

    private float GetClipLength(AnimationClip configuredClip, string stateName)
    {
        if (configuredClip != null && configuredClip.length > 0f)
            return configuredClip.length;

        AnimationClip resolvedClip = ResolveClip(stateName);
        return resolvedClip != null ? resolvedClip.length : 0f;
    }

    private float GetRequiredBookClipLength(AnimationClip configuredClip, string stateName, string label, ref bool warned)
    {
        float length = GetClipLength(configuredClip, stateName);
        if (length > 0f)
            return length;

        if (!warned)
        {
            warned = true;
            Debug.LogWarning($"[EncyclopediaBookPresentation] {label} clip could not be resolved. Check the authored AnimationClip reference and Animator state motion.", this);
        }

        return 0f;
    }

    private void SampleBookClip(AnimationClip configuredClip, string stateName, float time)
    {
        AnimationClip clip = configuredClip != null ? configuredClip : ResolveClip(stateName);
        if (clip == null || bookAnimator == null)
            return;

        clip.SampleAnimation(bookAnimator.gameObject, Mathf.Clamp(time, 0f, clip.length));
    }

    private AnimationClip ResolveClip(string stateName)
    {
        if (bookAnimator == null || bookAnimator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(stateName))
            return null;

        AnimationClip[] clips = bookAnimator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && string.Equals(clip.name, stateName, StringComparison.OrdinalIgnoreCase))
                return clip;
        }

        return null;
    }

    private void SetContentHiddenForLayout()
    {
        if (pageContentGroup == null)
        {
            SetContentVisible(false);
            return;
        }

        SetContentVisible(false, keepRootsActiveForLayout: true);
    }

    private void SetContentVisible(bool visible)
    {
        SetContentVisible(visible, keepRootsActiveForLayout: false);
    }

    private void SetContentVisible(bool visible, bool keepRootsActiveForLayout)
    {
        if (pageContentGroup != null)
        {
            pageContentGroup.alpha = visible ? 1f : 0f;
            pageContentGroup.interactable = visible;
            pageContentGroup.blocksRaycasts = visible;
        }

        if (!setContentRootsActive || pageContentRoots == null)
            return;

        bool rootsActive = visible || keepRootsActiveForLayout;
        for (int i = 0; i < pageContentRoots.Length; i++)
        {
            if (pageContentRoots[i] != null)
                pageContentRoots[i].SetActive(rootsActive);
        }
    }

    private void ShowPageCover()
    {
        if (pageCoverImage == null)
            return;

        pageCoverImage.gameObject.SetActive(true);
        pageCoverImage.enabled = true;
        pageCoverImage.raycastTarget = false;
    }

    private void HidePageCover()
    {
        if (pageCoverImage == null)
            return;

        pageCoverImage.enabled = false;
        pageCoverImage.raycastTarget = false;
        pageCoverImage.gameObject.SetActive(false);
    }

    private void DisablePageCoverAnimatorForSampling()
    {
        if (pageCoverAnimator == null || pageCoverAnimatorDisabledForSampling)
            return;

        pageCoverAnimatorRestoreEnabledAfterSampling = pageCoverAnimator.enabled;
        pageCoverAnimator.enabled = false;
        pageCoverAnimatorDisabledForSampling = true;
    }

    private void RestorePageCoverAnimatorAfterSampling()
    {
        if (!pageCoverAnimatorDisabledForSampling)
            return;

        if (pageCoverAnimator != null)
            pageCoverAnimator.enabled = pageCoverAnimatorRestoreEnabledAfterSampling;

        pageCoverAnimatorDisabledForSampling = false;
        pageCoverAnimatorRestoreEnabledAfterSampling = false;
    }

    private void ResolveReferences()
    {
        if (bookFrameImage == null)
            bookFrameImage = GetComponent<Image>();

        if (bookAnimator == null)
        {
            if (bookFrameImage != null)
                bookAnimator = bookFrameImage.GetComponent<Animator>();
            if (bookAnimator == null)
                bookAnimator = ResolveBookAnimator();
        }

        if (bookMotionRoot == null && bookAnimator != null)
        {
            RectTransform animatorRect = bookAnimator.transform as RectTransform;
            RectTransform parentRect = bookAnimator.transform.parent as RectTransform;
            bookMotionRoot = parentRect != null && parentRect.name.IndexOf("Motion", StringComparison.OrdinalIgnoreCase) >= 0
                ? parentRect
                : animatorRect;
        }

        ResolveDimPanelReferences();

        if (pageCoverImage == null)
            pageCoverImage = EncyclopediaReferenceResolver.FindComponent<Image>(
                transform,
                "RevealOverlay",
                "PageCover",
                "ContentAppearCover",
                "PageCoverImage");

        if (pageCoverAnimator == null && pageCoverImage != null)
            pageCoverAnimator = pageCoverImage.GetComponent<Animator>();

        UpgradeLegacyStateNames();
    }

    private void ConfigureAnimatorUpdateMode()
    {
        AnimatorUpdateMode updateMode = useUnscaledTime ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
        if (bookAnimator != null)
            bookAnimator.updateMode = updateMode;

        if (pageCoverAnimator != null)
            pageCoverAnimator.updateMode = updateMode;
    }

    private void CaptureBookBasePosition()
    {
        if (hasBookBaseAnchoredPosition || bookMotionRoot == null)
            return;

        bookBaseAnchoredPosition = bookMotionRoot.anchoredPosition;
        hasBookBaseAnchoredPosition = true;
    }

    private void SetBookAnchoredPosition(Vector2 anchoredPosition)
    {
        if (bookMotionRoot != null)
            bookMotionRoot.anchoredPosition = anchoredPosition;
    }

    private float GetDimAlpha()
    {
        if (dimPanelGroup != null)
            return dimPanelGroup.alpha;

        return dimPanelGraphic != null ? dimPanelGraphic.color.a : 0f;
    }

    private void SetDimAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        bool shouldBeActive = alpha > 0.001f;

        if (shouldBeActive)
            SetDimPanelActive(true);

        if (dimPanelGroup != null)
        {
            dimPanelGroup.alpha = alpha;
            dimPanelGroup.blocksRaycasts = alpha > 0.01f;
            dimPanelGroup.interactable = false;
        }

        if (dimPanelGraphic != null)
        {
            Color color = dimPanelGraphic.color;
            color.a = alpha;
            dimPanelGraphic.color = color;
            dimPanelGraphic.raycastTarget = alpha > 0.01f;
        }

        if (!shouldBeActive)
            SetDimPanelActive(false);
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine == null)
            return;

        StopCoroutine(activeRoutine);
        activeRoutine = null;
        RestoreBookAnimatorAfterStaticSample();
        RestorePageCoverAnimatorAfterSampling();
    }

    private void DisableBookAnimatorForStaticSample()
    {
        if (bookAnimator == null || bookAnimatorDisabledForStaticSample)
            return;

        bookAnimatorRestoreEnabledAfterStaticSample = bookAnimator.enabled;
        bookAnimator.enabled = false;
        bookAnimatorDisabledForStaticSample = true;
    }

    private void RestoreBookAnimatorAfterStaticSample()
    {
        if (!bookAnimatorDisabledForStaticSample)
            return;

        if (bookAnimator != null)
            bookAnimator.enabled = bookAnimatorRestoreEnabledAfterStaticSample;

        bookAnimatorDisabledForStaticSample = false;
        bookAnimatorRestoreEnabledAfterStaticSample = false;
    }

    private void UpgradeLegacyStateNames()
    {
        if (bookAnimator == null)
            return;

        openedStateName = ResolveAnimatorStateName(bookAnimator, openedStateName, "BookIdle", "Opened", "OpenIdle", "Idle");
        closedStateName = ResolveAnimatorStateName(bookAnimator, closedStateName, "BookIdle", "Closed", "ClosedIdle", "Idle");
        bookOpenStateName = ResolveAnimatorStateName(bookAnimator, bookOpenStateName, "BookOpen", "Open");
        bookCloseStateName = ResolveAnimatorStateName(bookAnimator, bookCloseStateName, "BookClose", "Close");
        leftPageStateName = ResolveAnimatorStateName(bookAnimator, leftPageStateName, "BookLeftPage", "LeftPage", "LeftPageTurn");
        rightPageStateName = ResolveAnimatorStateName(bookAnimator, rightPageStateName, "BookRightPage", "RightPage", "RightPageTurn");

        if (string.Equals(bookOpenStateName, "Open", StringComparison.OrdinalIgnoreCase) && HasState(bookAnimator, "BookOpen"))
            bookOpenStateName = "BookOpen";

        if (string.Equals(bookCloseStateName, "Close", StringComparison.OrdinalIgnoreCase) && HasState(bookAnimator, "BookClose"))
            bookCloseStateName = "BookClose";

        if (string.Equals(openedStateName, "Opened", StringComparison.OrdinalIgnoreCase) && HasState(bookAnimator, "BookIdle"))
            openedStateName = "BookIdle";

        if (string.Equals(closedStateName, "Closed", StringComparison.OrdinalIgnoreCase) && HasState(bookAnimator, "BookIdle"))
            closedStateName = "BookIdle";

        if (string.IsNullOrWhiteSpace(contentAppearStateName))
            contentAppearStateName = "ContentAppear";
    }

    private void ResolveDimPanelReferences()
    {
        if (dimPanelGroup != null && dimPanelGraphic != null)
            return;

        Transform searchRoot = transform.root != null ? transform.root : transform;
        Transform dimPanel = EncyclopediaReferenceResolver.FindTransform(
            searchRoot,
            "DimPanel",
            "Panel_Dim",
            "PanelDim",
            "Dim",
            "BackgroundDim");
        if (dimPanel == null)
            return;

        if (dimPanelGroup == null)
            dimPanelGroup = dimPanel.GetComponent<CanvasGroup>();

        if (dimPanelGraphic == null)
            dimPanelGraphic = dimPanel.GetComponent<Graphic>();
    }

    private void SetDimPanelActive(bool active)
    {
        if (!deactivateDimPanelWhenTransparent && !active)
            return;

        if (dimPanelGroup != null && dimPanelGroup.gameObject.activeSelf != active)
            dimPanelGroup.gameObject.SetActive(active);

        if (dimPanelGraphic != null && dimPanelGraphic.gameObject.activeSelf != active)
            dimPanelGraphic.gameObject.SetActive(active);
    }

    private Animator ResolveBookAnimator()
    {
        Animator directAnimator = GetComponent<Animator>();
        if (LooksLikeBookAnimator(directAnimator))
            return directAnimator;

        Transform namedOwner = EncyclopediaReferenceResolver.FindTransform(
            transform,
            "EarthTome",
            "Tome",
            "BookAnimator",
            "TomeAnimator",
            "BookFrame",
            "Book");
        if (namedOwner != null)
        {
            Animator namedAnimator = namedOwner.GetComponent<Animator>() ?? namedOwner.GetComponentInChildren<Animator>(true);
            if (LooksLikeBookAnimator(namedAnimator))
                return namedAnimator;
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
                continue;

            if (LooksLikeBookAnimator(animator))
                return animator;
        }

        return null;
    }

    private static bool LooksLikeBookAnimator(Animator animator)
    {
        if (animator == null)
            return false;

        return HasState(animator, "BookOpen") ||
            HasState(animator, "Open") ||
            HasState(animator, "BookClose") ||
            HasState(animator, "Close") ||
            HasState(animator, "BookLeftPage") ||
            HasState(animator, "BookRightPage") ||
            HasClip(animator, "BookOpen", "Open", "BookClose", "Close", "BookLeftPage", "BookRightPage");
    }

    private static string ResolveAnimatorStateName(Animator animator, string currentStateName, params string[] candidates)
    {
        if (HasState(animator, currentStateName))
            return currentStateName;

        if (candidates == null)
            return currentStateName;

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (HasState(animator, candidate))
                return candidate;
        }

        return currentStateName;
    }

    private static bool HasClip(Animator animator, params string[] clipNames)
    {
        if (animator == null || animator.runtimeAnimatorController == null || clipNames == null)
            return false;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
                continue;

            for (int j = 0; j < clipNames.Length; j++)
            {
                if (string.Equals(clip.name, clipNames[j], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static bool HasState(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        int shortHash = Animator.StringToHash(stateName);
        if (animator.HasState(BaseLayer, shortHash))
            return true;

        int qualifiedHash = Animator.StringToHash(BaseLayerPrefix + stateName);
        return animator.HasState(BaseLayer, qualifiedHash);
    }
}

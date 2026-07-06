using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임: 시네마틱 컷씬 중 전역 UI 레이어를 흐리게 하고 화면 상하단 레터박스 오버레이를 제어한다.
/// </summary>
public sealed class CinematicLetterboxOverlay : ICinematicLetterboxOverlayHandle
{
    private static readonly GlobalCanvasLayer[] FadedLayers =
    {
        GlobalCanvasLayer.GameplayHUD,
        GlobalCanvasLayer.Dialogue,
        GlobalCanvasLayer.Popup,
        GlobalCanvasLayer.Hover,
        GlobalCanvasLayer.Prompt,
        GlobalCanvasLayer.Reward,
        GlobalCanvasLayer.DamagePopup,
        GlobalCanvasLayer.BossHUD,
    };

    private readonly List<CanvasGroupState> canvasStates = new();

    private GameObject overlayRoot;
    private RectTransform rootRect;
    private RectTransform topBarRect;
    private RectTransform bottomBarRect;
    private bool isDisposed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBackend()
    {
        CinematicLetterboxPlayback.RegisterBackend(CinematicLetterboxBackend.Instance);
    }

    /// <summary>
    /// 책임 : Core 레터박스 생성 요청을 UI의 실제 레터박스 오버레이 구현으로 연결한다.
    /// </summary>
    private sealed class CinematicLetterboxBackend : ICinematicLetterboxBackend
    {
        public static readonly CinematicLetterboxBackend Instance = new();

        public ICinematicLetterboxOverlayHandle CreateOverlay()
        {
            return new CinematicLetterboxOverlay();
        }
    }

    /// <summary>
    /// 책임: 레터박스 연출 동안 임시 변경된 CanvasGroup 상태를 원상복구하기 위한 스냅샷을 보관한다.
    /// </summary>
    private sealed class CanvasGroupState
    {
        public CanvasGroup Group;
        public float OriginalAlpha;
        public bool OriginalInteractable;
        public bool OriginalBlocksRaycasts;
        public bool AddedCanvasGroup;
    }

    public IEnumerator PlayIn(float duration, float letterboxHeightRatio, float uiTargetAlpha)
    {
        yield return PlayIn(duration, letterboxHeightRatio, uiTargetAlpha, captureGlobalUiLayers: true);
    }

    public IEnumerator PlayIn(
        float duration,
        float letterboxHeightRatio,
        float uiTargetAlpha,
        bool captureGlobalUiLayers)
    {
        if (isDisposed)
            yield break;

        EnsureOverlayExists();
        if (captureGlobalUiLayers)
            CaptureCanvasStates();
        else
            RestoreCanvasStatesImmediate();

        float targetBarHeight = ResolveTargetBarHeight(letterboxHeightRatio);
        yield return Animate(
            duration,
            topBarTargetHeight: targetBarHeight,
            bottomBarTargetHeight: targetBarHeight,
            resolveTargetAlpha: _ => Mathf.Clamp01(uiTargetAlpha),
            restoreCanvasInteraction: false);
    }

    public IEnumerator PlayIn(
        float duration,
        float letterboxHeightRatio,
        float uiTargetAlpha,
        IReadOnlyList<GlobalCanvasLayer> fadedLayers)
    {
        if (isDisposed)
            yield break;

        EnsureOverlayExists();
        CaptureCanvasStates(fadedLayers);

        float targetBarHeight = ResolveTargetBarHeight(letterboxHeightRatio);
        yield return Animate(
            duration,
            topBarTargetHeight: targetBarHeight,
            bottomBarTargetHeight: targetBarHeight,
            resolveTargetAlpha: _ => Mathf.Clamp01(uiTargetAlpha),
            restoreCanvasInteraction: false);
    }

    public IEnumerator PlayOut(float duration)
    {
        if (isDisposed || overlayRoot == null)
            yield break;

        yield return Animate(
            duration,
            topBarTargetHeight: 0f,
            bottomBarTargetHeight: 0f,
            resolveTargetAlpha: state => state.OriginalAlpha,
            restoreCanvasInteraction: true);
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        RestoreCanvasStatesImmediate();

        if (overlayRoot != null)
            Object.Destroy(overlayRoot);

        overlayRoot = null;
        rootRect = null;
        topBarRect = null;
        bottomBarRect = null;
    }

    private IEnumerator Animate(
        float duration,
        float topBarTargetHeight,
        float bottomBarTargetHeight,
        System.Func<CanvasGroupState, float> resolveTargetAlpha,
        bool restoreCanvasInteraction)
    {
        if (isDisposed || topBarRect == null || bottomBarRect == null)
            yield break;

        float topBarStartHeight = topBarRect.sizeDelta.y;
        float bottomBarStartHeight = bottomBarRect.sizeDelta.y;

        float[] canvasStartAlphas = new float[canvasStates.Count];
        for (int i = 0; i < canvasStates.Count; i++)
            canvasStartAlphas[i] = canvasStates[i].Group != null ? canvasStates[i].Group.alpha : 1f;

        if (duration <= 0f)
        {
            ApplyBarHeight(topBarRect, topBarTargetHeight);
            ApplyBarHeight(bottomBarRect, bottomBarTargetHeight);
            ApplyCanvasTargets(resolveTargetAlpha, restoreCanvasInteraction);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (isDisposed)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            ApplyBarHeight(topBarRect, Mathf.Lerp(topBarStartHeight, topBarTargetHeight, t));
            ApplyBarHeight(bottomBarRect, Mathf.Lerp(bottomBarStartHeight, bottomBarTargetHeight, t));

            for (int i = 0; i < canvasStates.Count; i++)
            {
                CanvasGroupState state = canvasStates[i];
                if (state.Group == null)
                    continue;

                float targetAlpha = resolveTargetAlpha != null ? resolveTargetAlpha(state) : state.Group.alpha;
                state.Group.alpha = Mathf.Lerp(canvasStartAlphas[i], targetAlpha, t);
            }

            yield return null;
        }

        ApplyBarHeight(topBarRect, topBarTargetHeight);
        ApplyBarHeight(bottomBarRect, bottomBarTargetHeight);
        ApplyCanvasTargets(resolveTargetAlpha, restoreCanvasInteraction);
    }

    private void ApplyCanvasTargets(System.Func<CanvasGroupState, float> resolveTargetAlpha, bool restoreCanvasInteraction)
    {
        for (int i = 0; i < canvasStates.Count; i++)
        {
            CanvasGroupState state = canvasStates[i];
            if (state.Group == null)
                continue;

            if (resolveTargetAlpha != null)
                state.Group.alpha = resolveTargetAlpha(state);

            if (restoreCanvasInteraction)
            {
                state.Group.interactable = state.OriginalInteractable;
                state.Group.blocksRaycasts = state.OriginalBlocksRaycasts;
            }
            else
            {
                state.Group.interactable = false;
                state.Group.blocksRaycasts = false;
            }
        }
    }

    private void EnsureOverlayExists()
    {
        if (overlayRoot != null)
            return;

        RuntimePresentationFallbackAudit.Record(
            null,
            "Cinematic letterbox overlay fallback",
            "an authored cinematic letterbox overlay on GlobalUIRoot");

        overlayRoot = new GameObject("CinematicLetterboxOverlay", typeof(RectTransform), typeof(Canvas));
        rootRect = overlayRoot.GetComponent<RectTransform>();

        Canvas canvas = overlayRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        overlayRoot.hideFlags = HideFlags.DontSave;

        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
        }

        topBarRect = CreateBar("CinematicTopBar", isTop: true);
        bottomBarRect = CreateBar("CinematicBottomBar", isTop: false);
    }

    private RectTransform CreateBar(string objectName, bool isTop)
    {
        GameObject barObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.SetParent(rootRect, false);

        if (isTop)
        {
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
        }
        else
        {
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
        }

        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = Vector2.zero;

        Image image = barObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        return barRect;
    }

    private void CaptureCanvasStates()
    {
        CaptureCanvasStates(FadedLayers);
    }

    private void CaptureCanvasStates(IReadOnlyList<GlobalCanvasLayer> fadedLayers)
    {
        RestoreCanvasStatesImmediate();

        if (fadedLayers == null)
            return;

        for (int i = 0; i < fadedLayers.Count; i++)
        {
            Canvas canvas = GlobalUIRoot.GetCanvas(fadedLayers[i]);
            if (canvas == null)
                continue;

            CanvasGroup group = canvas.GetComponent<CanvasGroup>();
            bool addedCanvasGroup = false;
            if (group == null)
            {
                group = canvas.gameObject.AddComponent<CanvasGroup>();
                addedCanvasGroup = true;
            }

            canvasStates.Add(new CanvasGroupState
            {
                Group = group,
                OriginalAlpha = group.alpha,
                OriginalInteractable = group.interactable,
                OriginalBlocksRaycasts = group.blocksRaycasts,
                AddedCanvasGroup = addedCanvasGroup,
            });
        }
    }

    private void RestoreCanvasStatesImmediate()
    {
        for (int i = 0; i < canvasStates.Count; i++)
        {
            CanvasGroupState state = canvasStates[i];
            if (state.Group == null)
                continue;

            state.Group.alpha = state.OriginalAlpha;
            state.Group.interactable = state.OriginalInteractable;
            state.Group.blocksRaycasts = state.OriginalBlocksRaycasts;

            if (state.AddedCanvasGroup)
                Object.Destroy(state.Group);
        }

        canvasStates.Clear();
    }

    private static float ResolveTargetBarHeight(float letterboxHeightRatio)
    {
        float clampedRatio = Mathf.Clamp(letterboxHeightRatio, 0f, 0.5f);
        return Mathf.Max(0f, Screen.height * clampedRatio);
    }

    private static void ApplyBarHeight(RectTransform barRect, float height)
    {
        if (barRect == null)
            return;

        Vector2 sizeDelta = barRect.sizeDelta;
        sizeDelta.y = Mathf.Max(0f, height);
        barRect.sizeDelta = sizeDelta;
    }
}


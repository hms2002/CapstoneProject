using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

/// <summary>
/// 책임 : 호감도 수치 UI와 증가 연출을 표시하고 AffectionManager에 presentation view로 등록한다.
/// </summary>
public class AffectionUI : MonoBehaviour, IAffectionPresentationView
{
    [Header("UI Components")]
    [SerializeField] private RectTransform[] affectionSegmentRoots = new RectTransform[5];
    [SerializeField] private Image[] affectionFillImages = new Image[5];
    [SerializeField] private CanvasGroup uiCanvasGroup;
    [SerializeField] private AffectionGainScreenEffect gainScreenEffect;

    [Header("Presentation Settings")]
    [SerializeField] private float fillDuration = 0.5f;
    [SerializeField] private float resetDuration = 0.2f;

    private Sequence gainSequence;
    private Sequence openingRevealSequence;
    private Vector3[] affectionSegmentBaseScales;
    private Action pendingGainComplete;
    private int pendingGainFinalAffection;
    private bool hasPendingGainAnimation;

    public bool IsPresentationActive => gameObject.activeInHierarchy;

    private void Awake()
    {
        ResolveGainScreenEffect();
        CacheSegmentBaseScales();

        SetDisplayedLevel(0);
        if (uiCanvasGroup != null) uiCanvasGroup.alpha = 1f;
    }

    private void OnEnable()
    {
        if (AffectionManager.Instance != null)
        {
            AffectionManager.Instance.SetLinkedUI(this);
        }
    }

    private void OnDisable()
    {
        CompletePendingGainAnimation();
        CompleteOpeningReveal();
        KillTargetTweens();
    }

    private void OnDestroy()
    {
        CompletePendingGainAnimation();
        CompleteOpeningReveal();
    }

    public void Setup(int currentAffection)
    {
        SetDisplayedLevel(currentAffection);
    }

    public void PrepareOpeningReveal()
    {
        CacheSegmentBaseScales();
        KillOpeningRevealSequence();

        for (int i = 0; i < affectionSegmentRoots.Length; i++)
        {
            RectTransform segmentRoot = affectionSegmentRoots[i];
            if (segmentRoot != null)
                segmentRoot.localScale = Vector3.zero;
        }
    }

    public float PlayOpeningReveal(float beatInterval, float scaleDuration)
    {
        CacheSegmentBaseScales();
        KillOpeningRevealSequence();

        float safeBeatInterval = Mathf.Max(0f, beatInterval);
        float safeScaleDuration = Mathf.Max(0f, scaleDuration);
        int lastAnimatedIndex = -1;

        openingRevealSequence = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < affectionSegmentRoots.Length; i++)
        {
            RectTransform segmentRoot = affectionSegmentRoots[i];
            if (segmentRoot == null)
                continue;

            segmentRoot.DOKill(false);
            segmentRoot.localScale = Vector3.zero;
            openingRevealSequence.Insert(
                i * safeBeatInterval,
                segmentRoot.DOScale(affectionSegmentBaseScales[i], safeScaleDuration)
                    .SetEase(Ease.OutBack));
            lastAnimatedIndex = i;
        }

        if (lastAnimatedIndex < 0)
        {
            openingRevealSequence.Kill(false);
            openingRevealSequence = null;
            return 0f;
        }

        openingRevealSequence.OnComplete(() => openingRevealSequence = null);
        return lastAnimatedIndex * safeBeatInterval + safeScaleDuration;
    }

    public void CompleteOpeningReveal()
    {
        CacheSegmentBaseScales();
        KillOpeningRevealSequence();

        for (int i = 0; i < affectionSegmentRoots.Length; i++)
        {
            RectTransform segmentRoot = affectionSegmentRoots[i];
            if (segmentRoot != null)
                segmentRoot.localScale = affectionSegmentBaseScales[i];
        }
    }

    public void PlayGainAnimation(int prevAffection, int newAffection, Action onComplete)
    {
        CompletePendingGainAnimation();
        KillTargetTweens();

        pendingGainComplete = onComplete;
        pendingGainFinalAffection = newAffection;
        hasPendingGainAnimation = true;

        SetDisplayedLevel(prevAffection);

        if (newAffection > prevAffection)
        {
            ResolveGainScreenEffect();
            gainScreenEffect?.Play();
        }

        gainSequence = DOTween.Sequence();
        gainSequence.SetUpdate(true);

        AppendLevelTransition(gainSequence, prevAffection, newAffection);

        gainSequence.OnComplete(CompleteGainAnimation);
        gainSequence.OnKill(() =>
        {
            if (hasPendingGainAnimation)
                CompleteGainAnimation();
        });
    }

    private void KillTargetTweens()
    {
        if (affectionFillImages == null)
            return;

        for (int i = 0; i < affectionFillImages.Length; i++)
        {
            if (affectionFillImages[i] != null)
                affectionFillImages[i].DOKill(false);
        }

        if (affectionSegmentRoots == null)
            return;

        for (int i = 0; i < affectionSegmentRoots.Length; i++)
        {
            if (affectionSegmentRoots[i] != null)
                affectionSegmentRoots[i].DOKill(false);
        }
    }

    private void CacheSegmentBaseScales()
    {
        if (affectionSegmentRoots == null)
            affectionSegmentRoots = Array.Empty<RectTransform>();

        if (affectionSegmentBaseScales != null &&
            affectionSegmentBaseScales.Length == affectionSegmentRoots.Length)
        {
            return;
        }

        affectionSegmentBaseScales = new Vector3[affectionSegmentRoots.Length];
        for (int i = 0; i < affectionSegmentRoots.Length; i++)
        {
            RectTransform segmentRoot = affectionSegmentRoots[i];
            affectionSegmentBaseScales[i] = segmentRoot != null ? segmentRoot.localScale : Vector3.one;
        }
    }

    private void KillOpeningRevealSequence()
    {
        if (openingRevealSequence != null)
        {
            openingRevealSequence.Kill(false);
            openingRevealSequence = null;
        }
    }

    private void CompletePendingGainAnimation()
    {
        if (!hasPendingGainAnimation)
            return;

        if (gainSequence != null && gainSequence.IsActive())
        {
            gainSequence.Kill(false);
            return;
        }

        CompleteGainAnimation();
    }

    private void CompleteGainAnimation()
    {
        if (!hasPendingGainAnimation)
            return;

        hasPendingGainAnimation = false;
        gainSequence = null;

        SetDisplayedLevel(pendingGainFinalAffection);

        Action complete = pendingGainComplete;
        pendingGainComplete = null;
        complete?.Invoke();
    }

    private void AppendLevelTransition(Sequence sequence, int previousLevel, int newLevel)
    {
        int segmentCount = affectionFillImages != null ? affectionFillImages.Length : 0;
        int clampedPreviousLevel = Mathf.Clamp(previousLevel, 0, segmentCount);
        int clampedNewLevel = Mathf.Clamp(newLevel, 0, segmentCount);

        if (clampedNewLevel > clampedPreviousLevel)
        {
            for (int i = clampedPreviousLevel; i < clampedNewLevel; i++)
            {
                Image fillImage = affectionFillImages[i];
                if (fillImage != null)
                    sequence.Append(fillImage.DOFillAmount(1f, fillDuration).SetEase(Ease.OutQuad));
                else
                    sequence.AppendInterval(fillDuration);
            }

            return;
        }

        if (clampedNewLevel < clampedPreviousLevel)
        {
            for (int i = clampedPreviousLevel - 1; i >= clampedNewLevel; i--)
            {
                Image fillImage = affectionFillImages[i];
                if (fillImage != null)
                    sequence.Append(fillImage.DOFillAmount(0f, resetDuration).SetEase(Ease.InQuad));
                else
                    sequence.AppendInterval(resetDuration);
            }

            return;
        }

        sequence.AppendInterval(0f);
    }

    private void SetDisplayedLevel(int level)
    {
        if (affectionFillImages == null)
            return;

        int filledCount = Mathf.Clamp(level, 0, affectionFillImages.Length);
        for (int i = 0; i < affectionFillImages.Length; i++)
        {
            if (affectionFillImages[i] != null)
                affectionFillImages[i].fillAmount = i < filledCount ? 1f : 0f;
        }
    }

    private void ResolveGainScreenEffect()
    {
        if (gainScreenEffect != null)
            return;

        gainScreenEffect = GetComponent<AffectionGainScreenEffect>();
        if (gainScreenEffect != null)
            return;

        gainScreenEffect = GetComponentInChildren<AffectionGainScreenEffect>(true);
        if (gainScreenEffect != null)
            return;

        AffectionGainScreenEffect[] sceneEffects =
            FindObjectsByType<AffectionGainScreenEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (sceneEffects != null && sceneEffects.Length > 0)
            gainScreenEffect = sceneEffects[0];
    }
}

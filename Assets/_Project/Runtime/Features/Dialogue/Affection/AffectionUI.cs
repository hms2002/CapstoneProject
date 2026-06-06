using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

public class AffectionUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Slider affectionSlider;
    [SerializeField] private TextMeshProUGUI affectionText;
    [SerializeField] private CanvasGroup uiCanvasGroup;
    [SerializeField] private AffectionGainScreenEffect gainScreenEffect;

    [Header("Presentation Settings")]
    [SerializeField] private float fillDuration = 0.5f;
    [SerializeField] private float resetDuration = 0.2f;

    private Sequence gainSequence;
    private Action pendingGainComplete;
    private int pendingGainFinalAffection;
    private bool hasPendingGainAnimation;

    private void Awake()
    {
        ResolveGainScreenEffect();

        if (affectionSlider != null) affectionSlider.value = 0f;
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
        KillTargetTweens();
    }

    private void OnDestroy()
    {
        CompletePendingGainAnimation();
    }

    public void Setup(int currentAffection)
    {
        if (affectionText != null)
            affectionText.text = currentAffection.ToString();

        if (affectionSlider != null)
            affectionSlider.value = 0f;
    }

    public void PlayGainAnimation(int prevAffection, int newAffection, Action onComplete)
    {
        CompletePendingGainAnimation();
        KillTargetTweens();

        pendingGainComplete = onComplete;
        pendingGainFinalAffection = newAffection;
        hasPendingGainAnimation = true;

        if (affectionText != null) affectionText.text = prevAffection.ToString();

        if (newAffection > prevAffection)
        {
            ResolveGainScreenEffect();
            gainScreenEffect?.Play();
        }

        gainSequence = DOTween.Sequence();
        gainSequence.SetUpdate(true);

        if (affectionSlider != null)
            gainSequence.Append(affectionSlider.DOValue(1f, fillDuration).SetUpdate(true).SetEase(Ease.OutQuad));
        else
            gainSequence.AppendInterval(fillDuration);

        gainSequence.AppendCallback(() => {
            if (affectionText != null)
            {
                affectionText.text = newAffection.ToString();
                affectionText.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 5, 1f).SetUpdate(true);
            }
        });

        gainSequence.AppendInterval(0.4f);

        if (affectionSlider != null)
            gainSequence.Append(affectionSlider.DOValue(0f, resetDuration).SetUpdate(true).SetEase(Ease.InQuad));
        else
            gainSequence.AppendInterval(resetDuration);

        gainSequence.OnComplete(CompleteGainAnimation);
        gainSequence.OnKill(() =>
        {
            if (hasPendingGainAnimation)
                CompleteGainAnimation();
        });
    }

    private void KillTargetTweens()
    {
        if (affectionSlider != null) affectionSlider.DOKill(false);
        if (affectionText != null) affectionText.transform.DOKill(false);
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

        if (affectionText != null)
            affectionText.text = pendingGainFinalAffection.ToString();

        if (affectionSlider != null)
            affectionSlider.value = 0f;

        Action complete = pendingGainComplete;
        pendingGainComplete = null;
        complete?.Invoke();
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

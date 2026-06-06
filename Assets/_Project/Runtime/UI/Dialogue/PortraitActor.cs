using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PortraitActor : MonoBehaviour
{
    public Image image;
    public CanvasGroup canvasGroup;

    // [핵심 개선] 배우 본인이 자신의 상태를 직접 기억합니다!
    public int npcId;
    public string currentLabel = "Normal";
    public string currentPosition = "center";

    private void Awake()
    {
        if (image == null) image = GetComponent<Image>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    public void SetSprite(Sprite newFace)
    {
        if (newFace != null && image != null)
        {
            image.sprite = newFace;
        }
    }

    public void SetFocus(bool isFocused, float duration = 0.2f)
    {
        if (image == null) return;

        Color targetColor = isFocused ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
        image.DOKill();
        image.DOColor(targetColor, duration).SetUpdate(true);
    }

    public void FadeIn(float duration, System.Action onComplete = null)
    {
        gameObject.SetActive(true);
        canvasGroup.DOKill();
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, duration).SetUpdate(true).OnComplete(() => onComplete?.Invoke());
    }

    public void FadeOut(float duration)
    {
        canvasGroup.DOKill();
        canvasGroup.DOFade(0f, duration).SetUpdate(true).OnComplete(() => {
            gameObject.SetActive(false);
        });
    }

    public void HideImmediate()
    {
        canvasGroup.DOKill();
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}

using UnityEngine;
using DG.Tweening;

/// <summary>
/// 책임 : 대화 portrait 위 감정표현 말풍선과 icon 팝업 연출을 재생하고 재사용 가능한 UI 오브젝트를 비활성화한다.
/// </summary>
public class EmoteController : MonoBehaviour
{
    [Header("UI 컴포넌트 연결")]
    [SerializeField] private RectTransform balloonRect;
    [SerializeField] private RectTransform iconRect;
    [SerializeField] private Animator iconAnimator;

    [Header("연출 설정")]
    [SerializeField] private float balloonPopTime = 0.3f;
    [SerializeField] private float iconPopTime = 0.3f;
    [SerializeField] private float stayTime = 2.0f;

    private Sequence emoteSequence;

    public void Init(string emoteName)
    {
        if (emoteSequence != null) emoteSequence.Kill();

        gameObject.SetActive(true);
        balloonRect.localScale = Vector3.zero;
        iconRect.localScale = Vector3.zero;
        iconRect.gameObject.SetActive(false);

        emoteSequence = DOTween.Sequence();
        emoteSequence.SetUpdate(true);

        emoteSequence.Append(balloonRect.DOScale(1f, balloonPopTime).SetUpdate(true).SetEase(Ease.OutBack));

        emoteSequence.AppendCallback(() =>
        {
            iconRect.gameObject.SetActive(true);

            if (iconAnimator != null)
            {
                iconAnimator.Play(emoteName);
                iconAnimator.Update(0f);
            }
        });

        emoteSequence.Append(iconRect.DOScale(1f, iconPopTime).SetUpdate(true).SetEase(Ease.OutBack));
        emoteSequence.AppendInterval(stayTime);
        emoteSequence.Append(iconRect.DOScale(0f, 0.2f).SetUpdate(true).SetEase(Ease.InBack));
        emoteSequence.AppendCallback(() => iconRect.gameObject.SetActive(false));
        emoteSequence.Append(balloonRect.DOScale(0f, 0.2f).SetUpdate(true).SetEase(Ease.InBack));

        emoteSequence.OnComplete(() => gameObject.SetActive(false));
    }

    private void OnDestroy()
    {
        if (emoteSequence != null) emoteSequence.Kill();
    }
}

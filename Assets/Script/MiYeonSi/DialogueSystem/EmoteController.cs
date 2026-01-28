using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

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
        // 1. 초기화 (크기 0, 비활성화)
        balloonRect.localScale = Vector3.zero;
        iconRect.localScale = Vector3.zero;
        iconRect.gameObject.SetActive(false); // 흰 박스 방지

        // 2. 시퀀스 시작
        emoteSequence = DOTween.Sequence();

        // [단계 1] 말풍선 등장 (0 -> 1)
        emoteSequence.Append(balloonRect.DOScale(1f, balloonPopTime).SetEase(Ease.OutBack));

        // [단계 2] (핵심 수정) 아이콘 활성화 -> 애니메이션 즉시 실행 -> 그 다음 DOScale
        emoteSequence.AppendCallback(() =>
        {
            iconRect.gameObject.SetActive(true);

            if (iconAnimator != null)
            {
                // 애니메이션을 먼저 실행해서, 스프라이트가 흰색(None)에서 하트/땀 등으로 바뀌게 함
                iconAnimator.Play(emoteName);

                // (선택) 만약 0프레임 딜레이도 허용 안 되면 강제 업데이트
                iconAnimator.Update(0f);
            }
        });

        // [단계 3] 이미지가 세팅된 상태에서 쫀득하게 커짐
        emoteSequence.Append(iconRect.DOScale(1f, iconPopTime).SetEase(Ease.OutBack));

        // (기존에 뒤에서 실행하던 Play 코드는 위로 올렸으니 삭제)

        // [단계 4] 유지 시간
        emoteSequence.AppendInterval(stayTime);

        // [단계 5] 퇴장 (아이콘 작아짐 -> 끄기 -> 말풍선 작아짐)
        emoteSequence.Append(iconRect.DOScale(0f, 0.2f).SetEase(Ease.InBack));
        emoteSequence.AppendCallback(() => iconRect.gameObject.SetActive(false));
        emoteSequence.Append(balloonRect.DOScale(0f, 0.2f).SetEase(Ease.InBack));

        // [단계 6] 파괴
        emoteSequence.OnComplete(() => Destroy(gameObject));
    }

    private void OnDestroy()
    {
        if (emoteSequence != null) emoteSequence.Kill();
    }
}
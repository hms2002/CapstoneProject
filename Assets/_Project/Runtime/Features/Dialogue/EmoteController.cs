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

        Debug.Log("감정표현 실행됨");
        // 1. 재사용 시 기존에 재생 중이던 시퀀스가 있다면 즉시 강제 종료
        if (emoteSequence != null) emoteSequence.Kill();

        // 2. 초기화 (재사용을 위해 부모 활성화 및 크기 0)
        gameObject.SetActive(true);
        balloonRect.localScale = Vector3.zero;
        iconRect.localScale = Vector3.zero;
        iconRect.gameObject.SetActive(false); // 흰 박스 방지

        // 3. 시퀀스 시작
        emoteSequence = DOTween.Sequence();
        emoteSequence.SetUpdate(true);

        // [단계 1] 말풍선 등장 (0 -> 1)
        emoteSequence.Append(balloonRect.DOScale(1f, balloonPopTime).SetUpdate(true).SetEase(Ease.OutBack));

        // [단계 2] 아이콘 활성화 -> 애니메이션 즉시 실행
        emoteSequence.AppendCallback(() =>
        {
            iconRect.gameObject.SetActive(true);

            if (iconAnimator != null)
            {
                // 애니메이션을 먼저 실행해서, 스프라이트가 바뀌게 함
                iconAnimator.Play(emoteName);
                // 0프레임 딜레이(흰색 번쩍임) 방지를 위한 강제 업데이트
                iconAnimator.Update(0f);
            }
        });

        // [단계 3] 이미지가 세팅된 상태에서 쫀득하게 커짐
        emoteSequence.Append(iconRect.DOScale(1f, iconPopTime).SetUpdate(true).SetEase(Ease.OutBack));

        // [단계 4] 유지 시간
        emoteSequence.AppendInterval(stayTime);

        // [단계 5] 퇴장 (아이콘 작아짐 -> 끄기 -> 말풍선 작아짐)
        emoteSequence.Append(iconRect.DOScale(0f, 0.2f).SetUpdate(true).SetEase(Ease.InBack));
        emoteSequence.AppendCallback(() => iconRect.gameObject.SetActive(false));
        emoteSequence.Append(balloonRect.DOScale(0f, 0.2f).SetUpdate(true).SetEase(Ease.InBack));

        // [단계 6] 파괴(Destroy) 대신 꺼두기 (오브젝트 풀링 반환 효과)
        emoteSequence.OnComplete(() => gameObject.SetActive(false));
    }

    private void OnDestroy()
    {
        if (emoteSequence != null) emoteSequence.Kill();
    }
}

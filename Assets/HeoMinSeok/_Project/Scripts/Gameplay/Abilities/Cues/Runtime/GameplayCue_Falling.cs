using UnityEngine;
using UnityGAS;
using DG.Tweening; // DOTween 네임스페이스

public class GameplayCue_Falling : GameplayCueNotify
{
    [Header("Falling Animation")]
    [SerializeField] private float animDuration = 1.0f; // 애니메이션 시간
    [SerializeField] private Ease fallEase = Ease.InBack; // 빨려 들어가는 느낌의 Ease
    [SerializeField] private float rotateSpeed = 360f; // 회전 속도

    private Vector3 originalScale;
    private Tween scaleTween;
    private Tween rotateTween;
    private Tween moveTween;

    // [수정] 파라미터는 GameplayCueParams 하나만 받습니다.
    public override void OnAdd(GameplayCueParams parameters)
    {
        base.OnAdd(parameters);

        // Target은 parameters 안에 들어있습니다.
        GameObject target = parameters.Target;
        if (target == null) return;

        Transform targetTransform = target.transform;
        originalScale = targetTransform.localScale;

        // 1. 스케일 애니메이션 (작아짐)
        scaleTween = targetTransform.DOScale(Vector3.zero, animDuration)
            .SetEase(fallEase);

        // 2. 회전 애니메이션 (구멍에 빠질 때 뱅글뱅글)
        rotateTween = targetTransform.DORotate(new Vector3(0, 0, rotateSpeed), animDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental); // 계속 회전

        // 3. 위치 보정 (구멍의 중심으로 끌려감)
        // Causer(함정)가 있다면 그 위치로 이동
        if (parameters.Causer != null)
        {
            moveTween = targetTransform.DOMove(parameters.Causer.transform.position, animDuration)
                .SetEase(Ease.OutQuart);
        }
    }

    // [수정] 파라미터는 GameplayCueParams 하나만 받습니다.
    public override void OnRemove(GameplayCueParams parameters)
    {
        base.OnRemove(parameters);

        GameObject target = parameters.Target;

        // 애니메이션 중단
        if (scaleTween != null) scaleTween.Kill();
        if (rotateTween != null) rotateTween.Kill();
        if (moveTween != null) moveTween.Kill();

        // 상태 원상복구 (리스폰 되었을 때 정상 크기로)
        if (target != null)
        {
            target.transform.localScale = originalScale; // 원래 크기로 복구 (저장된 값이 없다면 Vector3.one 등 확인 필요)

            // 만약 originalScale이 0으로 저장되는 문제가 있다면 안전하게 1로 초기화하거나 로직 점검
            if (originalScale == Vector3.zero) target.transform.localScale = Vector3.one;

            target.transform.rotation = Quaternion.identity;
        }
    }
}
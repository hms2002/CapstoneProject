using UnityEngine;
using DG.Tweening;

public class LeverShortcut : PermanentShortcut
{
    public Transform handle; // 레버 손잡이

    protected override bool CheckCondition(TempPlayer player) => true; // 무조건 성공

    // 재접속 시 혹은 성공 시 호출됨
    protected override void SetActivatedVisual()
    {
        if (handle != null) handle.localRotation = Quaternion.Euler(45, 0, 0);
    }

    // 플레이어가 작동시켰을 때 연출
    protected override void OnSuccess()
    {
        base.OnSuccess(); // 문 열고 저장
        // 부드러운 애니메이션
        if (handle != null)
            handle.DORotate(new Vector3(45, 0, 0), 0.5f).SetRelative().SetEase(Ease.OutBack);
    }

    public override string GetInteractDescription() => "작동하기";
}
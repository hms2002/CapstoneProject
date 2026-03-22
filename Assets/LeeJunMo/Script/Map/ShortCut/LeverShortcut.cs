using UnityEngine;
using DG.Tweening;

public class LeverShortcut : PermanentShortcut
{
    public Transform handle;

    protected override bool CheckCondition(IPlayerInteractor player) => true;

    protected override void SetActivatedVisual()
    {
        if (handle != null)
            handle.localRotation = Quaternion.Euler(45, 0, 0);
    }

    protected override void OnSuccess()
    {
        base.OnSuccess();

        if (handle != null)
            handle.DORotate(new Vector3(45, 0, 0), 0.5f).SetRelative().SetEase(Ease.OutBack);
    }

    public override string GetInteractDescription() => "작동하기";
}

using UnityEngine;

/// <summary>
/// 책임 : 영구 해금 가능한 숏컷 상호작용의 공통 성공 처리와 저장된 활성화 표시 복원을 담당한다.
/// </summary>
public abstract class PermanentShortcut : ShortcutBase
{
    protected override bool RequiredDoorIsPermanent => true;

    protected override void OnSuccess()
    {
        if (targetDoor != null)
        {
            targetDoor.ForceOpen(immediate: false, save: true, instigator: gameObject);
            SetActivatedVisual();
        }
    }

    protected virtual void Start()
    {
        if (targetDoor == null || !ShortcutProgressStore.IsAvailable)
            return;

        if (ShortcutProgressStore.IsShortcutUnlocked(targetDoor.mapID, targetDoor.doorID))
            SetActivatedVisual();
    }

    protected virtual void SetActivatedVisual() { }
}

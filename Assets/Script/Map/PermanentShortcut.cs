using UnityEngine;

public abstract class PermanentShortcut : ShortcutBase
{
    // 성공 시 저장(Save=true)하며 문 열기
    protected override void OnSuccess()
    {
        if (targetDoor != null)
        {
            targetDoor.ForceOpen(immediate: false, save: true);
            SetActivatedVisual();
        }
    }

    // [수정] 게임 시작 시(재접속 시) 이미 해금된 문인지 확인
    protected virtual void Start()
    {
        if (targetDoor != null && GameDataManager.Instance.IsShortcutUnlocked(targetDoor.mapID, targetDoor.doorID))
        {
            SetActivatedVisual(); // 레버 꺾기 등 실행
        }
    }

    // 자식에서 구현
    protected virtual void SetActivatedVisual() { }
}
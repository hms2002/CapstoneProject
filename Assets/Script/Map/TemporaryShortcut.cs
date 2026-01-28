public abstract class TemporaryShortcut : ShortcutBase
{
    // 성공 시 -> 문을 열되 "저장하지 마라(false)" 명령
    protected override void OnSuccess()
    {
        if (targetDoor != null)
        {
            targetDoor.ForceOpen(immediate: false, save: false);
        }
    }
}
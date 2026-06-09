public abstract class TemporaryShortcut : ShortcutBase
{
    protected override bool RequiredDoorIsPermanent => false;

    protected override void OnSuccess()
    {
        if (targetDoor != null)
            targetDoor.ForceOpen(immediate: false, save: false, instigator: gameObject);
    }
}

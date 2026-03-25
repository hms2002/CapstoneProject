public abstract class TemporaryShortcut : ShortcutBase
{
    protected override void OnSuccess()
    {
        if (targetDoor != null)
            targetDoor.ForceOpen(immediate: false, save: false);
    }
}

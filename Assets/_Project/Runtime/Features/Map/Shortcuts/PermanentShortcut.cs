using UnityEngine;

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
        if (targetDoor == null || ShortcutProgressService.Instance == null)
            return;

        if (ShortcutProgressService.Instance.IsShortcutUnlocked(targetDoor.mapID, targetDoor.doorID))
            SetActivatedVisual();
    }

    protected virtual void SetActivatedVisual() { }
}

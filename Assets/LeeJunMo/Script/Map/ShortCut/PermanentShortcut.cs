using UnityEngine;

public abstract class PermanentShortcut : ShortcutBase
{
    protected override void OnSuccess()
    {
        if (targetDoor != null)
        {
            targetDoor.ForceOpen(immediate: false, save: true);
            SetActivatedVisual();
        }
    }

    protected virtual void Start()
    {
        if (targetDoor == null || GameDataManager.Instance == null)
            return;

        if (GameDataManager.Instance.IsShortcutUnlocked(targetDoor.mapID, targetDoor.doorID))
            SetActivatedVisual();
    }

    protected virtual void SetActivatedVisual() { }
}

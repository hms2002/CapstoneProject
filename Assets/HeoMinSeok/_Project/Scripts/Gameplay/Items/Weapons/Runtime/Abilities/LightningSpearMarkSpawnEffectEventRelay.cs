using UnityEngine;

[DisallowMultipleComponent]
public sealed class LightningSpearMarkSpawnEffectEventRelay : MonoBehaviour
{
    [SerializeField] private LightningSpearMarkActor markActor;

    private void Awake()
    {
        CacheMarkActor();
    }

    public void ActivateMark()
    {
        CacheMarkActor();
        markActor?.ActivateFromSpawnEffect();
    }

    private void CacheMarkActor()
    {
        if (markActor == null)
            markActor = GetComponentInParent<LightningSpearMarkActor>();
    }
}

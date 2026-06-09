using UnityEngine;

[DisallowMultipleComponent]
public sealed class LightningSpearRecoveredSpearEffectEventRelay : MonoBehaviour
{
    [SerializeField] private LightningSpearRecoveredSpearActor actor;

    private void Awake()
    {
        ResolveActor();
    }

    public void RevealBodyVisual()
    {
        ResolveActor();
        actor?.ShowBodyVisual();
    }

    public void ShowBodyVisual()
    {
        RevealBodyVisual();
    }

    public void HideBodyVisual()
    {
        ResolveActor();
        actor?.HideBodyVisual();
    }

    public void CompleteDespawn()
    {
        ResolveActor();
        actor?.CompleteDespawnAnimation();
    }

    public void NotifySpawnAnimationComplete()
    {
        ResolveActor();
        actor?.NotifySpawnAnimationComplete();
    }

    public void NotifyDespawnAnimationComplete()
    {
        ResolveActor();
        actor?.NotifyDespawnAnimationComplete();
    }

    private void ResolveActor()
    {
        if (actor == null)
            actor = GetComponentInParent<LightningSpearRecoveredSpearActor>();
    }
}

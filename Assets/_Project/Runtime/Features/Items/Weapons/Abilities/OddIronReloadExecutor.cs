using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>Waits for reload audio before committing the slot ammo and relic consumption.</summary>
public sealed class OddIronReloadExecutor : WeaponAbilityExecutor
{
    private SoundRef reloadSound;
    private AudioHandle soundHandle;
    private WeaponInventory2D inventory;
    private OddIronRuntimeData ammo;

    public void Configure(in SoundRef sound) => reloadSound = sound;

    protected override void OnBegin(in WeaponAbilityExecutionContext context)
    {
        inventory = context.Owner != null ? context.Owner.GetComponent<WeaponInventory2D>() : null;
        ammo = inventory != null ? inventory.ActiveRuntimeData as OddIronRuntimeData : null;
        if (ammo == null || ammo.HasAmmo ||
            !WeaponExclusiveRelics.Has(context.Owner, WeaponExclusiveRelics.OddIronMagazine))
        {
            Cancel();
            return;
        }

        soundHandle = SoundPlaybackUtility.PlayTrackedOneShot(reloadSound,
            AbilityAudioRouter.BuildContext(context.AbilitySystem, context.Spec));
        if (!soundHandle.IsValid)
            Cancel();
    }

    private void Update()
    {
        if (!IsRunning)
            return;

        if (Context.AbilitySystem == null || !Context.AbilitySystem.isActiveAndEnabled ||
            (Context.Spec?.Token != null && Context.Spec.Token.IsCancelled))
        {
            Cancel();
            return;
        }
        if (inventory == null || !ReferenceEquals(inventory.ActiveRuntimeData, ammo))
        {
            ForceStop(WeaponExecutorEndReason.WeaponSwapped);
            return;
        }
        if (Time.timeScale == 0f || AudioListener.pause || SoundPlaybackUtility.IsPlaying(soundHandle))
            return;

        // No ammo or relic state changes until the sound has finished.
        if (WeaponExclusiveRelics.TryReload(Context.Owner, ammo))
            Complete();
        else
            Cancel();
    }

    private void OnDisable() => ForceStop(WeaponExecutorEndReason.OwnerDisabled);

    protected override void Cleanup(WeaponExecutorEndReason reason)
    {
        SoundPlaybackUtility.Stop(soundHandle);
        soundHandle = AudioHandle.Invalid;
        inventory = null;
        ammo = null;
    }
}

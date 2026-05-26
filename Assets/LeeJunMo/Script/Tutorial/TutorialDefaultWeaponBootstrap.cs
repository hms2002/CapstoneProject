using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class TutorialDefaultWeaponBootstrap : MonoBehaviour
{
    [Header("Loadout")]
    [SerializeField] private WeaponDefinition defaultWeapon;
    [SerializeField, Min(0)] private int targetSlotIndex;
    [SerializeField] private bool clearOtherWeaponSlots = true;

    [Header("Player")]
    [SerializeField] private PlayerInteractor2D player;
    [SerializeField] private WeaponInventory2D weaponInventory;

    [Header("Startup")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField, Min(0)] private int startupDelayFrames = 2;

    [Header("Events")]
    [SerializeField] private UnityEvent onApplied = new();
    [SerializeField] private UnityEvent onApplyFailed = new();

    private Coroutine applyRoutine;
    private bool autoApplyCompleted;

    public UnityEvent OnApplied => onApplied;
    public UnityEvent OnApplyFailed => onApplyFailed;

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
    }

    private void Start()
    {
        if (applyOnStart)
            BeginApplyAfterStartupDelay();
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;

        if (applyRoutine != null)
        {
            StopCoroutine(applyRoutine);
            applyRoutine = null;
        }
    }

    public void ApplyNow()
    {
        if (applyRoutine != null)
        {
            StopCoroutine(applyRoutine);
            applyRoutine = null;
        }

        ApplyDefaultWeapon(invokeFailureEvent: true);
    }

    public void BeginApplyAfterStartupDelay()
    {
        if (autoApplyCompleted)
            return;

        if (applyRoutine != null)
            StopCoroutine(applyRoutine);

        applyRoutine = StartCoroutine(ApplyAfterStartupDelayRoutine());
    }

    private IEnumerator ApplyAfterStartupDelayRoutine()
    {
        for (int i = 0; i < startupDelayFrames; i++)
            yield return null;

        applyRoutine = null;
        ApplyDefaultWeapon(invokeFailureEvent: true);
    }

    private void HandlePlayerRegistered(PlayerInteractor2D registeredPlayer)
    {
        if (!applyOnStart || autoApplyCompleted)
            return;

        player = registeredPlayer;
        BeginApplyAfterStartupDelay();
    }

    private bool ApplyDefaultWeapon(bool invokeFailureEvent)
    {
        if (defaultWeapon == null)
        {
            Debug.LogWarning("[TutorialDefaultWeaponBootstrap] Default weapon is missing.", this);
            InvokeFailureIfRequested(invokeFailureEvent);
            return false;
        }

        WeaponInventory2D inventory = ResolveWeaponInventory();
        if (inventory == null)
        {
            Debug.LogWarning("[TutorialDefaultWeaponBootstrap] Weapon inventory is missing.", this);
            InvokeFailureIfRequested(invokeFailureEvent);
            return false;
        }

        if (inventory.SlotCount <= 0)
        {
            Debug.LogWarning("[TutorialDefaultWeaponBootstrap] Weapon inventory has no slots.", inventory);
            InvokeFailureIfRequested(invokeFailureEvent);
            return false;
        }

        int targetIndex = Mathf.Clamp(targetSlotIndex, 0, inventory.SlotCount - 1);

        if (clearOtherWeaponSlots)
        {
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                if (i == targetIndex || inventory.GetWeaponInSlot(i) == null)
                    continue;

                if (!inventory.TrySetWeaponSlot(i, null, autoEquipIfNone: false))
                {
                    Debug.LogWarning(
                        $"[TutorialDefaultWeaponBootstrap] Failed to clear weapon slot {i}.",
                        inventory);
                    InvokeFailureIfRequested(invokeFailureEvent);
                    return false;
                }
            }
        }

        if (!inventory.TrySetWeaponSlot(targetIndex, defaultWeapon, autoEquipIfNone: false))
        {
            Debug.LogWarning(
                $"[TutorialDefaultWeaponBootstrap] Failed to set default weapon '{defaultWeapon.name}' to slot {targetIndex}.",
                inventory);
            InvokeFailureIfRequested(invokeFailureEvent);
            return false;
        }

        inventory.Equip(targetIndex);
        autoApplyCompleted = true;
        onApplied?.Invoke();
        return true;
    }

    private WeaponInventory2D ResolveWeaponInventory()
    {
        if (weaponInventory != null)
            return weaponInventory;

        PlayerInteractor2D resolvedPlayer = ResolvePlayer();
        if (resolvedPlayer != null)
        {
            weaponInventory = resolvedPlayer.GetComponent<WeaponInventory2D>();
            if (weaponInventory == null)
                weaponInventory = resolvedPlayer.GetComponentInChildren<WeaponInventory2D>(true);
        }

        if (weaponInventory == null)
            weaponInventory = FindAnyObjectByType<WeaponInventory2D>(FindObjectsInactive.Include);

        return weaponInventory;
    }

    private PlayerInteractor2D ResolvePlayer()
    {
        if (player != null)
            return player;

        if (PlayerRuntimeRegistry.CurrentPlayer != null)
        {
            player = PlayerRuntimeRegistry.CurrentPlayer;
            return player;
        }

        player = PlayerRuntimeRegistry.GetPlayerComponent<PlayerInteractor2D>();
        if (player == null)
            player = FindAnyObjectByType<PlayerInteractor2D>(FindObjectsInactive.Include);

        return player;
    }

    private void InvokeFailureIfRequested(bool invokeFailureEvent)
    {
        if (invokeFailureEvent)
            onApplyFailed?.Invoke();
    }
}

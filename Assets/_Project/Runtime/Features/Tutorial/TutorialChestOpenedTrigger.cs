using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class TutorialChestOpenedTrigger : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private TreasureChest targetChest;
    [SerializeField] private bool firstOpenOnly = true;

    [Header("Trigger")]
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private UnityEvent onChestOpened = new();

    private bool hasTriggered;
    private TreasureChest subscribedChest;
    private bool subscribedFirstOpenOnly;

    public UnityEvent OnChestOpened => onChestOpened;

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToChest();
    }

    private void OnDisable()
    {
        UnsubscribeFromChest();
    }

    public void ResetRuntimeTrigger()
    {
        hasTriggered = false;
    }

    public void FireNow()
    {
        if (triggerOnce && hasTriggered)
            return;

        hasTriggered = true;
        onChestOpened?.Invoke();
    }

    private void HandleChestOpened(TreasureChest chest)
    {
        if (targetChest != null && chest != targetChest)
            return;

        FireNow();
    }

    private void SubscribeToChest()
    {
        UnsubscribeFromChest();

        if (targetChest == null)
            return;

        subscribedChest = targetChest;
        subscribedFirstOpenOnly = firstOpenOnly;

        if (firstOpenOnly)
            subscribedChest.FirstOpenedUi += HandleChestOpened;
        else
            subscribedChest.OpenedUi += HandleChestOpened;
    }

    private void UnsubscribeFromChest()
    {
        if (subscribedChest == null)
            return;

        if (subscribedFirstOpenOnly)
            subscribedChest.FirstOpenedUi -= HandleChestOpened;
        else
            subscribedChest.OpenedUi -= HandleChestOpened;

        subscribedChest = null;
    }

    private void ResolveReferences()
    {
        if (targetChest != null)
            return;

        targetChest = GetComponent<TreasureChest>();
        if (targetChest != null)
            return;

        targetChest = GetComponentInParent<TreasureChest>();
        if (targetChest != null)
            return;

        targetChest = GetComponentInChildren<TreasureChest>(includeInactive: true);
    }
}

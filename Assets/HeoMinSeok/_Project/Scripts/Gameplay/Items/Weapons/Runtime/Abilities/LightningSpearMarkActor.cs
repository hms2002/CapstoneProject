using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class LightningSpearMarkActor : MonoBehaviour
{
    [Header("Visual State")]
    [SerializeField] private GameObject spawnEffectVisual;
    [SerializeField] private GameObject activeVisual;
    [SerializeField] private GameObject selectedVisual;
    [SerializeField, HideInInspector] private GameObject landingPreviewVisual;
    [SerializeField, HideInInspector] private GameObject validRushVisual;

    private LightningSpearRuntimeState owner;
    private AbilitySystem sourceSystem;
    private AbilitySpec sourceSpec;
    private LightningSpearLoadout loadout;
    private float activationRemaining;
    private float lifetimeRemaining;
    private bool isActive;
    private bool isConsumed;
    private bool isDestroying;

    public MonsterRoomArea2D RoomArea { get; private set; }
    public bool IsActive => isActive && !isConsumed;

    public void Initialize(
        LightningSpearRuntimeState ownerState,
        MonsterRoomArea2D roomArea,
        float lifetimeSeconds,
        float activationDelaySeconds,
        AbilitySystem abilitySystem,
        AbilitySpec abilitySpec,
        LightningSpearLoadout sourceLoadout)
    {
        owner = ownerState;
        RoomArea = roomArea;
        sourceSystem = abilitySystem;
        sourceSpec = abilitySpec;
        loadout = sourceLoadout;
        activationRemaining = Mathf.Max(0f, activationDelaySeconds);
        lifetimeRemaining = Mathf.Max(0.01f, lifetimeSeconds);
        isActive = false;
        isConsumed = false;
        isDestroying = false;

        SetSpawnState();
        SetFeedback(false, false);

        if (spawnEffectVisual == null && activationRemaining <= 0f)
            ActivateFromSpawnEffect();
    }

    public void Consume()
    {
        if (isConsumed || isDestroying)
            return;

        isConsumed = true;
        DestroyMark();
    }

    public void ActivateFromSpawnEffect()
    {
        if (isConsumed || isDestroying || isActive)
            return;

        SetActiveState(true);
        NotifyActivated();
    }

    public void SetFeedback(bool inRushRange, bool _)
    {
        bool canShow = IsActive;

        if (validRushVisual != null)
            validRushVisual.SetActive(false);

        if (selectedVisual != null)
            selectedVisual.SetActive(canShow && inRushRange);
    }

    private void Update()
    {
        if (isConsumed)
            return;

        if (!isActive)
        {
            activationRemaining -= Time.deltaTime;
            if (activationRemaining <= 0f)
                ActivateFromSpawnEffect();

            return;
        }

        lifetimeRemaining -= Time.deltaTime;
        if (lifetimeRemaining <= 0f)
            DestroyMark();
    }

    private void SetActiveState(bool active)
    {
        isActive = active;

        if (spawnEffectVisual != null)
            spawnEffectVisual.SetActive(!active);

        if (landingPreviewVisual != null)
            landingPreviewVisual.SetActive(false);

        if (activeVisual != null)
            activeVisual.SetActive(active);
    }

    private void SetSpawnState()
    {
        if (spawnEffectVisual != null)
            spawnEffectVisual.SetActive(true);

        if (landingPreviewVisual != null)
            landingPreviewVisual.SetActive(false);

        if (activeVisual != null)
            activeVisual.SetActive(false);

        if (validRushVisual != null)
            validRushVisual.SetActive(false);

        if (selectedVisual != null)
            selectedVisual.SetActive(false);
    }

    private void NotifyActivated()
    {
        owner?.HandleMarkActivated(this, sourceSystem, sourceSpec, loadout);
    }

    private void DestroyMark()
    {
        if (isDestroying)
            return;

        isDestroying = true;
        SetFeedback(false, false);
        owner?.UnregisterMark(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        owner?.UnregisterMark(this);
    }
}

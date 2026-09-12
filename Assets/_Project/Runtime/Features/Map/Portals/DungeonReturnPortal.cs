using UnityEngine;

/// <summary>Gates a dead-end room's reusable return interaction using room entry, complete waves and split-aware encounter counts.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DungeonReturnPortal : InteractableBase
{
    [SerializeField] private DungeonReturnPortalView view;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string prompt = "시작 방으로 돌아가기";
    private DungeonReturnTravel travel;
    private MonsterSpawnRoomGroup group;
    private Collider2D interaction;
    private bool entryUnlock;
    private bool visited;
    private bool revealed;
    private float clearSince = -1f;
    private float nextCheck;
    public int RoomPlacementId { get; private set; }
    public bool IsRevealed => revealed;
    public bool EncounterBusy => group != null &&
        (group.RemainingRegisteredOrPendingCount > 0 ||
         (group.RoomEntrySpawnStarted && !group.RoomWavesCompleted));

    public void Configure(DungeonReturnTravel owner, int roomId, MonsterSpawnRoomGroup roomGroup,
        bool revealOnEntry, RoomSocketDirection wallDirection)
    {
        travel = owner; RoomPlacementId = roomId; group = roomGroup; entryUnlock = revealOnEntry;
        interaction = GetComponent<Collider2D>();
        interaction.isTrigger = true;
        interaction.enabled = false;
        view?.SelectDirection(wallDirection);
    }

    public void NotifyRoomEntered(int roomId)
    {
        if (roomId != RoomPlacementId) return;
        visited = true;
        if (entryUnlock) Reveal(false);
    }

    public void RestoreRevealed(bool value)
    {
        if (value) { visited = true; Reveal(true); }
    }

    private void OnEnable() { if (revealed) view?.Open(true); }

    private void Reveal(bool immediate)
    {
        if (revealed) return;
        revealed = true;
        view?.Open(immediate);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.1f;
        if (!revealed && !entryUnlock && group != null && group.RoomWavesCompleted && !EncounterBusy)
        {
            if (clearSince < 0f) clearSince = Time.unscaledTime;
            if (Time.unscaledTime - clearSince >= 0.35f) Reveal(false);
        }
        else if (EncounterBusy) clearSince = -1f;
        if (!revealed && visited && entryUnlock) Reveal(false);
        if (interaction != null) interaction.enabled = revealed && (view == null || !view.IsOpening);
    }

    public override bool CanInteract(IPlayerInteractor player) =>
        isActiveAndEnabled && revealed && (view == null || !view.IsOpening) && !EncounterBusy &&
        travel != null && travel.CanTravel(player);
    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (CanInteract(player)) travel.TryTravel(this, player);
    }
    public override InteractState GetInteractType() => InteractState.Idle;
    public override string GetInteractDescription() => prompt;
    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

#if UNITY_EDITOR
    public void EditorConfigure(DungeonReturnPortalView portalView, Transform anchor) { view = portalView; promptAnchor = anchor; }
#endif
}

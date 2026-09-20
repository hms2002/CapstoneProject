using UnityEngine;

/// <summary>Gates dead-end return visibility and interaction by completed room waves and encounter holds, and aligns its trigger along the portal wall.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DungeonReturnPortal : InteractableBase
{
    [SerializeField] private DungeonReturnPortalView view;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string prompt = "시작 방으로 돌아가기";
    [SerializeField, Min(0f)] private float encounterHideSeconds = 0.2f;
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
    private bool EncounterReady => group == null || (group.RoomWavesCompleted && !EncounterBusy);

    public void Configure(DungeonReturnTravel owner, int roomId, MonsterSpawnRoomGroup roomGroup,
        bool revealOnEntry, RoomSocketDirection wallDirection)
    {
        travel = owner; RoomPlacementId = roomId; group = roomGroup; entryUnlock = revealOnEntry;
        interaction = GetComponent<Collider2D>();
        if (interaction is CapsuleCollider2D capsule)
        {
            float length = Mathf.Max(capsule.size.x, capsule.size.y);
            float thickness = Mathf.Min(capsule.size.x, capsule.size.y);
            bool horizontal = wallDirection == RoomSocketDirection.Up || wallDirection == RoomSocketDirection.Down;
            capsule.direction = horizontal ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
            capsule.size = horizontal ? new Vector2(length, thickness) : new Vector2(thickness, length);
        }
        interaction.isTrigger = true;
        interaction.enabled = false;
        view?.SelectDirection(wallDirection);
    }

    public void NotifyRoomEntered(int roomId)
    {
        if (roomId != RoomPlacementId) return;
        visited = true;
        if (entryUnlock && EncounterReady) Reveal(false);
    }

    public void RestoreRevealed(bool value)
    {
        if (value) { visited = true; if (EncounterReady) Reveal(true); }
    }

    private void OnEnable()
    {
        if (revealed && EncounterReady) view?.Open(true);
        else if (!EncounterReady) HideForEncounter();
    }

    private void HideForEncounter()
    {
        OnUnHighlight();
        bool wasRevealed = revealed;
        revealed = false;
        clearSince = -1f;
        if (interaction != null) interaction.enabled = false;
        if (wasRevealed) view?.ShrinkAndHide(encounterHideSeconds);
    }

    private void Reveal(bool immediate)
    {
        if (revealed) return;
        revealed = true;
        view?.Open(immediate);
    }

    private void Update()
    {
        // Check before throttling: newly started bell combat must hide an already opened portal.
        if (!EncounterReady) { HideForEncounter(); return; }
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

    public override void OnHighlight() => view?.SetHighlighted(true);
    public override void OnUnHighlight() => view?.SetHighlighted(false);
    private void OnDisable() => OnUnHighlight();

    public override bool CanInteract(IPlayerInteractor player) =>
        isActiveAndEnabled && revealed && (view == null || !view.IsOpening) && EncounterReady &&
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

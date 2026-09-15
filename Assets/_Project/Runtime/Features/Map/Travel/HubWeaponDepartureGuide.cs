using UnityEngine;

/// <summary>Blocks unarmed Hub departures and keeps weapon guidance active until equipment or scene departure, independently of timed speech.</summary>
[DisallowMultipleComponent]
public sealed class HubWeaponDepartureGuide : MonoBehaviour
{
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private string[] lines = { "무기가 없어", "무기가 없으면 출발할 수 없어", "무기를 먼저 챙겨야겠어" };
    [Tooltip("Speech duration only. The arrow remains until a weapon is equipped.")]
    [SerializeField, Min(0.1f)] private float displaySeconds = 3f;
    [SerializeField, Min(0.1f)] private float speechCooldown = 1.5f;
    [SerializeField] private Vector3 arrowOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField, Min(0f)] private float bounceHeight = 0.2f;
    [SerializeField, Min(0.05f)] private float bouncePeriod = 0.6f;

    private IPlayerInteractor guidedPlayer;
    private InteractableBase target;
    private GameObject arrow;
    private float nextTargetRefresh;
    private const float TargetRefreshInterval = 0.5f;
    private float started;
    private float nextSpeech;

    public bool TryAllowDeparture(IPlayerInteractor player)
    {
        if (!SceneDomainNamePolicy.IsHubSceneName(gameObject.scene.name)) return true;
        if (player == null || player.Transform == null) return false;
        var inventory = player.Transform.GetComponent<WeaponInventory2D>();
        if (inventory != null && inventory.HasEquippedWeapon) { Clear(); return true; }

        if (Time.unscaledTime < nextSpeech) return false;
        nextSpeech = Time.unscaledTime + Mathf.Max(0.1f, speechCooldown);
        guidedPlayer = player;
        if (lines != null && lines.Length > 0)
            player.Transform.GetComponent<PlayerSpeechController>()?.SpeakLine(
                lines[Random.Range(0, lines.Length)], displaySeconds);
        RefreshTarget();
        ShowArrowIfAvailable();
        return false;
    }

    private InteractableBase FindTarget(IPlayerInteractor player)
    {
        InteractableBase best = null;
        int bestPriority = int.MaxValue;
        float bestDistance = float.PositiveInfinity;
        // Scan on rejection or a bounded refresh while guidance is active, never every frame.
        foreach (var candidate in FindObjectsByType<InteractableBase>(FindObjectsSortMode.None))
        {
            if (!IsAvailable(candidate, player)) continue;
            int priority = candidate is WeaponDrop2D || candidate is WorldItemPickup2D ? 0 : 1;
            float distance = (candidate.transform.position - player.Transform.position).sqrMagnitude;
            if (priority > bestPriority || (priority == bestPriority && distance >= bestDistance)) continue;
            best = candidate;
            bestPriority = priority;
            bestDistance = distance;
        }
        return best;
    }

    private bool IsAvailable(InteractableBase candidate, IPlayerInteractor player)
    {
        if (candidate == null || !candidate.isActiveAndEnabled || candidate.gameObject.scene != gameObject.scene ||
            !candidate.CanInteract(player)) return false;
        if (candidate is WeaponDrop2D drop) return drop.Weapon != null;
        if (candidate is WorldItemPickup2D pickup) return pickup.Item is WeaponDefinition;
        if (candidate is GraveInteractable grave)
            return grave.graveType == GraveType.Weapon || grave.graveType == GraveType.JunkWeapon;
        if (candidate is ChestInteractable chest)
            return chest.TryGetComponent<TreasureChest>(out var rewards) && rewards.HasAvailableWeaponReward;
        return false;
    }

    private void LateUpdate()
    {
        if (guidedPlayer == null) return;
        if (guidedPlayer.Transform == null ||
            !SceneDomainNamePolicy.IsHubSceneName(gameObject.scene.name)) { Clear(); return; }
        var inventory = guidedPlayer.Transform.GetComponent<WeaponInventory2D>();
        if (inventory != null && inventory.HasEquippedWeapon) { Clear(); return; }
        // Shopping/other temporary player states must not end the guidance session.
        if (guidedPlayer.CurrentState == InteractState.Idle && Time.unscaledTime >= nextTargetRefresh)
            RefreshTarget();
        ShowArrowIfAvailable();
    }

    private void RefreshTarget()
    {
        var next = FindTarget(guidedPlayer);
        if (next != target) started = Time.unscaledTime;
        target = next;
        nextTargetRefresh = Time.unscaledTime + TargetRefreshInterval;
    }

    private void ShowArrowIfAvailable()
    {
        if (target == null || !target.isActiveAndEnabled || arrowPrefab == null)
        {
            if (arrow != null) arrow.SetActive(false);
            return;
        }
        if (arrow == null) arrow = Instantiate(arrowPrefab, transform);
        arrow.SetActive(true);
        UpdateArrow();
    }

    private void UpdateArrow()
    {
        float phase = (Time.unscaledTime - started) / Mathf.Max(0.05f, bouncePeriod);
        float bounce = Mathf.Abs(Mathf.Sin(phase * Mathf.PI)) * bounceHeight;
        Transform anchor = target.GetPromptAnchor();
        arrow.transform.position = (anchor != null ? anchor.position : target.transform.position) + arrowOffset + Vector3.up * bounce;
        arrow.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
    }

    private void OnDisable() => Clear();
    private void Clear()
    {
        if (arrow != null) { arrow.SetActive(false); Destroy(arrow); }
        arrow = null;
        target = null;
        guidedPlayer = null;
    }
}

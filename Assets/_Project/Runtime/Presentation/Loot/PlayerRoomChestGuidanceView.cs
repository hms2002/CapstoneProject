using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Responsibility: project the current room's nearest unopened chest around the player
/// outside combat. Owns only authored child visuals, never loot or encounter state.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRoomChestGuidanceView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer arrow;
    [SerializeField] private SpriteRenderer chestIcon;
    [SerializeField, Min(0f)] private float distanceFromPlayer = 1.2f;
    [SerializeField, Min(0f)] private float arrowOffset = 0.35f;
    [SerializeField] private Vector2 anchorOffset = new Vector2(0f, 0.3f);
    [SerializeField] private Vector2 arrowVisualForward = Vector2.left;

    private PlayerInteractor2D player;
    private PlayerDeathReturnToHub2D death;
    private PrototypeTutorialUpgrade tutorial;
    private const float ApproachDistance = 4f;
    private PlayerInteractableTracker2D interactionTracker;
    private TreasureChest lastTarget;
    private ChestInteractable targetInteraction;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ResolveTutorial();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => ResolveTutorial();

    private void ResolveTutorial()
    {
        tutorial = FindAnyObjectByType<PrototypeTutorialUpgrade>();
    }

    private void Awake()
    {
        player = GetComponentInParent<PlayerInteractor2D>();
        interactionTracker = GetComponentInParent<PlayerInteractableTracker2D>();
        death = GetComponentInParent<PlayerDeathReturnToHub2D>();
        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (player == null || !player.isActiveAndEnabled || player.CurrentState != InteractState.Idle ||
            (death != null && death.IsDeathSequenceRunning) || Time.timeScale <= 0f ||
            arrow == null || chestIcon == null ||
            !TryGetTarget(out TreasureChest target))
        {
            SetVisible(false);
            return;
        }

        if (lastTarget != target)
        {
            lastTarget = target;
            targetInteraction = target.GetComponent<ChestInteractable>();
        }

        if (interactionTracker != null && interactionTracker.IsInInteractionRange(targetInteraction))
        {
            SetVisible(false);
            return;
        }

        RenderTarget(player.transform.position, target.transform.position);
    }

    private void RenderTarget(Vector3 playerPosition, Vector3 targetPosition)
    {
        float proximity = Mathf.Clamp01(Vector2.Distance(playerPosition, targetPosition) / ApproachDistance);
        Vector3 origin = playerPosition + (Vector3)(anchorOffset * proximity);
        Vector2 delta = targetPosition - origin;
        Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.up;
        transform.SetPositionAndRotation(origin + (Vector3)(direction * distanceFromPlayer * proximity), Quaternion.identity);
        chestIcon.transform.rotation = Quaternion.identity;
        arrow.transform.position = transform.position + (Vector3)(direction * arrowOffset * proximity);
        Vector2 forward = arrowVisualForward.sqrMagnitude > 0.0001f ? arrowVisualForward : Vector2.left;
        arrow.transform.rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(forward, direction));
        SetVisible(true);
    }

    private bool TryGetTarget(out TreasureChest target)
    {
        if (tutorial != null && tutorial.isActiveAndEnabled)
            return tutorial.TryGetChestGuidanceTarget(out target);

        target = null;
        DungeonMapRuntimeController map = DungeonMapRuntimeController.Active;
        return map != null && map.TryGetChestGuidanceTarget(player.transform.position, out target);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        tutorial = null;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (arrow != null) arrow.enabled = visible;
        if (chestIcon != null) chestIcon.enabled = visible;
    }
}

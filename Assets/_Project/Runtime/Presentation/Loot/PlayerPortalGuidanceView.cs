using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-authored portal marker that follows the player without owning travel or progression.</summary>
[DisallowMultipleComponent]
public sealed class PlayerPortalGuidanceView : MonoBehaviour
{
    [SerializeField] private Transform target;
    [Tooltip("Hub guidance uses equipped weapon state; Grand Hall guidance uses the scribe sequence.")]
    [SerializeField] private bool requireEquippedWeapon;
    [SerializeField] private GrandHallScribeSequence scribeSequence;
    [SerializeField] private SpriteRenderer arrow;
    [SerializeField] private SpriteRenderer portalIcon;
    [SerializeField, Min(0f)] private float distanceFromPlayer = 1.2f;
    [SerializeField, Min(0f)] private float arrowOffset = 0.35f;
    [SerializeField] private Vector2 anchorOffset = new Vector2(0f, 0.3f);

    private InteractableBase portal;
    private PlayerInteractor2D player;
    private WeaponInventory2D weapons;
    private PlayerDeathReturnToHub2D death;
    private const float ApproachDistance = 4f;
    private PlayerInteractableTracker2D interactionTracker;

    private void Awake()
    {
        // Hub uses SceneTravelInteractable; officer routes use ScenePortal.
        portal = target != null ? target.GetComponent<InteractableBase>() : null;
        SetVisible(false);
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += BindPlayer;
        PlayerRuntimeRegistry.PlayerUnregistered += UnbindPlayer;
        BindPlayer(PlayerRuntimeRegistry.CurrentPlayer);
    }

    private void BindPlayer(PlayerInteractor2D next)
    {
        player = next;
        interactionTracker = player != null ? player.GetComponent<PlayerInteractableTracker2D>() : null;
        weapons = player != null ? player.GetComponent<WeaponInventory2D>() : null;
        death = player != null ? player.GetComponent<PlayerDeathReturnToHub2D>() : null;
        SetVisible(false);
    }

    private void UnbindPlayer(PlayerInteractor2D previous)
    {
        if (player == previous) BindPlayer(null);
    }

    private void LateUpdate()
    {
        if (!CanShow())
        {
            SetVisible(false);
            return;
        }

        float proximity = Mathf.Clamp01(Vector2.Distance(player.transform.position, target.position) / ApproachDistance);
        Vector3 origin = player.transform.position + (Vector3)(anchorOffset * proximity);
        Vector2 delta = target.position - origin;
        Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.up;
        transform.SetPositionAndRotation(origin + (Vector3)(direction * distanceFromPlayer * proximity), Quaternion.identity);
        portalIcon.transform.rotation = Quaternion.identity;
        arrow.transform.position = transform.position + (Vector3)(direction * arrowOffset * proximity);
        arrow.transform.rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.left, direction));
        SetVisible(true);
    }

    private bool CanShow()
    {
        if (player == null || !player.isActiveAndEnabled || player.CurrentState != InteractState.Idle ||
            (death != null && death.IsDeathSequenceRunning) || Time.timeScale <= 0f ||
            SceneTransitionPlayback.IsTransitionActive || DialoguePlayback.IsPlaying ||
            SceneManager.GetActiveScene() != gameObject.scene ||
            target == null || portal == null || !portal.isActiveAndEnabled || arrow == null || portalIcon == null)
            return false;

        if (interactionTracker != null && interactionTracker.IsInInteractionRange(portal))
            return false;

        if (requireEquippedWeapon)
            return weapons != null && weapons.HasEquippedWeapon;

        return scribeSequence != null && scribeSequence.IsPortalGuidanceReady && portal is ScenePortal routePortal &&
            (!RunSessionStore.IsRunActive ||
             RunRoutePlayback.GetTravelBlockWarning(routePortal) != WarningPopupCode.BossAlreadyDefeatedThisRun);
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= BindPlayer;
        PlayerRuntimeRegistry.PlayerUnregistered -= UnbindPlayer;
        BindPlayer(null);
    }

    private void SetVisible(bool visible)
    {
        if (arrow != null) arrow.enabled = visible;
        if (portalIcon != null) portalIcon.enabled = visible;
    }
}

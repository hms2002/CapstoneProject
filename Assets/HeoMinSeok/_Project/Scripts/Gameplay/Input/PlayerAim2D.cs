using UnityEngine;
using UnityGAS;

/// <summary>
/// Owns player aim data only. Weapon presentation is driven by
/// WeaponPresentationRig2D.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAim2D : MonoBehaviour, IAimDirectionSource2D
{
    private const string AimBlockedTagResourcePath = "Tags/State.Aim.Blocked";

    [SerializeField] private Camera mainCamera;
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private GameplayTag aimLockedTag;

    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public Vector2 MouseWorld { get; private set; }

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (aimLockedTag == null) aimLockedTag = Resources.Load<GameplayTag>(AimBlockedTagResourcePath);
    }

    private void Update()
    {
        UpdateMouseAim();
    }

    public Vector2 GetAimDirection()
    {
        return AimDirection;
    }

    private void UpdateMouseAim()
    {
        if (mainCamera == null) return;
        if (tagSystem != null && aimLockedTag != null && tagSystem.HasTag(aimLockedTag))
            return;

        var world = InputBindingService.EnsureInstance().GetPointerWorldPosition(mainCamera, 0f);
        MouseWorld = world;

        Vector2 dir = world - transform.position;
        if (dir.sqrMagnitude > 0.0001f)
            AimDirection = dir.normalized;
    }
}

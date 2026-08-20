using UnityEngine;
using UnityGAS;

/// <summary>
/// Owns player aim data only. Weapon presentation is driven by
/// WeaponPresentationRig2D.
/// </summary>
[DisallowMultipleComponent]
// 책임: 플레이어 마우스 월드 위치와 조준 방향을 계산해 전투 입력/무기 로직에 제공한다.
public sealed class PlayerAim2D : MonoBehaviour, IAimDirectionSource2D, ICursorWorldSource2D
{
    private const string AimBlockedTagResourcePath = "Tags/State.Aim.Blocked";

    [SerializeField] private Camera mainCamera;
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private GameplayTag aimLockedTag;

    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public Vector2 MouseWorld { get; private set; }
    public Vector2 CursorWorld => MouseWorld;

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

    public bool SetAimDirectionForPresentation(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        AimDirection = direction.normalized;
        MouseWorld = (Vector2)transform.position + AimDirection;
        return true;
    }

    public bool SetAimWorldPositionForPresentation(Vector2 worldPosition)
    {
        Vector2 direction = worldPosition - (Vector2)transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        MouseWorld = worldPosition;
        AimDirection = direction.normalized;
        return true;
    }

    private void UpdateMouseAim()
    {
        if (mainCamera == null) return;
        if (tagSystem != null && aimLockedTag != null && tagSystem.HasTag(aimLockedTag))
            return;

        var world = InputActionQuery.GetPointerWorldPosition(mainCamera, 0f);
        MouseWorld = world;

        Vector2 dir = world - transform.position;
        if (dir.sqrMagnitude > 0.0001f)
            AimDirection = dir.normalized;
    }
}

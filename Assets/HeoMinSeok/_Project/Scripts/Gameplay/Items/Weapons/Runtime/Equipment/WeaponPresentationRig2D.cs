using UnityEngine;

/// <summary>
/// Drives the equipped weapon presentation from player aim data.
/// The weapon instance is always mounted under WeaponMount.
/// </summary>
[DisallowMultipleComponent]
public sealed class WeaponPresentationRig2D : MonoBehaviour
{
    private const string LeftMarkerName = "LeftWeaponSocketMarker";
    private const string RightMarkerName = "RightWeaponSocketMarker";
    private const string LegacyLeftMarkerName = "LHand";
    private const string LegacyRightMarkerName = "RHand";

    [Header("Refs")]
    [SerializeField] private PlayerAim2D aimSource;
    [SerializeField] private Transform ownerTransform;
    [SerializeField] private Transform leftWeaponSocketMarker;
    [SerializeField] private Transform rightWeaponSocketMarker;
    [SerializeField] private Transform sideOffsetRoot;
    [SerializeField] private Transform aimRoot;
    [SerializeField] private Transform weaponMount;

    [Header("Side Switch")]
    [SerializeField, Min(0f)] private float sideSwitchDeadZone = 0.1f;

    private int currentSideSign = 1;

    public Transform WeaponMount => weaponMount != null ? weaponMount : transform;
    public int CurrentSideSign => currentSideSign;

    public bool HasRequiredRig(out string missing)
    {
        if (aimSource == null)
        {
            missing = nameof(aimSource);
            return false;
        }

        if (ownerTransform == null)
        {
            missing = nameof(ownerTransform);
            return false;
        }

        if (leftWeaponSocketMarker == null)
        {
            missing = nameof(leftWeaponSocketMarker);
            return false;
        }

        if (rightWeaponSocketMarker == null)
        {
            missing = nameof(rightWeaponSocketMarker);
            return false;
        }

        if (sideOffsetRoot == null)
        {
            missing = nameof(sideOffsetRoot);
            return false;
        }

        if (aimRoot == null)
        {
            missing = nameof(aimRoot);
            return false;
        }

        if (weaponMount == null)
        {
            missing = nameof(weaponMount);
            return false;
        }

        missing = null;
        return true;
    }

    private void Awake()
    {
        ResolveReferences();
        RefreshNow();
    }

    private void LateUpdate()
    {
        RefreshNow();
    }

    public void RefreshNow()
    {
        if (aimSource == null)
            return;

        UpdateSideOffset();
        UpdateAimRotation();
    }

    private void UpdateSideOffset()
    {
        if (ownerTransform == null || sideOffsetRoot == null)
            return;

        float deltaX = aimSource.MouseWorld.x - ownerTransform.position.x;
        if (deltaX > sideSwitchDeadZone)
            currentSideSign = 1;
        else if (deltaX < -sideSwitchDeadZone)
            currentSideSign = -1;

        Transform marker = currentSideSign < 0 ? leftWeaponSocketMarker : rightWeaponSocketMarker;
        if (marker == null)
            return;

        sideOffsetRoot.position = marker.position;
        sideOffsetRoot.rotation = ownerTransform.rotation;
        sideOffsetRoot.localScale = Vector3.one;
    }

    private void UpdateAimRotation()
    {
        if (aimRoot == null)
            return;

        Vector2 direction = aimSource.AimDirection;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.right;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        aimRoot.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void ResolveReferences()
    {
        if (aimSource == null)
            aimSource = GetComponentInParent<PlayerAim2D>();

        if (ownerTransform == null)
            ownerTransform = aimSource != null ? aimSource.transform : transform.root;

        Transform searchRoot = ownerTransform != null ? ownerTransform : transform.root;

        if (leftWeaponSocketMarker == null)
            leftWeaponSocketMarker = FindDirectChild(searchRoot, LeftMarkerName) ?? FindDirectChild(searchRoot, LegacyLeftMarkerName);

        if (rightWeaponSocketMarker == null)
            rightWeaponSocketMarker = FindDirectChild(searchRoot, RightMarkerName) ?? FindDirectChild(searchRoot, LegacyRightMarkerName);

        if (sideOffsetRoot == null)
            sideOffsetRoot = transform.Find("SideOffsetRoot");

        if (aimRoot == null && sideOffsetRoot != null)
            aimRoot = sideOffsetRoot.Find("AimRoot");

        if (weaponMount == null && aimRoot != null)
            weaponMount = aimRoot.Find("WeaponMount");
    }

    private static Transform FindDirectChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        return root.Find(childName);
    }
}

using System.Collections;
using System.Collections.Generic;
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
    private int aimPresentationOverrideToken;
    private int activeAimPresentationOverrideToken;
    private float activeAimPresentationOverrideReleaseTime;
    private bool activeAimPresentationOverrideReleaseRequested;
    private int lockedFacingSideSign = 1;
    private float lockedAimAngleDeg;
    private readonly HashSet<object> cinematicPresentationLockOwners = new();
    private int cinematicLockedFacingSideSign = 1;
    private float cinematicLockedAimAngleDeg;
    private Coroutine aimPresentationOverrideReleaseRoutine;
    private WeaponAimPresentationMode activeAimPresentationMode = WeaponAimPresentationMode.FollowAim;

    public Transform WeaponMount => weaponMount != null ? weaponMount : transform;
    public int CurrentSideSign => currentSideSign;

    private bool IsCinematicPresentationLocked => cinematicPresentationLockOwners.Count > 0;

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

    private void OnDisable()
    {
        cinematicPresentationLockOwners.Clear();
        ClearAimPresentationOverride();
    }

    public void RefreshNow()
    {
        if (aimSource == null)
            return;

        UpdateSideOffset();
        UpdateAimRotation();
    }

    public int BeginAimPresentationOverride(
        WeaponAimPresentationMode mode,
        Vector2 castDirection,
        float minimumHoldTime = 0f)
    {
        if (mode == WeaponAimPresentationMode.FollowAim)
            return 0;

        ResolveReferences();
        RefreshNow();

        activeAimPresentationOverrideToken = ++aimPresentationOverrideToken;
        lockedFacingSideSign = ResolveFacingSideSign(castDirection, currentSideSign);
        activeAimPresentationMode = mode;
        activeAimPresentationOverrideReleaseTime = Time.time + Mathf.Max(0f, minimumHoldTime);
        activeAimPresentationOverrideReleaseRequested = false;
        lockedAimAngleDeg = ResolveDirectionAngle(castDirection);

        if (aimPresentationOverrideReleaseRoutine != null)
        {
            StopCoroutine(aimPresentationOverrideReleaseRoutine);
            aimPresentationOverrideReleaseRoutine = null;
        }

        UpdateAimRotation();
        return activeAimPresentationOverrideToken;
    }

    public void AcquireCinematicPresentationLock(object ownerToken)
    {
        if (ownerToken == null)
            return;

        ResolveReferences();
        if (!cinematicPresentationLockOwners.Add(ownerToken))
            return;

        if (cinematicPresentationLockOwners.Count != 1)
            return;

        cinematicLockedFacingSideSign = currentSideSign < 0 ? -1 : 1;
        cinematicLockedAimAngleDeg = ResolveCurrentAimRootAngle();
    }

    public void ReleaseCinematicPresentationLock(object ownerToken)
    {
        if (ownerToken == null)
            return;

        if (!cinematicPresentationLockOwners.Remove(ownerToken))
            return;

        if (cinematicPresentationLockOwners.Count > 0)
            return;

        cinematicLockedFacingSideSign = currentSideSign < 0 ? -1 : 1;
        cinematicLockedAimAngleDeg = 0f;
    }

    public void EndAimPresentationOverride(int token)
    {
        if (token == 0 || token != activeAimPresentationOverrideToken)
            return;

        float remaining = activeAimPresentationOverrideReleaseTime - Time.time;
        if (remaining <= 0f)
        {
            ClearAimPresentationOverride();
            return;
        }

        activeAimPresentationOverrideReleaseRequested = true;

        if (aimPresentationOverrideReleaseRoutine == null)
            aimPresentationOverrideReleaseRoutine = StartCoroutine(CoReleaseAimPresentationOverrideAfterHold(token));
    }

    private void UpdateSideOffset()
    {
        if (ownerTransform == null || sideOffsetRoot == null)
            return;

        float deltaX = aimSource.MouseWorld.x - ownerTransform.position.x;
        if (IsCinematicPresentationLocked)
        {
            currentSideSign = cinematicLockedFacingSideSign;
        }
        else if (activeAimPresentationMode != WeaponAimPresentationMode.FollowAim)
        {
            currentSideSign = lockedFacingSideSign;
        }
        else if (deltaX > sideSwitchDeadZone)
        {
            currentSideSign = 1;
        }
        else if (deltaX < -sideSwitchDeadZone)
        {
            currentSideSign = -1;
        }

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

        float angle = IsCinematicPresentationLocked
            ? cinematicLockedAimAngleDeg
            : ResolveAimRootAngle(direction);
        aimRoot.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private float ResolveCurrentAimRootAngle()
    {
        if (aimRoot != null)
            return aimRoot.eulerAngles.z;

        Vector2 direction = aimSource != null ? aimSource.AimDirection : Vector2.right;
        return ResolveDirectionAngle(direction);
    }

    private float ResolveAimRootAngle(Vector2 followAimDirection)
    {
        switch (activeAimPresentationMode)
        {
            case WeaponAimPresentationMode.FacingSideOnly:
                return lockedFacingSideSign < 0 ? 180f : 0f;

            case WeaponAimPresentationMode.LockedAtCast:
                return lockedAimAngleDeg;

            default:
                return ResolveDirectionAngle(followAimDirection);
        }
    }

    private IEnumerator CoReleaseAimPresentationOverrideAfterHold(int token)
    {
        while (token == activeAimPresentationOverrideToken &&
               activeAimPresentationOverrideReleaseRequested &&
               Time.time < activeAimPresentationOverrideReleaseTime)
        {
            yield return null;
        }

        if (token == activeAimPresentationOverrideToken &&
            activeAimPresentationOverrideReleaseRequested)
        {
            aimPresentationOverrideReleaseRoutine = null;
            ClearAimPresentationOverride();
        }
    }

    private void ClearAimPresentationOverride()
    {
        if (aimPresentationOverrideReleaseRoutine != null)
        {
            StopCoroutine(aimPresentationOverrideReleaseRoutine);
            aimPresentationOverrideReleaseRoutine = null;
        }

        activeAimPresentationOverrideToken = 0;
        activeAimPresentationOverrideReleaseTime = 0f;
        activeAimPresentationOverrideReleaseRequested = false;
        lockedFacingSideSign = currentSideSign < 0 ? -1 : 1;
        activeAimPresentationMode = WeaponAimPresentationMode.FollowAim;

        if (isActiveAndEnabled)
            RefreshNow();
    }

    private static float ResolveDirectionAngle(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.right;

        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    private static int ResolveFacingSideSign(Vector2 direction, int fallbackSideSign)
    {
        if (direction.x < -0.0001f)
            return -1;

        if (direction.x > 0.0001f)
            return 1;

        return fallbackSideSign < 0 ? -1 : 1;
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

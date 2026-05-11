using UnityEngine;

/// <summary>
/// Defines the internal presentation hierarchy of an equipped weapon.
/// Animation clips should key MotionRoot, not the prefab root.
/// </summary>
[DisallowMultipleComponent]
public sealed class WeaponVisualRig2D : MonoBehaviour
{
    public const string WeaponVisualRootName = "WeaponVisualRoot";
    public const string MirrorRootName = "MirrorRoot";
    public const string MotionRootName = "MotionRoot";
    public const string RenderRootName = "RenderRoot";
    public const string MotionRootPath = WeaponVisualRootName + "/" + MirrorRootName + "/" + MotionRootName;

    [Header("Refs")]
    [SerializeField] private Transform weaponVisualRoot;
    [SerializeField] private Transform mirrorRoot;
    [SerializeField] private Transform motionRoot;
    [SerializeField] private Transform renderRoot;

    public Transform WeaponVisualRoot => weaponVisualRoot != null ? weaponVisualRoot : transform;
    public Transform MirrorRoot => mirrorRoot != null ? mirrorRoot : WeaponVisualRoot;
    public Transform MotionRoot => motionRoot != null ? motionRoot : MirrorRoot;
    public Transform RenderRoot => renderRoot != null ? renderRoot : MotionRoot;

    public void SetFacingSideSign(int sideSign)
    {
        int resolvedSign = sideSign < 0 ? -1 : 1;
        Transform mirror = MirrorRoot;

        mirror.localRotation = Quaternion.identity;

        Vector3 scale = mirror.localScale;
        scale.x = NormalizeScaleMagnitude(scale.x);
        scale.y = NormalizeScaleMagnitude(scale.y) * resolvedSign;
        scale.z = NormalizeScaleMagnitude(scale.z);
        mirror.localScale = scale;
    }

    public void SetAttackSideSign(int sideSign)
    {
        _ = sideSign;
        // Kept as a compatibility hook. Combo sideSign belongs to hitbox/combo
        // data and must not flip the whole weapon presentation.
    }

    public bool HasRequiredRig(out string missing)
    {
        if (weaponVisualRoot == null)
        {
            missing = nameof(weaponVisualRoot);
            return false;
        }

        if (mirrorRoot == null)
        {
            missing = nameof(mirrorRoot);
            return false;
        }

        if (motionRoot == null)
        {
            missing = nameof(motionRoot);
            return false;
        }

        if (renderRoot == null)
        {
            missing = nameof(renderRoot);
            return false;
        }

        missing = null;
        return true;
    }

    private void Reset()
    {
        ResolveReferences();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif

    private void ResolveReferences()
    {
        if (weaponVisualRoot == null)
            weaponVisualRoot = transform.Find(WeaponVisualRootName);

        if (mirrorRoot == null && weaponVisualRoot != null)
            mirrorRoot = weaponVisualRoot.Find(MirrorRootName);

        if (motionRoot == null && mirrorRoot != null)
            motionRoot = mirrorRoot.Find(MotionRootName);

        if (renderRoot == null && motionRoot != null)
            renderRoot = motionRoot.Find(RenderRootName);
    }

    private static float NormalizeScaleMagnitude(float value)
    {
        float magnitude = Mathf.Abs(value);
        return magnitude > 0.0001f ? magnitude : 1f;
    }
}

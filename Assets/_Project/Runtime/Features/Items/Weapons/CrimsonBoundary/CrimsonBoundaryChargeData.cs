using UnityEngine;

// Authored timing and presentation for Skill1; Q and the shared burn rules stay in the existing data.
[CreateAssetMenu(fileName = "ALData_CrimsonBoundaryCharge", menuName = "GAS/Weapon/Crimson Boundary/Charge Data")]
public sealed class CrimsonBoundaryChargeData : ScriptableObject
{
    // Approved artwork: the inner circle spans 86 pixels inside the 128-pixel outer circle.
    public const float InnerCircleFraction = 86f / 128f;
    public CrimsonBoundaryWeaponData weaponData;
    public GameObject presentationPrefab;
    [Min(0.01f)] public float minimumChargeSeconds = 0.7f;
    [Min(0.01f)] public float fullChargeSeconds = 1.4f;
    [Min(0.01f)] public float maximumDiameter = 6f;
    [Min(0.01f)] public float flightSeconds = 0.5f;
    public float partialDamageMultiplier = 3f;
    public float fullDamageMultiplier = 4f;
    public int partialBurnStacks = 10;
    public int fullBurnStacks = 12;

    public float ChargeRatio(float seconds) => Mathf.Clamp01(seconds / Mathf.Max(0.01f, fullChargeSeconds));
    public float ChargeDiameter(float seconds)
    {
        float ratio = ChargeRatio(seconds);
        float fraction = ratio <= 0.5f
            ? InnerCircleFraction * ratio * 2f
            : Mathf.Lerp(InnerCircleFraction, 1f, ratio * 2f - 1f);
        return maximumDiameter * fraction;
    }
    public bool CanRelease(float seconds) => seconds >= minimumChargeSeconds;
    public bool IsFull(float seconds) => seconds >= fullChargeSeconds;
}

// Gameplay commands authored presentation without depending on the Presentation assembly.
public interface ICrimsonBoundaryChargePresentation
{
    void BindOwner(CrimsonBoundaryRuntimeState owner);
    void ShowCharge(Vector3 ownerPosition, Vector3 target, float ratio, float diameter);
    void BeginFlight(Vector3 start, Vector3 target);
    void MoveMeteor(Vector3 position);
    void PlayImpact(Vector3 position, float diameter);
}

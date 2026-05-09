using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "ALData_LightningSpearSkill1", menuName = "GAS/Weapon/Lightning Spear/Skill1 Data")]
public sealed class LightningSpearSkill1Data : ScriptableObject
{
    [Header("Animation")]
    [SerializeField] private string markRushAnimationTrigger;
    [SerializeField] private string noMarkSweepAnimationTrigger;
    [SerializeField] private GameplayTag noMarkSweepHitEventTag;
    [SerializeField, Min(0f)] private float noMarkSweepHitEventTimeout = 0.35f;
    [SerializeField, Min(0f)] private float noMarkSweepFallbackHitDelay;

    [Header("Q - Mark Rush")]
    [SerializeField, Min(0.01f)] private float cursorSelectRadius = 1.5f;
    [SerializeField, Min(0.01f)] private float markRushRange = 9f;
    [SerializeField, Min(0f)] private float markRushBodyRadius = 0.25f;
    [SerializeField, Min(0f)] private float markRushArrivalHitDelay = 0.05f;
    [SerializeField, Min(0f)] private float markRushInternalDelay = 0.15f;
    [SerializeField] private LightningSpearDashStabTrailEffect markRushTrailEffectPrefab;
    [SerializeField] private LightningSpearHitConfig markRushHit = new LightningSpearHitConfig();

    [Header("Q - No Mark Sweep")]
    [SerializeField] private LightningSpearHitConfig noMarkSweepHit = new LightningSpearHitConfig();

    public string MarkRushAnimationTrigger => markRushAnimationTrigger;
    public string NoMarkSweepAnimationTrigger => noMarkSweepAnimationTrigger;
    public GameplayTag NoMarkSweepHitEventTag => noMarkSweepHitEventTag;
    public float NoMarkSweepHitEventTimeout => Mathf.Max(0f, noMarkSweepHitEventTimeout);
    public float NoMarkSweepFallbackHitDelay => Mathf.Max(0f, noMarkSweepFallbackHitDelay);

    public float CursorSelectRadius => Mathf.Max(0.01f, cursorSelectRadius);
    public float MarkRushRange => Mathf.Max(0.01f, markRushRange);
    public float MarkRushBodyRadius => Mathf.Max(0f, markRushBodyRadius);
    public float MarkRushArrivalHitDelay => Mathf.Max(0f, markRushArrivalHitDelay);
    public float MarkRushInternalDelay => Mathf.Max(0f, markRushInternalDelay);
    public LightningSpearDashStabTrailEffect MarkRushTrailEffectPrefab => markRushTrailEffectPrefab;
    public LightningSpearHitConfig MarkRushHit => markRushHit;
    public LightningSpearHitConfig NoMarkSweepHit => noMarkSweepHit;
}

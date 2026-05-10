using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 번개 창 무기의 Attack, Q, E 능력 참조와 표식/판정/피드백 저작 설정을 제공할 책임을 가집니다.
/// </summary>
[CreateAssetMenu(fileName = "WAL_LightningSpear", menuName = "Game/Weapon Ability Loadout/Lightning Spear")]
public sealed class LightningSpearLoadout : WeaponAbilityLoadout
{
    [Header("Core Actions")]
    [SerializeField] private AbilityDefinition baseAttack;
    [SerializeField] private AbilityDefinition markRushOrSweep;
    [SerializeField] private AbilityDefinition markRain;

    [Header("Mark Authoring")]
    [SerializeField] private LightningSpearMarkActor markPrefab;

    [Header("Q - Mark Rush")]
    [SerializeField, Min(0.01f)] private float cursorSelectRadius = 1.5f;
    [SerializeField, Min(0.01f)] private float markRushRange = 9f;
    [SerializeField, Min(0f)] private float markRushDuration = 0.1f;
    [SerializeField, Min(0f)] private float markRushBodyRadius = 0.25f;
    [SerializeField, Min(0f)] private float markRushArrivalHitDelay = 0.08f;
    [SerializeField, Min(0f)] private float markRushInternalDelay = 0.15f;
    [SerializeField] private LightningSpearDashStabTrailEffect markRushTrailEffectPrefab;
    [SerializeField] private LightningSpearHitConfig markRushHit = new LightningSpearHitConfig();

    [Header("Q - No Mark Sweep")]
    [SerializeField] private LightningSpearHitConfig noMarkSweepHit = new LightningSpearHitConfig();

    [Header("E - Mark Rain")]
    [SerializeField, Min(0.01f)] private float markLifetimeSeconds = 7f;
    [SerializeField, Min(0)] private int markRainCount = 6;
    [SerializeField, Min(0f)] private float markRainDelay = 0.25f;
    [SerializeField, Min(0.01f)] private float fallbackCombatRadius = 7f;
    [SerializeField, Min(0f)] private float minPlayerDistance = 1.2f;
    [SerializeField, Min(0f)] private float minMarkSpacing = 2f;
    [SerializeField, Min(0f)] private float landingProbeRadius = 0.35f;
    [SerializeField, Min(1)] private int candidateSamples = 96;
    [SerializeField] private LightningSpearHitConfig landingHit = new LightningSpearHitConfig();

    [Header("Placement / Movement Layers")]
    [SerializeField] private LayerMask hardBlockLayers;
    [SerializeField] private LayerMask softBlockLayers;
    [SerializeField] private LayerMask landingBlockedLayers;
    [SerializeField] private LayerMask requiredGroundLayers;

    [Header("Feedback Prefabs")]
    [SerializeField] private GameObject rushRangeIndicatorPrefab;
    [SerializeField] private GameObject selectedMarkIndicatorPrefab;

    public AbilityDefinition BaseAttack => baseAttack;
    public AbilityDefinition MarkRushOrSweep => markRushOrSweep;
    public AbilityDefinition MarkRain => markRain;
    public LightningSpearMarkActor MarkPrefab => markPrefab;

    public float CursorSelectRadius => Mathf.Max(0.01f, cursorSelectRadius);
    public float MarkRushRange => Mathf.Max(0.01f, markRushRange);
    public float MarkRushDuration => Mathf.Max(0f, markRushDuration);
    public float MarkRushBodyRadius => Mathf.Max(0f, markRushBodyRadius);
    public float MarkRushArrivalHitDelay => Mathf.Max(0f, markRushArrivalHitDelay);
    public float MarkRushInternalDelay => Mathf.Max(0f, markRushInternalDelay);
    public LightningSpearDashStabTrailEffect MarkRushTrailEffectPrefab => markRushTrailEffectPrefab;
    public LightningSpearHitConfig MarkRushHit => markRushHit;
    public LightningSpearHitConfig NoMarkSweepHit => noMarkSweepHit;

    public float MarkLifetimeSeconds => Mathf.Max(0.01f, markLifetimeSeconds);
    public int MarkRainCount => Mathf.Max(0, markRainCount);
    public float MarkRainDelay => Mathf.Max(0f, markRainDelay);
    public float FallbackCombatRadius => Mathf.Max(0.01f, fallbackCombatRadius);
    public float MinPlayerDistance => Mathf.Max(0f, minPlayerDistance);
    public float MinMarkSpacing => Mathf.Max(0f, minMarkSpacing);
    public float LandingProbeRadius => Mathf.Max(0f, landingProbeRadius);
    public int CandidateSamples => Mathf.Max(1, candidateSamples);
    public LightningSpearHitConfig LandingHit => landingHit;

    public LayerMask HardBlockLayers => hardBlockLayers;
    public LayerMask SoftBlockLayers => softBlockLayers;
    public LayerMask LandingBlockedLayers => landingBlockedLayers;
    public LayerMask RequiredGroundLayers => requiredGroundLayers;
    public int HardBlockMask => hardBlockLayers.value;
    public int SoftBlockMask => softBlockLayers.value;
    public int StrictRushBlockMask => hardBlockLayers.value | softBlockLayers.value;
    public int LandingBlockedMask =>
        landingBlockedLayers.value != 0
            ? landingBlockedLayers.value
            : hardBlockLayers.value | softBlockLayers.value;

    public GameObject RushRangeIndicatorPrefab => rushRangeIndicatorPrefab;
    public GameObject SelectedMarkIndicatorPrefab => selectedMarkIndicatorPrefab;

    public override System.Type ExpectedRuntimeDataType => typeof(LightningSpearRuntimeData);

    public override AbilityDefinition GetDefaultAbility(WeaponAbilitySlot slot) => slot switch
    {
        WeaponAbilitySlot.Attack => baseAttack,
        WeaponAbilitySlot.Skill1 => markRushOrSweep,
        WeaponAbilitySlot.Skill2 => markRain,
        _ => null
    };

    public override IEnumerable<AbilityDefinition> EnumerateGrantedAbilities()
    {
        HashSet<AbilityDefinition> yielded = new HashSet<AbilityDefinition>();

        if (baseAttack != null && yielded.Add(baseAttack))
            yield return baseAttack;

        if (markRushOrSweep != null && yielded.Add(markRushOrSweep))
            yield return markRushOrSweep;

        if (markRain != null && yielded.Add(markRain))
            yield return markRain;
    }

    protected override IEnumerable<string> EnumerateCustomValidationErrors()
    {
        if (SelectionStrategy is not LightningSpearSelectionStrategy)
            yield return "LightningSpearLoadout requires LightningSpearSelectionStrategy.";

        if (baseAttack == null)
            yield return "Base Attack reference is empty.";

        if (markRushOrSweep == null)
            yield return "Q / Skill1 Mark Rush Or Sweep reference is empty.";

        if (markRain == null)
            yield return "E / Skill2 Mark Rain reference is empty.";

        if (markPrefab == null)
            yield return "Mark Prefab reference is empty. E cannot spawn lightning spear marks.";
    }
}

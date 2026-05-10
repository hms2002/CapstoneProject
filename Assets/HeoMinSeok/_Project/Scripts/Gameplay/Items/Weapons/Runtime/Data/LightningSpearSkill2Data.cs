using UnityEngine;
using UnityGAS;

/// <summary>
/// 번개 창 E의 표식 비 생성 위치, 타이밍, 착지 피해 데이터를 보관할 책임을 가집니다.
/// </summary>
[CreateAssetMenu(fileName = "ALData_LightningSpearSkill2", menuName = "GAS/Weapon/Lightning Spear/Skill2 Data")]
public sealed class LightningSpearSkill2Data : ScriptableObject
{
    [Header("Animation")]
    [SerializeField] private string markRainAnimationTrigger;
    [SerializeField] private WeaponAimPresentationSettings markRainAimPresentation = new WeaponAimPresentationSettings();
    [SerializeField] private GameplayTag markRainSpawnEventTag;
    [SerializeField, Min(0f)] private float markRainSpawnEventTimeout = 0.5f;
    [SerializeField, Min(0f)] private float markRainFallbackSpawnDelay;

    [Header("Mark Authoring")]
    [SerializeField] private LightningSpearMarkActor markPrefab;

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

    public string MarkRainAnimationTrigger => markRainAnimationTrigger;
    public WeaponAimPresentationSettings MarkRainAimPresentation => markRainAimPresentation;
    public GameplayTag MarkRainSpawnEventTag => markRainSpawnEventTag;
    public float MarkRainSpawnEventTimeout => Mathf.Max(0f, markRainSpawnEventTimeout);
    public float MarkRainFallbackSpawnDelay => Mathf.Max(0f, markRainFallbackSpawnDelay);

    public LightningSpearMarkActor MarkPrefab => markPrefab;
    public float MarkLifetimeSeconds => Mathf.Max(0.01f, markLifetimeSeconds);
    public int MarkRainCount => Mathf.Max(0, markRainCount);
    public float MarkRainDelay => Mathf.Max(0f, markRainDelay);
    public float FallbackCombatRadius => Mathf.Max(0.01f, fallbackCombatRadius);
    public float MinPlayerDistance => Mathf.Max(0f, minPlayerDistance);
    public float MinMarkSpacing => Mathf.Max(0f, minMarkSpacing);
    public float LandingProbeRadius => Mathf.Max(0f, landingProbeRadius);
    public int CandidateSamples => Mathf.Max(1, candidateSamples);
    public LightningSpearHitConfig LandingHit => landingHit;
}

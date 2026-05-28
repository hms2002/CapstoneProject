using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 번개 창 Q의 표식 돌진과 표식 없음 sweep 실행 데이터를 보관할 책임을 가집니다.
/// </summary>
[CreateAssetMenu(fileName = "ALData_LightningSpearSkill1", menuName = "GAS/Weapon/Lightning Spear/Skill1 Data")]
public sealed class LightningSpearSkill1Data : ScriptableObject, IAbilityTooltipVariantProvider
{
    [Header("Animation")]
    [SerializeField] private string markRushAnimationTrigger;
    [SerializeField] private string noMarkSweepAnimationTrigger;
    [SerializeField] private WeaponAimPresentationSettings markRushAimPresentation = new WeaponAimPresentationSettings();
    [SerializeField] private WeaponAimPresentationSettings noMarkSweepAimPresentation = new WeaponAimPresentationSettings();
    [SerializeField] private GameplayTag noMarkSweepHitEventTag;
    [SerializeField, Min(0f)] private float noMarkSweepHitEventTimeout = 0.35f;
    [SerializeField, Min(0f)] private float noMarkSweepFallbackHitDelay;

    [Header("HUD")]
    [SerializeField] private Sprite markRushHudIcon;

    [Header("Q - Mark Rush")]
    [SerializeField, Min(0.01f)] private float cursorSelectRadius = 1.5f;
    [SerializeField, Min(0.01f)] private float markRushRange = 9f;
    [SerializeField, Min(0f)] private float markRushBodyRadius = 0.25f;
    [SerializeField, Min(0f)] private float markRushArrivalHitDelay = 0.05f;
    [SerializeField, Min(0f)] private float markRushInternalDelay = 0.15f;
    [SerializeField, Min(0f)] private float markRushInputBufferSeconds = 0.35f;
    [SerializeField] private LightningSpearDashStabTrailEffect markRushTrailEffectPrefab;
    [SerializeField] private LightningSpearHitConfig markRushHit = new LightningSpearHitConfig();

    [Header("Q - No Mark Sweep")]
    [SerializeField] private LightningSpearHitConfig noMarkSweepHit = new LightningSpearHitConfig();

    [Header("Q - Sound")]
    [SerializeField] private SoundRef noMarkSweepHitSound;
    [SerializeField] private SoundRef markRushStartSound;
    [SerializeField] private SoundRef markRushArrivalSound;
    [SerializeField] private SoundRef recoveredSpearSpawnSound;
    [SerializeField] private SoundRef recoveredSpearDespawnSound;
    [SerializeField] private SoundRef recoveredSpearShotSpawnSound;
    [SerializeField] private SoundRef recoveredSpearShotFireSound;

    [Header("Recovered Spears")]
    [SerializeField] private LightningSpearRecoveredSpearActor recoveredSpearPrefab;
    [SerializeField] private LightningSpearRecoveredSpearProjectile2D recoveredSpearProjectilePrefab;
    [SerializeField] private LightningSpearRecoverShotTrailEffect recoveredShotTrailEffectPrefab;
    [SerializeField, Min(0)] private int recoveredSpearMaxCount = 6;
    [SerializeField] private Vector2 recoveredSpearBaseOffset = new Vector2(0f, 1.2f);
    [SerializeField, Min(0f)] private float recoveredSpearSpacing = 0.18f;
    [SerializeField, Min(0f)] private float recoveredSpearStockAngleStep = 8f;
    [SerializeField, Min(0f)] private float recoveredSpearStockMaxFanAngle = 50f;
    [SerializeField, Min(0f)] private float recoveredSpearStockVisualForwardOffset = 0.45f;
    [SerializeField, Min(0f)] private float recoveredSpearMoveTweenSeconds = 0.12f;
    [SerializeField, Min(0f)] private float recoveredSpearFollowSmoothTime = 0.08f;
    [SerializeField, Min(0f)] private float recoveredSpearWarpSnapDistance = 3f;
    [SerializeField, Min(0f)] private float recoveredSpearBackOffset = 0.75f;
    [SerializeField, Min(0f)] private float recoveredSpearFloatAmplitude = 0.12f;
    [SerializeField, Min(0.01f)] private float recoveredSpearFloatDuration = 0.8f;
    [SerializeField, Min(0f)] private float recoveredSpearSpawnFallbackSeconds = 0.12f;
    [SerializeField, Min(0f)] private float recoveredSpearDespawnFallbackSeconds = 0.12f;

    [Header("Recovered Spear Projectile")]
    [SerializeField, Min(0f)] private float recoveredSpearShotReleaseDelay = 0.12f;
    [SerializeField] private float recoveredSpearShotPivotForwardOffset = 0.85f;
    [SerializeField, Min(0f)] private float recoveredSpearShotInnerRadius = 0.45f;
    [SerializeField] private GameObject recoveredSpearShotSpawnEffectPrefab;
    [SerializeField, Min(0f)] private float recoveredSpearShotSpawnEffectLifetimeFallback = 0.25f;
    [SerializeField, Min(0f)] private float recoveredShotSliceMaxDistance = 2.5f;
    [SerializeField, Min(0f)] private float recoveredSpearProjectileSpeed = 14f;
    [SerializeField, Min(0.01f)] private float recoveredSpearProjectileLifetime = 0.75f;
    [SerializeField, Min(0f)] private float recoveredSpearProjectileStuckLifetime = 0.35f;
    [SerializeField, Min(0f)] private float recoveredSpearProjectileSpawnFallbackSeconds;
    [SerializeField, Min(0f)] private float recoveredSpearProjectileDespawnFallbackSeconds = 0.12f;
    [SerializeField, Min(0f)] private float recoveredSpearAngleStep = 10f;
    [SerializeField, Min(0f)] private float recoveredSpearMaxFanAngle = 50f;
    [SerializeField, Min(0f)] private float recoveredSpearShotInterval = 0.04f;
    [SerializeField] private LightningSpearHitConfig recoveredSpearProjectileHit = new LightningSpearHitConfig();

    public string MarkRushAnimationTrigger => markRushAnimationTrigger;
    public string NoMarkSweepAnimationTrigger => noMarkSweepAnimationTrigger;
    public WeaponAimPresentationSettings MarkRushAimPresentation => markRushAimPresentation;
    public WeaponAimPresentationSettings NoMarkSweepAimPresentation => noMarkSweepAimPresentation;
    public GameplayTag NoMarkSweepHitEventTag => noMarkSweepHitEventTag;
    public float NoMarkSweepHitEventTimeout => Mathf.Max(0f, noMarkSweepHitEventTimeout);
    public float NoMarkSweepFallbackHitDelay => Mathf.Max(0f, noMarkSweepFallbackHitDelay);
    public Sprite MarkRushHudIcon => markRushHudIcon;

    public float CursorSelectRadius => Mathf.Max(0.01f, cursorSelectRadius);
    public float MarkRushRange => Mathf.Max(0.01f, markRushRange);
    public float MarkRushBodyRadius => Mathf.Max(0f, markRushBodyRadius);
    public float MarkRushArrivalHitDelay => Mathf.Max(0f, markRushArrivalHitDelay);
    public float MarkRushInternalDelay => Mathf.Max(0f, markRushInternalDelay);
    public float MarkRushInputBufferSeconds => Mathf.Max(0f, markRushInputBufferSeconds);
    public LightningSpearDashStabTrailEffect MarkRushTrailEffectPrefab => markRushTrailEffectPrefab;
    public LightningSpearHitConfig MarkRushHit => markRushHit;
    public LightningSpearHitConfig NoMarkSweepHit => noMarkSweepHit;
    public SoundRef NoMarkSweepHitSound => noMarkSweepHitSound;
    public SoundRef MarkRushStartSound => markRushStartSound;
    public SoundRef MarkRushArrivalSound => markRushArrivalSound;
    public SoundRef RecoveredSpearSpawnSound => recoveredSpearSpawnSound;
    public SoundRef RecoveredSpearDespawnSound => recoveredSpearDespawnSound;
    public SoundRef RecoveredSpearShotSpawnSound => recoveredSpearShotSpawnSound;
    public SoundRef RecoveredSpearShotFireSound => recoveredSpearShotFireSound;

    public LightningSpearRecoveredSpearActor RecoveredSpearPrefab => recoveredSpearPrefab;
    public LightningSpearRecoveredSpearProjectile2D RecoveredSpearProjectilePrefab => recoveredSpearProjectilePrefab;
    public LightningSpearRecoverShotTrailEffect RecoveredShotTrailEffectPrefab => recoveredShotTrailEffectPrefab;
    public int RecoveredSpearMaxCount => Mathf.Max(0, recoveredSpearMaxCount);
    public Vector2 RecoveredSpearBaseOffset => recoveredSpearBaseOffset;
    public float RecoveredSpearSpacing => Mathf.Max(0f, recoveredSpearSpacing);
    public float RecoveredSpearStockAngleStep => Mathf.Max(0f, recoveredSpearStockAngleStep);
    public float RecoveredSpearStockMaxFanAngle => Mathf.Max(0f, recoveredSpearStockMaxFanAngle);
    public float RecoveredSpearStockVisualForwardOffset => Mathf.Max(0f, recoveredSpearStockVisualForwardOffset);
    public float RecoveredSpearMoveTweenSeconds => Mathf.Max(0f, recoveredSpearMoveTweenSeconds);
    public float RecoveredSpearFollowSmoothTime => Mathf.Max(0f, recoveredSpearFollowSmoothTime);
    public float RecoveredSpearWarpSnapDistance => Mathf.Max(0f, recoveredSpearWarpSnapDistance);
    public float RecoveredSpearBackOffset => Mathf.Max(0f, recoveredSpearBackOffset);
    public float RecoveredSpearFloatAmplitude => Mathf.Max(0f, recoveredSpearFloatAmplitude);
    public float RecoveredSpearFloatDuration => Mathf.Max(0.01f, recoveredSpearFloatDuration);
    public float RecoveredSpearSpawnFallbackSeconds => Mathf.Max(0f, recoveredSpearSpawnFallbackSeconds);
    public float RecoveredSpearDespawnFallbackSeconds => Mathf.Max(0f, recoveredSpearDespawnFallbackSeconds);
    public float RecoveredSpearShotReleaseDelay => Mathf.Max(0f, recoveredSpearShotReleaseDelay);
    public float RecoveredSpearShotPivotForwardOffset => recoveredSpearShotPivotForwardOffset;
    public float RecoveredSpearShotInnerRadius => Mathf.Max(0f, recoveredSpearShotInnerRadius);
    public GameObject RecoveredSpearShotSpawnEffectPrefab => recoveredSpearShotSpawnEffectPrefab;
    public float RecoveredSpearShotSpawnEffectLifetimeFallback => Mathf.Max(0f, recoveredSpearShotSpawnEffectLifetimeFallback);
    public float RecoveredShotSliceMaxDistance => Mathf.Max(0f, recoveredShotSliceMaxDistance);
    public float RecoveredSpearProjectileSpeed => Mathf.Max(0f, recoveredSpearProjectileSpeed);
    public float RecoveredSpearProjectileLifetime => Mathf.Max(0.01f, recoveredSpearProjectileLifetime);
    public float RecoveredSpearProjectileStuckLifetime => Mathf.Max(0f, recoveredSpearProjectileStuckLifetime);
    public float RecoveredSpearProjectileSpawnFallbackSeconds => Mathf.Max(0f, recoveredSpearProjectileSpawnFallbackSeconds);
    public float RecoveredSpearProjectileDespawnFallbackSeconds => Mathf.Max(0f, recoveredSpearProjectileDespawnFallbackSeconds);
    public float RecoveredSpearAngleStep => Mathf.Max(0f, recoveredSpearAngleStep);
    public float RecoveredSpearMaxFanAngle => Mathf.Max(0f, recoveredSpearMaxFanAngle);
    public float RecoveredSpearShotInterval => Mathf.Max(0f, recoveredSpearShotInterval);
    public LightningSpearHitConfig RecoveredSpearProjectileHit => recoveredSpearProjectileHit;

    public int GetAbilityTooltipVariantCount(AbilityDefinition ability, ItemDetailContext ctx)
    {
        return 2;
    }

    public AbilityTooltipVariant BuildAbilityTooltipVariant(AbilityDefinition ability, int variantIndex, ItemDetailContext ctx)
    {
        int normalizedIndex = Mathf.Abs(variantIndex) % 2;
        return normalizedIndex == 0
            ? BuildNoMarkTooltipVariant(ability)
            : BuildMarkRushTooltipVariant(ability);
    }

    private AbilityTooltipVariant BuildNoMarkTooltipVariant(AbilityDefinition ability)
    {
        return new AbilityTooltipVariant(
            "NoMark",
            "뇌창 휩쓸기",
            ability != null ? ability.icon : null,
            "● {em:표식이 없을 때} 이동하지 않고 전방을 휩쓴다\n● 보유한 {val:회수 창}이 있으면 조준 방향 앞쪽에서 부채꼴로 순차 사출",
            ability != null ? (float?)ability.cooldown : null);
    }

    private AbilityTooltipVariant BuildMarkRushTooltipVariant(AbilityDefinition ability)
    {
        Sprite icon = markRushHudIcon != null || ability == null ? markRushHudIcon : ability.icon;
        return new AbilityTooltipVariant(
            "MarkRush",
            "뇌창 돌격",
            icon,
            "● 커서 주변의 [[낙뢰 표식]]으로 즉시 돌진\n● 이동 궤적과 도착 지점을 공격\n● 표식 소모 시 Skill1 쿨타임 초기화 및 {val:회수 창 1개} 획득",
            ability != null ? (float?)ability.cooldown : null);
    }
}

using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityEngine.Serialization;
using UnityGAS;

public class AbilityLogic_WitchSealedCandleRampage : AbilityLogic
{
    // 이 클래스의 책임:
    // 현재 봉인된 촛대들을 이용해 마녀 보스의 폭주 탄막 패턴을 실행하고 전용 튜닝 데이터를 제공한다.

    private const float FallbackTelegraphTileUnitSize = 1.7f;
    private const float FallbackTelegraphTileDepth = 3f;
    private const float FallbackDefaultTelegraphRadius = FallbackTelegraphTileUnitSize * FallbackTelegraphTileDepth;

    [Header("Burst")]
    [SerializeField] private float windupSeconds = 0.5f;
    [SerializeField] private int burstRepeatCount = 2;
    [SerializeField] private float burstIntervalSeconds = 0.45f;
    [SerializeField] private int projectileCountPerCandle = 5;
    [SerializeField] private float spreadAngleDegrees = 52f;
    [SerializeField] private GE_Damage_Spec projectileDamageEffect;
    [SerializeField] private float projectileDamageAmount = 1f;

    [Header("Telegraph")]
    [SerializeField] private float telegraphRadius = 0f;
    [SerializeField] private AttackTelegraphStyle telegraphStyle;
    [SerializeField] private WorldPresentationHook candleAttackPresentation;
    [HideInInspector, FormerlySerializedAs("candleAttackSound")]
    [SerializeField] private SoundRef legacyCandleAttackSound;

    private struct CandleBurstShotPlan
    {
        public Candlestick candle;
        public Vector3 origin;
        public Vector2 direction;
    }

    private void OnValidate()
    {
        MigrateLegacyAttackPresentation();
    }

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        MigrateLegacyAttackPresentation();

        Witch witch = system != null ? system.GetComponent<Witch>() : null;
        if (witch == null || !witch.HasProjectilePatternConfig || !HasProjectileDamageConfig())
            yield break;

        witch.PlayPatternAttackMotion();
        GameObject targetObject = initialTarget != null ? initialTarget : witch.CurrentTarget != null ? witch.CurrentTarget.gameObject : null;
        List<Candlestick> sealedCandles = new List<Candlestick>();
        List<CandleBurstShotPlan> burstPlans = new List<CandleBurstShotPlan>();
        List<AttackTelegraphView> warningViews = new List<AttackTelegraphView>();
        AttackTelegraphService telegraphService = witch.GetComponent<AttackTelegraphService>();

        witch.CollectSealedCandles(sealedCandles);
        if (sealedCandles.Count == 0)
            yield break;

        BuildBurstPlans(witch, sealedCandles, targetObject, burstPlans);
        if (burstPlans.Count == 0)
            yield break;

        float resolvedWindupSeconds = ResolveWindupSeconds();
        SpawnBurstWarnings(telegraphService, burstPlans, resolvedWindupSeconds, warningViews, ResolveTelegraphRadius());

        if (resolvedWindupSeconds > 0f)
            yield return new WaitForSeconds(resolvedWindupSeconds);

        DestroyWarningViews(warningViews);

        int resolvedBurstRepeatCount = ResolveBurstRepeatCount();
        float resolvedBurstIntervalSeconds = ResolveBurstIntervalSeconds();
        int resolvedProjectileCountPerCandle = ResolveProjectileCountPerCandle();
        float resolvedSpreadAngleDegrees = ResolveSpreadAngleDegrees();

        for (int burstIndex = 0; burstIndex < resolvedBurstRepeatCount; burstIndex++)
        {
            for (int i = 0; i < burstPlans.Count; i++)
            {
                CandleBurstShotPlan plan = burstPlans[i];
                if (plan.candle == null)
                    continue;

                WorldPresentationRuntime.PlayDeferredAsync(
                    candleAttackPresentation,
                    WorldPresentationContext.AtWorld(
                        instigator: witch.gameObject,
                        position: plan.origin,
                        fallbackDirection: plan.direction,
                        target: targetObject,
                        sourceObject: this,
                        rotation: Quaternion.LookRotation(Vector3.forward, plan.direction),
                        causer: plan.candle.gameObject));
                WitchProjectileAttackHelper.SpawnLightBeadBurst(
                    system,
                    witch.gameObject,
                    plan.candle.gameObject,
                    witch.LightBeadPrefab,
                    projectileDamageEffect,
                    projectileDamageAmount,
                    witch.ProjectileSpeed,
                    plan.origin,
                    plan.direction,
                    resolvedProjectileCountPerCandle,
                    resolvedSpreadAngleDegrees,
                    targetObject);
            }

            if (burstIndex < resolvedBurstRepeatCount - 1 && resolvedBurstIntervalSeconds > 0f)
                yield return new WaitForSeconds(resolvedBurstIntervalSeconds);
        }
    }

    private void BuildBurstPlans(
        Witch witch,
        List<Candlestick> sealedCandles,
        GameObject targetObject,
        List<CandleBurstShotPlan> buffer)
    {
        if (buffer == null)
            return;

        buffer.Clear();

        for (int i = 0; i < sealedCandles.Count; i++)
        {
            Candlestick candle = sealedCandles[i];
            if (candle == null)
                continue;

            Vector3 origin = witch.GetCandleCenter(candle);
            Vector2 direction = witch.GetDirectionToTargetOrFacing(targetObject != null ? targetObject.transform : null, origin);

            buffer.Add(new CandleBurstShotPlan
            {
                candle = candle,
                origin = origin,
                direction = direction
            });
        }
    }

    private void SpawnBurstWarnings(
        AttackTelegraphService telegraphService,
        List<CandleBurstShotPlan> burstPlans,
        float duration,
        List<AttackTelegraphView> warningViews,
        float radius)
    {
        DestroyWarningViews(warningViews);

        if (telegraphService == null || burstPlans == null || warningViews == null)
            return;

        for (int i = 0; i < burstPlans.Count; i++)
        {
            CandleBurstShotPlan plan = burstPlans[i];
            if (plan.candle == null)
                continue;

            float angle = Mathf.Atan2(plan.direction.y, plan.direction.x) * Mathf.Rad2Deg;
            AttackTelegraphSpec spec = AttackTelegraphSpec.CreateSector(
                plan.origin,
                radius,
                ResolveSpreadAngleDegrees(),
                angle,
                duration,
                telegraphStyle);

            AttackTelegraphView view = telegraphService.SpawnDetachedView(spec);
            if (view != null)
                warningViews.Add(view);
        }
    }

    private float ResolveTelegraphRadius()
    {
        if (telegraphRadius > 0f)
            return telegraphRadius;

        return FallbackDefaultTelegraphRadius;
    }

    /// <summary>봉인된 촛대 폭주의 경고 시간을 반환합니다.</summary>
    private float ResolveWindupSeconds()
    {
        return Mathf.Max(0f, windupSeconds);
    }

    /// <summary>봉인된 촛대 폭주의 연속 발사 횟수를 반환합니다.</summary>
    private int ResolveBurstRepeatCount()
    {
        return Mathf.Max(1, burstRepeatCount);
    }

    /// <summary>봉인된 촛대 폭주의 연속 발사 간격을 반환합니다.</summary>
    private float ResolveBurstIntervalSeconds()
    {
        return Mathf.Max(0f, burstIntervalSeconds);
    }

    /// <summary>촛대 하나당 발사할 투사체 수를 반환합니다.</summary>
    private int ResolveProjectileCountPerCandle()
    {
        return Mathf.Max(1, projectileCountPerCandle);
    }

    /// <summary>촛대 폭주의 부채꼴 발사 각도를 반환합니다.</summary>
    private float ResolveSpreadAngleDegrees()
    {
        return Mathf.Max(0f, spreadAngleDegrees);
    }

    /// <summary>촛대 폭주에 사용할 피해 효과가 준비되어 있는지 확인합니다.</summary>
    private bool HasProjectileDamageConfig()
    {
        return projectileDamageEffect != null;
    }

    private static void DestroyWarningViews(List<AttackTelegraphView> warningViews)
    {
        if (warningViews == null)
            return;

        for (int i = 0; i < warningViews.Count; i++)
        {
            AttackTelegraphView view = warningViews[i];
            if (view != null)
                Object.Destroy(view.gameObject);
        }

        warningViews.Clear();
    }

    private void MigrateLegacyAttackPresentation()
    {
        if (!candleAttackPresentation.HasSound && legacyCandleAttackSound.IsSet)
            candleAttackPresentation.sound = legacyCandleAttackSound;
    }
}

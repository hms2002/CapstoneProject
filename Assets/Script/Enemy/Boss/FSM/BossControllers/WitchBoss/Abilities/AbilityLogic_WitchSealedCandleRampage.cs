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
    // 현재 봉인된 촛대들을 이용해 마녀 보스의 폭주 탄막 패턴을 실행한다.

    private const float WindupSeconds = 0.5f;
    private const int BurstRepeatCount = 2;
    private const float BurstIntervalSeconds = 0.45f;
    private const int ProjectileCountPerCandle = 5;
    private const float SpreadAngleDegrees = 52f;
    private const float TelegraphTileUnitSize = 1.7f;
    private const float TelegraphTileDepth = 3f;
    private const float DefaultTelegraphRadius = TelegraphTileUnitSize * TelegraphTileDepth;

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
        if (witch == null || !witch.HasProjectilePatternConfig)
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

        SpawnBurstWarnings(telegraphService, burstPlans, WindupSeconds, warningViews, ResolveTelegraphRadius());

        if (WindupSeconds > 0f)
            yield return new WaitForSeconds(WindupSeconds);

        DestroyWarningViews(warningViews);

        for (int burstIndex = 0; burstIndex < BurstRepeatCount; burstIndex++)
        {
            for (int i = 0; i < burstPlans.Count; i++)
            {
                CandleBurstShotPlan plan = burstPlans[i];
                if (plan.candle == null)
                    continue;

                WorldPresentationRuntime.Play(
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
                    witch.ProjectileDamageEffect,
                    witch.ProjectileDamage,
                    witch.ProjectileSpeed,
                    plan.origin,
                    plan.direction,
                    ProjectileCountPerCandle,
                    SpreadAngleDegrees,
                    targetObject);
            }

            if (burstIndex < BurstRepeatCount - 1 && BurstIntervalSeconds > 0f)
                yield return new WaitForSeconds(BurstIntervalSeconds);
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
                SpreadAngleDegrees,
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

        return DefaultTelegraphRadius;
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public class AbilityLogic_WitchSealedCandleRampage : AbilityLogic
{
    // 이 클래스의 책임:
    // 현재 봉인된 촛대들을 이용해 마녀 보스의 폭주 탄막 패턴을 실행한다.

    private const float WindupSeconds = 0.25f;
    private const int BurstRepeatCount = 2;
    private const float BurstIntervalSeconds = 0.45f;
    private const int ProjectileCountPerCandle = 5;
    private const float SpreadAngleDegrees = 52f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        Witch witch = system != null ? system.GetComponent<Witch>() : null;
        if (witch == null || !witch.HasProjectilePatternConfig)
            yield break;

        witch.PlayPatternAttackMotion();
        yield return new WaitForSeconds(WindupSeconds);

        GameObject targetObject = initialTarget != null ? initialTarget : witch.CurrentTarget != null ? witch.CurrentTarget.gameObject : null;
        List<Candlestick> sealedCandles = new List<Candlestick>();

        witch.CollectSealedCandles(sealedCandles);
        if (sealedCandles.Count == 0)
            yield break;

        for (int burstIndex = 0; burstIndex < BurstRepeatCount; burstIndex++)
        {
            witch.CollectSealedCandles(sealedCandles);
            if (sealedCandles.Count == 0)
                yield break;

            for (int i = 0; i < sealedCandles.Count; i++)
            {
                Candlestick candle = sealedCandles[i];
                if (candle == null)
                    continue;

                Vector3 origin = witch.GetCandleCenter(candle);
                Vector2 direction = witch.GetDirectionToTargetOrFacing(targetObject != null ? targetObject.transform : null, origin);

                WitchProjectileAttackHelper.SpawnLightBeadBurst(
                    system,
                    witch.gameObject,
                    candle.gameObject,
                    witch.LightBeadPrefab,
                    witch.ProjectileDamageEffect,
                    witch.ProjectileDamage,
                    witch.ProjectileSpeed,
                    origin,
                    direction,
                    ProjectileCountPerCandle,
                    SpreadAngleDegrees,
                    targetObject);
            }

            sealedCandles.Clear();

            if (burstIndex < BurstRepeatCount - 1)
                yield return new WaitForSeconds(BurstIntervalSeconds);
        }
    }
}

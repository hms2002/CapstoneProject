using System.Collections.Generic;
using UnityEngine;

public sealed class WitchCandleService : MonoBehaviour
{
    // 이 클래스의 책임:
    // 마녀 보스가 사용하는 촛대의 조회, 중심 계산, 봉인 집계, 전장 반경 계산 같은 공용 유틸을 전담한다.

    private Witch owner;

    private void Awake()
    {
        owner = GetComponent<Witch>();
    }

    /// <summary>현재 보스 위치 기준으로 가장 가까운 미봉인 촛대를 반환합니다.</summary>
    public Candlestick GetNearestAvailableCandle()
    {
        Vector3 fallbackPosition = owner != null ? owner.transform.position : Vector3.zero;
        float bestDistance = float.MaxValue;
        Candlestick bestCandle = null;

        for (int i = 0; i < Candlestick.Instances.Count; i++)
        {
            Candlestick candle = Candlestick.Instances[i];
            if (candle == null || candle.IsSealed)
                continue;

            float sqrDistance = (GetCandleCenter(candle) - fallbackPosition).sqrMagnitude;
            if (sqrDistance >= bestDistance)
                continue;

            bestDistance = sqrDistance;
            bestCandle = candle;
        }

        return bestCandle;
    }

    /// <summary>촛대 중심 위치를 계산합니다.</summary>
    public Vector3 GetCandleCenter(Candlestick candle)
    {
        if (candle == null)
            return owner != null ? owner.transform.position : Vector3.zero;

        Collider2D candleCollider = candle.GetComponent<Collider2D>();
        if (candleCollider != null)
            return candleCollider.bounds.center;

        SpriteRenderer candleSprite = candle.GetComponent<SpriteRenderer>();
        if (candleSprite != null)
            return candleSprite.bounds.center;

        return candle.transform.position;
    }

    /// <summary>현재 봉인된 촛대 수를 반환합니다.</summary>
    public int GetSealedCandleCount()
    {
        int sealedCount = 0;

        for (int i = 0; i < Candlestick.Instances.Count; i++)
        {
            Candlestick candle = Candlestick.Instances[i];
            if (candle != null && candle.IsSealed)
                sealedCount++;
        }

        return sealedCount;
    }

    /// <summary>봉인된 촛대가 하나라도 있는지 확인합니다.</summary>
    public bool HasAnySealedCandles()
    {
        return GetSealedCandleCount() > 0;
    }

    /// <summary>모든 촛대를 봉인 상태로 만듭니다.</summary>
    public void SealAllCandles()
    {
        for (int i = 0; i < Candlestick.Instances.Count; i++)
        {
            Candlestick candle = Candlestick.Instances[i];
            if (candle == null || candle.IsSealed)
                continue;

            candle.Seal();
        }
    }

    /// <summary>현재 봉인된 촛대들을 외부 버퍼에 수집합니다.</summary>
    public void CollectSealedCandles(List<Candlestick> buffer)
    {
        if (buffer == null)
            return;

        buffer.Clear();

        for (int i = 0; i < Candlestick.Instances.Count; i++)
        {
            Candlestick candle = Candlestick.Instances[i];
            if (candle != null && candle.IsSealed)
                buffer.Add(candle);
        }
    }

    /// <summary>가장 가까운 미봉인 촛대 하나를 즉시 봉인합니다.</summary>
    public Candlestick SealNearestAvailableCandle()
    {
        Candlestick candle = GetNearestAvailableCandle();
        if (candle == null)
            return null;

        candle.Seal();
        return candle;
    }

    /// <summary>모든 촛대 위치의 평균 중심을 계산합니다.</summary>
    public Vector3 GetCandlesCenter()
    {
        Vector3 fallbackPosition = owner != null ? owner.transform.position : Vector3.zero;
        int candleCount = 0;
        Vector3 accumulatedCenter = Vector3.zero;

        for (int i = 0; i < Candlestick.Instances.Count; i++)
        {
            Candlestick candle = Candlestick.Instances[i];
            if (candle == null)
                continue;

            accumulatedCenter += GetCandleCenter(candle);
            candleCount++;
        }

        if (candleCount > 0)
            return accumulatedCenter / candleCount;

        return fallbackPosition;
    }

    /// <summary>촛대들을 모두 덮는 전장 반경을 계산합니다.</summary>
    public float GetArenaRadiusFromCandles(Vector3 center, float fallbackRadius)
    {
        float radius = 0f;

        for (int i = 0; i < Candlestick.Instances.Count; i++)
        {
            Candlestick candle = Candlestick.Instances[i];
            if (candle == null)
                continue;

            Vector3 candleCenter = GetCandleCenter(candle);
            float candleDistance = Vector2.Distance(center, candleCenter);
            float candleExtent = GetObjectExtentRadius(candle.gameObject);
            radius = Mathf.Max(radius, candleDistance + candleExtent);
        }

        return Mathf.Max(fallbackRadius, radius);
    }

    /// <summary>오브젝트의 시각 또는 충돌 반경을 계산합니다.</summary>
    private static float GetObjectExtentRadius(GameObject gameObject)
    {
        if (gameObject == null)
            return 0f;

        Collider2D collider = gameObject.GetComponent<Collider2D>();
        if (collider != null)
            return Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y);

        SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            return Mathf.Max(spriteRenderer.bounds.extents.x, spriteRenderer.bounds.extents.y);

        return 0f;
    }
}

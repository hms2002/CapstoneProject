using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D), typeof(CandlestickSeal))]
public class Candlestick : MonoBehaviour, IDamageReceiver
{
    private static readonly List<Candlestick> instances = new();

    private CandlestickSeal candlestickSeal;
    private SpriteHitFlashController hitFlash;
    private int defaultLayer;
    private int sealedLayer;

    public static IReadOnlyList<Candlestick> Instances => instances;
    public bool IsSealed => candlestickSeal != null && candlestickSeal.IsSealed;

    private void Awake()
    {
        candlestickSeal = GetComponent<CandlestickSeal>();
        hitFlash = GetComponent<SpriteHitFlashController>();
        defaultLayer = gameObject.layer;
        sealedLayer = GetSealedLayer();

        if (candlestickSeal != null)
            candlestickSeal.SealChanged += SyncHitLayer;

        SyncHitLayer(IsSealed);
    }

    private void OnDestroy()
    {
        if (candlestickSeal != null)
            candlestickSeal.SealChanged -= SyncHitLayer;
    }

    private void OnEnable()
    {
        if (!instances.Contains(this))
            instances.Add(this);
    }

    private void OnDisable()
    {
        instances.Remove(this);
    }

    /// <summary>촛대를 봉인 상태로 바꿉니다.</summary>
    public bool Seal()
    {
        if (candlestickSeal == null) return false;

        candlestickSeal.Seal();
        return true;
    }

    /// <summary>봉인 상태일 때만 타격을 처리합니다.</summary>
    public bool TryApplyDamage(DamageRequest request)
    {
        if (!IsSealed || candlestickSeal == null) return false;

        if (!candlestickSeal.UseHit()) return false;

        if (hitFlash != null)
            hitFlash.PlayFlash();

        return true;
    }

    /// <summary>봉인 상태에 맞게 피격 레이어를 맞춥니다.</summary>
    private void SyncHitLayer(bool isSealed)
    {
        gameObject.layer = isSealed ? sealedLayer : defaultLayer;
    }

    /// <summary>봉인 상태에서 사용할 레이어를 찾습니다.</summary>
    private int GetSealedLayer()
    {
        int layer = LayerMask.NameToLayer("TEMP_Enemy_LAYER");
        if (layer < 0) return defaultLayer;

        return layer;
    }
}

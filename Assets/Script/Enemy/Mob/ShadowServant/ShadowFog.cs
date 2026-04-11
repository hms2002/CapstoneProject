using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D), typeof(Animator))]
public class ShadowFog : MonoBehaviour
{
    private const float FogTime = 3f;
    private const string PlayerTag = "Player";

    private float dieTime;

    private void Awake()
    {
        SetupTrigger();
        dieTime = Time.time + FogTime;
    }

    private void Update()
    {
        if (Time.time < dieTime)
            return;

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTouch(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleTouch(other);
    }

    /// <summary>안개 충돌을 트리거로 맞춥니다.</summary>
    private void SetupTrigger()
    {
        Collider2D triggerZone = GetComponent<Collider2D>();
        triggerZone.isTrigger = true;
    }

    /// <summary>닿은 대상에 안개 효과를 적용합니다.</summary>
    private void HandleTouch(Collider2D other)
    {
        if (other == null)
            return;

        GameObject targetObject = GetTarget(other);
        if (targetObject == null)
            return;

        if (targetObject.CompareTag(PlayerTag))
        {
            ApplyFog(targetObject);
            return;
        }

        TrySeal(other);
    }

    /// <summary>플레이어 시야 제한 시간을 갱신합니다.</summary>
    private void ApplyFog(GameObject playerObject)
    {
        FogSightLock sightLock = playerObject.GetComponent<FogSightLock>();
        if (sightLock == null)
            sightLock = playerObject.AddComponent<FogSightLock>();

        sightLock.ApplyFog(FogTime);
    }

    /// <summary>촛대 봉인을 시도합니다.</summary>
    private void TrySeal(Collider2D other)
    {
        CandlestickSeal candlestickSeal = other.GetComponent<CandlestickSeal>();
        if (candlestickSeal == null)
            candlestickSeal = other.GetComponentInParent<CandlestickSeal>();

        if (candlestickSeal == null)
            return;

        candlestickSeal.Seal();
    }

    /// <summary>충돌한 루트 오브젝트를 구합니다.</summary>
    private GameObject GetTarget(Collider2D other)
    {
        if (other.attachedRigidbody != null)
            return other.attachedRigidbody.gameObject;

        return other.gameObject;
    }
}

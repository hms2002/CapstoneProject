using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public class CandlestickLightZone : MonoBehaviour
{
    // 이 클래스의 책임:
    // 촛불의 빛 판정 범위를 CircleCollider2D 기준으로 유지하고, 필요 시 인스펙터 값으로 반경을 제어한다.

    [SerializeField] private float lightRadius = 3f;

    private CircleCollider2D triggerZone;

    private void Awake()
    {
        SyncRadius();
    }

    private void OnValidate()
    {
        SyncRadius();
    }

    /// <summary>지정된 빛 반경에 맞춰 트리거 크기를 맞춥니다.</summary>
    private void SyncRadius()
    {
        if (triggerZone == null)
            triggerZone = GetComponent<CircleCollider2D>();

        if (triggerZone == null)
            return;

        triggerZone.isTrigger = true;
        triggerZone.radius = Mathf.Max(0.01f, lightRadius);
    }
}

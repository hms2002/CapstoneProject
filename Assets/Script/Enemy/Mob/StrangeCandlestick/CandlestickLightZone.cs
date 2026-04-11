using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light2D), typeof(CircleCollider2D))]
public class CandlestickLightZone : MonoBehaviour
{
    private Light2D pointLight;
    private CircleCollider2D triggerZone;

    private void Awake()
    {
        SyncRadius();
    }

    private void OnValidate()
    {
        SyncRadius();
    }

    /// <summary>광원 반경에 맞춰 트리거 크기를 맞춥니다.</summary>
    private void SyncRadius()
    {
        if (pointLight == null)
            pointLight = GetComponent<Light2D>();

        if (triggerZone == null)
            triggerZone = GetComponent<CircleCollider2D>();

        if (pointLight == null || triggerZone == null)
            return;

        triggerZone.isTrigger = true;
        triggerZone.radius = Mathf.Max(0.01f, pointLight.pointLightOuterRadius);
    }
}

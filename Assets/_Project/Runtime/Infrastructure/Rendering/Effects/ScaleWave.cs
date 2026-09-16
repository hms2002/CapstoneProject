using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 책임:
/// - 시작 시점의 Transform scale을 기준으로 사인파 스케일 펄스를 적용한다.
/// - Spot Light2D는 Transform scale 대신 안쪽/바깥쪽 반경을 함께 변경한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScaleWave : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float amplitude = 0.02f;

    private Vector3 initialScale;
    private Light2D spotLight;
    private float initialInnerRadius;
    private float initialOuterRadius;

    public float Speed
    {
        get => speed;
        set => speed = value;
    }

    public float Amplitude
    {
        get => amplitude;
        set => amplitude = value;
    }

    private void Awake()
    {
        initialScale = transform.localScale;
        if (TryGetComponent(out Light2D light) && light.lightType == Light2D.LightType.Point)
        {
            spotLight = light;
            initialInnerRadius = light.pointLightInnerRadius;
            initialOuterRadius = light.pointLightOuterRadius;
        }
    }

    private void Update()
    {
        float waveOffset = Mathf.Sin(Time.time * speed) * amplitude;
        if (spotLight != null)
        {
            float radiusScale = Mathf.Max(0f, 1f + waveOffset);
            spotLight.pointLightInnerRadius = initialInnerRadius * radiusScale;
            spotLight.pointLightOuterRadius = initialOuterRadius * radiusScale;
            return;
        }

        transform.localScale = initialScale + initialScale * waveOffset;
    }

    private void OnDisable()
    {
        if (spotLight == null)
            return;

        spotLight.pointLightInnerRadius = initialInnerRadius;
        spotLight.pointLightOuterRadius = initialOuterRadius;
    }
}

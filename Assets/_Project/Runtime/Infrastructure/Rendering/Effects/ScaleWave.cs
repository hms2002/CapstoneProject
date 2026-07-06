using UnityEngine;

/// <summary>
/// 책임:
/// - 시작 시점의 Transform scale을 기준으로 사인파 스케일 펄스를 적용한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScaleWave : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float amplitude = 0.02f;

    private Vector3 initialScale;

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
    }

    private void Update()
    {
        float waveOffset = Mathf.Sin(Time.time * speed) * amplitude;
        transform.localScale = initialScale + initialScale * waveOffset;
    }
}

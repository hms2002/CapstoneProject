using UnityEngine;

/// <summary>
/// 책임 :
/// - 검에 결속된 파편 조각 하나가 원래 local pose 주변에서 은은하게 떠다니는 표현을 담당한다.
/// - gameplay 상태는 다루지 않고, 활성화된 bound shard visual의 위치/회전/스케일 흔들림만 만든다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FragmentBladeBoundShardFloat : MonoBehaviour
{
    [Header("Position")]
    [SerializeField, Min(0f)] private float verticalAmplitude = 0.035f;
    [SerializeField, Min(0f)] private float horizontalAmplitude = 0.012f;
    [SerializeField, Min(0f)] private float frequency = 1.6f;
    [SerializeField] private float phaseOffset;

    [Header("Rotation")]
    [SerializeField, Min(0f)] private float rotationAmplitudeDegrees = 2.5f;
    [SerializeField, Min(0f)] private float rotationFrequencyMultiplier = 0.7f;

    [Header("Scale")]
    [SerializeField, Min(0f)] private float scalePulseAmplitude = 0.015f;
    [SerializeField, Min(0f)] private float scaleFrequencyMultiplier = 0.9f;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private Vector3 baseLocalScale;
    private bool basePoseCached;

    private void Awake()
    {
        CacheBasePose();
    }

    private void OnEnable()
    {
        CacheBasePose();
    }

    private void Update()
    {
        if (!basePoseCached)
            CacheBasePose();

        float time = Time.time + phaseOffset;
        float positionWave = Mathf.Sin(time * frequency);
        float horizontalWave = Mathf.Cos(time * frequency * 0.83f);
        float rotationWave = Mathf.Sin(time * frequency * rotationFrequencyMultiplier);
        float scaleWave = Mathf.Sin(time * frequency * scaleFrequencyMultiplier);

        transform.localPosition = baseLocalPosition + new Vector3(
            horizontalWave * horizontalAmplitude,
            positionWave * verticalAmplitude,
            0f);

        transform.localRotation = baseLocalRotation * Quaternion.Euler(
            0f,
            0f,
            rotationWave * rotationAmplitudeDegrees);

        float scalePulse = 1f + scaleWave * scalePulseAmplitude;
        transform.localScale = baseLocalScale * scalePulse;
    }

    /// <summary>
    /// 책임 :
    /// - authoring 된 조각의 기준 pose를 현재 위치로 다시 잡는다.
    /// - 프리팹 배치 후 손으로 위치를 조정했을 때 기준점이 꼬이지 않도록 editor/런타임에서 호출할 수 있다.
    /// </summary>
    public void RebindBasePose()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        baseLocalScale = transform.localScale;
        basePoseCached = true;
    }

    private void CacheBasePose()
    {
        if (basePoseCached)
            return;

        RebindBasePose();
    }
}

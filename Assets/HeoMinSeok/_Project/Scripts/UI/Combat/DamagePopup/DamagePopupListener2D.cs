using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : AttributeSet의 체력 감소 이벤트를 감지하고,
/// 실제 피해량이 발생했을 때 데미지 팝업 표시를 요청한다.
/// 팝업의 실제 생성 위치 계산은 담당하지만,
/// 어떤 Canvas / Camera에 그릴지는 DamagePopupService에 위임한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AttributeSet))]
public class DamagePopupListener2D : MonoBehaviour
{
    [Header("Damage Source")]
    [Tooltip("피해로 감소하는 Attribute (보통 Health).")]
    [SerializeField] private AttributeDefinition healthAttribute;

    [Header("Spawn")]
    [Tooltip("팝업 위치 기준(비우면 이 오브젝트 Transform).")]
    [SerializeField] private Transform worldAnchor;

    [Tooltip("팝업 생성 월드 오프셋 (서비스의 기본 offset에 추가 적용).")]
    [SerializeField] private Vector3 extraWorldOffset = Vector3.zero;

    [Header("Throttle")]
    [Tooltip("연타/다중히트로 팝업이 과도하게 생성되는 것을 막는 최소 간격(초).")]
    [SerializeField] private float minInterval = 0.02f;

    private AttributeSet _attributeSet;
    private float _nextAllowedTime;

    private void Awake()
    {
        _attributeSet = GetComponent<AttributeSet>();

        if (worldAnchor == null)
            worldAnchor = transform;
    }

    private void OnEnable()
    {
        if (_attributeSet != null)
            _attributeSet.OnAttributeChanged += OnAttributeChanged;
    }

    private void OnDisable()
    {
        if (_attributeSet != null)
            _attributeSet.OnAttributeChanged -= OnAttributeChanged;
    }

    /// <summary>
    /// 책임 : 체력 Attribute의 감소만 필터링하여 실제 피해량을 계산하고,
    /// 스로틀 조건을 통과한 경우 데미지 팝업 서비스에 표시를 요청한다.
    /// </summary>
    private void OnAttributeChanged(AttributeDefinition attr, float oldValue, float newValue)
    {
        if (healthAttribute == null)
            return;

        if (attr != healthAttribute)
            return;

        // 감소만 처리
        if (newValue >= oldValue)
            return;

        if (Time.time < _nextAllowedTime)
            return;

        _nextAllowedTime = Time.time + Mathf.Max(0f, minInterval);

        float dmg = oldValue - newValue;
        if (dmg <= 0f)
            return;

        Vector3 pos = (worldAnchor != null ? worldAnchor.position : transform.position) + extraWorldOffset;
        DamagePopupService.Show(dmg, pos);
    }
}
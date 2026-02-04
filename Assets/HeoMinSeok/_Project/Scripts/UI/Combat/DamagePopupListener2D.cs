using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(AttributeSet))]
public class DamagePopupListener2D : MonoBehaviour
{
    [Header("Damage Source")]
    [Tooltip("피해로 감소하는 Attribute (보통 Health).")]
    [SerializeField] private AttributeDefinition healthAttribute;

    [Tooltip("DamagePopupSpawner2D 참조 (비우면 씬에서 자동 탐색).")]
    [SerializeField] private DamagePopupSpawner2D popupSpawner;

    [Header("Spawn")]
    [Tooltip("팝업 위치 기준(비우면 이 오브젝트 Transform).")]
    [SerializeField] private Transform worldAnchor;

    [Tooltip("팝업 생성 월드 오프셋 (Spawner의 worldOffset과 별개로 추가 적용).")]
    [SerializeField] private Vector3 extraWorldOffset = Vector3.zero;

    [Header("Throttle")]
    [Tooltip("연타/다중히트로 팝업이 과도하게 생성되는 것을 막는 최소 간격(초).")]
    [SerializeField] private float minInterval = 0.02f;

    private AttributeSet _attributeSet;
    private float _nextAllowedTime;

    private void Awake()
    {
        _attributeSet = GetComponent<AttributeSet>();
        if (worldAnchor == null) worldAnchor = transform;

        if (popupSpawner == null)
        {
            // 1) 자신/자식에서 찾기
            popupSpawner = GetComponentInChildren<DamagePopupSpawner2D>(true);
        }

        if (popupSpawner == null)
        {
            // 2) 씬에서 하나 찾기 (Canvas에 하나만 두는 경우)
#if UNITY_2023_1_OR_NEWER
            popupSpawner = FindFirstObjectByType<DamagePopupSpawner2D>(FindObjectsInactive.Include);
#else
            popupSpawner = FindObjectOfType<DamagePopupSpawner2D>(true);
#endif
        }
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

    private void OnAttributeChanged(AttributeDefinition attr, float oldValue, float newValue)
    {
        if (healthAttribute == null) return;
        if (attr != healthAttribute) return;

        if (newValue >= oldValue) return; // 감소만

        if (Time.time < _nextAllowedTime) return;
        _nextAllowedTime = Time.time + Mathf.Max(0f, minInterval);

        float dmg = oldValue - newValue;
        if (dmg <= 0f) return;

        if (popupSpawner == null) return;

        Vector3 pos = (worldAnchor != null ? worldAnchor.position : transform.position) + extraWorldOffset;
        popupSpawner.Spawn(dmg, pos);
    }
}

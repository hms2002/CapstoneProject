using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
public class MonsterInitializer : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] private MonsterDefinition definition;
    [SerializeField] private bool isElite = false;

    [Header("Attributes")]
    [SerializeField] private AttributeDefinition healthAttribute;
    [SerializeField] private AttributeDefinition maxHealthAttribute;

    [Header("Policy")]
    [SerializeField] private bool refillToFullOnInit = true;  // 기본: 스폰 시 풀피
    [SerializeField] private bool preserveHpRatioIfNotFull = false; // (옵션) max가 바뀔 때 비율 유지

    private AttributeSet _attributeSet;

    private void Awake()
    {
        _attributeSet = GetComponent<AttributeSet>();
    }

    private void Start()
    {
        InitializeVitals();
    }

    private void OnEnable()
    {
        // 풀링 사용 시 OnEnable에서 다시 초기화하는 게 안전
        InitializeVitals();
    }

    public void InitializeVitals()
    {
        if (_attributeSet == null) _attributeSet = GetComponent<AttributeSet>();
        if (_attributeSet == null) return;
        if (definition == null || healthAttribute == null || maxHealthAttribute == null) return;

        var maxAttr = _attributeSet.GetAttribute(maxHealthAttribute);
        var hpAttr = _attributeSet.GetAttribute(healthAttribute);
        if (maxAttr == null || hpAttr == null) return;

        float oldMax = maxAttr.CurrentValue;
        float oldHp = hpAttr.CurrentValue;

        float newMax = definition.GetMaxHealth(isElite);

        // 1) MaxHealth 설정
        maxAttr.SetBaseValue(newMax);
        maxAttr.ForceRecalculate();

        // 2) Health 설정
        float targetHp;

        if (refillToFullOnInit)
        {
            targetHp = newMax;
        }
        else if (preserveHpRatioIfNotFull && oldMax > 0.0001f)
        {
            float ratio = Mathf.Clamp01(oldHp / oldMax);
            targetHp = ratio * newMax;
        }
        else
        {
            // 그냥 clamp만 되게 두고 싶다면 oldHp 유지
            targetHp = Mathf.Min(oldHp, newMax);
        }

        hpAttr.SetBaseValue(targetHp);
        hpAttr.ForceRecalculate();
    }
}

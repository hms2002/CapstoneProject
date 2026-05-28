using UnityEngine;
using UnityGAS;

/// <summary>
/// [책임]
/// - 스포너가 전달한 난이도 배율을 몬스터의 AttributeSet에 반영한다.
/// - 현재 AttributeSet의 "원본 BaseValue"를 기준으로 재계산하여,
///   ApplyDifficulty가 여러 번 호출되어도 배율이 중첩 폭주하지 않도록 한다.
/// - HP/공격력처럼 난이도 보정이 필요한 속성만 선택적으로 조정한다.
///
/// [의도]
/// - MonsterSpawner는 "어떤 배율을 적용할지"만 결정한다.
/// - 실제 Attribute 반영은 이 Receiver가 담당한다.
/// - 스폰 규칙과 스탯 변경 책임을 분리하기 위한 브리지 컴포넌트다.
///
/// [주의]
/// - 이 구현은 업로드된 현재 코드 기준 예시다.
/// - 프로젝트의 실제 공격력 AttributeDefinition, MaxHealth 구조에 맞게
///   attackAttribute / maxHealthAttribute / healthAttribute를 인스펙터에서 연결해야 한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AttributeSet))]
public class MonsterDifficultyReceiver : MonoBehaviour, IMonsterDifficultyReceiver
{
    [Header("Target Attributes")]
    [SerializeField] private AttributeDefinition healthAttribute;
    [SerializeField] private AttributeDefinition maxHealthAttribute;
    [SerializeField] private AttributeDefinition attackAttribute;
    [SerializeField] private AttributeDefinition attackSpeedBaseAttribute;

    [Header("Policy")]
    [SerializeField] private bool scaleCurrentHealthWithMaxHealth = true;
    [SerializeField] private bool refillHealthToFullAfterScaling = false;

    private AttributeSet attributeSet;
    private AbilitySystem abilitySystem;

    private float baseMaxHealth;
    private float baseAttack;
    private float baseAttackSpeed = 1f;
    private bool cachedBaseValues;

    private void Awake()
    {
        attributeSet = GetComponent<AttributeSet>();
        abilitySystem = GetComponent<AbilitySystem>();
        ResolveStatBindingAttributes();
        CacheBaseValues();
    }

    /// <summary>
    /// [책임]
    /// - 난이도 배율을 현재 몬스터 AttributeSet에 적용한다.
    /// - 원본 BaseValue 기준으로 다시 계산하므로 중복 호출에 비교적 안전하다.
    /// </summary>
    public void ApplyDifficulty(DifficultyModifiers modifiers)
    {
        if (modifiers == null)
            return;

        if (attributeSet == null)
            attributeSet = GetComponent<AttributeSet>();

        if (attributeSet == null)
            return;

        if (abilitySystem == null)
            abilitySystem = GetComponent<AbilitySystem>();

        ResolveStatBindingAttributes();

        if (!cachedBaseValues)
            CacheBaseValues();

        ApplyMaxHealth(modifiers);
        ApplyAttack(modifiers);
        ApplyAttackSpeed(modifiers);
        LogDifficultyApplied(modifiers);
    }

    /// <summary>
    /// [책임]
    /// - 난이도 적용 전 기준 BaseValue를 캐시한다.
    /// - 이후 ApplyDifficulty는 이 기준값을 바탕으로 재계산한다.
    /// </summary>
    private void CacheBaseValues()
    {
        if (attributeSet == null)
            return;

        if (maxHealthAttribute != null &&
            attributeSet.TryGetReadOnly(maxHealthAttribute, out var maxHpReadOnly))
        {
            baseMaxHealth = maxHpReadOnly.BaseValue;
        }

        if (attackAttribute != null &&
            attributeSet.TryGetReadOnly(attackAttribute, out var attackReadOnly))
        {
            baseAttack = attackReadOnly.BaseValue;
        }

        if (attackSpeedBaseAttribute != null &&
            attributeSet.TryGetReadOnly(attackSpeedBaseAttribute, out var attackSpeedReadOnly))
        {
            baseAttackSpeed = attackSpeedReadOnly.BaseValue > 0.0001f
                ? attackSpeedReadOnly.BaseValue
                : 1f;
        }

        cachedBaseValues = true;
    }

    /// <summary>
    /// [책임]
    /// - MaxHealth를 난이도 배율에 맞게 재설정한다.
    /// - 옵션에 따라 현재 HP를 풀피로 채우거나, 비율을 유지한다.
    /// </summary>
    private void ApplyMaxHealth(DifficultyModifiers modifiers)
    {
        if (maxHealthAttribute == null || attributeSet == null)
            return;

        float hpMultiplier = Mathf.Max(0f, modifiers.hpMultiplier);
        float oldMax = attributeSet.GetAttributeValue(maxHealthAttribute);

        float newMax = baseMaxHealth * hpMultiplier;
        attributeSet.TrySetBaseValue(maxHealthAttribute, newMax, this);

        if (healthAttribute == null)
            return;

        float oldHealth = attributeSet.GetAttributeValue(healthAttribute);

        if (refillHealthToFullAfterScaling)
        {
            attributeSet.TrySetBaseValue(healthAttribute, newMax, this);
            return;
        }

        if (scaleCurrentHealthWithMaxHealth)
        {
            float ratio = oldMax > 0.0001f ? oldHealth / oldMax : 1f;
            float newHealth = newMax * Mathf.Clamp01(ratio);
            attributeSet.TrySetBaseValue(healthAttribute, newHealth, this);
        }
        else
        {
            // Current HP를 그대로 두되, Max 변경으로 인해 clamp는 AttributeSet 재계산에 맡긴다.
            // 필요 시 여기서 명시적으로 Clamp를 넣어도 된다.
        }
    }

    /// <summary>
    /// [책임]
    /// - 공격력을 난이도 배율에 맞게 재설정한다.
    /// </summary>
    private void ApplyAttack(DifficultyModifiers modifiers)
    {
        if (attackAttribute == null || attributeSet == null)
            return;

        float attackMultiplier = Mathf.Max(0f, modifiers.attackMultiplier);
        float newAttack = baseAttack * attackMultiplier;
        attributeSet.TrySetBaseValue(attackAttribute, newAttack, this);
    }

    /// <summary>
    /// [책임]
    /// - 공격속도 Base Attribute를 난이도 배율에 맞게 재설정한다.
    /// - CombatTimingService가 AttackSpeedFinal을 읽어 실제 전투 delay를 줄이는 기반 값을 제공한다.
    /// </summary>
    private void ApplyAttackSpeed(DifficultyModifiers modifiers)
    {
        if (attackSpeedBaseAttribute == null || attributeSet == null)
            return;

        float attackSpeedMultiplier = Mathf.Max(0f, modifiers.attackSpeedMultiplier);
        float newAttackSpeed = baseAttackSpeed * attackSpeedMultiplier;
        attributeSet.TrySetBaseValue(attackSpeedBaseAttribute, newAttackSpeed, this);
    }

    /// <summary>
    /// [책임]
    /// - CombatTimingService 테스트 중 몬스터가 받은 난이도/공격속도 보정값을 설정 토글 기반으로 출력한다.
    /// - 실제 스탯 적용 로직과 분리해 기본 플레이에서는 로그 비용을 만들지 않는다.
    /// </summary>
    private void LogDifficultyApplied(DifficultyModifiers modifiers)
    {
        MonsterStageHpScalingSettings settings = Resources.Load<MonsterStageHpScalingSettings>("MonsterStageHpScalingSettings");
        if (settings == null || !settings.LogStageScalingDebug)
            return;

        float currentAttackSpeed = attackSpeedBaseAttribute != null && attributeSet != null
            ? attributeSet.GetAttributeValue(attackSpeedBaseAttribute)
            : 0f;

        Debug.Log(
            $"[MonsterDifficultyReceiver] target={name}, hpMultiplier={modifiers.hpMultiplier:0.###}, attackMultiplier={modifiers.attackMultiplier:0.###}, attackSpeedMultiplier={modifiers.attackSpeedMultiplier:0.###}, attackSpeedBase={baseAttackSpeed:0.###}->{currentAttackSpeed:0.###}",
            this);
    }

    /// <summary>
    /// [책임]
    /// - 인스펙터에 직접 연결되지 않은 난이도 보정 대상 Attribute를 DamageProfile의 StatTypeBindings에서 보완한다.
    /// - 기존 프리팹을 모두 다시 authoring하지 않아도 HP/공격력/공격속도 보정이 동작하게 한다.
    /// </summary>
    private void ResolveStatBindingAttributes()
    {
        StatTypeBindings bindings = abilitySystem != null && abilitySystem.DamageProfile != null
            ? abilitySystem.DamageProfile.GetStatBindings()
            : null;

        ResolveBindingAttribute(ref healthAttribute, bindings, StatId.Health, "Health", "HealthAttribute");
        ResolveBindingAttribute(ref maxHealthAttribute, bindings, StatId.MaxHealth, "MaxHealth", "MaxHealthAttribute");
        ResolveBindingAttribute(ref attackAttribute, bindings, StatId.AttackBase, "AttackBase", "AttackBaseAttribute", "Attack");
        ResolveBindingAttribute(ref attackSpeedBaseAttribute, bindings, StatId.AttackSpeedBase, "AttackSpeedBase", "AttackSpeedBaseAttribute");
    }

    /// <summary>
    /// 책임:
    /// - StatTypeBindings와 AttributeSet 보유 정의를 순서대로 확인해 누락된 AttributeDefinition 참조를 채운다.
    /// - 자동 추가된 MonsterDifficultyReceiver가 프리팹별 수동 직렬화 없이도 핵심 스탯을 찾게 한다.
    /// </summary>
    private void ResolveBindingAttribute(
        ref AttributeDefinition target,
        StatTypeBindings bindings,
        StatId statId,
        params string[] fallbackNames)
    {
        if (target != null)
            return;

        if (bindings != null &&
            bindings.TryGetBinding(statId, out StatTypeBindings.Binding binding) &&
            binding != null &&
            binding.attribute != null)
        {
            target = binding.attribute;
            return;
        }

        if (attributeSet == null || fallbackNames == null || fallbackNames.Length == 0)
            return;

        foreach (AttributeDefinition definition in attributeSet.EnumerateDefinitions())
        {
            if (definition == null)
                continue;

            for (int i = 0; i < fallbackNames.Length; i++)
            {
                string fallbackName = fallbackNames[i];
                if (string.IsNullOrWhiteSpace(fallbackName))
                    continue;

                if (string.Equals(definition.name, fallbackName, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(definition.attributeName, fallbackName, System.StringComparison.OrdinalIgnoreCase))
                {
                    target = definition;
                    return;
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (refillHealthToFullAfterScaling && !scaleCurrentHealthWithMaxHealth)
        {
            // 풀피 회복이 우선이라면 ratio 유지 옵션은 사실상 의미가 없다.
        }
    }
#endif
}

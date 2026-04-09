using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 현재 체력 값에 따라 이동속도 보너스를 실시간으로 바꾸는 유물 로직이다.
/// 일반 장착과 복원 장착 모두 앞으로의 체력 변화에 반응할 runtime hook이 필요하다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Move Speed By Current Health (Managed)")]
public class RelicLogic_MoveSpeedByCurrentHealth_Managed : RelicLogic
{
    [Serializable]
    public struct Rule
    {
        [Min(0f)] public float minHealthInclusive;
        [Min(0f)] public float maxHealthInclusive;

        [Tooltip("레벨 1 기준 Percent modifier 값. 0.15 = +15%, -0.10 = -10%")]
        public float percentBonus;

        [Tooltip("레벨별 값 테이블(레벨1=0번째). 비어있으면 percentBonus * level 로 계산합니다.")]
        public List<float> percentBonusByLevel;
    }

    [Header("Watch")]
    [Tooltip("현재 체력을 읽는 AttributeDefinition. 보통 HealthAttribute.")]
    public AttributeDefinition healthAttribute;

    [Header("Apply To")]
    [Tooltip("이동속도 배율 AttributeDefinition. 보통 MoveSpeedMulAttribute.")]
    public AttributeDefinition moveSpeedAttribute;

    [Header("Rules")]
    [Tooltip("현재 체력 값이 범위 안에 들어오면 해당 Percent modifier를 적용합니다. 먼저 매칭된 규칙 하나만 사용합니다.")]
    public List<Rule> rules = new List<Rule>();

    public override void OnEquipped(RelicContext ctx)
    {
        RegisterProc(ctx);
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;

        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) return;

        mgr.UnregisterAll(ctx.token);
    }

    public override void OnRestoreAttached(RelicContext ctx)
    {
        RegisterProc(ctx);
    }

    private void RegisterProc(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;
        if (ctx.attributeSet == null) return;
        if (healthAttribute == null) return;
        if (moveSpeedAttribute == null) return;
        if (rules == null || rules.Count == 0) return;

        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) mgr = ctx.owner.AddComponent<RelicProcManager>();

        mgr.Register(new MoveSpeedByCurrentHealthProc(ctx, healthAttribute, moveSpeedAttribute, rules));
    }

    public float EvaluatePercentBonus(int level, float currentHealth)
    {
        if (rules == null) return 0f;

        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (currentHealth < rule.minHealthInclusive) continue;
            if (currentHealth > rule.maxHealthInclusive) continue;
            return EvalValue(rule, level);
        }

        return 0f;
    }

    private static float EvalValue(Rule rule, int level)
    {
        return RelicTooltipFormatter.EvaluateLeveledValue(rule.percentBonus, rule.percentBonusByLevel, level);
    }

    private sealed class MoveSpeedByCurrentHealthProc : IRelicProc
    {
        public UnityEngine.Object Token { get; }

        private readonly RelicContext _ctx;
        private readonly AttributeDefinition _healthAttribute;
        private readonly AttributeDefinition _moveSpeedAttribute;
        private readonly List<Rule> _rules;
        private AttributeModifier _currentMoveSpeedMod;

        public MoveSpeedByCurrentHealthProc(
            RelicContext ctx,
            AttributeDefinition healthAttribute,
            AttributeDefinition moveSpeedAttribute,
            List<Rule> rules)
        {
            _ctx = ctx;
            Token = ctx.token;
            _healthAttribute = healthAttribute;
            _moveSpeedAttribute = moveSpeedAttribute;
            _rules = rules;

            _ctx.attributeSet.OnAttributeChanged += OnAttributeChanged;
            RecomputeAndApply();
        }

        public void Handle(GameplayTag tag, AbilityEventData data)
        {
        }

        public void Dispose()
        {
            if (_ctx.attributeSet != null)
                _ctx.attributeSet.OnAttributeChanged -= OnAttributeChanged;

            RemoveCurrentModifier();
        }

        private void OnAttributeChanged(AttributeDefinition def, float oldValue, float newValue)
        {
            if (def != _healthAttribute) return;
            RecomputeAndApply();
        }

        private void RecomputeAndApply()
        {
            if (_ctx.attributeSet == null) return;

            float currentHealth = _ctx.attributeSet.GetAttributeValue(_healthAttribute);
            float percentBonus = 0f;

            int level = _ctx.level > 0 ? _ctx.level : 1;
            for (int i = 0; i < _rules.Count; i++)
            {
                var rule = _rules[i];
                if (currentHealth < rule.minHealthInclusive) continue;
                if (currentHealth > rule.maxHealthInclusive) continue;

                percentBonus = EvalValue(rule, level);
                break;
            }

            ApplyMoveSpeedModifier(percentBonus);
        }

        private void ApplyMoveSpeedModifier(float percentBonus)
        {
            RemoveCurrentModifier();

            if (_ctx.attributeSet == null || _moveSpeedAttribute == null) return;
            if (Mathf.Abs(percentBonus) <= 0.000001f) return;

            _currentMoveSpeedMod = new AttributeModifier(
                ModifierType.Percent,
                percentBonus,
                Token,
                duration: 0f);

            _ctx.attributeSet.TryAddModifier(_moveSpeedAttribute, _currentMoveSpeedMod);
        }

        private void RemoveCurrentModifier()
        {
            if (_ctx.attributeSet == null || _moveSpeedAttribute == null) return;
            _ctx.attributeSet.RemoveModifiersFromSource(Token);
            _currentMoveSpeedMod = default;
        }
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        var sb = new StringBuilder();

        if (rules == null || rules.Count == 0)
        {
            sb.AppendLine("(체력 구간 규칙 없음)");
        }
        else
        {
            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                string rangeText;
                bool hasLower = rule.minHealthInclusive > 0f;
                bool hasUpper = !float.IsInfinity(rule.maxHealthInclusive) && rule.maxHealthInclusive < 999999f;

                if (Mathf.Approximately(rule.minHealthInclusive, rule.maxHealthInclusive))
                    rangeText = $"현재 체력이 {rule.minHealthInclusive:0.##}";
                else if (hasLower && hasUpper)
                    rangeText = $"현재 체력이 {rule.minHealthInclusive:0.##}~{rule.maxHealthInclusive:0.##}";
                else if (hasLower)
                    rangeText = $"현재 체력이 {rule.minHealthInclusive:0.##} 이상";
                else
                    rangeText = $"현재 체력이 {rule.maxHealthInclusive:0.##} 이하";

                string bonus = RelicTooltipFormatter.FormatSignedValueToken(EvalValue(rule, previewLevel), true);
                sb.AppendLine($"● {rangeText}: [[이동속도]] {bonus}");
            }
        }

        return new RelicTooltipData
        {
            effectText = sb.ToString().TrimEnd()
        };
    }
}

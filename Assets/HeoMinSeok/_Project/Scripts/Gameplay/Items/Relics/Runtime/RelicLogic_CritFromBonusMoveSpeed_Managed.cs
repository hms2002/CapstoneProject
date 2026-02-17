using UnityEngine;
using UnityGAS;

/// <summary>
/// [추가 이동 속도] 10%마다 치명타 확률 +1%
/// - 추가 이동 속도 = max(0, MoveSpeedFinal - 1)  (MoveSpeedFinal은 x1 배수: 1.10 = +10%)
/// - 보너스 치확 = floor(추가이속 / 0.10) * 0.01
/// - AttributeSet.OnAttributeChanged를 구독해 실시간으로 갱신.
/// - RelicProcManager를 이용해 Unequip 시 Dispose 호출되게 관리.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Crit From Bonus MoveSpeed (Managed)")]
public class RelicLogic_CritFromBonusMoveSpeed_Managed : RelicLogic
{
    [Header("Read MoveSpeed (x1 multiplier)")]
    [Tooltip("권장: MoveSpeedFinal (x1). StatTypeBindings의 Composite(Final=(Base+Add)*Mul)를 활용합니다.")]
    public StatId moveSpeedFinalStatId = StatId.MoveSpeedFinal;

    [Tooltip("StatTypeBindings가 없거나 특별히 직접 읽고 싶을 때 사용하는 fallback. (x1 배수 Attribute)")]
    public AttributeDefinition moveSpeedMultiplierAttributeFallback;

    [Header("Apply To")]
    [Tooltip("치명타 확률 AttributeDefinition (0~1)")]
    public AttributeDefinition critChanceAttribute;

    [Header("Tuning")]
    [Tooltip("추가 이동속도 몇 %마다 보너스를 줄지. 0.10 = 10%")]
    public float bonusMoveStep = 0.10f;

    [Tooltip("스텝 1개당 치확 증가량. 0.01 = +1%p")]
    public float critPerStep = 0.01f;

    public override void OnEquipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;
        if (ctx.attributeSet == null) return;
        if (critChanceAttribute == null) return;

        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) mgr = ctx.owner.AddComponent<RelicProcManager>();

        var proc = new CritFromBonusMoveSpeedProc(
            ctx,
            moveSpeedFinalStatId,
            moveSpeedMultiplierAttributeFallback,
            critChanceAttribute,
            Mathf.Max(0.0001f, bonusMoveStep),
            critPerStep
        );

        mgr.Register(proc);
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;
        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) return;

        mgr.UnregisterAll(ctx.token);
    }

    private sealed class CritFromBonusMoveSpeedProc : IRelicProc
    {
        public Object Token { get; }

        private readonly RelicContext _ctx;
        private readonly StatId _moveSpeedFinalStatId;
        private readonly AttributeDefinition _moveSpeedFallback;
        private readonly AttributeDefinition _critChance;

        private readonly float _step;
        private readonly float _critPerStep;

        private AttributeModifier _currentCritMod;

        // MoveSpeed에 영향을 주는 Attribute들만 골라서 반응(불필요한 재계산 줄이기)
        private AttributeDefinition _watchA;
        private AttributeDefinition _watchB;
        private AttributeDefinition _watchC;
        private AttributeDefinition _watchSingle;

        public CritFromBonusMoveSpeedProc(
            RelicContext ctx,
            StatId moveSpeedFinalStatId,
            AttributeDefinition moveSpeedFallback,
            AttributeDefinition critChance,
            float step,
            float critPerStep)
        {
            _ctx = ctx;
            Token = ctx.token;
            _moveSpeedFinalStatId = moveSpeedFinalStatId;
            _moveSpeedFallback = moveSpeedFallback;
            _critChance = critChance;
            _step = step;
            _critPerStep = critPerStep;

            BuildWatchList();
            _ctx.attributeSet.OnAttributeChanged += OnAttributeChanged;

            RecomputeAndApply();
        }

        public void Handle(GameplayTag tag, AbilityEventData data)
        {
            // 이 유물은 GameplayEvent 기반이 아니라 Attribute 변화 기반이라 비워둡니다.
        }

        public void Dispose()
        {
            if (_ctx.attributeSet != null)
                _ctx.attributeSet.OnAttributeChanged -= OnAttributeChanged;

            // 남아있는 치확 보너스 제거
            RemoveCurrentModifier();
        }

        private void OnAttributeChanged(AttributeDefinition def, float oldValue, float newValue)
        {
            if (def == null) return;

            // 관련 Attribute 변화일 때만 갱신
            if (_watchSingle != null)
            {
                if (def != _watchSingle) return;
            }
            else
            {
                if (def != _watchA && def != _watchB && def != _watchC) return;
            }

            RecomputeAndApply();
        }

        private void BuildWatchList()
        {
            // AbilitySystem.DamageProfile.StatBindings가 있으면 composite를 통해 구성요소를 알아낼 수 있음
            StatTypeBindings bindings = null;
            if (_ctx.owner != null)
            {
                var asys = _ctx.owner.GetComponent<AbilitySystem>();
                if (asys != null && asys.DamageProfile != null)
                    bindings = asys.DamageProfile.GetStatBindings();
            }

            if (bindings == null)
            {
                _watchSingle = _moveSpeedFallback;
                return;
            }

            if (bindings.TryGetComposite(_moveSpeedFinalStatId, out var c) && c != null)
            {
                _watchA = GetAttr(bindings, c.baseId);
                _watchB = GetAttr(bindings, c.addId);
                _watchC = GetAttr(bindings, c.mulId);
                return;
            }

            // composite가 아니면 단일 바인딩
            _watchSingle = GetAttr(bindings, _moveSpeedFinalStatId);

            AttributeDefinition GetAttr(StatTypeBindings b, StatId id)
            {
                if (id == StatId.None) return null;
                if (b.TryGetBinding(id, out var bind) && bind != null) return bind.attribute;
                return null;
            }
        }

        private void RecomputeAndApply()
        {
            float moveMult = ReadMoveSpeedMultiplierX1();
            float bonusMove = Mathf.Max(0f, moveMult - 1f); // 0.35 = +35%

            int steps = Mathf.FloorToInt(bonusMove / _step);
            float bonusCrit = Mathf.Max(0f, steps) * _critPerStep; // 0.01 = +1%p

            ApplyCritBonus(bonusCrit);
        }

        private float ReadMoveSpeedMultiplierX1()
        {
            // 1) StatId + StatBindings 우선
            StatTypeBindings bindings = null;
            if (_ctx.owner != null)
            {
                var asys = _ctx.owner.GetComponent<AbilitySystem>();
                if (asys != null && asys.DamageProfile != null)
                    bindings = asys.DamageProfile.GetStatBindings();
            }

            if (bindings != null)
            {
                var provider = new AttributeStatProvider(_ctx.attributeSet, bindings);
                float v = provider.Get(_moveSpeedFinalStatId);
                return v != 0f ? Mathf.Max(0f, v) : 1f;
            }

            // 2) fallback attribute
            if (_moveSpeedFallback != null)
            {
                float v = _ctx.attributeSet.GetAttributeValue(_moveSpeedFallback);
                return v != 0f ? Mathf.Max(0f, v) : 1f;
            }

            return 1f;
        }

        private void ApplyCritBonus(float bonusCrit)
        {
            RemoveCurrentModifier();

            if (_ctx.attributeSet == null || _critChance == null) return;
            if (bonusCrit <= 0.000001f) return;

            _currentCritMod = new AttributeModifier(ModifierType.Flat, bonusCrit, Token, duration: 0f);
            _ctx.attributeSet.AddModifier(_critChance, _currentCritMod);
        }

        private void RemoveCurrentModifier()
        {
            if (_currentCritMod == null) return;
            if (_ctx.attributeSet == null || _critChance == null) { _currentCritMod = null; return; }

            var av = _ctx.attributeSet.GetAttribute(_critChance);
            if (av != null) av.RemoveModifier(_currentCritMod);

            _currentCritMod = null;
        }
    }
}

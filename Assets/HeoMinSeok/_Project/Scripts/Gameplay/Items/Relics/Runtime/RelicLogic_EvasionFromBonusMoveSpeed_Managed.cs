using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 추가 이동속도를 읽어 회피 확률 보너스를 실시간으로 갱신하는 유물 로직이다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Evasion From Bonus MoveSpeed (Managed)")]
public class RelicLogic_EvasionFromBonusMoveSpeed_Managed : RelicLogic
{
    [Header("Read MoveSpeed (x1 multiplier)")]
    public StatId moveSpeedFinalStatId = StatId.MoveSpeedFinal;
    public AttributeDefinition moveSpeedMultiplierAttributeFallback;

    [Header("Apply To")]
    public AttributeDefinition evasionAddAttribute;

    [Header("Tuning")]
    [Tooltip("추가 이동속도 몇 %마다 보너스를 줄지. 0.40 = 40%")]
    public float bonusMoveStep = 0.40f;

    [Tooltip("스텝 1개당 회피 확률 증가량. 0.03 = +3%p")]
    public float evasionPerStep = 0.03f;

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
        if (evasionAddAttribute == null) return;

        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) mgr = ctx.owner.AddComponent<RelicProcManager>();

        mgr.Register(new EvasionFromBonusMoveSpeedProc(
            ctx,
            moveSpeedFinalStatId,
            moveSpeedMultiplierAttributeFallback,
            evasionAddAttribute,
            Mathf.Max(0.0001f, bonusMoveStep),
            evasionPerStep));
    }

    private sealed class EvasionFromBonusMoveSpeedProc : IRelicProc
    {
        public Object Token { get; }

        private readonly RelicContext _ctx;
        private readonly StatId _moveSpeedFinalStatId;
        private readonly AttributeDefinition _moveSpeedFallback;
        private readonly AttributeDefinition _evasionAddAttribute;
        private readonly float _step;
        private readonly float _evasionPerStep;

        private AttributeModifier _currentEvasionMod;
        private AttributeDefinition _watchA;
        private AttributeDefinition _watchB;
        private AttributeDefinition _watchC;
        private AttributeDefinition _watchSingle;

        public EvasionFromBonusMoveSpeedProc(
            RelicContext ctx,
            StatId moveSpeedFinalStatId,
            AttributeDefinition moveSpeedFallback,
            AttributeDefinition evasionAddAttribute,
            float step,
            float evasionPerStep)
        {
            _ctx = ctx;
            Token = ctx.token;
            _moveSpeedFinalStatId = moveSpeedFinalStatId;
            _moveSpeedFallback = moveSpeedFallback;
            _evasionAddAttribute = evasionAddAttribute;
            _step = step;
            _evasionPerStep = evasionPerStep;

            BuildWatchList();
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
            if (def == null) return;

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
            float bonusMove = Mathf.Max(0f, moveMult - 1f);

            int steps = Mathf.FloorToInt(bonusMove / _step);
            float bonusEvasion = Mathf.Max(0f, steps) * _evasionPerStep;

            ApplyEvasionBonus(bonusEvasion);
        }

        private float ReadMoveSpeedMultiplierX1()
        {
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

            if (_moveSpeedFallback != null)
            {
                float v = _ctx.attributeSet.GetAttributeValue(_moveSpeedFallback);
                return v != 0f ? Mathf.Max(0f, v) : 1f;
            }

            return 1f;
        }

        private void ApplyEvasionBonus(float bonusEvasion)
        {
            RemoveCurrentModifier();

            if (_ctx.attributeSet == null || _evasionAddAttribute == null) return;
            if (bonusEvasion <= 0.000001f) return;

            _currentEvasionMod = new AttributeModifier(ModifierType.Flat, bonusEvasion, Token, duration: 0f);
            _ctx.attributeSet.TryAddModifier(_evasionAddAttribute, _currentEvasionMod);
        }

        private void RemoveCurrentModifier()
        {
            if (_ctx.attributeSet == null || _evasionAddAttribute == null) return;
            _ctx.attributeSet.RemoveModifiersFromSource(Token);
            _currentEvasionMod = default;
        }
    }
}

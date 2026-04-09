using UnityEngine;
using UnityGAS;
using System.Collections.Generic;

/// <summary>
/// 책임 : 추가 이동속도를 읽어 치명타 확률 보너스를 실시간으로 갱신하는 유물 로직이다.
/// 일반 장착과 복원 장착 모두 앞으로의 이동속도 변화에 반응할 proc 등록이 필요하다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Crit From Bonus MoveSpeed (Managed)")]
public class RelicLogic_CritFromBonusMoveSpeed_Managed : RelicLogic
{
    protected override string DefaultEffectTemplate => "● [[추가 이동속도]] {bonus_move_step}마다 [[치명타 확률]] {crit_gain_per_step} 추가";

    [Header("Read MoveSpeed (x1 multiplier)")]
    [Tooltip("권장: MoveSpeedFinal (x1). StatTypeBindings의 Composite(Final=(Base+Add)*Mul)를 활용합니다.")]
    public StatId moveSpeedFinalStatId = StatId.MoveSpeedFinal;

    [Tooltip("StatTypeBindings가 없거나 특별히 직접 읽고 싶을 때 사용하는 fallback. (x1 배수 Attribute)")]
    public AttributeDefinition moveSpeedMultiplierAttributeFallback;

    [Header("Apply To")]
    [Tooltip("치명타 확률 Add AttributeDefinition (0~1)")]
    public AttributeDefinition critChanceAddAttribute;

    [Header("Tuning")]
    [Tooltip("추가 이동속도 몇 %마다 보너스를 줄지. 0.10 = 10%")]
    public float bonusMoveStep = 0.10f;

    [Tooltip("스텝 1개당 치확 증가량. 0.01 = +1%p")]
    public float critPerStep = 0.01f;

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
        if (critChanceAddAttribute == null) return;

        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) mgr = ctx.owner.AddComponent<RelicProcManager>();

        var proc = new CritFromBonusMoveSpeedProc(
            ctx,
            moveSpeedFinalStatId,
            moveSpeedMultiplierAttributeFallback,
            critChanceAddAttribute,
            Mathf.Max(0.0001f, bonusMoveStep),
            critPerStep
        );

        mgr.Register(proc);
    }

    private sealed class CritFromBonusMoveSpeedProc : IRelicProc
    {
        public Object Token { get; }

        private readonly RelicContext _ctx;
        private readonly StatId _moveSpeedFinalStatId;
        private readonly AttributeDefinition _moveSpeedFallback;
        private readonly AttributeDefinition _critChanceAdd;

        private readonly float _step;
        private readonly float _critPerStep;

        private AttributeModifier _currentCritMod;

        private AttributeDefinition _watchA;
        private AttributeDefinition _watchB;
        private AttributeDefinition _watchC;
        private AttributeDefinition _watchSingle;

        public CritFromBonusMoveSpeedProc(
            RelicContext ctx,
            StatId moveSpeedFinalStatId,
            AttributeDefinition moveSpeedFallback,
            AttributeDefinition critChanceAdd,
            float step,
            float critPerStep)
        {
            _ctx = ctx;
            Token = ctx.token;
            _moveSpeedFinalStatId = moveSpeedFinalStatId;
            _moveSpeedFallback = moveSpeedFallback;
            _critChanceAdd = critChanceAdd;
            _step = step;
            _critPerStep = critPerStep;

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
            float bonusCrit = Mathf.Max(0f, steps) * _critPerStep;

            ApplyCritBonus(bonusCrit);
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

        private void ApplyCritBonus(float bonusCrit)
        {
            RemoveCurrentModifier();

            if (_ctx.attributeSet == null || _critChanceAdd == null) return;
            if (bonusCrit <= 0.000001f) return;

            _currentCritMod = new AttributeModifier(ModifierType.Flat, bonusCrit, Token, duration: 0f);
            _ctx.attributeSet.TryAddModifier(_critChanceAdd, _currentCritMod);
        }

        private void RemoveCurrentModifier()
        {
            if (_ctx.attributeSet == null || _critChanceAdd == null) return;
            _ctx.attributeSet.RemoveModifiersFromSource(Token);
            _currentCritMod = default;
        }
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return BuildTemplatedTooltip(
            "● [[추가 이동속도]] {bonus_move_step}마다 [[치명타 확률]] {crit_gain_per_step} 추가",
            new Dictionary<string, string>
            {
                ["bonus_move_step"] = RelicTooltipFormatter.FormatSignedValueToken(bonusMoveStep, true),
                ["crit_gain_per_step"] = RelicTooltipFormatter.FormatSignedValueToken(critPerStep, true),
            });
    }
}

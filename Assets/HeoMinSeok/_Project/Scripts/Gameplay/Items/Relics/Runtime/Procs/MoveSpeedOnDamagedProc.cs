using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>
/// 피격(토큰 감소 또는 체력 감소) 시 일정 시간 이동속도(%) 버프를 부여합니다.
/// AttributeModifier 기반(Percent).
/// </summary>
public sealed class MoveSpeedOnDamagedProc : IRelicProc
{
    public Object Token { get; }

    private readonly RelicContext _ctx;
    private readonly AttributeDefinition _moveSpeedAttr;
    private readonly float _percent;
    private readonly float _duration;
    private readonly bool _refresh;

    // 토큰 체력 트리거
    private readonly PlayerTokenHealth _tokenHealth;

    // (fallback) Attribute 기반 트리거
    private readonly AttributeDefinition _healthAttrFallback;

    // 버프 전용 Source 토큰(다른 유물 효과와 충돌 방지)
    private readonly RelicRuntimeToken _buffSource;

    public MoveSpeedOnDamagedProc(
        RelicContext ctx,
        AttributeDefinition moveSpeedAttr,
        float percent,
        float duration,
        bool refresh,
        AttributeDefinition healthAttributeFallback = null)
    {
        _ctx = ctx;
        Token = ctx.token;
        _moveSpeedAttr = moveSpeedAttr;
        _percent = percent;
        _duration = duration;
        _refresh = refresh;
        _healthAttrFallback = healthAttributeFallback;

        _buffSource = ScriptableObject.CreateInstance<RelicRuntimeToken>();
        _buffSource.hideFlags = HideFlags.HideAndDontSave;

        // 1) 토큰 체력 우선
        if (_ctx.owner != null)
        {
            _tokenHealth = _ctx.owner.GetComponent<PlayerTokenHealth>();
            if (_tokenHealth != null)
                _tokenHealth.OnTokenDamaged += OnTokenDamaged;
        }

        // 2) 토큰 체력이 없으면 Attribute 감소로 감지(옵션)
        if (_tokenHealth == null && _ctx.attributeSet != null && _healthAttrFallback != null)
        {
            _ctx.attributeSet.OnAttributeChanged += OnAttributeChanged;
        }
    }

    // RelicProcManager의 GameplayEvent 라우팅이 필요 없는 유물이라 비워둡니다.
    public void Handle(GameplayTag tag, AbilityEventData data) { }

    public void Dispose()
    {
        if (_tokenHealth != null)
            _tokenHealth.OnTokenDamaged -= OnTokenDamaged;

        if (_ctx.attributeSet != null)
            _ctx.attributeSet.OnAttributeChanged -= OnAttributeChanged;

        if (_ctx.attributeSet != null)
            _ctx.attributeSet.RemoveModifiersFromSource(_buffSource);

        if (_buffSource != null)
            Object.Destroy(_buffSource);
    }

    private void OnTokenDamaged(int amount)
    {
        if (amount <= 0) return;
        ApplyOrRefreshBuff();
    }

    private void OnAttributeChanged(AttributeDefinition def, float oldValue, float newValue)
    {
        if (_healthAttrFallback == null) return;
        if (def != _healthAttrFallback) return;

        // "피격"만 트리거: 체력이 감소했을 때만
        if (newValue < oldValue)
            ApplyOrRefreshBuff();
    }

    private void ApplyOrRefreshBuff()
    {
        if (_ctx.attributeSet == null) return;
        if (_moveSpeedAttr == null) return;

        if (_refresh)
            _ctx.attributeSet.RemoveModifiersFromSource(_buffSource);

        var mod = new AttributeModifier(
            ModifierType.Percent,
            _percent,
            _buffSource,
            duration: Mathf.Max(0.01f, _duration)
        );

        _ctx.attributeSet.TryAddModifier(_moveSpeedAttr, mod);
    }
}

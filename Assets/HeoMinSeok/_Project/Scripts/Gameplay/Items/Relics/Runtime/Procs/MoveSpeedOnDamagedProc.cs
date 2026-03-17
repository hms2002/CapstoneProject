using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>
/// 피격(Damaged 태그 수신) 시 일정 시간 이동속도(%) 버프를 부여합니다.
/// AttributeModifier 기반(Percent).
/// </summary>
public sealed class MoveSpeedOnDamagedProc : IRelicProc
{
    public Object Token { get; }

    private readonly RelicContext _ctx;
    private readonly GameplayTag _triggerTag;
    private readonly AttributeDefinition _moveSpeedAttr;
    private readonly float _percent;
    private readonly float _duration;
    private readonly bool _refresh;

    private readonly RelicRuntimeToken _buffSource;

    public MoveSpeedOnDamagedProc(
        RelicContext ctx,
        GameplayTag triggerTag,
        AttributeDefinition moveSpeedAttr,
        float percent,
        float duration,
        bool refresh)
    {
        _ctx = ctx;
        Token = ctx.token;
        _triggerTag = triggerTag;
        _moveSpeedAttr = moveSpeedAttr;
        _percent = percent;
        _duration = duration;
        _refresh = refresh;

        _buffSource = ScriptableObject.CreateInstance<RelicRuntimeToken>();
        _buffSource.hideFlags = HideFlags.HideAndDontSave;
    }

    public void Handle(GameplayTag tag, AbilityEventData data)
    {
        if (_ctx.attributeSet == null) return;
        if (_moveSpeedAttr == null) return;
        if (_triggerTag == null) return;
        if (tag != _triggerTag) return;

        // 이 owner(보통 플레이어)에게 들어온 피격 이벤트만 처리
        if (data.Target != null && _ctx.owner != null && data.Target != _ctx.owner)
            return;

        ApplyOrRefreshBuff();
    }

    public void Dispose()
    {
        if (_ctx.attributeSet != null)
            _ctx.attributeSet.RemoveModifiersFromSource(_buffSource);

        if (_buffSource != null)
            Object.Destroy(_buffSource);
    }

    private void ApplyOrRefreshBuff()
    {
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
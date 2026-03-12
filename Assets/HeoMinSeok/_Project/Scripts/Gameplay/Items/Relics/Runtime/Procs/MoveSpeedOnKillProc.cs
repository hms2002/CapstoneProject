using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>
/// KillConfirmed 태그를 수신하면 일정 시간 이동속도(%) 버프를 부여합니다.
/// AttributeModifier 기반(Percent).
/// </summary>
public sealed class MoveSpeedOnKillProc : IRelicProc
{
    public Object Token { get; }

    private readonly RelicContext _ctx;
    private readonly GameplayTag _triggerTag;
    private readonly AttributeDefinition _moveSpeedAttr;
    private readonly float _percent;
    private readonly float _duration;
    private readonly bool _refresh;

    // 버프 전용 Source 토큰(다른 유물 효과와 충돌 방지)
    private readonly RelicRuntimeToken _buffSource;

    public MoveSpeedOnKillProc(
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

        // safety: 이 AbilitySystem(플레이어)에서 발행한 이벤트만 받는 구조지만,
        // 혹시 다른 라우팅이 생기면 Instigator 체크로 방어할 수 있음.
        if (data.Instigator != null && _ctx.owner != null && data.Instigator != _ctx.owner)
            return;

        if (_refresh)
            _ctx.attributeSet.RemoveModifiersFromSource(_buffSource);

        var mod = new AttributeModifier(
            ModifierType.Percent,
            _percent,
            _buffSource,
            duration: Mathf.Max(0.01f, _duration)
        );

        _ctx.attributeSet.AddModifier(_moveSpeedAttr, mod);
    }

    public void Dispose()
    {
        if (_ctx.attributeSet != null)
            _ctx.attributeSet.RemoveModifiersFromSource(_buffSource);

        if (_buffSource != null)
            Object.Destroy(_buffSource);
    }
}

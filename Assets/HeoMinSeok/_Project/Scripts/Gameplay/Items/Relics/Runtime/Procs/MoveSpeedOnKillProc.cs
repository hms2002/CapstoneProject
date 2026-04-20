using UnityEngine;
using UnityGAS;
using System.Collections.Generic;
using Object = UnityEngine.Object;

/// <summary>
/// 책임 :
/// - KillConfirmed 태그를 수신하면 일정 시간 이동속도(%) 버프를 부여한다.
/// - 버프 남은 시간과 활성 중첩 수를 추적해 플레이어 상태 HUD에 같은 정보를 projection 한다.
/// - 유물 proc 자체가 버프 owner 역할을 맡아, 시작/갱신/만료/해제 시점에 상태 handle을 함께 관리한다.
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
    private readonly StatusHudDefinition _statusDefinition;

    // 버프 전용 Source 토큰(다른 유물 효과와 충돌 방지)
    private readonly RelicRuntimeToken _buffSource;
    private readonly List<float> _activeExpiryTimes = new();
    private readonly PlayerStatusRuntime _playerStatusRuntime;
    private readonly string _ownerKey;
    private StatusHandle _statusHandle;

    public MoveSpeedOnKillProc(
        RelicContext ctx,
        GameplayTag triggerTag,
        AttributeDefinition moveSpeedAttr,
        float percent,
        float duration,
        bool refresh,
        StatusHudDefinition statusDefinition)
    {
        _ctx = ctx;
        Token = ctx.token;
        _triggerTag = triggerTag;
        _moveSpeedAttr = moveSpeedAttr;
        _percent = percent;
        _duration = duration;
        _refresh = refresh;
        _statusDefinition = statusDefinition;

        _buffSource = ScriptableObject.CreateInstance<RelicRuntimeToken>();
        _buffSource.hideFlags = HideFlags.HideAndDontSave;

        _playerStatusRuntime = ctx.owner != null ? PlayerStatusRuntime.GetOrAdd(ctx.owner) : null;
        string relicId = ctx.relicDef != null ? ctx.relicDef.relicId : "relic";
        int tokenId = Token != null ? Token.GetInstanceID() : 0;
        _ownerKey = $"relic.move_speed_on_kill.{relicId}.{tokenId}";
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
        {
            _ctx.attributeSet.RemoveModifiersFromSource(_buffSource);
            _activeExpiryTimes.Clear();
        }

        var mod = new AttributeModifier(
            ModifierType.Percent,
            _percent,
            _buffSource,
            duration: Mathf.Max(0.01f, _duration)
        );

        _ctx.attributeSet.TryAddModifier(_moveSpeedAttr, mod);

        _activeExpiryTimes.Add(Time.time + Mathf.Max(0.01f, _duration));
        PublishStatusHud();
    }

    public void Tick(float deltaTime)
    {
        if (_activeExpiryTimes.Count == 0)
            return;

        float now = Time.time;
        bool removedAny = false;
        for (int i = _activeExpiryTimes.Count - 1; i >= 0; i--)
        {
            if (_activeExpiryTimes[i] > now)
                continue;

            _activeExpiryTimes.RemoveAt(i);
            removedAny = true;
        }

        if (removedAny || _statusHandle.IsValid)
            PublishStatusHud();
    }

    public void Dispose()
    {
        ReleaseStatusHud();

        if (_ctx.attributeSet != null)
            _ctx.attributeSet.RemoveModifiersFromSource(_buffSource);

        if (_buffSource != null)
            Object.Destroy(_buffSource);
    }

    private void PublishStatusHud()
    {
        if (_statusDefinition == null || _playerStatusRuntime == null)
            return;

        int activeCount = _activeExpiryTimes.Count;
        if (activeCount <= 0)
        {
            ReleaseStatusHud();
            return;
        }

        float remainingTime = 0f;
        float now = Time.time;
        for (int i = 0; i < _activeExpiryTimes.Count; i++)
        {
            float remaining = Mathf.Max(0f, _activeExpiryTimes[i] - now);
            if (remaining > remainingTime)
                remainingTime = remaining;
        }

        StatusApplyRequest request = new(
            _statusDefinition,
            _ownerKey,
            stackCount: activeCount,
            remainingTime: remainingTime,
            maxTime: Mathf.Max(0.01f, _duration),
            isVisible: true,
            showStacksOverride: !_refresh && activeCount > 1,
            showDurationOverride: true);

        if (_statusHandle.IsValid)
        {
            _playerStatusRuntime.UpdateStatus(_statusHandle, request);
            return;
        }

        _statusHandle = _playerStatusRuntime.Apply(request);
    }

    private void ReleaseStatusHud()
    {
        if (!_statusHandle.IsValid)
            return;

        _statusHandle.Release();
        _statusHandle = default;
    }
}

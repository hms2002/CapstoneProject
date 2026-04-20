using UnityEngine;
using UnityGAS;
using System.Collections.Generic;
using Object = UnityEngine.Object;

/// <summary>
/// 책임 :
/// - 피격 이벤트를 감지해 일정 시간 이동속도 버프를 부여하고, 같은 주기로 상태 HUD를 갱신한다.
/// - 실제 능력치 버프와 HUD 표시 수명을 같은 owner 기준으로 묶어 해제까지 일관되게 관리한다.
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
    private readonly StatusHudDefinition _statusDefinition;

    private readonly RelicRuntimeToken _buffSource;
    private readonly List<float> _activeExpiryTimes = new();
    private readonly PlayerStatusRuntime _playerStatusRuntime;
    private readonly string _ownerKey;
    private StatusHandle _statusHandle;

    public MoveSpeedOnDamagedProc(
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
        _ownerKey = $"relic.move_speed_on_damaged.{relicId}.{tokenId}";
    }

    public void Handle(GameplayTag tag, AbilityEventData data)
    {
        if (_ctx.attributeSet == null)
            return;

        if (_moveSpeedAttr == null)
            return;

        if (_triggerTag == null)
            return;

        if (tag != _triggerTag)
            return;

        // 이 owner(보통 플레이어)에게 들어온 피격 이벤트만 처리
        if (data.Target != null && _ctx.owner != null && data.Target != _ctx.owner)
            return;

        ApplyOrRefreshBuff();
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

    private void ApplyOrRefreshBuff()
    {
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

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>
/// 책임 :
/// - 치명타 적중 횟수에 따라 이동속도 보너스를 누적하는 유물 로직이다.
/// - 실제 피해를 받으면 누적 스택을 초기화하고, 공용 런타임 허브를 통해 상태 저장/복원을 수행한다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Move Speed Stack On Critical Hit (Managed)")]
public class RelicLogic_MoveSpeedStackOnCriticalHit_Managed : RelicLogic, IRelicRuntimeStateSerializer
{
    public const string StateTypeKey = "RelicMoveSpeedStackOnCriticalHit";
    protected override string DefaultEffectTemplate => "● [[치명타]] 시 [[이동속도]] {move_speed_gain}씩 증가\n● 최대 {max_move_speed_bonus}까지 증가 가능\n● 피해를 받으면 위 보너스가 초기화됨";

    [Header("Trigger")]
    [Tooltip("치명타 판정을 읽을 적중 이벤트 태그. 보통 Event.HitConfirm.")]
    public GameplayTag criticalHitTriggerTag;

    [Header("Reset")]
    [Tooltip("실제 피해 여부를 감지할 체력 AttributeDefinition. 보통 HealthAttribute.")]
    public AttributeDefinition healthAttribute;

    [Header("Apply To")]
    [Tooltip("이동속도 배율 AttributeDefinition. 보통 MoveSpeedMulAttribute.")]
    public AttributeDefinition moveSpeedAttribute;

    [Header("Tuning")]
    [Tooltip("치명타 1회당 이동속도 증가량. 0.02 = +2%")]
    public float percentPerCritical = 0.02f;

    [Tooltip("누적 가능한 최대 이동속도 증가량. 0.50 = +50%")]
    public float maxPercentBonus = 0.50f;

    public override void OnEquipped(RelicContext ctx)
    {
        RegisterProc(ctx);
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null)
            return;

        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr != null)
            mgr.UnregisterAll(ctx.token);

        var hub = ctx.owner.GetComponent<RelicRuntimeStateHub>();
        if (hub != null)
            hub.Clear(ctx.token);
    }

    public override void OnRestoreAttached(RelicContext ctx)
    {
        RegisterProc(ctx);
    }

    /// <summary>
    /// 책임 : 현재 token에 쌓인 달리는 장부 스택 상태를 runtime state DTO로 내보낸다.
    /// </summary>
    public bool TryCaptureRuntimeState(
        RelicContext ctx,
        RelicRuntimeStateHub hub,
        int slotIndex,
        out RelicRuntimeState state)
    {
        state = null;

        if (ctx.relicDef == null || ctx.token == null || hub == null)
            return false;

        if (!hub.TryGetJson(ctx.token, out var json))
            return false;

        var payload = JsonUtility.FromJson<RelicMoveSpeedStackOnCriticalHitRuntimePayload>(json);
        if (payload == null || payload.stackCount <= 0)
            return false;

        state = new RelicRuntimeState
        {
            slotIndex = slotIndex,
            relicId = ctx.relicDef.relicId,
            level = Mathf.Max(1, ctx.level),
            stateType = StateTypeKey,
            json = json
        };

        return true;
    }

    /// <summary>
    /// 책임 : 저장된 달리는 장부 스택 상태를 현재 token의 공용 런타임 허브에 다시 주입한다.
    /// </summary>
    public void RestoreRuntimeState(
        RelicContext ctx,
        RelicRuntimeState state,
        RelicRuntimeStateHub hub)
    {
        if (ctx.token == null || state == null || hub == null)
            return;

        if (!string.Equals(state.stateType, StateTypeKey, StringComparison.Ordinal))
            return;

        if (string.IsNullOrWhiteSpace(state.json))
            return;

        hub.RestoreJson(ctx.token, state.json);
    }

    private void RegisterProc(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;
        if (ctx.attributeSet == null) return;
        if (criticalHitTriggerTag == null) return;
        if (healthAttribute == null) return;
        if (moveSpeedAttribute == null) return;

        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null)
            mgr = ctx.owner.AddComponent<RelicProcManager>();

        var hub = ctx.owner.GetComponent<RelicRuntimeStateHub>();
        if (hub == null)
            hub = ctx.owner.AddComponent<RelicRuntimeStateHub>();

        mgr.Register(new MoveSpeedStackOnCriticalHitProc(
            ctx,
            hub,
            criticalHitTriggerTag,
            healthAttribute,
            moveSpeedAttribute,
            Mathf.Max(0f, percentPerCritical),
            Mathf.Max(0f, maxPercentBonus)));
    }

    /// <summary>
    /// 책임 :
    /// - 치명타 적중 이벤트와 체력 감소를 함께 감시하며 이동속도 보너스를 누적/초기화한다.
    /// - 공용 허브와 동기화해 씬 이동 시 스택을 저장/복원할 수 있게 만든다.
    /// </summary>
    private sealed class MoveSpeedStackOnCriticalHitProc : IRelicProc
    {
        public Object Token { get; }

        private readonly RelicContext _ctx;
        private readonly RelicRuntimeStateHub _hub;
        private readonly GameplayTag _criticalHitTriggerTag;
        private readonly AttributeDefinition _healthAttribute;
        private readonly AttributeDefinition _moveSpeedAttribute;
        private readonly float _percentPerCritical;
        private readonly float _maxPercentBonus;
        private readonly MoveSpeedStackRuntimeToken _buffSource;
        private readonly Action<string> _restoreHandler;

        private int _stackCount;
        private float _currentBonus;

        public MoveSpeedStackOnCriticalHitProc(
            RelicContext ctx,
            RelicRuntimeStateHub hub,
            GameplayTag criticalHitTriggerTag,
            AttributeDefinition healthAttribute,
            AttributeDefinition moveSpeedAttribute,
            float percentPerCritical,
            float maxPercentBonus)
        {
            _ctx = ctx;
            _hub = hub;
            Token = ctx.token;
            _criticalHitTriggerTag = criticalHitTriggerTag;
            _healthAttribute = healthAttribute;
            _moveSpeedAttribute = moveSpeedAttribute;
            _percentPerCritical = percentPerCritical;
            _maxPercentBonus = maxPercentBonus;

            _buffSource = ScriptableObject.CreateInstance<MoveSpeedStackRuntimeToken>();
            _buffSource.hideFlags = HideFlags.HideAndDontSave;

            _restoreHandler = ApplyRestoredStateJson;

            _ctx.attributeSet.OnAttributeChanged += OnAttributeChanged;
            _hub?.Bind(Token, _restoreHandler);
            PublishState();
        }

        public void Handle(GameplayTag tag, AbilityEventData data)
        {
            if (_ctx.attributeSet == null) return;
            if (_criticalHitTriggerTag == null) return;
            if (tag != _criticalHitTriggerTag) return;
            if (!data.IsCriticalHit) return;

            if (_ctx.abilitySystem != null && data.AbilitySystem != _ctx.abilitySystem)
                return;

            AddCriticalStack();
        }

        public void Dispose()
        {
            if (_ctx.attributeSet != null)
                _ctx.attributeSet.OnAttributeChanged -= OnAttributeChanged;

            _hub?.Unbind(Token, _restoreHandler);
            _hub?.Clear(Token);

            RemoveCurrentModifier();

            if (_buffSource != null)
                Object.Destroy(_buffSource);
        }

        private void OnAttributeChanged(AttributeDefinition def, float oldValue, float newValue)
        {
            if (def != _healthAttribute) return;
            if (newValue >= oldValue) return;

            ResetStacks();
        }

        private void AddCriticalStack()
        {
            if (_percentPerCritical <= 0f || _maxPercentBonus <= 0f)
                return;

            float nextBonus = Mathf.Min(_maxPercentBonus, _currentBonus + _percentPerCritical);
            if (nextBonus <= _currentBonus + 0.000001f)
                return;

            _stackCount++;
            _currentBonus = nextBonus;
            ApplyCurrentModifier();
            PublishState();
        }

        private void ResetStacks()
        {
            if (_stackCount == 0 && _currentBonus <= 0.000001f)
                return;

            _stackCount = 0;
            _currentBonus = 0f;
            RemoveCurrentModifier();
            PublishState();
        }

        private void ApplyRestoredStateJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            var payload = JsonUtility.FromJson<RelicMoveSpeedStackOnCriticalHitRuntimePayload>(json);
            if (payload == null)
                return;

            _stackCount = Mathf.Max(0, payload.stackCount);
            _currentBonus = Mathf.Min(_maxPercentBonus, _stackCount * _percentPerCritical);
            ApplyCurrentModifier();
            PublishState();
        }

        private void ApplyCurrentModifier()
        {
            RemoveCurrentModifier();

            if (_ctx.attributeSet == null || _moveSpeedAttribute == null) return;
            if (_currentBonus <= 0.000001f) return;

            var mod = new AttributeModifier(
                ModifierType.Percent,
                _currentBonus,
                _buffSource,
                duration: 0f);

            _ctx.attributeSet.TryAddModifier(_moveSpeedAttribute, mod);
        }

        private void RemoveCurrentModifier()
        {
            if (_ctx.attributeSet == null)
                return;

            _ctx.attributeSet.RemoveModifiersFromSource(_buffSource);
        }

        private void PublishState()
        {
            if (_hub == null || Token == null)
                return;

            if (_stackCount <= 0)
            {
                _hub.SetJson(Token, null);
                return;
            }

            var payload = new RelicMoveSpeedStackOnCriticalHitRuntimePayload
            {
                stackCount = _stackCount
            };

            _hub.SetJson(Token, JsonUtility.ToJson(payload));
        }
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return BuildTemplatedTooltip(
            "● [[치명타]] 시 [[이동속도]] {move_speed_gain}씩 증가\n● 최대 {max_move_speed_bonus}까지 증가 가능\n● 피해를 받으면 위 보너스가 초기화됨",
            new Dictionary<string, string>
            {
                ["move_speed_gain"] = RelicTooltipFormatter.FormatSignedValueToken(percentPerCritical, true),
                ["max_move_speed_bonus"] = RelicTooltipFormatter.FormatSignedValueToken(maxPercentBonus, true),
            });
    }
}

/// <summary>
/// 책임 :
/// - 달리는 장부 유물의 지속 modifier source를 구분하기 위한 런타임 토큰이다.
/// </summary>
public sealed class MoveSpeedStackRuntimeToken : ScriptableObject
{
}

/// <summary>
/// 책임 :
/// - 달리는 장부 유물의 씬 이동용 저장 payload를 JSON 직렬화 가능한 형태로 보관한다.
/// </summary>
[Serializable]
public sealed class RelicMoveSpeedStackOnCriticalHitRuntimePayload
{
    public int stackCount;
}

using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 사슬창의 던지기 이후 링크 대기 구간을 시간축으로 운영하고, 첫 적중이 오면 runtime state에 링크를 확정한 뒤 실행을 종료한다.
/// - 기다리는 동안의 timeout, 취소, 강제 종료 cleanup을 공용 executor 생명주기 위에서 처리한다.
/// </summary>
public sealed class ChainSpearThrowExecutor : WeaponAbilityExecutor
{
    private const string HitConfirmTagResourcePath = "Tags/Event.HitConfirm";

    [Header("Link Wait")]
    [SerializeField] private float maxAwaitSeconds = 3f;
    [SerializeField] private bool establishLinkFromBaseAttack = true;

    private static GameplayTag hitConfirmRootTag;

    private ChainSpearRuntimeState runtimeState;
    private ChainSpearLoadout loadout;
    private float startedAt;

    private void Update()
    {
        if (!IsRunning)
            return;

        if (maxAwaitSeconds > 0f && Time.time >= startedAt + maxAwaitSeconds)
            ForceStop(WeaponExecutorEndReason.Timeout);
    }

    protected override void OnBegin(in WeaponAbilityExecutionContext context)
    {
        runtimeState = context.RuntimeState as ChainSpearRuntimeState;
        loadout = context.Loadout as ChainSpearLoadout;
        startedAt = Time.time;

        runtimeState?.BeginAwaitingLinkHit();
    }

    public override void HandleGameplayEvent(GameplayTag tag, in AbilityEventData data)
    {
        if (!IsRunning || runtimeState == null || loadout == null)
            return;

        if (!establishLinkFromBaseAttack)
            return;

        if (!MatchesHitConfirmTag(tag))
            return;

        if (data.Spec?.Definition != loadout.BaseAttack)
            return;

        runtimeState.ConfirmLinkedTarget(data.Target, data.WorldPosition, data.IsCriticalHit);
        Complete();
    }

    protected override void Cleanup(WeaponExecutorEndReason reason)
    {
        if (runtimeState != null && runtimeState.IsAwaitingLinkHit && !runtimeState.HasLinkedTarget)
            runtimeState.ClearLinkedTarget();

        runtimeState = null;
        loadout = null;
    }

    private static bool MatchesHitConfirmTag(GameplayTag raisedTag)
    {
        hitConfirmRootTag ??= Resources.Load<GameplayTag>(HitConfirmTagResourcePath);
        if (raisedTag == null || hitConfirmRootTag == null)
            return false;

        for (GameplayTag current = raisedTag; current != null; current = current.Parent)
        {
            if (current == hitConfirmRootTag)
                return true;
        }

        return false;
    }
}

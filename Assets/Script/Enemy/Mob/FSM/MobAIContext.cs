using UnityEngine;

/// <summary>
/// 책임:
/// - 특정 몬스터가 공통 추적 감지 범위 대신 자기 전용 감지 성공 조건을 FSM에 제공하게 한다.
/// - Rook처럼 "같은 방 + 경로 차단물 없음" 같은 몬스터별 전투 진입 조건을 공통 FSM에 누수 없이 연결한다.
/// </summary>
public interface IMobTargetDetectionOverride
{
    bool HasDetectedTargetForMobFsm();
}

/// <summary>
/// 책임 :
/// - 일반 몬스터 FSM 상태가 공통으로 참조할 owner, chase intent 인터페이스, bridge, attack decision source를 한 묶음으로 제공한다.
/// - 상태 클래스가 개별 컴포넌트 탐색 없이 필요한 최소 의존만 주입받아 동작하게 한다.
/// </summary>
public sealed class MobAIContext
{
    public Mob Owner { get; }
    public IEnemyChaseIntent ChaseIntent { get; }
    public IMobAbilityBridge AbilityBridge { get; }
    public IMobAttackDecisionSource AttackDecisionSource { get; }
    public IMobPatternRunner[] PatternRunnerTargets { get; }
    public IMobPresentationCleanup[] PresentationCleanupTargets { get; }

    public MobAIContext(
        Mob owner,
        IEnemyChaseIntent chaseIntent,
        IMobAbilityBridge abilityBridge,
        IMobAttackDecisionSource attackDecisionSource,
        IMobPatternRunner[] patternRunnerTargets,
        IMobPresentationCleanup[] presentationCleanupTargets)
    {
        Owner = owner;
        ChaseIntent = chaseIntent;
        AbilityBridge = abilityBridge;
        AttackDecisionSource = attackDecisionSource;
        PatternRunnerTargets = patternRunnerTargets;
        PresentationCleanupTargets = presentationCleanupTargets;
    }

    public bool HasDetectedTarget()
    {
        if (Owner == null || Owner.IsDead)
            return false;

        if (Owner.Target == null)
            return false;

        if (Owner is IMobTargetDetectionOverride detectionOverride)
            return detectionOverride.HasDetectedTargetForMobFsm();

        return ChaseIntent == null || ChaseIntent.IsTargetWithinDetectionRange();
    }

    public bool CanUseChaseState()
    {
        return Owner != null &&
               ChaseIntent != null &&
               Owner.CanUseChaseMovement() &&
               HasDetectedTarget();
    }

    public bool IsInStaggerState()
    {
        return AbilityBridge != null && AbilityBridge.IsAbilityExecutionSuppressed;
    }

    /// <summary>
    /// 책임 :
    /// - 제압 진입 시 일반 몬스터가 반드시 끊어야 하는 공통 실행/이동 경로를 한 곳에서 정리한다.
    /// - Stagger 같은 전역 상태가 개별 상태/러너 구현 세부를 몰라도 최소 cleanup 규칙을 일관되게 적용하게 한다.
    /// </summary>
    public void PerformSuppressionCleanup()
    {
        ChaseIntent?.StopChase();
        CancelPatternRunners();
        AbilityBridge?.CancelActiveAbility(true);
        CleanupPresentation();
    }

    /// <summary>
    /// 책임 :
    /// - 비활성화/사망처럼 fail-safe 성격의 종료 경로에서 일반 몬스터가 남기지 말아야 할 실행/이동을 강제로 정리한다.
    /// - 상태 전이 기반 cleanup이 타지 못하는 종료 경로에서도 동일한 최소 정리 규칙을 보장한다.
    /// </summary>
    public void PerformFailSafeCleanup()
    {
        ChaseIntent?.StopChase();
        CancelPatternRunners();
        AbilityBridge?.CancelActiveAbility(true);
        CleanupPresentation();
    }

    /// <summary>
    /// 책임 :
    /// - suppression / death / disable 같은 전역 종료 경로에서 현재 오브젝트가 가진 모든 패턴 실행기에게 강제 취소를 전달한다.
    /// - coordinator가 추적하지 못한 러너가 있더라도 전투 객체 레벨에서 마지막 방어선으로 실행을 끊게 한다.
    /// </summary>
    public void CancelPatternRunners()
    {
        if (PatternRunnerTargets == null)
            return;

        for (int i = 0; i < PatternRunnerTargets.Length; i++)
            PatternRunnerTargets[i]?.Cancel();
    }

    /// <summary>
    /// 책임 :
    /// - suppression / death / disable 같은 전역 종료 경로에서 경고, 마스크, 오버레이처럼 남기기 쉬운 presentation을 한 번에 정리한다.
    /// - runner와 helper가 각자 가진 시각 cleanup 구현을 전투 객체가 공통 경로로 orchestration 하게 한다.
    /// </summary>
    public void CleanupPresentation()
    {
        if (PresentationCleanupTargets == null)
            return;

        for (int i = 0; i < PresentationCleanupTargets.Length; i++)
            PresentationCleanupTargets[i]?.CleanupPresentation();
    }
}

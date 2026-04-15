using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - Dead'sSkeleton 자폭 패턴 1회의 인트로, armed 유지, 완료/취소 흐름을 실행한다.
/// - AbilitySpec 취소 토큰과 MobAbilityCoordinator에 종속되어 ASC 생명주기와 함께 정리되도록 한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DeadsSkeleton))]
public class DeadsSkeletonSelfDestructPatternExecutor : MonoBehaviour, IMobPatternRunner
{
    /// <summary>
    /// 책임 :
    /// - Dead'sSkeleton 자폭 패턴 1회 실행에 필요한 최소 런타임 문맥을 담는다.
    /// - 현재 타깃처럼 실행 시점마다 달라지는 값만 전달한다.
    /// </summary>
    public readonly struct SelfDestructContext
    {
        public readonly GameObject TargetObject;

        public SelfDestructContext(GameObject targetObject)
        {
            TargetObject = targetObject;
        }
    }

    [SerializeField] private DeadsSkeleton owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;

    private bool isRunning;
    private bool cancelRequested;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<DeadsSkeleton>();

        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();
    }

    public IEnumerator Run(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (owner == null)
            yield break;

        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this))
            yield break;

        if (!owner.TryCreateSelfDestructContext(initialTarget, out SelfDestructContext context))
        {
            abilityCoordinator?.EndRunner(this);
            yield break;
        }

        isRunning = true;
        cancelRequested = false;
        owner.BeginSelfDestructSequence(context.TargetObject);

        try
        {
            while (true)
            {
                if (IsCancelled(spec) || cancelRequested)
                {
                    owner.CancelSelfDestructSequence();
                    yield break;
                }

                if (owner.IsDead)
                    yield break;

                SelfDestructSequenceStatus status = owner.AdvanceSelfDestructSequence();
                if (status != SelfDestructSequenceStatus.Running)
                    yield break;

                yield return null;
            }
        }
        finally
        {
            cancelRequested = false;
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    public void Cancel()
    {
        cancelRequested = true;
        owner?.CancelSelfDestructSequence();
    }

    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec != null && spec.Token != null && spec.Token.IsCancelled;
    }
}

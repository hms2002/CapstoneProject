using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - StrangeCandlestick 공격 1회의 락온 경고 유지, 취소 판정, 최종 발사를 순서대로 실행한다.
/// - AbilitySpec 취소 토큰과 MobAbilityCoordinator에 종속되어 ASC 생명주기와 함께 정리되도록 한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(StrangeCandlestick))]
public class StrangeCandlestickAttackRunner : MonoBehaviour, IMobPatternRunner
{
    /// <summary>
    /// 책임 :
    /// - StrangeCandlestick 공격 1회 실행에 필요한 최소 런타임 문맥을 담는다.
    /// - 타깃과 락온 유지 시간처럼 매 실행마다 달라지는 값만 전달한다.
    /// </summary>
    public readonly struct AttackContext
    {
        public readonly GameObject TargetObject;
        public readonly float DelaySeconds;

        public AttackContext(GameObject targetObject, float delaySeconds)
        {
            TargetObject = targetObject;
            DelaySeconds = delaySeconds;
        }
    }

    [SerializeField] private StrangeCandlestick owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private AttackTelegraphService telegraphService;

    private bool isRunning;
    private bool cancelRequested;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<StrangeCandlestick>();

        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();

        if (telegraphService == null)
            telegraphService = GetComponent<AttackTelegraphService>();
    }

    public IEnumerator Run(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (owner == null)
            yield break;

        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this))
            yield break;

        if (!owner.TryCreateAttackContext(initialTarget, out AttackContext context))
        {
            abilityCoordinator?.EndRunner(this);
            yield break;
        }

        isRunning = true;
        cancelRequested = false;
        ShowWarning(context);

        float elapsed = 0f;

        try
        {
            while (elapsed < context.DelaySeconds)
            {
                if (IsCancelled(spec) || cancelRequested || !owner.CanContinueAttack(context.TargetObject))
                    yield break;

                UpdateWarning(context);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (IsCancelled(spec) || cancelRequested || !owner.CanContinueAttack(context.TargetObject))
                yield break;

            HideWarning();
            owner.FireProjectile(context.TargetObject);
        }
        finally
        {
            HideWarning();
            cancelRequested = false;
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    public void Cancel()
    {
        cancelRequested = true;
        HideWarning();
    }

    private void ShowWarning(AttackContext context)
    {
        if (telegraphService == null)
            return;

        telegraphService.Show(owner.MakeLockOnSpec(context.TargetObject));
    }

    private void UpdateWarning(AttackContext context)
    {
        if (telegraphService == null)
            return;

        AttackTelegraphSpec spec = owner.MakeLockOnSpec(context.TargetObject);
        if (telegraphService.HasActiveTelegraph)
            telegraphService.UpdateCurrentGeometry(spec);
        else
            telegraphService.Show(spec);
    }

    private void HideWarning()
    {
        if (telegraphService != null)
            telegraphService.HideCurrent();
    }

    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec != null && spec.Token != null && spec.Token.IsCancelled;
    }
}

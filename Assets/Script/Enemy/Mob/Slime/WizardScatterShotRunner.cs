using System.Collections;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(Wizard))]
public class WizardScatterShotRunner : MonoBehaviour, IMobPatternRunner
{
    [SerializeField] private Wizard owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;

    private bool isRunning;
    private bool cancelRequested;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<Wizard>();

        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();
    }

    /// <summary>마법사의 산탄 발사를 한 번 실행합니다.</summary>
    public IEnumerator Run(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (owner == null) yield break;
        if (!owner.TryBuildShotContext(system, spec, initialTarget, out Wizard.ScatterShotContext context)) yield break;
        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this)) yield break;

        isRunning = true;
        cancelRequested = false;

        try
        {
            if (cancelRequested || owner.IsDead || IsCancelled(spec)) yield break;

            owner.FireScatterShot(context);
            yield return null;
        }
        finally
        {
            cancelRequested = false;
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    /// <summary>실행 중인 산탄 공격을 취소 상태로 바꿉니다.</summary>
    public void Cancel()
    {
        cancelRequested = true;
    }

    /// <summary>어빌리티 취소 여부를 확인합니다.</summary>
    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec != null && spec.Token != null && spec.Token.IsCancelled;
    }
}

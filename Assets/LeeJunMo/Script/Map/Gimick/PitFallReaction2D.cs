using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 이 컴포넌트가 붙은 전투 오브젝트만 HoleTrap 공통 낙하 파이프라인에 참여하게 한다.
/// - 낙하 시작 시 이동/패턴을 정리하고, 낙하 완료 후 사망 명령 또는 대상별 구덩이 후처리로 넘긴다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PitFallReaction2D : MonoBehaviour, IPitFallReaction
{
    [Header("Behavior")]
    [SerializeField] private bool canReact = true;
    [SerializeField] private bool stopMovementOnStart = true;
    [SerializeField] private bool cancelMobAbilityOnStart = true;
    [SerializeField] private bool requestDeathOnComplete = true;

    [Header("Pit Fall Executor")]
    [SerializeField] private bool useDefaultRespawn;
    [SerializeField] private bool removeFallingEffectOnComplete;

    private ICombatDeathCommand deathCommand;
    private IPitFallDeathHandler deathHandler;
    private MovementMotor2D movementMotor;
    private AbilityMotionController2D motionController;
    private MobAbilityCoordinator mobAbilityCoordinator;
    private Rigidbody2D body;
    private Enemy enemy;
    private bool isPitFallActive;

    public bool UseDefaultRespawn => useDefaultRespawn;
    public bool RemoveFallingEffectOnComplete => removeFallingEffectOnComplete;
    public bool IsPitFallActive => isPitFallActive;

    private void Awake()
    {
        CacheTargets();
    }

    /// <summary>구덩이 낙하 처리에 필요한 대상 컴포넌트를 부모 체인에서 찾습니다.</summary>
    private void CacheTargets()
    {
        ResolveInterfaceTargets();
        movementMotor = GetComponentInParent<MovementMotor2D>();
        motionController = GetComponentInParent<AbilityMotionController2D>();
        mobAbilityCoordinator = GetComponentInParent<MobAbilityCoordinator>();
        body = GetComponentInParent<Rigidbody2D>();
        enemy = GetComponentInParent<Enemy>();
    }

    /// <summary>Unity 버전별 interface GetComponent 지원 차이를 피하기 위해 MonoBehaviour 부모 체인에서 인터페이스를 직접 찾습니다.</summary>
    private void ResolveInterfaceTargets()
    {
        MonoBehaviour[] behaviours = GetComponentsInParent<MonoBehaviour>(includeInactive: true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            deathCommand ??= behaviour as ICombatDeathCommand;
            deathHandler ??= behaviour as IPitFallDeathHandler;

            if (deathCommand != null && deathHandler != null)
                return;
        }
    }

    public bool CanReactToPitFall(HoleTrap trap)
    {
        if (!isActiveAndEnabled || !canReact)
            return false;

        if (isPitFallActive)
            return false;

        if (enemy != null && enemy.IsDead)
            return false;

        return deathHandler != null || deathCommand != null || useDefaultRespawn;
    }

    public void OnPitFallStarted(PitFallContext context)
    {
        isPitFallActive = true;

        if (cancelMobAbilityOnStart)
            mobAbilityCoordinator?.CancelActiveAbility(true);

        if (stopMovementOnStart)
        {
            movementMotor?.StopAllMotion();
            motionController?.CancelMotion();
            ResetBodyVelocity();
        }
    }

    public void OnPitFallCompleted(PitFallContext context)
    {
        isPitFallActive = false;

        if (!requestDeathOnComplete)
            return;

        if (deathHandler != null)
        {
            deathHandler.HandlePitFallDeath(context);
            return;
        }

        deathCommand?.RequestDeath(context.TrapObject);
    }

    private void OnDisable()
    {
        isPitFallActive = false;
    }

    /// <summary>낙하 연출 중 기존 이동 관성이 남아 있지 않도록 Rigidbody2D 속도를 초기화합니다.</summary>
    private void ResetBodyVelocity()
    {
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }
}

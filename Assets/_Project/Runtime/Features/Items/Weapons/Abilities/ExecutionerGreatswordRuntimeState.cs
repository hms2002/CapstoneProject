using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 대검 처형자의 처형 대기 여부와 처형 성공 가능 상태를 런타임으로 보관한다.
/// - 준비, 기본 공격 적중, 성공/실패 분기 AD가 일어났을 때 처형 창을 열고 닫는 상태 전이를 담당한다.
/// - 최근 적중 대상과 적중 위치를 저장해 후속 분기나 실행기에서 참조할 수 있는 최소 전투 문맥을 제공한다.
/// </summary>
public sealed class ExecutionerGreatswordRuntimeState : WeaponAbilityRuntimeState
{
    private const string HitConfirmTagResourcePath = "Tags/Event.HitConfirm";

    [Header("Executioner State")]
    [SerializeField] private bool startWaitingForExecution;
    [SerializeField] private bool grantExecuteAfterAttack = true;

    private static GameplayTag hitConfirmRootTag;

    private bool isWaitingForExecutionWindow;
    private bool canExecute;
    private bool hasRecentHitConfirm;
    private GameObject lastHitTarget;
    private Vector3 lastHitWorldPosition;
    private bool lastHitWasCritical;

    public bool IsWaitingForExecutionWindow => isWaitingForExecutionWindow;
    public bool CanExecute => canExecute;
    public bool HasRecentHitConfirm => hasRecentHitConfirm;
    public GameObject LastHitTarget => lastHitTarget;
    public Vector3 LastHitWorldPosition => lastHitWorldPosition;
    public bool LastHitWasCritical => lastHitWasCritical;

    private void Awake()
    {
        ResetState();
    }

    private void OnEnable()
    {
        ResetState();
    }

    public override void HandleEquippedWeaponChanged(WeaponDefinition previousWeapon, WeaponDefinition newWeapon)
    {
        ResetState();
    }

    public override bool TrySelectAbility(
        WeaponDefinition weapon,
        WeaponAbilitySlot slot,
        out AbilityDefinition ability)
    {
        ability = null;
        return false;
    }

    public override void HandleAbilityActivated(
        WeaponDefinition weapon,
        WeaponAbilitySlot slot,
        AbilityDefinition activatedAbility)
    {
        if (weapon == null || activatedAbility == null)
            return;

        if (weapon.abilityLoadout is not ExecutionerGreatswordLoadout loadout)
            return;

        if (activatedAbility == loadout.ExecutionReadyAttack)
        {
            BeginExecutionWindow();
            return;
        }

        if (activatedAbility == loadout.ExecutionFinish || activatedAbility == loadout.ExecutionFallback)
        {
            ResetState();
            return;
        }
    }

    public override void HandleGameplayEvent(WeaponDefinition weapon, GameplayTag tag, in AbilityEventData data)
    {
        if (weapon == null || tag == null || data.Spec?.Definition == null)
            return;

        if (weapon.abilityLoadout is not ExecutionerGreatswordLoadout loadout)
            return;

        if (!MatchesHitConfirmTag(tag))
            return;

        if (!isWaitingForExecutionWindow || !grantExecuteAfterAttack)
            return;

        if (data.Spec.Definition != loadout.BaseAttack)
            return;

        hasRecentHitConfirm = true;
        canExecute = true;
        lastHitTarget = data.Target;
        lastHitWorldPosition = data.WorldPosition;
        lastHitWasCritical = data.IsCriticalHit;
    }

    /// <summary>
    /// 책임 :
    /// - 처형 준비 AD가 성공했을 때 처형 대기 창을 열고 후속 공격 기반 분기를 위한 초기 상태를 만든다.
    /// - 실제 적중 전까지는 Finish 대신 Fallback으로 분기되도록 canExecute와 최근 적중 정보를 초기화한다.
    /// </summary>
    private void BeginExecutionWindow()
    {
        isWaitingForExecutionWindow = true;
        canExecute = false;
        hasRecentHitConfirm = false;
        lastHitTarget = null;
        lastHitWorldPosition = Vector3.zero;
        lastHitWasCritical = false;
    }

    private void ResetState()
    {
        isWaitingForExecutionWindow = startWaitingForExecution;
        canExecute = false;
        hasRecentHitConfirm = false;
        lastHitTarget = null;
        lastHitWorldPosition = Vector3.zero;
        lastHitWasCritical = false;
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

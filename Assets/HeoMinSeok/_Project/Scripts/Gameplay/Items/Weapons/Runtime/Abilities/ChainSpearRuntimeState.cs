using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 사슬창의 연결 대기, 연결 유지, 최근 연결 대상 정보를 런타임으로 보관한다.
/// - 연결 시작 스킬 이후 첫 실제 적중으로 연결 상태를 확정하고, 당기기/회수 실행 뒤에는 상태를 정리한다.
/// </summary>
public sealed class ChainSpearRuntimeState : WeaponAbilityRuntimeState
{
    [Header("Chain Spear State")]
    [SerializeField] private bool startAwaitingLinkHit;

    private bool isAwaitingLinkHit;
    private bool hasLinkedTarget;
    private GameObject linkedTarget;
    private Vector3 linkedWorldPosition;
    private bool linkedHitWasCritical;

    public bool IsAwaitingLinkHit => isAwaitingLinkHit;
    public bool HasLinkedTarget => hasLinkedTarget;
    public GameObject LinkedTarget => linkedTarget;
    public Vector3 LinkedWorldPosition => linkedWorldPosition;
    public bool LinkedHitWasCritical => linkedHitWasCritical;

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

        if (weapon.abilityLoadout is not ChainSpearLoadout loadout)
            return;

        if (activatedAbility == loadout.ChainPull || activatedAbility == loadout.ChainRecall)
        {
            ClearLinkedTarget();
        }
    }

    /// <summary>
    /// 책임 :
    /// - 사슬 던지기 executor가 시작될 때 다음 실제 적중을 연결 대상으로 해석할 준비 상태를 연다.
    /// - 이전에 남아 있던 연결 대상 정보는 초기화해 새로운 링크가 확정되기 전까지 빈 상태를 유지한다.
    /// </summary>
    public void BeginAwaitingLinkHit()
    {
        isAwaitingLinkHit = true;
        hasLinkedTarget = false;
        linkedTarget = null;
        linkedWorldPosition = Vector3.zero;
        linkedHitWasCritical = false;
    }

    /// <summary>
    /// 책임 :
    /// - 사슬 던지기 executor가 실제 적중을 받았을 때 연결 대상, 위치, 치명타 여부를 런타임 상태에 기록한다.
    /// - 연결이 확정되면 대기 상태를 닫아 selector가 Pull/Recall 분기로 넘어갈 수 있게 만든다.
    /// </summary>
    public void ConfirmLinkedTarget(GameObject target, Vector3 worldPosition, bool wasCritical)
    {
        hasLinkedTarget = true;
        isAwaitingLinkHit = false;
        linkedTarget = target;
        linkedWorldPosition = worldPosition;
        linkedHitWasCritical = wasCritical;
    }

    /// <summary>
    /// 책임 :
    /// - 당기기나 회수처럼 연결을 소비하는 액션 이후 링크 상태를 깨끗하게 비워 다음 연결 시도를 준비한다.
    /// - 대기 중 플래그도 함께 끄어 이미 소비된 링크가 후속 입력에 재사용되지 않게 한다.
    /// </summary>
    public void ClearLinkedTarget()
    {
        isAwaitingLinkHit = false;
        hasLinkedTarget = false;
        linkedTarget = null;
        linkedWorldPosition = Vector3.zero;
        linkedHitWasCritical = false;
    }

    private void ResetState()
    {
        isAwaitingLinkHit = startAwaitingLinkHit;
        hasLinkedTarget = false;
        linkedTarget = null;
        linkedWorldPosition = Vector3.zero;
        linkedHitWasCritical = false;
    }
}

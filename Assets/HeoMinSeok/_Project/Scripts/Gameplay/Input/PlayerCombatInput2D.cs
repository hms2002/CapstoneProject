using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 플레이어의 공격, 스킬, 대시, 무기 스왑 입력을 AbilitySystem과 WeaponInventory에 전달한다.
/// - block tag 상태를 확인해 UI나 특수 상태에서 전투 조작이 들어가지 않도록 차단한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCombatInput2D : MonoBehaviour, IAbilityGameplayEventListener
{
    private const string AttackBlockedTagResourcePath = "Tags/State.Attacking.Blocked";
    private const string SkillBlockedTagResourcePath = "Tags/State.Skill.Blocked";

    [Header("Refs")]
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private WeaponInventory2D weaponInventory;
    [SerializeField] private WeaponEquipController weaponEquipController;
    [SerializeField] private WeaponExecutorRunner weaponExecutorRunner;
    [SerializeField] private PlayerInteractor2D player;
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private AbilityGameplayEventRelay gameplayEventRelay;

    [Header("Movement Ability")]
    [SerializeField] private AbilityDefinition dash;

    [Header("Attack Input (Hold)")]
    [SerializeField] private float attackRepeatInterval = 0.06f;
    [SerializeField] private float reAimGapAfterAttackEnd = 0.06f;

    [Header("Gameplay Events")]
    [SerializeField] private GameplayTag attackPressedEvent;
    [SerializeField] private GameplayTag attackReleasedEvent;

    [Header("Block Tags")]
    [SerializeField] private GameplayTag attackBlockedTag;
    [SerializeField] private GameplayTag skillBlockedTag;

    private float nextAutoAttackTime;
    private bool wasBusyLastFrame;
    private bool isHoldingAttack;
    private WeaponAbilitySelector weaponAbilitySelector;
    private WeaponAbilityBridge weaponAbilityBridge;

    private void Awake()
    {
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (weaponInventory == null) weaponInventory = GetComponent<WeaponInventory2D>();
        if (weaponEquipController == null) weaponEquipController = GetComponentInChildren<WeaponEquipController>(true);
        if (weaponEquipController == null && weaponInventory != null) weaponEquipController = weaponInventory.EquipController;
        if (weaponExecutorRunner == null) weaponExecutorRunner = GetComponent<WeaponExecutorRunner>();
        if (player == null) player = GetComponent<PlayerInteractor2D>();
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (gameplayEventRelay == null) gameplayEventRelay = GetComponent<AbilityGameplayEventRelay>();
        if (gameplayEventRelay == null && abilitySystem != null) gameplayEventRelay = gameObject.AddComponent<AbilityGameplayEventRelay>();
        if (weaponExecutorRunner == null) weaponExecutorRunner = gameObject.AddComponent<WeaponExecutorRunner>();
        if (attackBlockedTag == null) attackBlockedTag = Resources.Load<GameplayTag>(AttackBlockedTagResourcePath);
        if (skillBlockedTag == null) skillBlockedTag = Resources.Load<GameplayTag>(SkillBlockedTagResourcePath);

        weaponAbilitySelector = new WeaponAbilitySelector(weaponInventory, weaponEquipController);
        weaponAbilityBridge = new WeaponAbilityBridge(abilitySystem, weaponExecutorRunner);
    }

    private void OnEnable()
    {
        if (weaponInventory != null)
            weaponInventory.OnEquippedChanged += HandleEquippedChanged;

        gameplayEventRelay?.Register(this);
    }

    private void OnDisable()
    {
        if (weaponInventory != null)
            weaponInventory.OnEquippedChanged -= HandleEquippedChanged;

        gameplayEventRelay?.Unregister(this);
    }

    private void Update()
    {
        InputBindingService input = InputBindingService.EnsureInstance();
        SyncAttackHoldWithRealInput(input);

        if (player != null && player.CurrentState != InteractState.Idle)
            return;

        if (IsCombatBlocked())
        {
            TryHandleBlockedWeaponAbilityInput(input);
            ReleaseAttackHoldIfNeeded();
            return;
        }

        HandleCombatInput();
    }

    private void HandleCombatInput()
    {
        InputBindingService input = InputBindingService.EnsureInstance();
        var atk = GetBasicAttack();

        if (input.WasPressedThisFrame(InputActionId.PrimaryAttack))
        {
            isHoldingAttack = true;
            SendGameplayEventSafe(attackPressedEvent);
            nextAutoAttackTime = 0f;

            if (atk != null)
                TryActivateSafe(WeaponAbilitySlot.Attack, atk);
        }

        if (input.WasReleasedThisFrame(InputActionId.PrimaryAttack))
        {
            isHoldingAttack = false;
            SendGameplayEventSafe(attackReleasedEvent);
        }

        if (weaponAbilityBridge != null)
        {
            bool busyNow = weaponAbilityBridge.IsBusy;

            if (wasBusyLastFrame && !busyNow)
                nextAutoAttackTime = Time.time + reAimGapAfterAttackEnd;

            wasBusyLastFrame = busyNow;
        }

        if (isHoldingAttack && atk != null && weaponAbilityBridge != null)
        {
            if (!weaponAbilityBridge.IsBusy && Time.time >= nextAutoAttackTime)
            {
                if (TryActivateSafe(WeaponAbilitySlot.Attack, atk))
                {
                    nextAutoAttackTime = 0f;
                }
                else
                {
                    float nextActivationRemaining = weaponAbilityBridge.GetNextActivationRemaining(atk);
                    nextAutoAttackTime = Time.time + (
                        nextActivationRemaining > 0f
                            ? nextActivationRemaining
                            : attackRepeatInterval);
                }
            }
        }

        if (input.WasPressedThisFrame(InputActionId.Skill1)) TryActivateSafe(WeaponAbilitySlot.Skill1, GetSkill1());
        if (input.WasPressedThisFrame(InputActionId.Skill2)) TryActivateSafe(WeaponAbilitySlot.Skill2, GetSkill2());
        if (input.WasPressedThisFrame(InputActionId.Dash)) TryActivateSafe(default, dash);

        if (weaponInventory != null && input.WasPressedThisFrame(InputActionId.SwapWeapon))
            weaponInventory.Swap();
    }

    private void TryHandleBlockedWeaponAbilityInput(InputBindingService input)
    {
        if (input == null)
            return;

        if (input.WasPressedThisFrame(InputActionId.Skill1))
            TryHandleCurrentWeaponAbilityInput(WeaponAbilitySlot.Skill1, GetSkill1());
    }

    private bool IsCombatBlocked()
    {
        if (tagSystem == null)
            return false;

        bool attackBlocked = attackBlockedTag != null && tagSystem.HasTag(attackBlockedTag);
        bool skillBlocked = skillBlockedTag != null && tagSystem.HasTag(skillBlockedTag);
        return attackBlocked || skillBlocked;
    }

    /// <summary>
    /// 책임 : UI 잠금 등으로 공격 입력이 차단될 때 홀드 상태와 release 이벤트를 안전하게 정리한다.
    /// </summary>
    private void ReleaseAttackHoldIfNeeded()
    {
        if (!isHoldingAttack)
            return;

        isHoldingAttack = false;
        nextAutoAttackTime = 0f;
        SendGameplayEventSafe(attackReleasedEvent);
    }

    /// <summary>
    /// 책임 :
    /// - Update 초반에 실제 입력 눌림 상태와 내부 홀드 캐시를 동기화한다.
    /// - UI/상호작용/상태 전환 중 KeyUp 이벤트를 놓쳐도 자동 공격이 고착되지 않게 막는다.
    /// </summary>
    private void SyncAttackHoldWithRealInput(InputBindingService input)
    {
        if (!isHoldingAttack || input == null)
            return;

        if (!input.IsPressed(InputActionId.PrimaryAttack))
            ReleaseAttackHoldIfNeeded();
    }

    /// <summary>
    /// 책임 :
    /// - 무기 장착/교체 직후 이전 무기에서 유지되던 공격 홀드 캐시를 끊는다.
    /// - 무기 픽업이나 스왑 이후 새 기본 공격이 자동 연타되는 현상을 방지한다.
    /// </summary>
    private void HandleEquippedChanged(int previousIndex, int newIndex, WeaponDefinition previousWeapon, WeaponDefinition newWeapon)
    {
        if (weaponEquipController == null && weaponInventory != null)
            weaponEquipController = weaponInventory.EquipController;

        weaponAbilityBridge?.ForceStopActiveExecutor(WeaponExecutorEndReason.WeaponSwapped);

        WeaponAbilityRuntimeState runtimeState = weaponEquipController != null
            ? weaponEquipController.GetCurrentWeaponRuntimeState()
            : null;

        if (runtimeState != null)
            runtimeState.HandleEquippedWeaponChanged(previousWeapon, newWeapon);

        ReleaseAttackHoldIfNeeded();
    }

    private bool TryActivateSafe(WeaponAbilitySlot slot, AbilityDefinition def)
    {
        if (def == null || weaponAbilityBridge == null) return false;

        if (TryHandleCurrentWeaponAbilityInput(slot, def))
            return true;

        bool activated = weaponAbilityBridge.TryActivate(def, null);
        if (activated)
            NotifyCurrentWeaponAbilityActivated(slot, def);
        else
            NotifyCurrentWeaponAbilityActivationRejected(slot, def);

        return activated;
    }

    private bool TryHandleCurrentWeaponAbilityInput(WeaponAbilitySlot slot, AbilityDefinition ability)
    {
        if (weaponInventory == null || weaponEquipController == null)
            return false;

        WeaponDefinition activeWeapon = weaponInventory.ActiveWeapon;
        if (activeWeapon == null)
            return false;

        WeaponAbilityRuntimeState runtimeState = weaponEquipController.GetCurrentWeaponRuntimeState();
        return runtimeState != null && runtimeState.TryHandleAbilityInput(activeWeapon, slot, ability);
    }

    /// <summary>
    /// 책임 :
    /// - 현재 장착 무기의 WeaponAbilityRuntimeState에 성공 발동 사실을 전달한다.
    /// - 선택 토글, 콤보 진전 같은 무기 내부 상태를 ASC 세부사항과 분리된 경계에서 갱신한다.
    /// </summary>
    private void NotifyCurrentWeaponAbilityActivated(WeaponAbilitySlot slot, AbilityDefinition activatedAbility)
    {
        if (weaponInventory == null || weaponEquipController == null)
            return;

        WeaponDefinition activeWeapon = weaponInventory.ActiveWeapon;
        if (activeWeapon == null)
            return;

        WeaponAbilityRuntimeState runtimeState = weaponEquipController.GetCurrentWeaponRuntimeState();
        if (runtimeState == null)
            return;

        runtimeState.HandleAbilityActivated(activeWeapon, slot, activatedAbility);
    }

    private void NotifyCurrentWeaponAbilityActivationRejected(WeaponAbilitySlot slot, AbilityDefinition rejectedAbility)
    {
        if (weaponInventory == null || weaponEquipController == null)
            return;

        WeaponDefinition activeWeapon = weaponInventory.ActiveWeapon;
        if (activeWeapon == null)
            return;

        WeaponAbilityRuntimeState runtimeState = weaponEquipController.GetCurrentWeaponRuntimeState();
        if (runtimeState == null)
            return;

        runtimeState.HandleAbilityActivationRejected(activeWeapon, slot, rejectedAbility);
    }

    private void SendGameplayEventSafe(GameplayTag tag)
    {
        if (weaponAbilityBridge == null || tag == null) return;
        weaponAbilityBridge.SendGameplayEvent(tag);
    }

    private AbilityDefinition GetBasicAttack()
    {
        if (weaponAbilitySelector == null) return null;
        return weaponAbilitySelector.ResolveAbility(WeaponAbilitySlot.Attack);
    }

    private AbilityDefinition GetSkill1()
    {
        if (weaponAbilitySelector == null) return null;
        return weaponAbilitySelector.ResolveAbility(WeaponAbilitySlot.Skill1);
    }

    private AbilityDefinition GetSkill2()
    {
        if (weaponAbilitySelector == null) return null;
        return weaponAbilitySelector.ResolveAbility(WeaponAbilitySlot.Skill2);
    }

    /// <summary>
    /// 책임 :
    /// - ASC 이벤트 relay가 전달한 gameplay event를 현재 장착 무기의 runtime state로 넘긴다.
    /// - 입력 계층은 현재 무기 경계를 알고 있으므로, runtime state가 직접 ASC를 구독하지 않도록 브리지 역할을 맡는다.
    /// </summary>
    public void HandleGameplayEvent(GameplayTag tag, in AbilityEventData data)
    {
        if (weaponInventory == null || weaponEquipController == null)
            return;

        WeaponDefinition activeWeapon = weaponInventory.ActiveWeapon;
        if (activeWeapon == null)
            return;

        WeaponAbilityRuntimeState runtimeState = weaponEquipController.GetCurrentWeaponRuntimeState();
        if (runtimeState == null)
            return;

        runtimeState.HandleGameplayEvent(activeWeapon, tag, data);
    }
}

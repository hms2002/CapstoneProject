using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 플레이어의 공격, 스킬, 대시, 무기 스왑 입력을 AbilitySystem과 WeaponInventory에 전달한다.
/// - UI/대화/연출 흐름과 block tag 상태를 확인해 전투 조작이 들어가지 않도록 차단한다.
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

    private bool meleeControlLockActive;
    private bool meleeAnimationObserved;
    private bool meleeAnimationFinished;
    private int meleeStartFrame;
    private int meleeTickFrame = -1;
    private int meleePreviousStateHash;
    private float meleePreviousNormalizedTime;
    private int meleeAttackStateHash;
    private float meleeFallbackRemaining;
    private Animator meleeLockAnimator;
    private AbilityMotionController2D meleeMotion;

    public bool IsMeleeControlLocked
    {
        get
        {
            RefreshMeleeControlLock();
            return isActiveAndEnabled && meleeControlLockActive;
        }
    }

    private void BeginMeleeControlLock(WeaponAbilitySlot slot, AbilityDefinition definition,
        int previousStateHash, float previousNormalizedTime)
    {
        if (slot != WeaponAbilitySlot.Attack || definition != GetBasicAttack())
        {
            meleeControlLockActive = false;
            return;
        }
        // These two current weapons fire ranged basic attacks.
        if (definition.logic is UnityGAS.Sample.AbilityLogic_OddIronShot ||
            definition.logic is AbilityLogic_CrimsonBoundaryAttack) return;
        meleeControlLockActive = true;
        meleeAnimationObserved = meleeAnimationFinished = false;
        meleeStartFrame = Time.frameCount;
        meleeTickFrame = -1;
        meleePreviousStateHash = previousStateHash;
        meleePreviousNormalizedTime = previousNormalizedTime;
        meleeLockAnimator = definition.animationChannel == AbilityDefinition.AnimationChannel.Weapon
            ? abilitySystem.WeaponAnimator : abilitySystem.PlayerAnimator;
        meleeMotion = abilitySystem.GetComponent<AbilityMotionController2D>();
        float recovery = definition.recoveryTime / Mathf.Max(0.0001f, AbilityAttackSpeedResolver.ResolveFinalAttackSpeed(abilitySystem));
        meleeFallbackRemaining = 0.8f * Mathf.Max(0.02f, recovery,
            weaponAbilityBridge.GetNextActivationRemaining(definition));
    }

    private void RefreshMeleeControlLock()
    {
        if (!meleeControlLockActive || meleeTickFrame == Time.frameCount) return;
        meleeTickFrame = Time.frameCount;
        if (Time.frameCount <= meleeStartFrame) return;
        if (!CombatHitPause2D.IsPausedOn(gameObject))
            meleeFallbackRemaining -= Time.deltaTime;
        if (meleeLockAnimator != null && meleeLockAnimator.isActiveAndEnabled &&
            meleeLockAnimator.runtimeAnimatorController != null)
        {
            AnimatorStateInfo state = meleeLockAnimator.IsInTransition(0)
                ? meleeLockAnimator.GetNextAnimatorStateInfo(0)
                : meleeLockAnimator.GetCurrentAnimatorStateInfo(0);
            if (!meleeAnimationObserved && !state.loop &&
                (state.fullPathHash != meleePreviousStateHash || state.normalizedTime < meleePreviousNormalizedTime))
            {
                meleeAnimationObserved = true;
                meleeAttackStateHash = state.fullPathHash;
            }
            if (meleeAnimationObserved && (state.fullPathHash != meleeAttackStateHash || state.normalizedTime >= 0.8f))
                meleeAnimationFinished = true;
        }
        bool motionPending = meleeAnimationObserved ? !meleeAnimationFinished : meleeFallbackRemaining > 0f;
        bool lunging = meleeMotion != null && meleeMotion.IsLunging;
        if (!motionPending && !lunging)
        {
            meleeControlLockActive = false;
            // Let aim/movement update in the released tail before a held input starts the next swing.
            nextAutoAttackTime = Mathf.Max(nextAutoAttackTime, Time.time + reAimGapAfterAttackEnd);
        }
    }

    private float nextAutoAttackTime;
    private readonly HashSet<AbilityDefinition> knownBasicAttackAbilities = new();
    private bool wasBusyLastFrame;
    private bool isHoldingAttack;
    private WeaponAbilitySelector weaponAbilitySelector;
    private WeaponAbilityBridge weaponAbilityBridge;
    private AbilityDefinition pendingApprenticeSkill;
    private WeaponAbilitySlot pendingApprenticeSlot;
    private AbilitySpec pendingApprenticeAttack;
    private bool apprenticeCancelRequested;

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
        meleeControlLockActive = false;
        ClearApprenticeSkillInput();
        if (weaponInventory != null)
            weaponInventory.OnEquippedChanged -= HandleEquippedChanged;

        gameplayEventRelay?.Unregister(this);
    }

    private void Update()
    {
        SyncAttackHoldWithRealInput();
        RefreshMeleeControlLock();

        if (IsGameplayInputBlockedByUiOrFlow())
        {
            meleeControlLockActive = false;
            ClearApprenticeSkillInput();
            ReleaseAttackHoldIfNeeded();
            return;
        }

        if (TimeScalePausePlayback.IsPaused) return;

        if (player != null && player.CurrentState != InteractState.Idle)
        {
            meleeControlLockActive = false;
            ClearApprenticeSkillInput();
            ReleaseAttackHoldIfNeeded();
            return;
        }

        if (IsCombatBlocked())
        {
            ClearApprenticeSkillInput();
            TryHandleBlockedWeaponAbilityInput();
            ReleaseAttackHoldIfInputEnded();
            return;
        }

        HandleCombatInput();
    }

    private void HandleCombatInput()
    {
        // Resolve this weapon's manual skill before the held basic attack can restart.
        if (HandleApprenticeSkillInput())
        {
            if (InputActionQuery.WasPressedThisFrame(InputActionId.Dash))
            {
                ClearApprenticeSkillInput();
                TryActivateSafe(default, dash);
            }
            if (weaponInventory != null && InputActionQuery.WasPressedThisFrame(InputActionId.SwapWeapon))
                weaponInventory.Swap();
            return;
        }

        var atk = GetBasicAttack();

        if (InputActionQuery.WasPressedThisFrame(InputActionId.PrimaryAttack))
        {
            isHoldingAttack = true;
            SendGameplayEventSafe(attackPressedEvent);
            nextAutoAttackTime = 0f;

            if (atk != null)
                TryActivateSafe(WeaponAbilitySlot.Attack, atk);
        }

        if (InputActionQuery.WasReleasedThisFrame(InputActionId.PrimaryAttack))
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
            if (!weaponAbilityBridge.IsBusy && !IsMeleeControlLocked && Time.time >= nextAutoAttackTime)
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

        if (InputActionQuery.WasPressedThisFrame(InputActionId.Skill1)) TryActivateSafe(WeaponAbilitySlot.Skill1, GetSkill1());
        if (InputActionQuery.WasPressedThisFrame(InputActionId.Skill2)) TryActivateSafe(WeaponAbilitySlot.Skill2, GetSkill2());
        if (InputActionQuery.WasPressedThisFrame(InputActionId.Dash)) TryActivateSafe(default, dash);

        if (weaponInventory != null && InputActionQuery.WasPressedThisFrame(InputActionId.SwapWeapon))
            weaponInventory.Swap();
    }

    private void TryHandleBlockedWeaponAbilityInput()
    {
        if (InputActionQuery.WasPressedThisFrame(InputActionId.Skill1))
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

    private static bool IsGameplayInputBlockedByUiOrFlow()
    {
        if (DialoguePlayback.IsPlaying)
            return true;

        if (UiInteractionStateQuery.HasBlockingUI())
            return true;

        if (SceneTransitionPlayback.IsTransitionActive)
            return true;

        return LoadingPresentationQuery.IsActiveLoadingPresentation;
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
    /// - 피격 경직처럼 짧은 전투 차단 상태에서는 실제 공격 입력이 유지되는 한 홀드 판정을 보존한다.
    /// - 실제 입력이 끝난 경우에만 release 이벤트를 보내 차지/홀드 무기 상태가 고착되지 않게 한다.
    /// </summary>
    private void ReleaseAttackHoldIfInputEnded()
    {
        if (!isHoldingAttack)
            return;

        if (InputActionQuery.IsPressed(InputActionId.PrimaryAttack))
            return;

        ReleaseAttackHoldIfNeeded();
    }

    /// <summary>
    /// 책임 :
    /// - Update 초반에 실제 입력 눌림 상태와 내부 홀드 캐시를 동기화한다.
    /// - UI/상호작용/상태 전환 중 KeyUp 이벤트를 놓쳐도 자동 공격이 고착되지 않게 막는다.
    /// </summary>
    private void SyncAttackHoldWithRealInput()
    {
        if (!isHoldingAttack)
            return;

        if (!InputActionQuery.IsPressed(InputActionId.PrimaryAttack))
            ReleaseAttackHoldIfNeeded();
    }

    /// <summary>
    /// 책임 :
    /// - 무기 장착/교체 직후 이전 무기에서 유지되던 공격 홀드 캐시를 끊는다.
    /// - 무기 픽업이나 스왑 이후 새 기본 공격이 자동 연타되는 현상을 방지한다.
    /// </summary>
    private void HandleEquippedChanged(int previousIndex, int newIndex, WeaponDefinition previousWeapon, WeaponDefinition newWeapon)
    {
        meleeControlLockActive = false;
        ClearApprenticeSkillInput();
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

    private void ClearApprenticeSkillInput()
    {
        pendingApprenticeSkill = null;
        pendingApprenticeAttack = null;
        apprenticeCancelRequested = false;
    }

    private bool HandleApprenticeSkillInput()
    {
        if (abilitySystem == null || GetBasicAttack()?.logic is not AbilityLogic_ApprenticeHeroSwordAttack)
        {
            ClearApprenticeSkillInput();
            return false;
        }

        bool chargePressed = InputActionQuery.WasPressedThisFrame(InputActionId.Skill1);
        bool dashPressed = InputActionQuery.WasPressedThisFrame(InputActionId.Skill2);
        if (chargePressed || dashPressed)
        {
            WeaponAbilitySlot slot = dashPressed ? WeaponAbilitySlot.Skill2 : WeaponAbilitySlot.Skill1;
            AbilityDefinition skill = dashPressed ? GetSkill2() : GetSkill1();
            if (skill != null && abilitySystem.GetNextActivationRemaining(skill) <= 0f &&
                abilitySystem.GetCooldownRemaining(skill) <= 0f &&
                skill.CanActivate(gameObject, null))
            {
                AbilitySpec attack = abilitySystem.CurrentExecSpec;
                if (!abilitySystem.IsBusy || attack?.Definition?.logic is AbilityLogic_ApprenticeHeroSwordAttack)
                {
                    pendingApprenticeSkill = skill;
                    pendingApprenticeSlot = slot;
                    pendingApprenticeAttack = attack;
                    apprenticeCancelRequested = false;
                }
            }
        }

        if (pendingApprenticeSkill == null)
            return false;

        if (pendingApprenticeSlot == WeaponAbilitySlot.Skill1 &&
            !InputActionQuery.IsPressed(InputActionId.Skill1))
        {
            ClearApprenticeSkillInput();
            return false;
        }

        AbilitySpec current = abilitySystem.CurrentExecSpec;
        if (abilitySystem.IsBusy)
        {
            if (current != pendingApprenticeAttack ||
                (!apprenticeCancelRequested && current?.Token != null && current.Token.IsCancelled))
            {
                ClearApprenticeSkillInput();
                return false;
            }

            if (!apprenticeCancelRequested &&
                (pendingApprenticeSlot == WeaponAbilitySlot.Skill2 ||
                 current.GetInt(AbilityLogic_ApprenticeHeroSwordAttack.HitSpawnedKey, 0) != 0))
            {
                apprenticeCancelRequested = true;
                abilitySystem.CancelExecution(force: true);
                // Stop the attack lunge before the next skill starts its own motion.
                GetComponent<AbilityMotionController2D>()?.CancelMotion();
            }

            // Cancellation completes through the normal coroutine cleanup before retrying.
            return true;
        }

        AbilityDefinition pending = pendingApprenticeSkill;
        WeaponAbilitySlot pendingSlot = pendingApprenticeSlot;
        ClearApprenticeSkillInput();
        TryActivateSafe(pendingSlot, pending);
        return true;
    }

    private bool TryActivateSafe(WeaponAbilitySlot slot, AbilityDefinition def)
    {
        if (def == null || weaponAbilityBridge == null) return false;

        Animator attackAnimator = def.animationChannel == AbilityDefinition.AnimationChannel.Weapon
            ? abilitySystem.WeaponAnimator : abilitySystem.PlayerAnimator;
        AnimatorStateInfo previousState = attackAnimator != null && attackAnimator.runtimeAnimatorController != null
            ? attackAnimator.GetCurrentAnimatorStateInfo(0) : default;
        RememberBasicAttack(slot, def);

        if (TryHandleCurrentWeaponAbilityInput(slot, def))
        {
            BeginMeleeControlLock(slot, def, previousState.fullPathHash, previousState.normalizedTime);
            return true;
        }

        bool activated = weaponAbilityBridge.TryActivate(def, null);
        if (activated)
        {
            BeginMeleeControlLock(slot, def, previousState.fullPathHash, previousState.normalizedTime);
            NotifyCurrentWeaponAbilityActivated(slot, def);
        }
        else
            NotifyCurrentWeaponAbilityActivationRejected(slot, def);

        return activated;
    }

    public bool IsKnownBasicAttackAbility(AbilityDefinition ability)
    {
        return ability != null && knownBasicAttackAbilities.Contains(ability);
    }

    private void RememberBasicAttack(WeaponAbilitySlot slot, AbilityDefinition ability)
    {
        if (slot == WeaponAbilitySlot.Attack && ability != null)
            knownBasicAttackAbilities.Add(ability);
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

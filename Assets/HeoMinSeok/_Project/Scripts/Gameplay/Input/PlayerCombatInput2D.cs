using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 플레이어의 공격, 스킬, 대시, 무기 스왑 입력을 AbilitySystem과 WeaponInventory에 전달한다.
/// - block tag 상태를 확인해 UI나 특수 상태에서 전투 조작이 들어가지 않도록 차단한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCombatInput2D : MonoBehaviour
{
    private const string AttackBlockedTagResourcePath = "Tags/State.Attacking.Blocked";
    private const string SkillBlockedTagResourcePath = "Tags/State.Skill.Blocked";

    [Header("Refs")]
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private WeaponInventory2D weaponInventory;
    [SerializeField] private PlayerInteractor2D player;
    [SerializeField] private TagSystem tagSystem;

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

    private void Awake()
    {
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (weaponInventory == null) weaponInventory = GetComponent<WeaponInventory2D>();
        if (player == null) player = GetComponent<PlayerInteractor2D>();
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (attackBlockedTag == null) attackBlockedTag = Resources.Load<GameplayTag>(AttackBlockedTagResourcePath);
        if (skillBlockedTag == null) skillBlockedTag = Resources.Load<GameplayTag>(SkillBlockedTagResourcePath);
    }

    private void Update()
    {
        if (player != null && player.CurrentState != InteractState.Idle)
            return;

        if (IsCombatBlocked())
        {
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
                TryActivateSafe(atk);
        }

        if (input.WasReleasedThisFrame(InputActionId.PrimaryAttack))
        {
            isHoldingAttack = false;
            SendGameplayEventSafe(attackReleasedEvent);
        }

        if (abilitySystem != null)
        {
            bool busyNow = abilitySystem.IsBusy;

            if (wasBusyLastFrame && !busyNow)
                nextAutoAttackTime = Time.time + reAimGapAfterAttackEnd;

            wasBusyLastFrame = busyNow;
        }

        if (isHoldingAttack && atk != null && abilitySystem != null)
        {
            if (!abilitySystem.IsBusy && Time.time >= nextAutoAttackTime)
            {
                nextAutoAttackTime = Time.time + attackRepeatInterval;
                TryActivateSafe(atk);
            }
        }

        if (input.WasPressedThisFrame(InputActionId.Skill1)) TryActivateSafe(GetSkill1());
        if (input.WasPressedThisFrame(InputActionId.Skill2)) TryActivateSafe(GetSkill2());
        if (input.WasPressedThisFrame(InputActionId.Dash)) TryActivateSafe(dash);

        if (weaponInventory != null && input.WasPressedThisFrame(InputActionId.SwapWeapon))
            weaponInventory.Swap();
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
        SendGameplayEventSafe(attackReleasedEvent);
    }

    private void TryActivateSafe(AbilityDefinition def)
    {
        if (def == null || abilitySystem == null) return;
        abilitySystem.TryActivateAbility(def, null);
    }

    private void SendGameplayEventSafe(GameplayTag tag)
    {
        if (abilitySystem == null || tag == null) return;
        abilitySystem.SendGameplayEvent(tag);
    }

    private AbilityDefinition GetBasicAttack()
    {
        if (weaponInventory == null) return null;
        return weaponInventory.GetActiveAbility(WeaponAbilitySlot.Attack);
    }

    private AbilityDefinition GetSkill1()
    {
        if (weaponInventory == null) return null;
        return weaponInventory.GetActiveAbility(WeaponAbilitySlot.Skill1);
    }

    private AbilityDefinition GetSkill2()
    {
        if (weaponInventory == null) return null;
        return weaponInventory.GetActiveAbility(WeaponAbilitySlot.Skill2);
    }
}

using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class PlayerCombatInput2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private WeaponInventory2D weaponInventory;

    [Header("Movement Ability")]
    [SerializeField] private AbilityDefinition dash;

    [Header("Hotkeys")]
    [SerializeField] private KeyCode dashKey = KeyCode.Space;
    [SerializeField] private KeyCode skill1Key = KeyCode.Q;
    [SerializeField] private KeyCode skill2Key = KeyCode.E;
    [SerializeField] private KeyCode swapKey = KeyCode.Tab;

    [Header("Attack Input (Hold)")]
    [SerializeField] private float attackRepeatInterval = 0.06f;
    [SerializeField] private float reAimGapAfterAttackEnd = 0.06f;

    [Header("Gameplay Events")]
    [SerializeField] private GameplayTag attackPressedEvent;
    [SerializeField] private GameplayTag attackReleasedEvent;

    private float nextAutoAttackTime;
    private bool wasBusyLastFrame;
    private bool isHoldingAttack;

    private void Awake()
    {
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (weaponInventory == null) weaponInventory = GetComponent<WeaponInventory2D>();
    }

    private void Update()
    {
        HandleCombatInput();
    }

    private void HandleCombatInput()
    {
        var atk = GetBasicAttack();

        // 1) Press
        if (Input.GetMouseButtonDown(0))
        {
            isHoldingAttack = true;
            SendGameplayEventSafe(attackPressedEvent);
            nextAutoAttackTime = 0f;

            if (atk != null)
                TryActivateSafe(atk);
        }

        // 2) Release
        if (Input.GetMouseButtonUp(0))
        {
            isHoldingAttack = false;
            SendGameplayEventSafe(attackReleasedEvent);
        }

        // 3) Busy -> Idle 전환 감지
        if (abilitySystem != null)
        {
            bool busyNow = abilitySystem.IsBusy;

            if (wasBusyLastFrame && !busyNow)
                nextAutoAttackTime = Time.time + reAimGapAfterAttackEnd;

            wasBusyLastFrame = busyNow;
        }

        // 4) Hold 자동 연타
        if (isHoldingAttack && atk != null && abilitySystem != null)
        {
            if (!abilitySystem.IsBusy && Time.time >= nextAutoAttackTime)
            {
                nextAutoAttackTime = Time.time + attackRepeatInterval;
                TryActivateSafe(atk);
            }
        }

        // Skills
        if (Input.GetKeyDown(skill1Key)) TryActivateSafe(GetSkill1());
        if (Input.GetKeyDown(skill2Key)) TryActivateSafe(GetSkill2());

        // Dash
        if (Input.GetKeyDown(dashKey)) TryActivateSafe(dash);

        // Weapon swap
        if (weaponInventory != null && Input.GetKeyDown(swapKey))
            weaponInventory.Swap();
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
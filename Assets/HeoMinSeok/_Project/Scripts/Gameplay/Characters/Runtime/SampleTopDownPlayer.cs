using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[RequireComponent(typeof(Rigidbody2D))]
public class SampleTopDownPlayer : MonoBehaviour, IPlayerInteractor
{
    public static SampleTopDownPlayer Instance { get; private set; }

    // ---- IPlayerInteractor ----
    public Transform Transform => transform;
    public InteractState CurrentState { get; private set; } = InteractState.Idle;
    public void SetInteractState(InteractState state)
    {
        CurrentState = state;

        // 상태 전환 시 하이라이트 정리(TempPlayer 동작 유지 느낌)
        if (state == InteractState.Talking && currentTarget != null)
        {
            currentTarget.OnUnHighlight();
            currentTarget = null;
        }
    }

    // -----------------------
    // Movement
    // -----------------------
    [Header("Movement")]
    [Tooltip("기본 이동 속도. 최종 속도는 baseMoveSpeed * MoveSpeedMultiplier(CurrentValue)로 계산됩니다.")]
    public float baseMoveSpeed = 6f;

    [Tooltip("이동속도 배수 StatId (권장: MoveSpeedMultiplierFinal, x1 기반). StatTypeBindings가 설정돼 있으면 이 값을 우선 사용합니다.")]
    public StatId moveSpeedMultiplierStatId = StatId.MoveSpeedFinal;

    [Tooltip("Legacy: 이동속도 배수 Attribute (기본값 1 권장). StatTypeBindings가 없을 때 fallback으로 사용합니다.")]
    public AttributeDefinition moveSpeedMultiplierAttribute;

    [Tooltip("이 태그가 있으면 WASD 입력을 무시하고, 마우스 조준 방향(AimDirection)으로 강제 이동합니다.")]
    public GameplayTag forcedMoveTag;

    [Tooltip("이 태그가 있으면 이동 입력 무시")]
    public GameplayTag movementLockedTag;

    // -----------------------
    // Interaction (from TempPlayer)
    // -----------------------
    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    private readonly List<IInteractable> nearbyObjects = new();
    private IInteractable currentTarget;

    // -----------------------
    // Combat / GAS
    // -----------------------
    [Header("GAS / Combat")]
    public AbilitySystem abilitySystem;
    public TagSystem tagSystem;
    public Camera mainCamera;

    [Tooltip("이 태그가 있으면 Hand 회전(에임) 무시")]
    public GameplayTag aimLockedTag;

    [Header("Weapon Inventory (Optional)")]
    public WeaponInventory2D weaponInventory;

    [Header("Fallback Abilities (No Inventory)")]
    public AbilityDefinition basicAttack;
    public AbilityDefinition skill1;
    public AbilityDefinition skill2;

    [Header("Movement Ability")]
    public AbilityDefinition dash;
    public KeyCode dashKey = KeyCode.Space;

    [Header("Optional Skill Hotkeys")]
    public KeyCode skill1Key = KeyCode.Q;
    public KeyCode skill2Key = KeyCode.E;

    [Header("Attack Input (Hold)")]
    public float attackRepeatInterval = 0.06f;   // (기존 유지) 홀드 중 다음 공격 최소 간격
    public float reAimGapAfterAttackEnd = 0.06f; // ✅ 공격 끝난 직후 방향 전환 틈(추천 0.05~0.12)

    private float nextAutoAttackTime;
    private bool wasBusyLastFrame;

    public GameplayTag attackPressedEvent;
    public GameplayTag attackReleasedEvent;

    [Header("Hand Aim")]
    public Transform Hand;
    public float weaponZOffset = 0f;

    public Vector2 MoveInput { get; private set; }
    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public Vector2 MouseWorld { get; private set; }

    private Rigidbody2D rb;
    private AttributeSet _attr;

    private bool isHoldingAttack;
    private float attackRepeatTimer;

    private void Awake()
    {
        Instance = this;

        rb = GetComponent<Rigidbody2D>();

        _attr = GetComponent<AttributeSet>();

        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (mainCamera == null) mainCamera = Camera.main;
        if (weaponInventory == null) weaponInventory = GetComponent<WeaponInventory2D>();
    }

    private void Update()
    {
        // TempPlayer가 하던 것처럼 “대화 중이면 입력 컷”
        if (CurrentState == InteractState.Talking) return;

        // 1) 상호작용(근처 탐색/하이라이트/F키)
        HandleInteractSearch();
        if (Input.GetKeyDown(interactKey) && currentTarget != null && currentTarget.CanInteract(this))
        {
            currentTarget.OnPlayerInteract(this);
        }

        // 2) 전투 입력은 Idle일 때만(원하면 Shopping도 막기)
        if (CurrentState != InteractState.Idle) return;

        UpdateMouseAim();
        HandleCombatInput();
        UpdateHandRotation();
    }

    private void FixedUpdate()
    {
        HandleMovementFixed();
    }

    // -----------------------
    // Movement
    // -----------------------
    private void HandleMovementFixed()
    {
        if (rb == null) return;

        if (tagSystem != null && movementLockedTag != null && tagSystem.HasTag(movementLockedTag))
        {
            rb.linearVelocity = Vector2.zero;
            MoveInput = Vector2.zero;
            return;
        }

        bool forced = (tagSystem != null && forcedMoveTag != null && tagSystem.HasTag(forcedMoveTag));

        if (!forced)
        {
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            MoveInput = new Vector2(x, y).normalized;
        }
        else
        {
            // 강제 이동: AimDirection 방향으로만 이동 (WASD 무시)
            MoveInput = AimDirection.sqrMagnitude > 0.0001f ? AimDirection.normalized : Vector2.right;
        }

        float mult = 1f;
        if (_attr != null)
        {
            // Prefer StatId + StatTypeBindings (Final = (Base+Add)*Mul) so relic/weapon buffs are reflected.
            StatTypeBindings bindings = null;
            if (abilitySystem != null && abilitySystem.DamageProfile != null)
                bindings = abilitySystem.DamageProfile.GetStatBindings();

            if (bindings != null)
            {
                var provider = new AttributeStatProvider(_attr, bindings);
                mult = provider.Get(moveSpeedMultiplierStatId);
            }
            else if (moveSpeedMultiplierAttribute != null)
            {
                // Legacy fallback
                mult = _attr.GetAttributeValue(moveSpeedMultiplierAttribute);
            }

            mult = Mathf.Max(0f, mult);
        }

        rb.linearVelocity = MoveInput * (baseMoveSpeed * mult);
    }

    // -----------------------
    // Interaction (from TempPlayer)
    // -----------------------
    private void HandleInteractSearch()
    {
        IInteractable nearest = GetClosestInteractable();

        if (nearest != currentTarget)
        {
            if (currentTarget != null) currentTarget.OnUnHighlight();

            currentTarget = nearest;

            if (currentTarget != null)
            {
                currentTarget.OnHighlight();
                // 여기서 UI 프롬프트 이벤트를 쏘고 싶으면 연결
                // ex) GameEvents.OnShowInteractKey?.Invoke(((MonoBehaviour)currentTarget).transform, currentTarget.GetInteractDescription());
            }
        }
    }

    private IInteractable GetClosestInteractable()
    {
        if (nearbyObjects.Count == 0) return null;

        float closestDist = float.MaxValue;
        IInteractable closestObj = null;

        for (int i = nearbyObjects.Count - 1; i >= 0; i--)
        {
            var obj = nearbyObjects[i];
            if (obj == null || (obj is MonoBehaviour mb && mb == null))
            {
                nearbyObjects.RemoveAt(i);
                continue;
            }

            var mbObj = (MonoBehaviour)obj;
            float dist = Vector2.Distance(transform.position, mbObj.transform.position);

            if (dist < closestDist && obj.CanInteract(this))
            {
                closestDist = dist;
                closestObj = obj;
            }
        }

        return closestObj;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 기존 TempPlayer가 other.TryGetComponent(out IInteractable) 하던 방식 유지
        if (other.TryGetComponent(out IInteractable interactable))
        {
            if (!nearbyObjects.Contains(interactable))
            {
                nearbyObjects.Add(interactable);
                interactable.OnPlayerNearby();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out IInteractable interactable))
        {
            if (nearbyObjects.Contains(interactable))
            {
                interactable.OnPlayerLeave();

                if (currentTarget == interactable)
                {
                    currentTarget.OnUnHighlight();
                    currentTarget = null;
                }

                nearbyObjects.Remove(interactable);
            }
        }
    }

    // -----------------------
    // Aim / Hand
    // -----------------------
    private void UpdateMouseAim()
    {
        if (mainCamera == null) return;

        var world = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;
        MouseWorld = world;

        Vector2 dir = (world - transform.position);
        if (dir.sqrMagnitude > 0.0001f)
            AimDirection = dir.normalized;
    }

    private void UpdateHandRotation()
    {
        if (tagSystem != null && aimLockedTag != null && tagSystem.HasTag(aimLockedTag))
            return;

        if (Hand == null) return;

        Vector2 dir = (MouseWorld - (Vector2)transform.position).normalized;
        float rad = Mathf.Atan2(dir.y, dir.x);
        float degreeRaw = rad * Mathf.Rad2Deg;
        float degree = (degreeRaw < 0f) ? degreeRaw + 360f : degreeRaw;

        Hand.rotation = Quaternion.Euler(new Vector3(0, 0, degree + weaponZOffset));
    }

    // -----------------------
    // Combat
    // -----------------------
    private void HandleCombatInput()
    {
        var atk = GetBasicAttack();

        // 1) Press
        if (Input.GetMouseButtonDown(0))
        {
            isHoldingAttack = true;

            // 즉시 1회 시도
            SendGameplayEventSafe(attackPressedEvent);
            nextAutoAttackTime = 0f;

            if (atk != null) TryActivateSafe(atk);
        }

        // 2) Release
        if (Input.GetMouseButtonUp(0))
        {
            isHoldingAttack = false;
            SendGameplayEventSafe(attackReleasedEvent);
        }

        // 3) Busy 전환 감지 → 공격이 끝난 순간 re-aim 틈 부여
        if (abilitySystem != null)
        {
            bool busyNow = abilitySystem.IsBusy;

            if (wasBusyLastFrame && !busyNow)
            {
                // 방금 공격이 끝남 → 방향 전환할 시간
                nextAutoAttackTime = Time.time + reAimGapAfterAttackEnd;
            }

            wasBusyLastFrame = busyNow;
        }

        // 4) Hold 자동 연타: Busy가 아닐 때만 시도
        if (isHoldingAttack && atk != null && abilitySystem != null)
        {
            // ✅ 핵심: Busy 동안에는 TryActivate를 스팸하지 않는다
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
        if (Input.GetKeyDown(dashKey))
            TryActivateSafe(dash);

        if (weaponInventory != null && Input.GetKeyDown(KeyCode.Tab))
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
        if (weaponInventory != null)
            return weaponInventory.GetActiveAbility(WeaponAbilitySlot.Attack);
        return basicAttack;
    }

    private AbilityDefinition GetSkill1()
    {
        if (weaponInventory != null)
            return weaponInventory.GetActiveAbility(WeaponAbilitySlot.Skill1);
        return skill1;
    }

    private AbilityDefinition GetSkill2()
    {
        if (weaponInventory != null)
            return weaponInventory.GetActiveAbility(WeaponAbilitySlot.Skill2);
        return skill2;
    }
}

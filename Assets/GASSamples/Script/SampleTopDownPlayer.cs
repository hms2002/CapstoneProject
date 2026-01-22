using UnityEngine;
using UnityGAS;

[RequireComponent(typeof(Rigidbody2D))]
public class SampleTopDownPlayer : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 6f;

    [Tooltip("이 태그가 있으면 이동 입력을 무시합니다 (예: State.MovementLocked)")]
    public GameplayTag movementLockedTag;

    [Tooltip("이 태그가 있으면 무기가 마우스 커서를 따라 회전하는 입력을 무시합니다 (예: State.Aim.Blocked)")]
    public GameplayTag aimLockedTag;

    [Header("Attack Input (Hold)")]
    [Tooltip("좌클릭 홀드 중 기본공격을 몇 초마다 재요청할지 (버퍼/콤보 테스트용)")]
    public float attackRepeatInterval = 0.06f;

    [Tooltip("LMB Pressed 이벤트 태그 (예: Event.Input.Attack.Pressed)")]
    public GameplayTag attackPressedEvent;

    [Tooltip("LMB Released 이벤트 태그 (예: Event.Input.Attack.Released)")]
    public GameplayTag attackReleasedEvent;

    [Header("Weapon Inventory (Optional)")]
    [Tooltip("있으면 무기 장착에 따라 공격/스킬이 자동으로 바뀜")]
    public WeaponInventory2D weaponInventory;

    [Header("Fallback Abilities (No Inventory)")]
    [Tooltip("인벤토리를 사용하지 않는 테스트 씬용(기존과 동일)")]
    public AbilityDefinition basicAttack;   // AD_SwordCombo
    public AbilityDefinition skill1;        // AD_SwordSkill1_Projectile
    public AbilityDefinition skill2;        // AD_SwordSkill2_BigSwing

    [Header("Optional Skill Hotkeys")]
    public KeyCode skill1Key = KeyCode.Q;
    public KeyCode skill2Key = KeyCode.E;

    [Header("References")]
    public AbilitySystem abilitySystem;
    public TagSystem tagSystem;
    public Camera mainCamera;

    // 읽기용(다른 스크립트/애니가 활용 가능)
    public Vector2 MoveInput { get; private set; }
    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public Vector2 MouseWorld { get; private set; }

    private Rigidbody2D rb;

    // LMB 홀드 반복 요청
    private bool isHoldingAttack;
    private float attackRepeatTimer;

    [Header("Hand Aim")]
    public Transform Hand;
    public float weaponZOffset = 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (mainCamera == null) mainCamera = Camera.main;
        if (weaponInventory == null) weaponInventory = GetComponent<WeaponInventory2D>();
    }

    private void Update()
    {
        UpdateMouseAim();
        HandleInput();

        // ✅ (예전 로직 그대로) 에임 락 태그면 Hand 회전 중지
        if (tagSystem != null && aimLockedTag != null && tagSystem.HasTag(aimLockedTag))
        {
            // Debug.Log("어 잠깐 막았어~");
            return;
        }

        // ✅ (예전 로직 그대로) Hand 회전: 0~360 정규화 + weaponZOffset
        if (Hand != null)
        {
            Vector2 dir = (MouseWorld - (Vector2)transform.position).normalized;
            float rad = Mathf.Atan2(dir.y, dir.x);
            float degreeRaw = rad * Mathf.Rad2Deg;
            float degree = (degreeRaw < 0f) ? degreeRaw + 360f : degreeRaw;

            Hand.rotation = Quaternion.Euler(new Vector3(0, 0, degree + weaponZOffset));
        }
    }

    private void FixedUpdate()
    {
        HandleMovementFixed();
    }

    private void UpdateMouseAim()
    {
        if (mainCamera == null) return;

        var mouse = Input.mousePosition;
        var world = mainCamera.ScreenToWorldPoint(mouse);
        world.z = 0f;
        MouseWorld = world;

        Vector2 dir = (world - transform.position);
        if (dir.sqrMagnitude > 0.0001f)
            AimDirection = dir.normalized;
    }

    private void HandleInput()
    {
        // 이동 입력 (탑다운)
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        MoveInput = new Vector2(x, y).normalized;

        // 기본 공격: LMB
        if (Input.GetMouseButtonDown(0))
        {
            isHoldingAttack = true;
            attackRepeatTimer = 0f;

            SendGameplayEventSafe(attackPressedEvent);

            // 첫 타 즉시 요청
            TryActivateSafe(GetBasicAttack());
        }

        if (Input.GetMouseButtonUp(0))
        {
            isHoldingAttack = false;
            attackRepeatTimer = 0f;

            SendGameplayEventSafe(attackReleasedEvent);
        }

        // LMB 홀드 동안 주기적으로 계속 요청
        var atk = GetBasicAttack();
        if (isHoldingAttack && atk != null && abilitySystem != null)
        {
            attackRepeatTimer -= Time.deltaTime;
            if (attackRepeatTimer <= 0f)
            {
                attackRepeatTimer = attackRepeatInterval;
                TryActivateSafe(atk);
            }
        }

        // 스킬 핫키(임시)
        if (Input.GetKeyDown(skill1Key))
            TryActivateSafe(GetSkill1());

        if (Input.GetKeyDown(skill2Key))
            TryActivateSafe(GetSkill2());

        // (선택) 무기 교체 테스트 키
        if (weaponInventory != null && Input.GetKeyDown(KeyCode.Tab))
            weaponInventory.Swap();
    }

    private void HandleMovementFixed()
    {
        if (rb == null) return;

        // 이동 제한 태그가 있으면 이동 무시
        if (tagSystem != null && movementLockedTag != null && tagSystem.HasTag(movementLockedTag))
        {
            rb.linearVelocity = Vector2.zero; // 예전 코드 유지
            return;
        }

        Vector2 vel = MoveInput * moveSpeed;
        rb.linearVelocity = vel;
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

    // -----------------------
    // Inventory-linked abilities
    // -----------------------
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

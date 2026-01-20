using UnityEngine;
using UnityGAS; // 네 GAS 네임스페이스가 다르면 바꿔줘

/// <summary>
/// 2D 탑다운 샘플용 임시 Player 컨트롤러.
/// - WASD 이동
/// - 마우스 방향(플레이어 -> 커서) 계산
/// - LMB 홀드: 기본 공격 계속 요청(바쁠 땐 AbilitySystem의 버퍼가 받아줌)
/// - (선택) Q/E로 스킬1/스킬2 발동
/// - 이동 제한: TagSystem에 movementLockedTag가 있으면 이동 입력 무시
/// </summary>
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
    public float attackRepeatInterval = 0.06f; // 0.05~0.1 권장

    [Tooltip("LMB Pressed 이벤트 태그 (예: Event.Input.Attack.Pressed)")]
    public GameplayTag attackPressedEvent;

    [Tooltip("LMB Released 이벤트 태그 (예: Event.Input.Attack.Released)")]
    public GameplayTag attackReleasedEvent;

    [Header("Abilities (AbilityDefinition Assets)")]
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

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (mainCamera == null) mainCamera = Camera.main;
    }

    public Transform Hand;
    public float weaponZOffset = 0f;
    private void Update()
    {
        UpdateMouseAim();
        HandleInput();

        // 이동 제한 태그가 있으면 이동 무시
        if (tagSystem != null && aimLockedTag != null && tagSystem.HasTag(aimLockedTag))
        {
            Debug.Log("어 잠깐 막았어~");
            return;
        }
        if (Hand != null) 
        {
            Vector2 dir = (MouseWorld - (Vector2)transform.position).normalized;
            float rad = Mathf.Atan2(dir.y, dir.x);
            float degreeRaw = rad * Mathf.Rad2Deg;
            float degree = (degreeRaw < 0f) ? degreeRaw + 360f : degreeRaw;
            //Debug.Log($"Rad : {rad}, Degree : {degree}");
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

            // 이벤트 발송(선택)
            SendGameplayEventSafe(attackPressedEvent);

            // 첫 타 즉시 요청
            TryActivateSafe(basicAttack);
        }

        if (Input.GetMouseButtonUp(0))
        {
            isHoldingAttack = false;
            attackRepeatTimer = 0f;

            SendGameplayEventSafe(attackReleasedEvent);
        }

        // LMB 홀드 동안 주기적으로 계속 요청
        if (isHoldingAttack && basicAttack != null && abilitySystem != null)
        {
            attackRepeatTimer -= Time.deltaTime;
            if (attackRepeatTimer <= 0f)
            {
                attackRepeatTimer = attackRepeatInterval;
                TryActivateSafe(basicAttack);
            }
        }

        // 스킬 핫키(임시)
        if (Input.GetKeyDown(skill1Key))
            TryActivateSafe(skill1);

        if (Input.GetKeyDown(skill2Key))
            TryActivateSafe(skill2);
    }

    private void HandleMovementFixed()
    {
        if (rb == null) return;

        // 이동 제한 태그가 있으면 이동 무시
        if (tagSystem != null && movementLockedTag != null && tagSystem.HasTag(movementLockedTag))
        {
            rb.linearVelocity = Vector2.zero; // Unity 6/2022 호환: velocity 사용 중이면 rb.velocity로 바꿔
            return;
        }

        // 이동
        Vector2 vel = MoveInput * moveSpeed;
        rb.linearVelocity = vel;
    }

    private void TryActivateSafe(AbilityDefinition def)
    {
        if (def == null || abilitySystem == null) return;

        // 탑다운 근접/박스 공격은 target이 없어도 되도록 만들 예정이니까 null로 호출
        // 투사체도 로직 내부에서 AimDirection/MouseWorld를 참조해 발사하면 됨
        abilitySystem.TryActivateAbility(def, null);
    }

    private void SendGameplayEventSafe(GameplayTag tag)
    {
        if (abilitySystem == null || tag == null) return;

        // payload를 쓸 계획이면 여기에서 채워도 됨(현재는 null)
        abilitySystem.SendGameplayEvent(tag);
    }
}

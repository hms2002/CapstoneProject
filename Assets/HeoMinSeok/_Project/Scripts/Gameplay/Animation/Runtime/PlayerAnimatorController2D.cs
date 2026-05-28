using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 플레이어 Animator의 "상태 파라미터"를 한 곳에서 갱신한다.
/// - 이동 여부와 4방향 값을 Movement/Aim 상태에서 계산해 Animator에 반영한다.
/// - 조준 방향을 기준으로 본체 SpriteRenderer의 좌우 반전을 동기화한다.
/// - 공격/피격/사망 같은 트리거성 연출과 분리된 상시 상태 동기화 창구다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAnimatorController2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private MovementMotor2D movementMotor;
    [SerializeField] private PlayerAim2D aimSource;
    [SerializeField] private SpriteRenderer[] visualRenderers;

    [Header("Parameter Names")]
    [SerializeField] private string isMovingBool = "IsMoving";
    [SerializeField] private string directionInt = "Direction";

    [Header("Direction Mapping")]
    [SerializeField] private int southDirectionValue = 0;
    [SerializeField] private int westDirectionValue = 1;
    [SerializeField] private int northDirectionValue = 2;
    [SerializeField] private int eastDirectionValue = 3;

    [Header("Flip")]
    [SerializeField] private bool flipWhenFacingLeft = true;

    private int isMovingBoolHash;
    private int directionIntHash;
    private int lastDirectionValue;
    private readonly HashSet<object> cinematicFacingLockOwners = new();

    private bool IsCinematicFacingLocked => cinematicFacingLockOwners.Count > 0;

    public static PlayerAnimatorController2D GetOrAdd(Transform owner)
    {
        if (owner == null)
            return null;

        var controller = owner.GetComponent<PlayerAnimatorController2D>();
        return controller != null ? controller : owner.gameObject.AddComponent<PlayerAnimatorController2D>();
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (movementMotor == null)
            movementMotor = GetComponent<MovementMotor2D>();

        if (aimSource == null)
            aimSource = GetComponent<PlayerAim2D>();

        ResolveVisualRenderers();

        isMovingBoolHash = string.IsNullOrWhiteSpace(isMovingBool) ? 0 : Animator.StringToHash(isMovingBool);
        directionIntHash = string.IsNullOrWhiteSpace(directionInt) ? 0 : Animator.StringToHash(directionInt);
        lastDirectionValue = southDirectionValue;
    }

    private void Update()
    {
        if (animator == null)
            return;

        SyncMovementState();

        if (IsCinematicFacingLocked)
            return;

        SyncDirectionState();
        SyncVisualFlip();
    }

    private void OnDisable()
    {
        cinematicFacingLockOwners.Clear();
    }

    public void AcquireCinematicFacingLock(object ownerToken)
    {
        if (ownerToken == null)
            return;

        cinematicFacingLockOwners.Add(ownerToken);
    }

    public void ReleaseCinematicFacingLock(object ownerToken)
    {
        if (ownerToken == null)
            return;

        cinematicFacingLockOwners.Remove(ownerToken);
    }

    public void ApplyFacingDirectionForPresentation(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Vector2 normalizedDirection = direction.normalized;
        if (directionIntHash == 0 && !string.IsNullOrWhiteSpace(directionInt))
            directionIntHash = Animator.StringToHash(directionInt);

        if (animator != null && directionIntHash != 0)
        {
            lastDirectionValue = ConvertToDirectionValue(normalizedDirection);
            animator.SetInteger(directionIntHash, lastDirectionValue);
        }

        SyncVisualFlip(normalizedDirection);
    }

    private void SyncMovementState()
    {
        if (isMovingBoolHash == 0)
            return;

        bool isMoving = movementMotor != null && movementMotor.IsMoving;
        animator.SetBool(isMovingBoolHash, isMoving);
    }

    private void SyncDirectionState()
    {
        if (directionIntHash == 0)
            return;

        Vector2 sourceDirection = ResolveFacingDirection();
        if (sourceDirection.sqrMagnitude > 0.0001f)
            lastDirectionValue = ConvertToDirectionValue(sourceDirection.normalized);

        animator.SetInteger(directionIntHash, lastDirectionValue);
    }

    private Vector2 ResolveFacingDirection()
    {
        if (aimSource != null && aimSource.AimDirection.sqrMagnitude > 0.0001f)
            return aimSource.AimDirection;

        if (movementMotor != null && movementMotor.LastFinalVelocity.sqrMagnitude > 0.0001f)
            return movementMotor.LastFinalVelocity;

        return Vector2.zero;
    }

    private int ConvertToDirectionValue(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            return direction.x >= 0f ? eastDirectionValue : westDirectionValue;

        return direction.y >= 0f ? northDirectionValue : southDirectionValue;
    }

    /// <summary>
    /// 책임 :
    /// - flip 대상 SpriteRenderer를 자동으로 찾아 인스펙터 누락 시에도 본체 비주얼 반전이 동작하게 한다.
    /// - 기본값은 루트 SpriteRenderer 하나이며, 그림자 등 예외 렌더러는 인스펙터 override로 분리할 수 있다.
    /// </summary>
    private void ResolveVisualRenderers()
    {
        if (visualRenderers != null && visualRenderers.Length > 0)
            return;

        var rootRenderer = GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
            visualRenderers = new[] { rootRenderer };
    }

    /// <summary>
    /// 책임 :
    /// - 현재 조준 방향을 기준으로 플레이어 본체 비주얼의 flipX를 맞춘다.
    /// - 애니메이션 상태와 무관한 좌우 반전 표현은 여기서만 관리한다.
    /// </summary>
    private void SyncVisualFlip()
    {
        SyncVisualFlip(ResolveFacingDirection());
    }

    private void SyncVisualFlip(Vector2 facingDirection)
    {
        if (visualRenderers == null || visualRenderers.Length == 0)
            return;

        if (facingDirection.sqrMagnitude <= 0.0001f)
            return;

        bool shouldFlip = flipWhenFacingLeft && facingDirection.x < 0f;
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            var renderer = visualRenderers[i];
            if (renderer == null)
                continue;

            renderer.flipX = shouldFlip;
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class TutorialPlayerAutoMove : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform targetPoint;
    [SerializeField] private PlayerCinematicProtection playerProtection;
    [SerializeField] private MovementMotor2D movementMotor;
    [SerializeField] private ExternalMovementController2D externalMovement;
    [SerializeField] private Rigidbody2D playerBody;

    [Header("Movement")]
    [SerializeField, Min(0.01f)] private float moveSpeed = 4f;
    [SerializeField, Min(0.001f)] private float arriveDistance = 0.05f;
    [SerializeField, Min(0f)] private float maxDurationSeconds = 8f;
    [SerializeField] private bool lockPlayerInput = true;
    [SerializeField] private bool releaseLockOnComplete = true;
    [SerializeField] private bool stopMotionOnComplete = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onMoveStarted = new();
    [SerializeField] private UnityEvent onMoveCompleted = new();
    [SerializeField] private UnityEvent onMoveCanceled = new();

    private Coroutine moveRoutine;
    private bool hasAcquiredProtection;

    public bool IsMoving => moveRoutine != null;
    public UnityEvent OnMoveStarted => onMoveStarted;
    public UnityEvent OnMoveCompleted => onMoveCompleted;
    public UnityEvent OnMoveCanceled => onMoveCanceled;

    private void OnDisable()
    {
        CancelMove(invokeCanceled: false);
        ReleasePlayerProtection();
    }

    public void MoveToTarget()
    {
        if (targetPoint == null)
        {
            Debug.LogWarning("[TutorialPlayerAutoMove] Target point is missing.", this);
            return;
        }

        MoveTo(targetPoint);
    }

    public void MoveTo(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning("[TutorialPlayerAutoMove] Target is missing.", this);
            return;
        }

        MoveToPosition(target.position);
    }

    public void MoveToPosition(Vector3 worldPosition)
    {
        CancelMove(invokeCanceled: false);
        ResolveReferences();

        if (playerTransform == null)
        {
            Debug.LogWarning("[TutorialPlayerAutoMove] Player transform is missing.", this);
            return;
        }

        moveRoutine = StartCoroutine(MoveRoutine(worldPosition));
    }

    public void CancelMove()
    {
        CancelMove(invokeCanceled: true);
    }

    private IEnumerator MoveRoutine(Vector3 worldPosition)
    {
        onMoveStarted?.Invoke();

        if (lockPlayerInput)
            AcquirePlayerProtection();

        float elapsed = 0f;
        Vector2 target = worldPosition;
        float arriveDistanceSqr = arriveDistance * arriveDistance;

        while (playerTransform != null)
        {
            Vector2 current = playerTransform.position;
            Vector2 delta = target - current;
            if (delta.sqrMagnitude <= arriveDistanceSqr)
                break;

            if (maxDurationSeconds > 0f && elapsed >= maxDurationSeconds)
                break;

            Vector2 direction = delta.normalized;
            ApplyMovement(direction, target);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        CleanupMovement(releaseProtection: releaseLockOnComplete);
        moveRoutine = null;
        onMoveCompleted?.Invoke();
    }

    private void ApplyMovement(Vector2 direction, Vector2 target)
    {
        if (externalMovement != null)
        {
            externalMovement.RemoveTimedVelocitiesFromSource(this);
            externalMovement.AddTimedVelocity(
                direction * Mathf.Max(0.01f, moveSpeed),
                Time.fixedDeltaTime * 1.5f,
                0f,
                this);
            return;
        }

        Vector2 current = playerTransform.position;
        Vector2 next = Vector2.MoveTowards(
            current,
            target,
            Mathf.Max(0.01f, moveSpeed) * Time.fixedDeltaTime);

        if (playerBody != null)
        {
            playerBody.MovePosition(next);
            return;
        }

        playerTransform.position = new Vector3(next.x, next.y, playerTransform.position.z);
    }

    private void CancelMove(bool invokeCanceled)
    {
        if (moveRoutine == null)
            return;

        StopCoroutine(moveRoutine);
        moveRoutine = null;
        CleanupMovement(releaseProtection: true);

        if (invokeCanceled)
            onMoveCanceled?.Invoke();
    }

    private void ResolveReferences()
    {
        if (playerTransform == null)
            playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();

        if (playerTransform == null)
            return;

        if (playerProtection == null)
            playerProtection = playerTransform.GetComponent<PlayerCinematicProtection>();

        if (movementMotor == null)
            movementMotor = playerTransform.GetComponent<MovementMotor2D>();

        if (externalMovement == null)
            externalMovement = playerTransform.GetComponent<ExternalMovementController2D>();

        if (playerBody == null)
            playerBody = playerTransform.GetComponent<Rigidbody2D>();
    }

    private void AcquirePlayerProtection()
    {
        ResolveReferences();

        if (playerTransform == null)
            return;

        if (playerProtection == null)
            playerProtection = playerTransform.gameObject.AddComponent<PlayerCinematicProtection>();

        playerProtection.Acquire(this);
        hasAcquiredProtection = true;
    }

    private void CleanupMovement(bool releaseProtection)
    {
        externalMovement?.RemoveTimedVelocitiesFromSource(this);

        if (stopMotionOnComplete)
            movementMotor?.StopAllMotion();

        if (releaseProtection && hasAcquiredProtection)
            ReleasePlayerProtection();
    }

    private void ReleasePlayerProtection()
    {
        if (!hasAcquiredProtection)
            return;

        playerProtection?.Release(this);
        hasAcquiredProtection = false;
    }
}

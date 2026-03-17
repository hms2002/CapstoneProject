using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class EnemyFacing2D : MonoBehaviour, IFacingDirectionSource2D
{
    [Header("Refs")]
    [SerializeField] private Enemy enemy;
    [SerializeField] private SpriteRenderer sprite;
    [SerializeField] private MovementMotor2D movementMotor;

    [Header("Policy")]
    [SerializeField] private bool flipSpriteX = true;

    private Vector2 lastFacing = Vector2.right;

    private void Awake()
    {
        if (enemy == null)
            enemy = GetComponent<Enemy>();

        if (sprite == null)
            sprite = GetComponent<SpriteRenderer>();

        if (movementMotor == null)
            movementMotor = GetComponent<MovementMotor2D>();
    }

    private void LateUpdate()
    {
        Vector2 dir = ResolveFacingDirection();
        if (dir.sqrMagnitude > 0.0001f)
            lastFacing = dir.normalized;

        if (flipSpriteX && sprite != null)
        {
            if (lastFacing.x < -0.001f) sprite.flipX = true;
            else if (lastFacing.x > 0.001f) sprite.flipX = false;
        }
    }

    public Vector2 GetFacingDirection()
    {
        return lastFacing.sqrMagnitude > 0.0001f ? lastFacing : Vector2.right;
    }

    private Vector2 ResolveFacingDirection()
    {
        if (enemy != null && enemy.Target != null)
        {
            Vector2 toTarget = (Vector2)(enemy.Target.position - transform.position);
            if (toTarget.sqrMagnitude > 0.0001f)
                return toTarget.normalized;
        }

        if (movementMotor != null && movementMotor.LastFinalVelocity.sqrMagnitude > 0.0001f)
            return movementMotor.LastFinalVelocity.normalized;

        return lastFacing;
    }
}
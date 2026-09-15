using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 플레이어의 이동 의도를 수집해 이동 시스템이 읽을 수 있는 형태로 제공한다.
/// - 강제 이동 및 이동 차단 tag를 함께 반영해 현재 상태에 맞는 최종 이동 입력만 내보낸다.
/// - 걷기는 실제 몸체가 구덩이에 겹치기 전에 제한하며 안전한 축으로 미끄러지고, 대시와 외압은 제한하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerIntentInput2D : MonoBehaviour, IIntentMovementSource2D, IAbilityMoveInputSource2D, IWallSlidingMovementSource2D, IIntentVelocityFilter2D
{
    private const string MoveBlockedTagResourcePath = "Tags/State.Move.Intent.Blocked";

    [Header("Refs")]
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private PlayerAim2D aim;
    [SerializeField] private PlayerInteractor2D player;

    [Header("Tags")]
    [Tooltip("이 태그가 있으면 WASD 대신 AimDirection 방향으로 강제 이동합니다.")]
    [SerializeField] private GameplayTag forcedMoveTag;
    [SerializeField] private GameplayTag moveBlockedTag;

    /// <summary>
    /// 책임 :
    /// - 플레이어가 실제로 누르고 있는 원본 이동 입력을 보관한다.
    /// - 이동 차단 tag와 무관하게 "입력이 무엇이었는지"를 참조해야 하는 능력이 사용한다.
    /// </summary>
    public Vector2 RawMoveInput { get; private set; }

    /// <summary>
    /// 책임 :
    /// - 이동 차단 및 강제 이동 tag를 반영한 최종 이동 입력을 보관한다.
    /// - 일반 이동 시스템은 이 값을 읽어 현재 허용된 이동만 적용한다.
    /// </summary>
    public Vector2 MoveInput { get; private set; }

    // Lunge direction is intent, not the movement currently permitted by the attack lock.
    public Vector2 AbilityMoveInput => InputActionQuery.GetMoveVectorNormalized();

    private PlayerCombatInput2D combatInput;
    private Rigidbody2D body;
    private Collider2D[] bodyColliders;
    private SafetyTracker safetyTracker;
    private ContactFilter2D pitFilter;
    private readonly Collider2D[] pitHits = new Collider2D[16];
    private readonly RaycastHit2D[] pitBodyHits = new RaycastHit2D[16];
    private const float PitBodySkin = 0.03f;
    private const float PitProbeStep = 0.025f;
    private const int MaxPitProbeSteps = 256;

    private void Awake()
    {
        combatInput = GetComponent<PlayerCombatInput2D>();
        body = GetComponent<Rigidbody2D>();
        bodyColliders = GetComponentsInChildren<Collider2D>(true);
        safetyTracker = GetComponent<SafetyTracker>();
        pitFilter = ContactFilter2D.noFilter;
        pitFilter.SetLayerMask(LayerMask.GetMask("HoleTrap"));
        pitFilter.useTriggers = true;
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (aim == null) aim = GetComponent<PlayerAim2D>();
        if (player == null) player = GetComponent<PlayerInteractor2D>();
        if (moveBlockedTag == null) moveBlockedTag = Resources.Load<GameplayTag>(MoveBlockedTagResourcePath);
    }

    private void Update()
    {
        RawMoveInput = InputActionQuery.GetMoveVectorNormalized();
        if (combatInput != null && combatInput.IsBasicAttackMovementLocked)
        {
            MoveInput = Vector2.zero;
            return;
        }

        if (player != null && player.CurrentState != InteractState.Idle)
        {
            MoveInput = Vector2.zero;
            return;
        }

        if (tagSystem != null && moveBlockedTag != null && tagSystem.HasTag(moveBlockedTag))
        {
            MoveInput = Vector2.zero;
            return;
        }

        bool forced = tagSystem != null &&
                      forcedMoveTag != null &&
                      tagSystem.HasTag(forcedMoveTag);

        if (!forced)
        {
            MoveInput = RawMoveInput;
        }
        else
        {
            Vector2 aimDir = aim != null ? aim.AimDirection : Vector2.right;
            MoveInput = aimDir.sqrMagnitude > 0.0001f
                ? aimDir.normalized
                : Vector2.right;
        }
    }

    public IntentMovementData GetIntent()
    {
        if (combatInput != null && combatInput.IsBasicAttackMovementLocked) return IntentMovementData.None;
        if (player != null && player.CurrentState != InteractState.Idle)
            return IntentMovementData.None;

        if (tagSystem != null && moveBlockedTag != null && tagSystem.HasTag(moveBlockedTag))
            return IntentMovementData.None;

        return IntentMovementData.FromDirection(MoveInput);
    }

    /// <summary>Responsibility: preserve safe walking components along pit edges without changing raw input or ability motion.</summary>
    public Vector2 FilterIntentVelocity(Vector2 velocity, float deltaTime)
    {
        if (deltaTime <= 0f || velocity.sqrMagnitude < 0.000001f)
            return velocity;
        if (tagSystem != null && forcedMoveTag != null && tagSystem.HasTag(forcedMoveTag))
            return velocity;

        if (body == null || bodyColliders == null)
            return Vector2.zero;

        Vector2 foot = ResolveFootPosition();
        Vector2 delta = velocity * deltaTime;
        Vector2 safe = TraceSafePitMovement(foot, delta);
        if ((safe - delta).sqrMagnitude < 0.00000001f)
            return velocity;

        // Each candidate is swept from the real start pose; do not cut across a corner
        // by combining an L-shaped hypothetical path into one physics-frame velocity.
        Vector2 x = TraceSafePitMovement(foot, new Vector2(delta.x, 0f));
        Vector2 y = TraceSafePitMovement(foot, new Vector2(0f, delta.y));
        Vector2 slide = x.sqrMagnitude >= y.sqrMagnitude ? x : y;
        return (slide.sqrMagnitude > safe.sqrMagnitude ? slide : safe) / deltaTime;
    }

    private Vector2 ResolveFootPosition()
    {
        if (safetyTracker != null)
            return safetyTracker.FootPosition;
        foreach (Collider2D collider in bodyColliders)
            if (collider != null && collider.enabled && !collider.isTrigger && collider.attachedRigidbody == body)
                return collider.bounds.center;
        return body.position;
    }

    private Vector2 TraceSafePitMovement(Vector2 start, Vector2 delta)
    {
        float distance = delta.magnitude;
        if (distance < 0.000001f) return Vector2.zero;
        Vector2 direction = delta / distance;
        float allowed = distance;
        foreach (Collider2D collider in bodyColliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                collider.isTrigger || collider.attachedRigidbody != body)
                continue;
            int count = collider.Cast(direction, pitFilter, pitBodyHits, distance + PitBodySkin);
            if (count == pitBodyHits.Length) return Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = pitBodyHits[i];
                if (hit.collider == null || hit.collider.attachedRigidbody == body) continue;
                if (hit.distance <= 0.0001f)
                {
                    ColliderDistance2D overlap = collider.Distance(hit.collider);
                    if (overlap.isValid && overlap.isOverlapped)
                    {
                        // Initial-overlap cast normals are synthetic. Use the actual
                        // separation direction so a shallow dash can escape or slide.
                        Vector2 escape = overlap.normal * overlap.distance;
                        if (Vector2.Dot(direction, escape) < -0.000001f) return Vector2.zero;
                        continue;
                    }
                }
                if (Vector2.Dot(direction, hit.normal) >= -0.0001f) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - PitBodySkin));
            }
        }
        // Retain the independent foot check for recovery from existing overlaps.
        return TraceSafeFootMovement(start, direction * allowed);
    }

    private Vector2 TraceSafeFootMovement(Vector2 start, Vector2 delta)
    {
        float distance = delta.magnitude;
        if (distance < 0.000001f)
            return Vector2.zero;
        int steps = Mathf.CeilToInt(distance / PitProbeStep);
        Vector2 safe = Vector2.zero;
        for (int step = 1; step <= Mathf.Min(steps, MaxPitProbeSteps); step++)
        {
            Vector2 candidate = delta * ((float)step / steps);
            int count = Physics2D.OverlapPoint(start + candidate, pitFilter, pitHits);
            if (count == pitHits.Length) return safe;
            for (int i = 0; i < count; i++)
                if (PlayerPitFootprint2D.IsInside(pitHits[i], start + candidate, PlayerPitFootprint2D.WalkingInset))
                {
                    // A dash can leave the foot in the narrow safe margin. Allow escape
                    // or tangent movement there, but never permit deeper entry by walking.
                    if (PlayerPitFootprint2D.IsInside(pitHits[i], start, PlayerPitFootprint2D.WalkingInset) &&
                        !PlayerPitFootprint2D.IsInside(pitHits[i], start, PlayerPitFootprint2D.FallInset) &&
                        !PlayerPitFootprint2D.IsInside(pitHits[i], start + candidate, PlayerPitFootprint2D.FallInset) &&
                        Vector2.Dot(delta, PlayerPitFootprint2D.GetExitDirection(pitHits[i], start)) >= -0.000001f)
                        continue;
                    return safe;
                }
            safe = candidate;
        }
        return safe;
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 플레이어의 2D trigger 진입을 SceneTravelEndpoint 이동 요청으로 변환하고,
/// 도착 직후 역이동 억제·경계 차단과 실패 요청 연타 제한을 관리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SceneTravelEndpoint))]
public sealed class SceneTravelTrigger2D : MonoBehaviour
{
    [SerializeField] private SceneTravelEndpoint endpoint;
    [SerializeField, Min(0f)] private float rejectedRequestCooldown = 0.75f;
    [Tooltip("도착 직후 같은 trigger가 역방향 이동을 다시 허용하기까지의 최소 대기시간입니다.")]
    [SerializeField, Min(0f)] private float arrivalReactivationDelay = 0.75f;
    [Tooltip("도착 보호 중 trigger 바깥쪽을 막는 Wall 콜라이더의 로컬 두께입니다.")]
    [SerializeField, Min(0.05f)] private float arrivalBlockerThickness = 0.35f;

    private float nextAllowedRequestTime;
    private Transform suppressedArrivalPlayer;
    private Collider2D arrivalSuppressionBlocker;
    private float arrivalReactivationTime;
    private bool suppressedPlayerInsideTrigger;
    private readonly HashSet<Collider2D> suppressedPlayerColliders = new();

    private void Awake()
    {
        if (endpoint == null)
            endpoint = GetComponent<SceneTravelEndpoint>();

        EnsureArrivalSuppressionBlocker();
    }

    private void Update()
    {
        if (suppressedArrivalPlayer == null ||
            suppressedPlayerInsideTrigger ||
            Time.unscaledTime < arrivalReactivationTime)
        {
            return;
        }

        ReleaseArrivalSuppression();
    }

    private void OnDisable()
    {
        ReleaseArrivalSuppression();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        IPlayerInteractor player = ResolvePlayer(other);
        if (player == null)
            return;

        if (suppressedArrivalPlayer == player.Transform)
        {
            suppressedPlayerColliders.Add(other);
            suppressedPlayerInsideTrigger = suppressedPlayerColliders.Count > 0;
            return;
        }

        if (endpoint == null ||
            endpoint.IsTravelReserved ||
            Time.unscaledTime < nextAllowedRequestTime ||
            player.CurrentState != InteractState.Idle)
        {
            return;
        }

        bool accepted = SceneTravelPlayback.TryTravel(
            endpoint,
            player,
            SceneTravelActivationKind.Trigger);
        if (!accepted)
            nextAllowedRequestTime = Time.unscaledTime + Mathf.Max(0f, rejectedRequestCooldown);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (suppressedArrivalPlayer == null)
            return;

        IPlayerInteractor player = ResolvePlayer(other);
        if (player == null || suppressedArrivalPlayer != player.Transform)
            return;

        suppressedPlayerColliders.Remove(other);
        suppressedPlayerInsideTrigger = suppressedPlayerColliders.Count > 0;
        if (Time.unscaledTime >= arrivalReactivationTime)
            ReleaseArrivalSuppression();
    }

    /// <summary>
    /// 책임 : 이 trigger를 도착점으로 사용한 플레이어의 즉시 역이동을 억제하고,
    /// 재활성화 전에는 도착점 반대편 경계를 물리 콜라이더로 막는다.
    /// </summary>
    public void SuppressTravelUntilExit(Transform playerTransform)
    {
        EnsureArrivalSuppressionBlocker();
        suppressedArrivalPlayer = playerTransform;
        suppressedPlayerColliders.Clear();
        suppressedPlayerInsideTrigger = false;
        arrivalReactivationTime = Time.unscaledTime + Mathf.Max(0f, arrivalReactivationDelay);
        SetArrivalSuppressionBlockerActive(playerTransform != null);
    }

    private void ReleaseArrivalSuppression()
    {
        suppressedArrivalPlayer = null;
        suppressedPlayerColliders.Clear();
        suppressedPlayerInsideTrigger = false;
        arrivalReactivationTime = 0f;
        SetArrivalSuppressionBlockerActive(false);
    }

    private void EnsureArrivalSuppressionBlocker()
    {
        if (arrivalSuppressionBlocker != null || endpoint == null)
            return;

        Collider2D triggerCollider = ResolveActiveTriggerCollider();
        Transform arrivalAnchor = endpoint.ArrivalAnchor;
        if (triggerCollider == null || !triggerCollider.isTrigger || arrivalAnchor == null)
            return;

        ResolveLocalTriggerBounds(
            triggerCollider,
            out Vector2 localCenter,
            out Vector2 localSize);

        Vector2 localArrival = transform.InverseTransformPoint(arrivalAnchor.position);
        Vector2 inwardDelta = localArrival - localCenter;
        if (inwardDelta.sqrMagnitude <= 0.01f)
            return;

        bool horizontal = Mathf.Abs(inwardDelta.x) >= Mathf.Abs(inwardDelta.y);
        float outwardSign = horizontal
            ? -Mathf.Sign(inwardDelta.x)
            : -Mathf.Sign(inwardDelta.y);
        float thickness = Mathf.Max(0.05f, arrivalBlockerThickness);
        const float overlap = 0.02f;

        Vector2 blockerCenter = localCenter;
        Vector2 blockerSize = localSize;
        if (horizontal)
        {
            blockerCenter.x += outwardSign *
                               (localSize.x * 0.5f + thickness * 0.5f - overlap);
            blockerSize.x = thickness;
        }
        else
        {
            blockerCenter.y += outwardSign *
                               (localSize.y * 0.5f + thickness * 0.5f - overlap);
            blockerSize.y = thickness;
        }

        int wallLayer = LayerMask.NameToLayer("Wall");
        GameObject blockerObject = new("ArrivalSuppressionBlocker")
        {
            layer = wallLayer >= 0 ? wallLayer : gameObject.layer
        };
        blockerObject.transform.SetParent(transform, false);
        blockerObject.transform.localPosition = blockerCenter;
        blockerObject.transform.localRotation = Quaternion.identity;
        blockerObject.transform.localScale = Vector3.one;

        BoxCollider2D blocker = blockerObject.AddComponent<BoxCollider2D>();
        blocker.isTrigger = false;
        blocker.size = blockerSize;
        blocker.enabled = false;
        arrivalSuppressionBlocker = blocker;
    }

    private Collider2D ResolveActiveTriggerCollider()
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
        {
            Collider2D collider = colliders[colliderIndex];
            if (collider.enabled && collider.isTrigger)
                return collider;
        }

        return null;
    }

    private void SetArrivalSuppressionBlockerActive(bool active)
    {
        if (arrivalSuppressionBlocker != null)
            arrivalSuppressionBlocker.enabled = active;
    }

    private void ResolveLocalTriggerBounds(
        Collider2D triggerCollider,
        out Vector2 localCenter,
        out Vector2 localSize)
    {
        if (triggerCollider is BoxCollider2D boxCollider)
        {
            localCenter = boxCollider.offset;
            localSize = boxCollider.size;
            return;
        }

        Bounds worldBounds = triggerCollider.bounds;
        localCenter = transform.InverseTransformPoint(worldBounds.center);
        Vector3 lossyScale = transform.lossyScale;
        localSize = new Vector2(
            worldBounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(lossyScale.x)),
            worldBounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(lossyScale.y)));
    }

    private static IPlayerInteractor ResolvePlayer(Collider2D other)
    {
        if (other == null)
            return null;

        IPlayerInteractor player = other.GetComponent<IPlayerInteractor>();
        if (player != null)
            return player;

        return other.GetComponentInParent<IPlayerInteractor>();
    }
}

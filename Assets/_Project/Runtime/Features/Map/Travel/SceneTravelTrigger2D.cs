using UnityEngine;

/// <summary>
/// 책임 : 플레이어가 2D trigger에 진입하면 같은 GameObject의 SceneTravelEndpoint로 자동 이동을 한 번 요청하고 실패 연타를 제한한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SceneTravelEndpoint))]
public sealed class SceneTravelTrigger2D : MonoBehaviour
{
    [SerializeField] private SceneTravelEndpoint endpoint;
    [SerializeField, Min(0f)] private float rejectedRequestCooldown = 0.75f;

    private float nextAllowedRequestTime;
    private Transform suppressedArrivalPlayer;

    private void Awake()
    {
        if (endpoint == null)
            endpoint = GetComponent<SceneTravelEndpoint>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (endpoint == null || endpoint.IsTravelReserved || Time.unscaledTime < nextAllowedRequestTime)
            return;

        IPlayerInteractor player = ResolvePlayer(other);
        if (player == null || player.CurrentState != InteractState.Idle)
            return;

        if (suppressedArrivalPlayer == player.Transform)
            return;

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
        if (player != null && suppressedArrivalPlayer == player.Transform)
            suppressedArrivalPlayer = null;
    }

    /// <summary>
    /// 책임 : 이 trigger를 도착점으로 사용한 플레이어가 최초로 영역을 빠져나갈 때까지 역방향 이동을 억제한다.
    /// </summary>
    public void SuppressTravelUntilExit(Transform playerTransform)
    {
        suppressedArrivalPlayer = playerTransform;
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

using UnityEngine;

/// <summary>
/// 책임: 적 사망 위치에서 잠시 대기한 뒤 플레이어를 추적하고, 접촉 시 현재 런에 골드를 지급한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class GoldPickup2D : MonoBehaviour
{
    [Header("Homing")]
    [SerializeField, Min(0f)] private float homingDelay = 1f;
    [SerializeField, Min(0f)] private float homingSpeed = 8f;

    private int goldAmount;
    private float homingStartTime;
    private Transform target;
    private bool consumed;

    public int GoldAmount => goldAmount;

    public void Initialize(int amount)
    {
        goldAmount = Mathf.Max(0, amount);
        homingStartTime = Time.time + homingDelay;
        target = null;
        consumed = false;
    }

    private void Update()
    {
        if (consumed || Time.time < homingStartTime)
            return;

        if (target == null)
            target = PlayerRuntimeRegistry.GetPlayerTransform();

        if (target == null)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            homingSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || goldAmount <= 0 || !IsPlayerCollider(other))
            return;

        if (!RunSessionStore.IsRunActive || CurrencyManager.Instance == null) return;
        CurrencyManager.Instance.AddGold(goldAmount);
        consumed = true;
        Destroy(gameObject);
    }

    private static bool IsPlayerCollider(Collider2D candidate)
    {
        if (candidate == null)
            return false;

        Transform player = PlayerRuntimeRegistry.GetPlayerTransform();
        if (player != null)
        {
            Transform candidateTransform = candidate.transform;
            if (candidateTransform == player || candidateTransform.IsChildOf(player))
                return true;

            Rigidbody2D body = candidate.attachedRigidbody;
            if (body != null && (body.transform == player || body.transform.IsChildOf(player)))
                return true;
        }

        return candidate.GetComponentInParent<PlayerInteractor2D>() != null;
    }
}

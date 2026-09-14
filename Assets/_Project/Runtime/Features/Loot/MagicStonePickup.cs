using UnityEngine;

/// <summary>Accelerates toward the player after the drop delay and grants currency on arrival.</summary>
public class MagicStonePickup : MonoBehaviour
{
    [Header("Settings")]
    public int amount = 1;

    [Header("Magnet Effect")]
    public float magnetSpeed = 10f;
    public float delayBeforeMagnet = 0.5f;

    [Header("Acquisition Presentation")]
    [SerializeField] private ParticleSystem gainParticlePrefab;

    private Transform targetPlayer;
    private bool collected;
    private float homingStartTime;
    private float trackingSeconds;

    private void Awake()
    {
        // Existing prefab colliders must remain non-solid, but do not grant currency.
        var collider = GetComponent<Collider2D>();
        if (collider != null) collider.isTrigger = true;
    }

    private void OnEnable()
    {
        collected = false;
        targetPlayer = null;
        trackingSeconds = 0f;
        homingStartTime = Time.time + Mathf.Max(0f, delayBeforeMagnet);
    }

    private void OnDisable() => targetPlayer = null;

    private void Update()
    {
        if (collected || Time.time < homingStartTime) return;
        if (targetPlayer == null) targetPlayer = PlayerRuntimeRegistry.GetPlayerTransform();
        if (targetPlayer == null) return;

        transform.position = Vector3.MoveTowards(transform.position, targetPlayer.position,
            HomingPickupTravel.Speed(magnetSpeed, trackingSeconds) * Time.deltaTime);
        trackingSeconds += Time.deltaTime;
        if ((transform.position - targetPlayer.position).sqrMagnitude <= 0.0001f)
            Collect();
    }

    public bool TryCollectForTravel(Transform player)
    {
        if (collected || targetPlayer == null || targetPlayer != player) return true;
        Collect();
        return collected;
    }

    private void Collect()
    {
        if (collected || amount <= 0 || CurrencyManager.Instance == null || GameDataStore.Data == null)
            return;
        collected = true;
        CurrencyManager.Instance.AddMagicStone(amount);
        PlayerHealParticlePlayback.PlayAttached(gainParticlePrefab, targetPlayer, Vector3.zero);
        Destroy(gameObject);
    }
}

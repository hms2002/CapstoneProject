using UnityEngine;

/// <summary>
/// 책임: 적 사망 위치에서 잠시 대기한 뒤 플레이어를 추적하고, 도착 시 현재 런에 경험치를 지급한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ExperiencePickup2D : MonoBehaviour
{
    [Header("Progression")]
    [SerializeField] private LevelProgressionConfigSO progressionConfig;

    [Header("Homing")]
    [SerializeField, Min(0f)] private float homingDelay = 1f;
    [SerializeField, Min(0f)] private float homingSpeed = 8f;

    [Header("Acquisition Presentation")]
    [SerializeField] private ParticleSystem gainParticlePrefab;

    private int experienceAmount;
    private float homingStartTime;
    private Transform target;
    private bool consumed;
    private float trackingSeconds;

    public int ExperienceAmount => experienceAmount;

    public void Initialize(int amount)
    {
        experienceAmount = Mathf.Max(0, amount);
        homingStartTime = Time.time + homingDelay;
        target = null;
        trackingSeconds = 0f;
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
            HomingPickupTravel.Speed(homingSpeed, trackingSeconds) * Time.deltaTime);
        trackingSeconds += Time.deltaTime;

        if ((transform.position - target.position).sqrMagnitude <= 0.0001f)
            TryCollect();
    }

    public bool TryCollectForTravel(Transform player)
    {
        if (consumed || target == null || target != player) return true;
        TryCollect();
        return consumed;
    }

    private void TryCollect()
    {
        if (consumed || experienceAmount <= 0)
            return;

        if (progressionConfig == null)
        {
            CapstoneDiagnostics.EditorOnlyLog.LogWarning("[ExperiencePickup2D] LevelProgressionConfig is not assigned.", this);
            return;
        }

        consumed = true;
        if (!RunLevelProgression.TryGrantExperience(progressionConfig, experienceAmount, out _))
        {
            consumed = false;
            return;
        }
        PlayerHealParticlePlayback.PlayAttached(gainParticlePrefab, target, Vector3.zero);
        Destroy(gameObject);
    }

}

using UnityEngine;

/// <summary>
/// 책임: 전투 중 바닥에서 대기하고, 전투 종료 후 함께 떠올라 플레이어에게 이동한 뒤 런 경험치를 지급한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ExperiencePickup2D : MonoBehaviour
{
    [Header("Progression")]
    [SerializeField] private LevelProgressionConfigSO progressionConfig;

    [Header("Homing")]
    [Tooltip("전투 종료 후 흡수 전 떠오르는 시간(초)")]
    [SerializeField, Min(0f)] private float homingDelay = 0.15f;
    [Tooltip("초당 흡수 진행률. 3.333333이면 거리에 관계없이 약 0.3초에 도착합니다.")]
    [SerializeField, Min(0.01f)] private float homingSpeed = 3.333333f;

    [Header("Combat Ground Presentation")]
    [SerializeField] private SpriteRenderer pickupRenderer;
    [SerializeField, Range(0f, 1f)] private float groundAlpha = 0.45f;
    [SerializeField, Min(0f)] private float riseHeight = 0.25f;

    [Header("Acquisition Presentation")]
    [SerializeField] private ParticleSystem gainParticlePrefab;

    private int experienceAmount;
    private Transform target;
    private bool consumed;
    private readonly CombatRewardPickupMotion motion = new();

    public int ExperienceAmount => experienceAmount;

    public void Initialize(int amount)
    {
        experienceAmount = Mathf.Max(0, amount);
        target = null;
        consumed = false;
        motion.Reset(transform, pickupRenderer, groundAlpha);
    }

    private void OnEnable() => motion.Reset(transform, pickupRenderer, groundAlpha);

    private void OnDisable()
    {
        motion.Cancel();
        target = null;
    }

    private void LateUpdate()
    {
        if (consumed)
            return;

        if (target == null)
            target = PlayerRuntimeRegistry.GetPlayerTransform();

        if (motion.Tick(target, HomingPickupTravel.IsCombatActiveForPickupFrame,
            groundAlpha, riseHeight, homingDelay, homingSpeed))
            TryCollect();
    }

    public bool TryCollectForTravel(Transform player)
    {
        if (consumed || !isActiveAndEnabled || player == null) return true;
        if (target == null) target = PlayerRuntimeRegistry.GetPlayerTransform();
        if (target != player) return true;
        // Grounded/rising rewards must not be lost to an immediate post-clear portal.
        if (MonsterSpawnRoomGroup.IsPlayerInCombat) return false;
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

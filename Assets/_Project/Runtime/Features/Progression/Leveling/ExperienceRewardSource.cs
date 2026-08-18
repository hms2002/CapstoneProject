using UnityEngine;

/// <summary>
/// 책임: Enemy 공통 사망 이벤트를 경험치 보상 픽업 생성으로 변환한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public sealed class ExperienceRewardSource : MonoBehaviour
{
    [Header("Reward")]
    [SerializeField, Min(0)] private int baseExperience;
    [SerializeField] private bool grantExperience = true;
    [SerializeField, Min(0f)] private float experienceMultiplier = 1f;

    [Header("Pickup")]
    [SerializeField] private ExperiencePickup2D pickupPrefab;
    [SerializeField, Min(1)] private int experiencePerPickup = 5;
    [SerializeField, Min(1)] private int maximumPickupCount = 30;
    [SerializeField, Min(0f)] private float pickupScatterRadius = 0.8f;

    private Enemy enemy;

    public int BaseExperience => baseExperience;
    public bool GrantsExperience => grantExperience;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        if (enemy == null)
            enemy = GetComponent<Enemy>();

        if (enemy != null)
            enemy.DeathStarted += HandleDeathStarted;
    }

    private void OnDisable()
    {
        if (enemy != null)
            enemy.DeathStarted -= HandleDeathStarted;
    }

    /// <summary>소환, 부활, 무한 스포너 같은 런타임 생성 경로가 이 개체의 EXP 지급 여부를 덮어쓴다.</summary>
    public void SetGrantExperience(bool shouldGrant)
    {
        grantExperience = shouldGrant;
    }

    /// <summary>런 변형치 등 외부 규칙이 이 개체의 최종 EXP 배율을 덮어쓴다.</summary>
    public void SetExperienceMultiplier(float multiplier)
    {
        experienceMultiplier = Mathf.Max(0f, multiplier);
    }

    private void HandleDeathStarted(Enemy defeatedEnemy)
    {
        if (!grantExperience || baseExperience <= 0 || pickupPrefab == null || !RunSessionStore.IsRunActive)
            return;

        int finalExperience = Mathf.Max(0, Mathf.RoundToInt(baseExperience * experienceMultiplier));
        if (finalExperience <= 0)
            return;

        ExperiencePickupDropSpawner.SpawnDistributed(
            pickupPrefab,
            defeatedEnemy.transform.position,
            finalExperience,
            experiencePerPickup,
            maximumPickupCount,
            pickupScatterRadius);
    }
}

/// <summary>
/// 책임: 총 경험치를 보존하면서 제한된 수의 개별 경험치 픽업으로 나누어 생성한다.
/// </summary>
internal static class ExperiencePickupDropSpawner
{
    private const float GoldenAngleRadians = 2.39996323f;

    public static int SpawnDistributed(
        ExperiencePickup2D pickupPrefab,
        Vector3 origin,
        int totalExperience,
        int experiencePerPickup,
        int maximumPickupCount,
        float scatterRadius)
    {
        if (pickupPrefab == null || totalExperience <= 0)
            return 0;

        int safeExperiencePerPickup = Mathf.Max(1, experiencePerPickup);
        int safeMaximumPickupCount = Mathf.Max(1, maximumPickupCount);
        int pickupCount = Mathf.Clamp(
            Mathf.CeilToInt(totalExperience / (float)safeExperiencePerPickup),
            1,
            safeMaximumPickupCount);
        int baseAmount = totalExperience / pickupCount;
        int remainder = totalExperience % pickupCount;
        float safeScatterRadius = Mathf.Max(0f, scatterRadius);

        for (int i = 0; i < pickupCount; i++)
        {
            float normalizedRadius = Mathf.Sqrt((i + 0.5f) / pickupCount);
            float angle = i * GoldenAngleRadians;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) *
                             (safeScatterRadius * normalizedRadius);
            int amount = baseAmount + (i < remainder ? 1 : 0);

            ExperiencePickup2D pickup = Object.Instantiate(
                pickupPrefab,
                origin + offset,
                Quaternion.identity);
            pickup.Initialize(amount);
        }

        return pickupCount;
    }
}

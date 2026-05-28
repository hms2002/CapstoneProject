using UnityEngine;
using CapstoneAudio;

[DisallowMultipleComponent]
[RequireComponent(typeof(Candlestick), typeof(CandlestickSeal))]
public class CandlestickShieldBreakerLauncher : MonoBehaviour
{
    // 이 클래스의 책임:
    // 촛대가 봉인 해제될 때 마녀 보호막이 살아 있으면 보호막 파괴 전용 투사체를 발사한다.

    private static readonly SoundRef FallbackLaunchSound = SoundRef.FromKey("sound_candlestick_ShotShieldBreaker");

    [SerializeField] private Candlestick ownerCandlestick;
    [SerializeField] private CandlestickSeal seal;
    [SerializeField] private Witch cachedWitch;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 7.5f;
    [SerializeField] private float projectileLifetime = 2.2f;
    [SerializeField] private float projectileHitRadius = 0.08f;
    [SerializeField] private float spawnHeightOffset = 0.35f;
    [SerializeField] private SoundRef launchSound;
    [SerializeField] private SoundRef shieldHitSound;

    private void Awake()
    {
        if (ownerCandlestick == null)
            ownerCandlestick = GetComponent<Candlestick>();

        if (seal == null)
            seal = GetComponent<CandlestickSeal>();

        if (cachedWitch == null)
            cachedWitch = FindAnyObjectByType<Witch>();

        if (seal != null)
            seal.SealChanged += OnSealChanged;
    }

    private void OnDestroy()
    {
        if (seal != null)
            seal.SealChanged -= OnSealChanged;
    }

    /// <summary>
    /// 책임 :
    /// - 촛대 봉인 해제 시점에만 보호막 파괴 투사체 발사를 시도한다.
    /// </summary>
    private void OnSealChanged(bool isSealed)
    {
        if (isSealed)
            return;

        Witch witch = ResolveWitch();
        if (witch == null || witch.ShieldController == null || !witch.ShieldController.HasShield)
            return;

        Vector3 spawnPosition = ownerCandlestick != null
            ? ownerCandlestick.transform.position + new Vector3(0f, spawnHeightOffset, 0f)
            : transform.position;

        PlayLaunchSound(spawnPosition);

        if (projectilePrefab != null)
        {
            GameObject projectileObject = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
            WitchShieldBreakerProjectile2D projectile = projectileObject.GetComponent<WitchShieldBreakerProjectile2D>();
            if (projectile != null)
            {
                projectile.Setup(witch.ShieldController, projectileSpeed, projectileLifetime, projectileHitRadius, shieldHitSound);
                return;
            }

            Destroy(projectileObject);
        }

        WitchShieldBreakerProjectile2D.Spawn(
            spawnPosition,
            witch.ShieldController,
            projectileSpeed,
            projectileLifetime,
            projectileHitRadius,
            shieldHitSound);
    }

    /// <summary>보호막 파괴 투사체 발사 시점의 사운드를 재생합니다.</summary>
    private void PlayLaunchSound(Vector3 spawnPosition)
    {
        SoundRef sound = launchSound.IsSet ? launchSound : FallbackLaunchSound;
        SoundPlaybackUtility.Play(
            sound,
            causer: gameObject,
            position: spawnPosition,
            sourceObject: this);
    }

    private Witch ResolveWitch()
    {
        if (cachedWitch == null)
            cachedWitch = FindAnyObjectByType<Witch>();

        return cachedWitch;
    }
}

using UnityEngine;
using CapstoneAudio;

[DisallowMultipleComponent]
public class WitchShieldBreakerProjectile2D : MonoBehaviour
{
    // 이 클래스의 책임:
    // 촛대에서 마녀 보호막으로 날아가 보호막 단계만 감소시키는 전용 투사체를 표현한다.

    private static Sprite s_runtimeSprite;

    private WitchShieldController targetShield;
    private float speed;
    private float lifetime;
    private float hitRadius;
    private SoundRef hitSound;

    /// <summary>
    /// 책임 :
    /// - 보호막 파괴 투사체 런타임 오브젝트를 생성하고 최소 시각/이동 구성을 마친다.
    /// </summary>
    public static WitchShieldBreakerProjectile2D Spawn(
        Vector3 spawnPosition,
        WitchShieldController targetShield,
        float speed,
        float lifetime,
        float hitRadius = 0.08f,
        SoundRef hitSound = default)
    {
        if (targetShield == null)
            return null;

        GameObject projectileObject = new GameObject("WitchShieldBreakerProjectile");
        projectileObject.transform.position = spawnPosition;

        SpriteRenderer spriteRenderer = projectileObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetRuntimeSprite();
        spriteRenderer.color = new Color(1f, 0.96f, 0.56f, 0.95f);
        spriteRenderer.sortingLayerID = targetShield.GetComponent<SpriteRenderer>() != null
            ? targetShield.GetComponent<SpriteRenderer>().sortingLayerID
            : 0;
        spriteRenderer.sortingOrder = targetShield.GetComponent<SpriteRenderer>() != null
            ? targetShield.GetComponent<SpriteRenderer>().sortingOrder + 3
            : 3;

        projectileObject.transform.localScale = new Vector3(0.22f, 0.22f, 1f);

        WitchShieldBreakerProjectile2D projectile = projectileObject.AddComponent<WitchShieldBreakerProjectile2D>();
        projectile.Setup(targetShield, speed, lifetime, hitRadius, hitSound);
        return projectile;
    }

    /// <summary>
    /// 책임 :
    /// - 목표 보호막과 이동/충돌 기본값을 초기화한다.
    /// </summary>
    public void Setup(
        WitchShieldController shieldTarget,
        float projectileSpeed,
        float projectileLifetime,
        float projectileHitRadius,
        SoundRef projectileHitSound = default)
    {
        targetShield = shieldTarget;
        speed = Mathf.Max(0.01f, projectileSpeed);
        lifetime = Mathf.Max(0.1f, projectileLifetime);
        hitRadius = Mathf.Max(0.05f, projectileHitRadius);
        hitSound = projectileHitSound;
    }

    private void Update()
    {
        if (targetShield == null)
        {
            Destroy(gameObject);
            return;
        }

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition = targetShield.transform.position + new Vector3(0f, 0.2f, 0f);
        Vector3 toTarget = targetPosition - transform.position;
        float distance = toTarget.magnitude;

        if (distance <= hitRadius)
        {
            if (targetShield.TryApplyShieldHit(1))
            {
                SoundPlaybackUtility.Play(
                    hitSound,
                    instigator: targetShield.gameObject,
                    causer: gameObject,
                    position: transform.position,
                    sourceObject: this);
            }
            Destroy(gameObject);
            return;
        }

        Vector3 direction = distance > 0.0001f ? toTarget / distance : Vector3.zero;
        transform.position += direction * speed * Time.deltaTime;
    }

    private static Sprite GetRuntimeSprite()
    {
        if (s_runtimeSprite != null)
            return s_runtimeSprite;

        Rect rect = new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height);
        s_runtimeSprite = Sprite.Create(Texture2D.whiteTexture, rect, new Vector2(0.5f, 0.5f), 100f);
        return s_runtimeSprite;
    }
}

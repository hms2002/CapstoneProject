using CapstoneAudio;
using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 월드 체력 회복 픽업의 수집 판정, 회복 적용, 드롭/대기/수집 표현을 관리한다.
/// 수집 가능 오브젝트의 생성/착지/획득/제거 상태를 외부 관찰자에게 알린다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FieldHealPickup2D : MonoBehaviour
{
    private static readonly SoundRef CollectSound = SoundRef.FromKey("sound_player_GetFiledHeart");

    [Header("Heal")]
    [SerializeField] private AttributeDefinition healthAttribute;
    [SerializeField, Min(1)] private int healAmount = 1;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite icon;

    [Header("Drop Presentation")]
    [SerializeField] private Transform visualRoot;
    [SerializeField, Min(0.1f)] private float minDropDuration = 0.25f;
    [SerializeField, Min(0.1f)] private float maxDropDuration = 0.55f;
    [SerializeField, Min(0f)] private float dropDurationPerUnit = 0.08f;
    [SerializeField, Min(0f)] private float minDropArcHeight = 0.25f;
    [SerializeField, Min(0f)] private float dropArcHeightPerUnit = 0.2f;
    [SerializeField, Min(0f)] private float maxDropArcHeight = 0.65f;

    [Header("Idle Presentation")]
    [SerializeField, Min(0f)] private float idleFloatAmplitude = 0.04f;
    [SerializeField, Min(0f)] private float idleFloatFrequency = 1.4f;
    [SerializeField, Min(0f)] private float heartbeatScaleAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float heartbeatFrequency = 2.2f;
    [SerializeField, Min(0.01f)] private float heartbeatScalePulseDuration = 0.22f;

    [Header("Collect Presentation")]
    [SerializeField] private ParticleSystem collectParticlePrefab;
    [SerializeField] private ParticleSystem healParticlePrefab;
    [SerializeField] private Vector3 healParticleLocalOffset = Vector3.zero;

    private bool collected;
    private Vector3 dropLandingPosition;
    public bool IsCollected => collected;
    public Vector3 GroundPosition => interactionLocked ? dropLandingPosition : transform.position;
    public static event System.Action<FieldHealPickup2D> WorldStateChanged;

    private void OnEnable() => WorldStateChanged?.Invoke(this);
    private void OnDisable() => WorldStateChanged?.Invoke(this);
    private bool interactionLocked;
    private Coroutine dropRoutine;
    private Vector3 visualBaseLocalPosition;
    private Vector3 visualBaseLocalScale = Vector3.one;
    private bool hasVisualBaseTransform;
    private float idleTimeOffset;

    public void Configure(AttributeDefinition healthAttributeOverride, int healAmountOverride, Sprite iconOverride)
    {
        healthAttribute = healthAttributeOverride;
        healAmount = Mathf.Max(1, healAmountOverride);
        icon = iconOverride;
        RefreshVisual();
    }

    private void Awake()
    {
        CacheReferences();
        CaptureVisualBaseTransform();
        idleTimeOffset = Random.value * 10f;

        Collider2D pickupCollider = GetComponent<Collider2D>();
        if (pickupCollider != null)
            pickupCollider.isTrigger = true;

        RefreshVisual();
    }

    private void OnValidate()
    {
        if (healAmount < 1)
            healAmount = 1;

        maxDropDuration = Mathf.Max(minDropDuration, maxDropDuration);
        maxDropArcHeight = Mathf.Max(minDropArcHeight, maxDropArcHeight);
        heartbeatScalePulseDuration = Mathf.Max(0.01f, heartbeatScalePulseDuration);

        CacheReferences();

        RefreshVisual();
    }

    private void LateUpdate()
    {
        if (collected || interactionLocked)
            return;

        TickIdlePresentation();
    }

    private void OnDestroy()
    {
        StopDropRoutine();
    }

    public void PlayDrop(Vector3 startPosition, Vector3 landingPosition)
    {
        StopDropRoutine();
        CaptureVisualBaseTransform();
        ResetVisualTransform();

        transform.position = startPosition;
        dropLandingPosition = landingPosition;
        interactionLocked = true;
        WorldStateChanged?.Invoke(this);

        float distance = Vector2.Distance(startPosition, landingPosition);
        float duration = Mathf.Clamp(minDropDuration + distance * dropDurationPerUnit, minDropDuration, maxDropDuration);
        float arcHeight = Mathf.Clamp(minDropArcHeight + distance * dropArcHeightPerUnit, minDropArcHeight, maxDropArcHeight);

        dropRoutine = StartCoroutine(PlayDropRoutine(startPosition, landingPosition, arcHeight, duration));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider2D other)
    {
        if (collected || interactionLocked || other == null)
            return;

        if (!TryResolvePlayerAttributeSet(other, out AttributeSet attributeSet, out Transform playerTransform))
            return;

        bool didHeal = TryApplyHeal(attributeSet);
        if (!didHeal)
            return;

        collected = true;
        WorldStateChanged?.Invoke(this);
        SoundPlaybackUtility.Play(CollectSound, instigator: playerTransform.gameObject, causer: gameObject, position: transform.position, sourceObject: this);
        PlayerHealParticlePlayback.PlayAttached(healParticlePrefab, playerTransform, healParticleLocalOffset);
        PlayCollectPresentation();
        Destroy(gameObject);
    }

    private bool TryApplyHeal(AttributeSet attributeSet)
    {
        if (attributeSet == null || healthAttribute == null)
            return false;

        float before = attributeSet.GetCurrentValue(healthAttribute);
        if (!attributeSet.TryModifyAttributeValue(healthAttribute, healAmount, this))
            return false;

        float after = attributeSet.GetCurrentValue(healthAttribute);
        return after > before;
    }

    private bool TryResolvePlayerAttributeSet(Collider2D other, out AttributeSet attributeSet, out Transform playerTransform)
    {
        attributeSet = null;
        playerTransform = null;

        PickupCollector2D pickupCollector = other.GetComponent<PickupCollector2D>();
        if (pickupCollector == null)
            return false;

        attributeSet = pickupCollector.AttributeSet;
        if (attributeSet == null)
            return false;

        PlayerInteractor2D player = pickupCollector.PlayerInteractor;
        playerTransform = player != null ? player.transform : attributeSet.transform;
        return playerTransform != null;
    }

    private void RefreshVisual()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.sprite = icon;
        spriteRenderer.enabled = spriteRenderer.sprite != null;
    }

    private void CacheReferences()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (visualRoot == null && spriteRenderer != null)
            visualRoot = spriteRenderer.transform;
    }

    private void CaptureVisualBaseTransform()
    {
        if (hasVisualBaseTransform || visualRoot == null)
            return;

        visualBaseLocalPosition = visualRoot.localPosition;
        visualBaseLocalScale = visualRoot.localScale;
        hasVisualBaseTransform = true;
    }

    private void ResetVisualTransform()
    {
        if (visualRoot == null || visualRoot == transform || !hasVisualBaseTransform)
            return;

        visualRoot.localPosition = visualBaseLocalPosition;
        visualRoot.localScale = visualBaseLocalScale;
    }

    private void TickIdlePresentation()
    {
        if (visualRoot == null || visualRoot == transform)
            return;

        CaptureVisualBaseTransform();

        float time = Time.time + idleTimeOffset;
        float floatOffset = idleFloatAmplitude > 0f && idleFloatFrequency > 0f
            ? Mathf.Sin(time * idleFloatFrequency * Mathf.PI * 2f) * idleFloatAmplitude
            : 0f;
        float heartbeatScale = ResolveHeartbeatScale(time);

        visualRoot.localPosition = visualBaseLocalPosition + Vector3.up * floatOffset;
        visualRoot.localScale = visualBaseLocalScale * heartbeatScale;
    }

    private float ResolveHeartbeatScale(float time)
    {
        if (heartbeatScaleAmplitude <= 0f || heartbeatFrequency <= 0f)
            return 1f;

        float beatDuration = 1f / heartbeatFrequency;
        float pulseDuration = Mathf.Min(Mathf.Max(0.01f, heartbeatScalePulseDuration), beatDuration);
        float beatTime = Mathf.Repeat(time, beatDuration);
        if (beatTime >= pulseDuration)
            return 1f;

        float pulseT = Mathf.Clamp01(beatTime / pulseDuration);
        float pulse = pulseT < 0.5f
            ? Mathf.SmoothStep(0f, 1f, pulseT * 2f)
            : Mathf.SmoothStep(1f, 0f, (pulseT - 0.5f) * 2f);

        return 1f + pulse * heartbeatScaleAmplitude;
    }

    private void PlayCollectPresentation()
    {
        if (collectParticlePrefab == null)
            return;

        ParticleSystem particle = Instantiate(collectParticlePrefab, transform.position, Quaternion.identity);
        particle.gameObject.SetActive(true);
        particle.Play(true);

        ParticleSystem.MainModule main = particle.main;
        float lifetime = main.duration + main.startLifetime.constantMax;
        Destroy(particle.gameObject, Mathf.Max(0.1f, lifetime));
    }

    private void StopDropRoutine()
    {
        if (dropRoutine == null)
            return;

        StopCoroutine(dropRoutine);
        dropRoutine = null;
    }

    private IEnumerator PlayDropRoutine(Vector3 startPosition, Vector3 landingPosition, float arcHeight, float duration)
    {
        Collider2D pickup = GetComponent<Collider2D>();
        ContactFilter2D walls = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = LayerMask.GetMask("Wall"),
            useTriggers = false
        };
        var hits = new RaycastHit2D[16];
        var overlaps = new Collider2D[16];
        Vector2 velocity = ((Vector2)landingPosition - (Vector2)startPosition) / Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float dt = Mathf.Min(Time.deltaTime, duration - elapsed);
            elapsed += dt;
            ResolveDropWallOverlap(pickup, walls, overlaps);
            float remainingDistance = velocity.magnitude * dt;
            Vector2 direction = velocity.sqrMagnitude > 0f ? velocity.normalized : Vector2.zero;
            for (int bounce = 0; bounce < 6 && remainingDistance > 0.0001f; bounce++)
            {
                Physics2D.SyncTransforms();
                int count = pickup.Cast(direction, walls, hits, remainingDistance + 0.02f, ignoreSiblingColliders: false);
                RaycastHit2D nearest = default;
                float nearestDistance = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    if (hits[i].collider == null || Vector2.Dot(direction, hits[i].normal) >= 0f) continue;
                    if (hits[i].distance < nearestDistance)
                    {
                        nearest = hits[i];
                        nearestDistance = nearest.distance;
                    }
                }
                if (nearest.collider == null)
                {
                    transform.position += (Vector3)(direction * remainingDistance);
                    break;
                }
                float travel = Mathf.Clamp(nearestDistance - 0.02f, 0f, remainingDistance);
                transform.position += (Vector3)(direction * travel);
                remainingDistance -= travel;
                direction = Vector2.Reflect(direction, nearest.normal).normalized;
                velocity = direction * velocity.magnitude;
            }
            ResolveDropWallOverlap(pickup, walls, overlaps);
            dropLandingPosition = transform.position;
            if (visualRoot != null && visualRoot != transform)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                visualRoot.localPosition = visualBaseLocalPosition + Vector3.up * (4f * arcHeight * t * (1f - t));
            }
            yield return null;
        }

        ResolveDropWallOverlap(pickup, walls, overlaps);
        dropLandingPosition = transform.position;
        dropRoutine = null;
        interactionLocked = false;
        ResetVisualTransform();
        WorldStateChanged?.Invoke(this);
    }

    private void ResolveDropWallOverlap(Collider2D pickup, ContactFilter2D walls, Collider2D[] overlaps)
    {
        for (int pass = 0; pass < 6; pass++)
        {
            Physics2D.SyncTransforms();
            int count = pickup.Overlap(walls, overlaps);
            bool corrected = false;
            for (int i = 0; i < count; i++)
            {
                ColliderDistance2D distance = pickup.Distance(overlaps[i]);
                if (!distance.isOverlapped) continue;
                transform.position += (Vector3)(distance.normal * (distance.distance - 0.02f));
                corrected = true;
                Physics2D.SyncTransforms();
            }
            if (!corrected) break;
        }
    }
}

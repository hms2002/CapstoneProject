using CapstoneAudio;
using UnityEngine;
using DG.Tweening;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FieldHealPickup2D : MonoBehaviour
{
    // 이 클래스의 책임:
    // 월드에 떨어진 체력 회복 픽업을 관리하고, 유효한 PickupCollector2D와 접촉했을 때만 회복 후 자신을 제거한다.
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
    private bool interactionLocked;
    private Sequence dropSequence;
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
        KillDropSequence();
    }

    public void PlayDrop(Vector3 startPosition, Vector3 landingPosition)
    {
        KillDropSequence();
        CaptureVisualBaseTransform();
        ResetVisualTransform();

        transform.position = startPosition;
        interactionLocked = true;

        float distance = Vector2.Distance(startPosition, landingPosition);
        float duration = Mathf.Clamp(minDropDuration + distance * dropDurationPerUnit, minDropDuration, maxDropDuration);
        float arcHeight = Mathf.Clamp(minDropArcHeight + distance * dropArcHeightPerUnit, minDropArcHeight, maxDropArcHeight);

        dropSequence = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .SetUpdate(UpdateType.Normal);

        dropSequence.Append(DOVirtual.Float(0f, 1f, duration, t =>
            {
                transform.position = EvaluateDropPosition(startPosition, landingPosition, arcHeight, t);
            })
            .SetEase(Ease.Linear));

        dropSequence.OnComplete(() =>
        {
            dropSequence = null;
            transform.position = landingPosition;
            interactionLocked = false;
            ResetVisualTransform();
        });
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
        if (visualRoot == null || !hasVisualBaseTransform)
            return;

        visualRoot.localPosition = visualBaseLocalPosition;
        visualRoot.localScale = visualBaseLocalScale;
    }

    private void TickIdlePresentation()
    {
        if (visualRoot == null)
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

    private void KillDropSequence()
    {
        if (dropSequence == null)
            return;

        dropSequence.Kill();
        dropSequence = null;
    }

    private static Vector3 EvaluateDropPosition(Vector3 startPosition, Vector3 landingPosition, float arcHeight, float t)
    {
        t = Mathf.Clamp01(t);
        Vector3 position = Vector3.LerpUnclamped(startPosition, landingPosition, t);
        position.y += 4f * arcHeight * t * (1f - t);
        return position;
    }
}

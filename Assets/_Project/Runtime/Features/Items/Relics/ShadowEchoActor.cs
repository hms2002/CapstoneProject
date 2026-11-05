using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>고정된 출발점의 대기/단발 피해를 소유하고 작성된 시각물에 같은 타이밍과 범위를 투영한다.</summary>
public sealed class ShadowEchoActor : MonoBehaviour
{
    public SpriteRenderer ghost;
    public MeshRenderer warningRing;
    public MeshRenderer burstRing;
    public MeshRenderer burstFill;
    public ParticleSystem wisps;
    public ParticleSystem burstParticles;
    public Color echoColor = new(0.66f, 0.40f, 0.95f, 1f);

    private readonly List<Collider2D> candidates = new(32);
    private readonly HashSet<GameObject> hitTargets = new();
    private MaterialPropertyBlock properties;
    private AbilitySystem owner;
    private CombatHitPayload payload;
    private LayerMask targetLayers;
    private float delay;
    private float radius;
    private float yScale;
    private float burstDuration;
    private float elapsed;
    private float burstElapsed;
    private bool initialized;
    private bool confirmed;
    private bool exploded;

    private void Awake() => properties = new MaterialPropertyBlock();

    public void Initialize(AbilitySystem system, RelicLogic_ShadowEcho data, SpriteRenderer source, CombatHitPayload hitPayload)
    {
        owner = system;
        payload = hitPayload;
        targetLayers = data.targetLayers;
        delay = data.delay;
        radius = data.radius;
        yScale = data.circleYScale;
        burstDuration = data.burstDuration;
        ghost.sprite = source.sprite;
        ghost.flipX = source.flipX;
        ghost.flipY = source.flipY;
        ghost.sortingLayerID = source.sortingLayerID;
        ghost.sortingOrder = source.sortingOrder - 1;
        ghost.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        ghost.transform.localScale = source.transform.lossyScale;
        warningRing.transform.localScale = RangeScale();
        burstRing.transform.localScale = RangeScale();
        burstFill.transform.localScale = RangeScale();
        burstRing.gameObject.SetActive(false);
        burstFill.gameObject.SetActive(false);
        wisps.Play(true);
        initialized = true;
        UpdateWarning();
    }

    public void ConfirmDash() => confirmed = true;

    private Vector3 RangeScale() => new(radius, radius * yScale, 1f);

    private void Update()
    {
        if (!initialized) return;
        if (owner == null || !owner.gameObject.activeInHierarchy ||
            CombatTargetDeathUtility.IsPlayerDeathSequenceRunning(owner.transform)) { Cancel(); return; }
        if (!exploded)
        {
            elapsed += Time.deltaTime;
            UpdateWarning();
            if (elapsed < delay || !confirmed) return;
            // Set the guard before callbacks: a hit can unequip the relic, kill actors or change scenes.
            exploded = true;
            SoundPlaybackUtility.Play(RelicLogic_ShadowEcho.ExplosionSound,
                causer: owner.gameObject, position: transform.position, sourceObject: this);
            warningRing.gameObject.SetActive(false);
            burstRing.gameObject.SetActive(true);
            burstFill.gameObject.SetActive(true);
            wisps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            burstParticles.Play(true);
            ApplyDamage();
        }
        if (!isActiveAndEnabled) return;
        burstElapsed += Time.deltaTime;
        float p = Mathf.Clamp01(burstElapsed / burstDuration);
        ghost.color = WithAlpha(0.5f * (1f - Mathf.Clamp01(p * 2f)));
        burstRing.transform.localScale = RangeScale() * Mathf.Lerp(0.75f, 1f, p);
        SetColor(burstRing, 0.85f * (1f - p));
        SetColor(burstFill, 0.24f * (1f - p));
        if (p >= 1f) Cancel();
    }

    private void UpdateWarning()
    {
        ghost.color = WithAlpha(0.5f + 0.06f * Mathf.Sin(elapsed * 12f));
        SetColor(warningRing, 0.48f + 0.12f * Mathf.Sin(elapsed * 12f));
    }

    private Color WithAlpha(float alpha) => new(echoColor.r, echoColor.g, echoColor.b, alpha);

    private void SetColor(MeshRenderer renderer, float alpha)
    {
        properties.SetColor("_Color", WithAlpha(alpha));
        renderer.SetPropertyBlock(properties);
    }

    private void ApplyDamage()
    {
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(targetLayers);
        Physics2D.OverlapCircle(transform.position, radius, filter, candidates);
        foreach (Collider2D candidate in candidates)
        {
            if (!isActiveAndEnabled) break;
            if (candidate == null || candidate.GetComponent<CombatHurtbox2D>() == null ||
                !TopDownEllipseHitUtility2D.ContainsCollider(candidate, transform.position, radius * 2f, yScale)) continue;
            GameObject root = CombatTargetResolver2D.ResolveDamageTarget(candidate);
            if (root == null || !hitTargets.Add(root)) continue;
            Enemy enemy = root.GetComponent<Enemy>();
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDead) continue;
            CombatHitPayloadApplier.Apply(root, payload, CommonMonsterCombatUtility.ResolveAimPoint(root));
        }
    }

    public void Cancel()
    {
        initialized = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDisable()
    {
        initialized = false;
        wisps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        burstParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}

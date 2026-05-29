using System.Collections;
using UnityEngine;

/// <summary>
/// 책임:
/// - WaterJet 레이저가 벽에 닿은 지점에서 물방울이 벽 법선 방향으로 튀는 원샷 파티클을 재생한다.
/// - 프리팹 ParticleSystem authoring이 덜 되어 있어도 런타임에서 짧은 물튀김 파라미터로 보정한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WaterJetWallHitParticleEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem particleSystemRef;
    [SerializeField, Min(1)] private int burstCount = 24;
    [SerializeField, Min(0f)] private float lifetimeMin = 0.18f;
    [SerializeField, Min(0f)] private float lifetimeMax = 0.38f;
    [SerializeField, Min(0f)] private float speedMin = 2.8f;
    [SerializeField, Min(0f)] private float speedMax = 5.4f;
    [SerializeField, Min(0f)] private float tangentSpread = 1.55f;
    [SerializeField, Min(0f)] private float normalJitterDegrees = 22f;
    [SerializeField, Min(0f)] private float sizeMin = 0.045f;
    [SerializeField, Min(0f)] private float sizeMax = 0.11f;
    [SerializeField] private Color colorStart = new(0.55f, 1f, 1f, 0.95f);
    [SerializeField] private Color colorEnd = new(0.15f, 0.75f, 1f, 0f);
    [SerializeField, Min(0f)] private float destroyPaddingSeconds = 0.08f;

    private Coroutine destroyRoutine;

    private void Awake()
    {
        EnsureParticleSystem();
        ConfigureParticleSystem();
    }

    private void OnValidate()
    {
        if (particleSystemRef == null)
            particleSystemRef = GetComponentInChildren<ParticleSystem>(true);
    }

    /// <summary>벽 hit point와 normal을 기준으로 물튀김 파티클을 즉시 재생합니다.</summary>
    public void Play(Vector2 hitPoint, Vector2 wallNormal)
    {
        EnsureParticleSystem();
        ConfigureParticleSystem();

        Vector2 safeNormal = wallNormal.sqrMagnitude > 0.0001f ? wallNormal.normalized : Vector2.up;
        transform.SetPositionAndRotation(
            new Vector3(hitPoint.x, hitPoint.y, transform.position.z),
            Quaternion.Euler(0f, 0f, Mathf.Atan2(safeNormal.y, safeNormal.x) * Mathf.Rad2Deg));

        particleSystemRef.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleSystemRef.Play(withChildren: true);

        Vector2 tangent = new(-safeNormal.y, safeNormal.x);
        for (int i = 0; i < burstCount; i++)
        {
            float normalAngle = Random.Range(-normalJitterDegrees, normalJitterDegrees);
            Vector2 direction = (Quaternion.Euler(0f, 0f, normalAngle) * safeNormal).normalized;
            Vector2 velocity = direction * Random.Range(speedMin, speedMax) +
                               tangent * Random.Range(-tangentSpread, tangentSpread);

            ParticleSystem.EmitParams emitParams = new()
            {
                position = transform.position,
                velocity = velocity,
                startLifetime = Random.Range(lifetimeMin, lifetimeMax),
                startSize = Random.Range(sizeMin, sizeMax),
                startColor = Color.Lerp(colorStart, colorEnd, Random.Range(0f, 0.25f))
            };
            particleSystemRef.Emit(emitParams, 1);
        }

        if (destroyRoutine != null)
            StopCoroutine(destroyRoutine);

        destroyRoutine = StartCoroutine(DestroyAfterLifetime());
    }

    private void EnsureParticleSystem()
    {
        if (particleSystemRef == null)
            particleSystemRef = GetComponentInChildren<ParticleSystem>(true);

        if (particleSystemRef == null)
            particleSystemRef = gameObject.AddComponent<ParticleSystem>();
    }

    private void ConfigureParticleSystem()
    {
        ParticleSystem.MainModule main = particleSystemRef.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(colorStart, colorEnd);
        main.gravityModifier = 0.35f;
        main.maxParticles = Mathf.Max(64, burstCount * 2);

        ParticleSystem.EmissionModule emission = particleSystemRef.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;
        emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());

        ParticleSystem.ShapeModule shape = particleSystemRef.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystemRef.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(colorStart, 0f),
                new GradientColorKey(new Color(0.35f, 0.9f, 1f), 0.55f),
                new GradientColorKey(colorEnd, 1f)
            },
            new[]
            {
                new GradientAlphaKey(colorStart.a, 0f),
                new GradientAlphaKey(0.7f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystemRef.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new(
            new Keyframe(0f, 1f),
            new Keyframe(0.65f, 0.72f),
            new Keyframe(1f, 0.18f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer particleRenderer = particleSystemRef.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.sortingLayerName = "Projectile";
            particleRenderer.sortingOrder = 3;
        }
    }

    private IEnumerator DestroyAfterLifetime()
    {
        float maxLifetime = Mathf.Max(lifetimeMin, lifetimeMax) + destroyPaddingSeconds;
        yield return new WaitForSeconds(maxLifetime);
        Destroy(gameObject);
    }
}

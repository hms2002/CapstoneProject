using UnityEngine;

/// <summary>
/// 책임:
/// 부채꼴 패턴 정보를 받아 파티클/스프라이트 기반 화염 연출 루트를 배치하고 재생/정지한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ConeParticlePatternVisual2D : MonoBehaviour, IConePatternVisual2D
{
    /// <summary>
    /// 책임:
    /// 파티클 시스템의 프리펩 기본 값을 저장해 cone spec 적용 시 상대 보정 기준으로 사용한다.
    /// </summary>
    private struct ParticleBaseline
    {
        public ParticleSystem System;
        public float ShapeAngle;
        public float ShapeLength;
        public ParticleSystem.MinMaxCurve StartLifetime;
        public ParticleSystem.MinMaxCurve StartSpeed;
        public ParticleSystem.MinMaxCurve RateOverTime;
    }

    [Header("Refs")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private ParticleSystem[] particleSystems;

    [Header("Placement")]
    [SerializeField] private bool rotateToDirection = true;
    [SerializeField] private float rotationOffsetDegrees;

    [Header("Cone Spec Mapping")]
    [SerializeField] private bool applyShapeFromSpec = true;
    [SerializeField] private bool applyLifetimeFromSpec = true;
    [SerializeField] private bool applyEmissionFromSpec = true;
    [SerializeField, Min(0.01f)] private float referenceRange = 5f;
    [SerializeField, Min(1f)] private float referenceAngleDegrees = 55f;
    [SerializeField, Range(1f, 180f)] private float minParticleAngleDegrees = 4f;
    [SerializeField, Range(1f, 180f)] private float maxParticleAngleDegrees = 120f;
    [SerializeField, Min(0.01f)] private float minParticleLength = 0.25f;
    [SerializeField, Min(0.1f)] private float emissionScalePower = 0.75f;

    private Vector3 baseLocalScale = Vector3.one;
    private bool hasBaseScale;
    private ParticleBaseline[] baselines;

    private void Awake()
    {
        CacheReferences();
        CaptureBaseScale();
        CaptureParticleBaselines();
        Stop();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void Play(ConePatternVisualSpec2D spec)
    {
        CacheReferences();
        CaptureBaseScale();
        CaptureParticleBaselines();

        Transform root = visualRoot != null ? visualRoot : transform;
        root.position = spec.Origin;

        if (rotateToDirection)
            root.rotation = Quaternion.Euler(0f, 0f, spec.RotationDegrees + rotationOffsetDegrees);

        root.localScale = baseLocalScale;
        root.gameObject.SetActive(true);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] == null)
                continue;

            ApplySpecToParticleSystem(particleSystems[i], FindBaseline(particleSystems[i]), spec);
            particleSystems[i].Clear(true);
            particleSystems[i].Play(true);
        }
    }

    public void Stop()
    {
        CacheReferences();

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] == null)
                continue;

            particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void CacheReferences()
    {
        if (visualRoot == null)
            visualRoot = transform;

        if (particleSystems == null || particleSystems.Length == 0)
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    private void CaptureBaseScale()
    {
        if (hasBaseScale)
            return;

        Transform root = visualRoot != null ? visualRoot : transform;
        baseLocalScale = root.localScale;
        hasBaseScale = true;
    }

    private void CaptureParticleBaselines()
    {
        if (baselines != null && baselines.Length == particleSystems.Length)
            return;

        baselines = new ParticleBaseline[particleSystems.Length];
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem system = particleSystems[i];
            if (system == null)
                continue;

            ParticleSystem.MainModule main = system.main;
            ParticleSystem.ShapeModule shape = system.shape;
            ParticleSystem.EmissionModule emission = system.emission;

            baselines[i] = new ParticleBaseline
            {
                System = system,
                ShapeAngle = shape.angle,
                ShapeLength = shape.length,
                StartLifetime = main.startLifetime,
                StartSpeed = main.startSpeed,
                RateOverTime = emission.rateOverTime
            };
        }
    }

    private ParticleBaseline FindBaseline(ParticleSystem system)
    {
        if (baselines == null)
            return default;

        for (int i = 0; i < baselines.Length; i++)
        {
            if (baselines[i].System == system)
                return baselines[i];
        }

        return default;
    }

    private void ApplySpecToParticleSystem(ParticleSystem system, ParticleBaseline baseline, ConePatternVisualSpec2D spec)
    {
        if (system == null || baseline.System == null)
            return;

        float rangeScale = Mathf.Max(0.01f, spec.Range / referenceRange);
        float angleScale = Mathf.Max(0.01f, spec.AngleDegrees / referenceAngleDegrees);

        if (applyShapeFromSpec)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.angle = Mathf.Clamp(baseline.ShapeAngle * angleScale, minParticleAngleDegrees, maxParticleAngleDegrees);
            shape.length = Mathf.Max(minParticleLength, baseline.ShapeLength * rangeScale);
        }

        ParticleSystem.MainModule main = system.main;
        if (applyLifetimeFromSpec)
        {
            main.startLifetime = ScaleCurve(baseline.StartLifetime, rangeScale);
            main.startSpeed = baseline.StartSpeed;
        }

        if (applyEmissionFromSpec)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            float emissionScale = Mathf.Pow(rangeScale * angleScale, emissionScalePower);
            emission.rateOverTime = ScaleCurve(baseline.RateOverTime, emissionScale);
        }
    }

    private static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve source, float scale)
    {
        ParticleSystem.MinMaxCurve scaled = source;
        scaled.constant *= scale;
        scaled.constantMin *= scale;
        scaled.constantMax *= scale;
        scaled.curveMultiplier *= scale;
        return scaled;
    }
}

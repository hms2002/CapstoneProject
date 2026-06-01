using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CapstonePresentation
{
    [DisallowMultipleComponent]
    public sealed class TopDownDebrisBounceEmitter2D : MonoBehaviour
    {
        private const float FinalFragmentFadeSeconds = 0.24f;

        [SerializeField] private ParticleSystem debrisParticles;
        [SerializeField] private ParticleSystem contactParticles;

        [Header("Playback")]
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private bool randomizeSeed = true;
        [SerializeField] private int randomSeed = 7321;
        [SerializeField, Min(0.1f)] private float maxSimulationSeconds = 3.2f;

        [Header("Fragment Count")]
        [SerializeField, Min(0)] private int minFragments = 18;
        [SerializeField, Min(0)] private int maxFragments = 28;

        [Header("Ground Motion")]
        [SerializeField] private Vector2 groundSpeedRange = new(1.4f, 4.2f);
        [SerializeField] private Vector2 groundSpreadScale = Vector2.one;
        [SerializeField] private float groundSpreadRotationDegrees;
        [SerializeField, Range(0f, 1f)] private float groundFriction = 0.58f;

        [Header("Virtual Height")]
        [SerializeField] private Vector2 verticalSpeedRange = new(4.5f, 8f);
        [SerializeField, Min(0.01f)] private float gravity = 17f;
        [SerializeField] private Vector2 heightScreenOffsetPerUnit = new(0f, 0.16f);
        [SerializeField, Min(0.01f)] private float heightForMaxSizeBoost = 1.4f;
        [SerializeField, Min(0f)] private float heightSizeBoost = 0.22f;

        [Header("Bounce")]
        [SerializeField, Min(0)] private int maxBounces = 3;
        [SerializeField, Range(0f, 1f)] private float bounceDamping = 0.46f;
        [SerializeField, Min(0f)] private float minBounceVelocity = 1.05f;
        [SerializeField] private Vector2 bounceRandomMultiplierRange = new(0.85f, 1.08f);
        [SerializeField] private Vector2 frictionRandomMultiplierRange = new(0.9f, 1.05f);

        [Header("Fragment Look")]
        [SerializeField] private Vector2 fragmentSizeRange = new(0.055f, 0.14f);
        [SerializeField] private Vector2 fragmentSpinDegreesRange = new(-540f, 540f);
        [SerializeField] private Color fragmentColorA = new(0.33f, 0.29f, 0.24f, 1f);
        [SerializeField] private Color fragmentColorB = new(1f, 0.58f, 0.22f, 1f);
        [SerializeField, Range(0f, 1f)] private float hotFragmentChance = 0.22f;

        [Header("Contact Puff")]
        [SerializeField] private Vector2Int contactBurstCountRange = new(2, 5);
        [SerializeField] private Vector2 contactLifetimeRange = new(0.12f, 0.26f);
        [SerializeField] private Vector2 contactSizeMultiplierRange = new(1.4f, 2.7f);
        [SerializeField] private Vector2 contactSpeedRange = new(0.08f, 0.35f);
        [SerializeField] private Color contactColor = new(0.55f, 0.48f, 0.39f, 0.72f);
        [SerializeField, Range(0f, 1f)] private float contactInheritFragmentColor = 0.25f;

        private readonly struct DebrisPiece
        {
            public DebrisPiece(
                bool active,
                Vector2 groundPosition,
                Vector2 groundVelocity,
                float height,
                float verticalVelocity,
                int bouncesRemaining,
                float size,
                float spinDegreesPerSecond,
                float rotationDegrees,
                Color32 color,
                float fadeRemainingSeconds = 0f)
            {
                Active = active;
                GroundPosition = groundPosition;
                GroundVelocity = groundVelocity;
                Height = height;
                VerticalVelocity = verticalVelocity;
                BouncesRemaining = bouncesRemaining;
                Size = size;
                SpinDegreesPerSecond = spinDegreesPerSecond;
                RotationDegrees = rotationDegrees;
                Color = color;
                FadeRemainingSeconds = fadeRemainingSeconds;
            }

            public bool Active { get; }
            public Vector2 GroundPosition { get; }
            public Vector2 GroundVelocity { get; }
            public float Height { get; }
            public float VerticalVelocity { get; }
            public int BouncesRemaining { get; }
            public float Size { get; }
            public float SpinDegreesPerSecond { get; }
            public float RotationDegrees { get; }
            public Color32 Color { get; }
            public float FadeRemainingSeconds { get; }
            public bool IsFading => FadeRemainingSeconds > 0f;

            public DebrisPiece WithMotion(
                Vector2 groundPosition,
                Vector2 groundVelocity,
                float height,
                float verticalVelocity,
                int bouncesRemaining,
                float rotationDegrees)
            {
                return new DebrisPiece(
                    Active,
                    groundPosition,
                    groundVelocity,
                    height,
                    verticalVelocity,
                    bouncesRemaining,
                    Size,
                    SpinDegreesPerSecond,
                    rotationDegrees,
                    Color,
                    FadeRemainingSeconds);
            }

            public DebrisPiece BeginFade(Vector2 groundPosition, float rotationDegrees)
            {
                return new DebrisPiece(
                    true,
                    groundPosition,
                    Vector2.zero,
                    0f,
                    0f,
                    0,
                    Size,
                    SpinDegreesPerSecond,
                    rotationDegrees,
                    Color,
                    FinalFragmentFadeSeconds);
            }

            public DebrisPiece WithFade(float fadeRemainingSeconds)
            {
                return new DebrisPiece(
                    true,
                    GroundPosition,
                    Vector2.zero,
                    0f,
                    0f,
                    0,
                    Size,
                    SpinDegreesPerSecond,
                    RotationDegrees,
                    Color,
                    fadeRemainingSeconds);
            }

            public DebrisPiece Deactivate()
            {
                return new DebrisPiece(
                    false,
                    GroundPosition,
                    GroundVelocity,
                    Height,
                    VerticalVelocity,
                    BouncesRemaining,
                    Size,
                    SpinDegreesPerSecond,
                    RotationDegrees,
                    Color);
            }
        }

        private DebrisPiece[] pieces = Array.Empty<DebrisPiece>();
        private ParticleSystem.Particle[] renderParticles = Array.Empty<ParticleSystem.Particle>();
        private System.Random random;
        private bool pendingPlay;
        private bool isPlaying;
        private float elapsedSeconds;

#if UNITY_EDITOR
        private const int GizmoCircleSegments = 64;
        private static readonly Color GizmoOriginColor = new(1f, 0.84f, 0.18f, 0.9f);
        private static readonly Color GizmoFirstContactColor = new(1f, 0.58f, 0.16f, 0.7f);
        private static readonly Color GizmoTravelEnvelopeColor = new(0.95f, 0.95f, 0.95f, 0.35f);
        private static readonly Color GizmoHeightOffsetColor = new(0.25f, 0.68f, 1f, 0.85f);
        private static readonly Color GizmoRuntimeGroundColor = new(1f, 0.74f, 0.28f, 0.9f);
        private static readonly Color GizmoRuntimeHeightLineColor = new(0.25f, 0.68f, 1f, 0.55f);
#endif

        public bool IsPlaying => isPlaying;

        private void Reset()
        {
            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            if (systems.Length > 0)
                debrisParticles = systems[0];
            if (systems.Length > 1)
                contactParticles = systems[1];
        }

        private void Awake()
        {
            CacheParticleSystemsIfNeeded();
        }

        private void OnEnable()
        {
            if (playOnEnable)
                pendingPlay = true;
        }

        private void OnDisable()
        {
            Stop(clear: true);
        }

        public void Play()
        {
            pendingPlay = true;
        }

        public void Stop(bool clear)
        {
            pendingPlay = false;
            isPlaying = false;
            elapsedSeconds = 0f;

            if (debrisParticles != null)
            {
                debrisParticles.SetParticles(renderParticles, 0);
                debrisParticles.Stop(withChildren: true, clear
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting);
            }

            if (contactParticles != null)
            {
                contactParticles.Stop(withChildren: true, clear
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void LateUpdate()
        {
            if (pendingPlay)
                BeginPlayback();

            if (!isPlaying)
                return;

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            Tick(deltaTime);
        }

#if UNITY_EDITOR
        public void RestartEditorPreview()
        {
            pendingPlay = true;
            BeginPlayback();
        }

        public void StepEditorPreview(float deltaTime)
        {
            if (pendingPlay)
                BeginPlayback();

            if (!isPlaying)
                return;

            Tick(deltaTime);
        }

        private void OnDrawGizmosSelected()
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            DrawAuthoringGizmos();
            DrawRuntimePieceGizmos();

            Gizmos.matrix = previousMatrix;
        }

        private void DrawAuthoringGizmos()
        {
            Vector2 spreadScale = ResolveGroundSpreadScale();
            Vector2 firstContactRadii = spreadScale * EstimateFirstContactBaseRadius();
            Vector2 travelRadii = spreadScale * EstimateTravelEnvelopeBaseRadius();
            float maxApexHeight = EstimateMaxApexHeight();
            Vector2 heightOffset = heightScreenOffsetPerUnit * maxApexHeight;

            Gizmos.color = GizmoTravelEnvelopeColor;
            DrawWireEllipse(Vector2.zero, travelRadii, groundSpreadRotationDegrees);
            DrawEllipseAxes(travelRadii, groundSpreadRotationDegrees);

            Gizmos.color = GizmoFirstContactColor;
            DrawWireEllipse(Vector2.zero, firstContactRadii, groundSpreadRotationDegrees);

            Gizmos.color = GizmoOriginColor;
            float originMarkerRadius = Mathf.Max(0.08f, Mathf.Min(MaxComponent(firstContactRadii), 0.25f));
            DrawCross(Vector2.zero, originMarkerRadius);

            Gizmos.color = GizmoHeightOffsetColor;
            DrawVector(Vector2.zero, heightOffset);
        }

        private void DrawRuntimePieceGizmos()
        {
            if (!isPlaying || pieces.Length == 0)
                return;

            foreach (DebrisPiece piece in pieces)
            {
                if (!piece.Active)
                    continue;

                Vector2 groundPosition = piece.GroundPosition;
                Vector2 visualPosition = piece.GroundPosition + heightScreenOffsetPerUnit * piece.Height;
                float markerRadius = Mathf.Max(0.025f, piece.Size * 0.6f);

                Gizmos.color = GizmoRuntimeHeightLineColor;
                Gizmos.DrawLine(ToVector3(groundPosition), ToVector3(visualPosition));

                Gizmos.color = GizmoRuntimeGroundColor;
                DrawWireCircle(groundPosition, markerRadius);

                Gizmos.color = piece.Color;
                DrawWireCircle(visualPosition, markerRadius);
            }
        }

        private float EstimateFirstContactBaseRadius()
        {
            float groundSpeed = MaxAbs(groundSpeedRange);
            float verticalSpeed = MaxPositive(verticalSpeedRange);
            float safeGravity = Mathf.Max(0.01f, gravity);
            float firstContactTime = Mathf.Min(maxSimulationSeconds, 2f * verticalSpeed / safeGravity);
            return Mathf.Max(0f, groundSpeed * firstContactTime);
        }

        private float EstimateTravelEnvelopeBaseRadius()
        {
            float groundSpeed = MaxAbs(groundSpeedRange);
            float verticalVelocity = MaxPositive(verticalSpeedRange);
            float safeGravity = Mathf.Max(0.01f, gravity);
            float remainingTime = Mathf.Max(0f, maxSimulationSeconds);
            float radius = 0f;
            int bouncesRemaining = Mathf.Max(0, maxBounces);

            for (int i = 0; i < maxBounces + 2 && remainingTime > 0f && verticalVelocity > 0f; i++)
            {
                float flightTime = 2f * verticalVelocity / safeGravity;
                float usedTime = Mathf.Min(remainingTime, flightTime);
                radius += groundSpeed * usedTime;
                remainingTime -= usedTime;

                if (remainingTime <= 0f)
                    break;

                bool canBounce = bouncesRemaining > 0 && verticalVelocity >= minBounceVelocity;
                if (!canBounce)
                    break;

                verticalVelocity *= Mathf.Clamp01(bounceDamping) * MaxPositive(bounceRandomMultiplierRange);
                groundSpeed *= Mathf.Clamp01(groundFriction) * MaxPositive(frictionRandomMultiplierRange);
                bouncesRemaining--;
            }

            return Mathf.Max(EstimateFirstContactBaseRadius(), radius);
        }

        private float EstimateMaxApexHeight()
        {
            float verticalSpeed = MaxPositive(verticalSpeedRange);
            float safeGravity = Mathf.Max(0.01f, gravity);
            return verticalSpeed * verticalSpeed / (2f * safeGravity);
        }

        private static void DrawWireCircle(Vector2 center, float radius)
        {
            if (radius <= 0f)
                return;

            Vector3 previous = ToVector3(center + new Vector2(radius, 0f));
            for (int i = 1; i <= GizmoCircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / GizmoCircleSegments;
                Vector3 next = ToVector3(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private static void DrawWireEllipse(Vector2 center, Vector2 radii, float rotationDegrees)
        {
            if (radii.x <= 0f && radii.y <= 0f)
                return;

            Vector3 previous = ToVector3(center + Rotate(new Vector2(radii.x, 0f), rotationDegrees));
            for (int i = 1; i <= GizmoCircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / GizmoCircleSegments;
                Vector2 localPoint = new(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y);
                Vector3 next = ToVector3(center + Rotate(localPoint, rotationDegrees));
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private static void DrawEllipseAxes(Vector2 radii, float rotationDegrees)
        {
            if (radii.x <= 0f && radii.y <= 0f)
                return;

            Vector2 xAxis = Rotate(Vector2.right * radii.x, rotationDegrees);
            Vector2 yAxis = Rotate(Vector2.up * radii.y, rotationDegrees);
            Gizmos.DrawLine(ToVector3(-xAxis), ToVector3(xAxis));
            Gizmos.DrawLine(ToVector3(-yAxis), ToVector3(yAxis));
        }

        private static void DrawCross(Vector2 center, float radius)
        {
            Gizmos.DrawLine(ToVector3(center + Vector2.left * radius), ToVector3(center + Vector2.right * radius));
            Gizmos.DrawLine(ToVector3(center + Vector2.down * radius), ToVector3(center + Vector2.up * radius));
        }

        private static void DrawVector(Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            float magnitude = delta.magnitude;
            if (magnitude <= 0.001f)
                return;

            Vector2 direction = delta / magnitude;
            Vector2 perpendicular = new(-direction.y, direction.x);
            float headSize = Mathf.Min(0.18f, magnitude * 0.25f);

            Gizmos.DrawLine(ToVector3(start), ToVector3(end));
            Gizmos.DrawLine(ToVector3(end), ToVector3(end - direction * headSize + perpendicular * headSize * 0.5f));
            Gizmos.DrawLine(ToVector3(end), ToVector3(end - direction * headSize - perpendicular * headSize * 0.5f));
        }

        private static Vector3 ToVector3(Vector2 value)
        {
            return new Vector3(value.x, value.y, 0f);
        }

        private static float MaxAbs(Vector2 range)
        {
            return Mathf.Max(Mathf.Abs(range.x), Mathf.Abs(range.y));
        }

        private static float MaxPositive(Vector2 range)
        {
            return Mathf.Max(0f, Mathf.Max(range.x, range.y));
        }

        private static float MaxComponent(Vector2 value)
        {
            return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y));
        }
#endif

        private void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            elapsedSeconds += deltaTime;
            Simulate(deltaTime);

            int activeCount = WriteRenderParticles();
            if (debrisParticles != null)
                debrisParticles.SetParticles(renderParticles, activeCount);

            if (activeCount == 0 || elapsedSeconds >= maxSimulationSeconds)
                isPlaying = false;
        }

        private void BeginPlayback()
        {
            pendingPlay = false;
            CacheParticleSystemsIfNeeded();
            if (debrisParticles == null)
                return;

            random = randomizeSeed
                ? new System.Random(Environment.TickCount ^ RuntimeHelpers.GetHashCode(this))
                : new System.Random(randomSeed);

            Stop(clear: true);
            elapsedSeconds = 0f;

            int fragmentCount = ResolveFragmentCount();
            EnsureCapacity(fragmentCount);
            for (int i = 0; i < pieces.Length; i++)
                pieces[i] = default;

            for (int i = 0; i < fragmentCount; i++)
                pieces[i] = CreatePiece();

            debrisParticles.Play(withChildren: false);
            if (contactParticles != null)
                contactParticles.Play(withChildren: false);

            int activeCount = WriteRenderParticles();
            debrisParticles.SetParticles(renderParticles, activeCount);
            isPlaying = activeCount > 0;
        }

        private int ResolveFragmentCount()
        {
            int min = Mathf.Max(0, Mathf.Min(minFragments, maxFragments));
            int max = Mathf.Max(min, Mathf.Max(minFragments, maxFragments));
            return RangeInt(min, max);
        }

        private DebrisPiece CreatePiece()
        {
            float angle = Range(0f, Mathf.PI * 2f);
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            float groundSpeed = Range(groundSpeedRange);
            float verticalSpeed = Range(verticalSpeedRange);
            float size = Range(fragmentSizeRange);
            float spin = Range(fragmentSpinDegreesRange);
            float rotation = Range(0f, 360f);
            Color32 color = PickFragmentColor();

            return new DebrisPiece(
                true,
                Vector2.zero,
                ApplyGroundSpread(direction) * groundSpeed,
                0f,
                verticalSpeed,
                Mathf.Max(0, maxBounces),
                size,
                spin,
                rotation,
                color);
        }

        private void Simulate(float deltaTime)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                DebrisPiece piece = pieces[i];
                if (!piece.Active)
                    continue;

                if (piece.IsFading)
                {
                    float fadeRemainingSeconds = piece.FadeRemainingSeconds - deltaTime;
                    pieces[i] = fadeRemainingSeconds > 0f
                        ? piece.WithFade(fadeRemainingSeconds)
                        : piece.Deactivate();
                    continue;
                }

                Vector2 groundPosition = piece.GroundPosition + piece.GroundVelocity * deltaTime;
                Vector2 groundVelocity = piece.GroundVelocity;
                float verticalVelocity = piece.VerticalVelocity - gravity * deltaTime;
                float height = piece.Height + verticalVelocity * deltaTime;
                int bouncesRemaining = piece.BouncesRemaining;
                float rotation = piece.RotationDegrees + piece.SpinDegreesPerSecond * deltaTime;

                if (height <= 0f && verticalVelocity <= 0f)
                {
                    bool canBounce = bouncesRemaining > 0 && -verticalVelocity >= minBounceVelocity;

                    if (canBounce)
                    {
                        EmitContactPuff(piece, groundPosition, finalContact: false);
                        height = 0f;
                        verticalVelocity = -verticalVelocity
                            * bounceDamping
                            * Range(bounceRandomMultiplierRange);
                        groundVelocity *= groundFriction * Range(frictionRandomMultiplierRange);
                        bouncesRemaining--;
                    }
                    else
                    {
                        pieces[i] = piece.BeginFade(groundPosition, rotation);
                        continue;
                    }
                }

                pieces[i] = piece.WithMotion(
                    groundPosition,
                    groundVelocity,
                    Mathf.Max(0f, height),
                    verticalVelocity,
                    bouncesRemaining,
                    rotation);
            }
        }

        private int WriteRenderParticles()
        {
            int count = 0;
            for (int i = 0; i < pieces.Length; i++)
            {
                DebrisPiece piece = pieces[i];
                if (!piece.Active)
                    continue;

                if (count >= renderParticles.Length)
                    break;

                Vector2 visualPosition = piece.GroundPosition + heightScreenOffsetPerUnit * piece.Height;
                float heightRatio = Mathf.Clamp01(piece.Height / heightForMaxSizeBoost);
                float size = piece.Size * (1f + heightRatio * heightSizeBoost);
                Color32 color = piece.Color;
                if (piece.IsFading)
                {
                    float fadeRatio = Mathf.Clamp01(piece.FadeRemainingSeconds / FinalFragmentFadeSeconds);
                    color.a = (byte)Mathf.RoundToInt(color.a * fadeRatio);
                }

                ParticleSystem.Particle particle = renderParticles[count];
                particle.position = new Vector3(visualPosition.x, visualPosition.y, 0f);
                particle.velocity = Vector3.zero;
                particle.remainingLifetime = 1f;
                particle.startLifetime = 1f;
                particle.startSize = Mathf.Max(0.001f, size);
                particle.rotation = piece.RotationDegrees * Mathf.Deg2Rad;
                particle.startColor = color;
                renderParticles[count] = particle;
                count++;
            }

            return count;
        }

        private void EmitContactPuff(DebrisPiece piece, Vector2 groundPosition, bool finalContact)
        {
            if (contactParticles == null)
                return;

            int min = Mathf.Max(0, Mathf.Min(contactBurstCountRange.x, contactBurstCountRange.y));
            int max = Mathf.Max(min, Mathf.Max(contactBurstCountRange.x, contactBurstCountRange.y));
            int count = RangeInt(min, max);
            float finalScale = finalContact ? 1.25f : 1f;
            Color color = Color.Lerp(contactColor, piece.Color, contactInheritFragmentColor);

            for (int i = 0; i < count; i++)
            {
                Vector2 direction = RandomUnitVector2();
                ParticleSystem.EmitParams emitParams = new()
                {
                    position = new Vector3(groundPosition.x, groundPosition.y, 0f),
                    velocity = new Vector3(direction.x, direction.y, 0f) * Range(contactSpeedRange),
                    startLifetime = Range(contactLifetimeRange),
                    startSize = piece.Size * Range(contactSizeMultiplierRange) * finalScale,
                    startColor = color,
                    rotation = Range(0f, 360f) * Mathf.Deg2Rad
                };

                contactParticles.Emit(emitParams, 1);
            }
        }

        private void EnsureCapacity(int fragmentCount)
        {
            int capacity = Mathf.Max(0, fragmentCount);
            if (pieces.Length != capacity)
                pieces = new DebrisPiece[capacity];
            if (renderParticles.Length < capacity)
                renderParticles = new ParticleSystem.Particle[capacity];
        }

        private void CacheParticleSystemsIfNeeded()
        {
            if (debrisParticles != null)
                return;

            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            if (systems.Length > 0)
                debrisParticles = systems[0];
            if (systems.Length > 1 && contactParticles == null)
                contactParticles = systems[1];
        }

        private Color32 PickFragmentColor()
        {
            float colorMix = Range(0f, 1f);
            Color color = Color.Lerp(fragmentColorA, fragmentColorB, colorMix);
            if (Range(0f, 1f) > hotFragmentChance)
                color = Color.Lerp(fragmentColorA, color, 0.55f);

            return color;
        }

        private Vector2 RandomUnitVector2()
        {
            float angle = Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private int RangeInt(int minInclusive, int maxInclusive)
        {
            if (random == null)
                random = new System.Random(randomSeed);

            if (maxInclusive <= minInclusive)
                return minInclusive;

            return random.Next(minInclusive, maxInclusive + 1);
        }

        private float Range(Vector2 range)
        {
            return Range(range.x, range.y);
        }

        private float Range(float a, float b)
        {
            if (random == null)
                random = new System.Random(randomSeed);

            float min = Mathf.Min(a, b);
            float max = Mathf.Max(a, b);
            if (Mathf.Approximately(min, max))
                return min;

            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        private Vector2 ApplyGroundSpread(Vector2 direction)
        {
            Vector2 scale = ResolveGroundSpreadScale();
            Vector2 scaledDirection = new(direction.x * scale.x, direction.y * scale.y);
            return Rotate(scaledDirection, groundSpreadRotationDegrees);
        }

        private Vector2 ResolveGroundSpreadScale()
        {
            Vector2 scale = new(Mathf.Max(0f, groundSpreadScale.x), Mathf.Max(0f, groundSpreadScale.y));
            return scale.sqrMagnitude > 0.0001f ? scale : Vector2.one;
        }

        private static Vector2 Rotate(Vector2 value, float degrees)
        {
            if (Mathf.Approximately(degrees, 0f))
                return value;

            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                value.x * cos - value.y * sin,
                value.x * sin + value.y * cos);
        }

        public struct AuthoringPreset
        {
            public int MinFragments;
            public int MaxFragments;
            public float MaxSimulationSeconds;
            public Vector2 GroundSpeedRange;
            public Vector2 GroundSpreadScale;
            public float GroundSpreadRotationDegrees;
            public float GroundFriction;
            public Vector2 VerticalSpeedRange;
            public float Gravity;
            public float HeightScreenOffset;
            public float HeightSizeBoost;
            public int MaxBounces;
            public float BounceDamping;
            public float MinBounceVelocity;
            public Vector2 FragmentSizeRange;
            public Vector2 FragmentSpinDegreesRange;
            public Color FragmentColorA;
            public Color FragmentColorB;
            public float HotFragmentChance;
            public Color ContactColor;
        }

        public void ApplyEditorPreset(
            ParticleSystem debrisParticleSystem,
            ParticleSystem contactParticleSystem,
            in AuthoringPreset preset)
        {
            debrisParticles = debrisParticleSystem;
            contactParticles = contactParticleSystem;
            playOnEnable = true;
            useUnscaledTime = false;
            randomizeSeed = true;
            randomSeed = 7321;
            minFragments = Mathf.Max(0, preset.MinFragments);
            maxFragments = Mathf.Max(minFragments, preset.MaxFragments);
            maxSimulationSeconds = Mathf.Max(0.1f, preset.MaxSimulationSeconds);
            groundSpeedRange = preset.GroundSpeedRange;
            groundSpreadScale = preset.GroundSpreadScale.sqrMagnitude > 0.0001f
                ? new Vector2(Mathf.Max(0f, preset.GroundSpreadScale.x), Mathf.Max(0f, preset.GroundSpreadScale.y))
                : Vector2.one;
            groundSpreadRotationDegrees = preset.GroundSpreadRotationDegrees;
            groundFriction = Mathf.Clamp01(preset.GroundFriction);
            verticalSpeedRange = preset.VerticalSpeedRange;
            gravity = Mathf.Max(0.01f, preset.Gravity);
            heightScreenOffsetPerUnit = new Vector2(0f, preset.HeightScreenOffset);
            heightSizeBoost = Mathf.Max(0f, preset.HeightSizeBoost);
            maxBounces = Mathf.Max(0, preset.MaxBounces);
            bounceDamping = Mathf.Clamp01(preset.BounceDamping);
            minBounceVelocity = Mathf.Max(0f, preset.MinBounceVelocity);
            fragmentSizeRange = preset.FragmentSizeRange;
            fragmentSpinDegreesRange = preset.FragmentSpinDegreesRange;
            fragmentColorA = preset.FragmentColorA;
            fragmentColorB = preset.FragmentColorB;
            hotFragmentChance = Mathf.Clamp01(preset.HotFragmentChance);
            contactColor = preset.ContactColor;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum UIParticleShape
{
    Point,
    Circle,
    Ring,
}

public enum UIParticleStartColorMode
{
    Color,
    [InspectorName("Between Two Value")]
    RandomBetweenTwoColors,
}

[Serializable]
public struct UIParticleBurst
{
    [Min(0f)] public float time;
    [Range(0, 256)] public int count;
    [Min(1)] public int cycles;
    [Min(0f)] public float interval;

    public static UIParticleBurst Once(int count)
    {
        return new UIParticleBurst
        {
            time = 0f,
            count = Mathf.Max(0, count),
            cycles = 1,
            interval = 0f,
        };
    }
}

[DisallowMultipleComponent]
public sealed class UIParticleEmitter : MonoBehaviour
{
    [Header("Renderer")]
    [SerializeField] private RectTransform particleRoot;
    [SerializeField] private Texture particleTexture;
    [SerializeField] private Material particleMaterial;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField, Range(1, 256)] private int maxParticles = 64;
    [SerializeField] private bool clearOnPlay = true;
    [SerializeField] private bool playOnEnable;

    [Header("Emission")]
    [SerializeField, Min(0f)] private float duration = 0.35f;
    [SerializeField] private bool looping;
    [SerializeField, Min(0f)] private float rateOverTime;
    [SerializeField] private UIParticleBurst[] bursts = { UIParticleBurst.Once(18) };

    [Header("Shape")]
    [SerializeField] private UIParticleShape shape = UIParticleShape.Point;
    [SerializeField, Min(0f)] private float shapeRadius = 20f;
    [SerializeField] private Vector2 emitterOffset;
    [SerializeField, Range(-180f, 180f)] private float directionAngle;
    [SerializeField, Range(0f, 360f)] private float spreadAngle = 360f;
    [SerializeField] private bool distributeBurstEvenly = true;
    [SerializeField, Range(0f, 180f)] private float burstAngleJitter = 14f;

    [Header("Start Lifetime")]
    [SerializeField] private Vector2 startLifetime = new(0.18f, 0.28f);
    [SerializeField] private Vector2 startSpeed = new(360f, 520f);
    [SerializeField] private Vector2 startLength = new(22f, 34f);
    [SerializeField] private Vector2 startThickness = new(3f, 5f);
    [SerializeField] private Vector2 startRotation = Vector2.zero;
    [SerializeField] private Vector2 angularVelocity = Vector2.zero;
    [SerializeField] private UIParticleStartColorMode startColorMode;
    [SerializeField] private Color startColor = new(1f, 0.76f, 0.32f, 0.95f);
    [SerializeField] private Color startColorB = new(1f, 0.35f, 0.1f, 0.95f);

    [Header("Gizmos")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool showGizmosOnlyWhenSelected;

    [Header("Velocity Over Lifetime")]
    [SerializeField] private bool velocityOverLifetime;
    [SerializeField] private Vector2 acceleration;
    [SerializeField] private AnimationCurve speedMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Header("Limit Velocity Over Lifetime")]
    [SerializeField] private bool limitVelocityOverLifetime;
    [SerializeField, Min(0f)] private float maxSpeed = 520f;
    [SerializeField, Range(0f, 1f)] private float velocityDampen = 0.7f;

    [Header("Gravity")]
    [SerializeField] private bool gravityEnabled;
    [SerializeField] private Vector2 gravity = new(0f, -980f);
    [SerializeField] private float gravityScale = 1f;

    [Header("Color Over Lifetime")]
    [SerializeField] private bool colorOverLifetime = true;
    [SerializeField] private Gradient lifetimeColor = CreateDefaultColorGradient();

    [Header("Size Over Lifetime")]
    [SerializeField] private bool sizeOverLifetime = true;
    [SerializeField] private AnimationCurve lengthOverLifetime = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private AnimationCurve thicknessOverLifetime = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Trails")]
    [SerializeField] private bool trailsEnabled;
    [SerializeField, Range(1, 8)] private int trailSegmentCount = 3;
    [SerializeField, Min(0f)] private float trailSpacing = 16f;
    [SerializeField, Range(0f, 1f)] private float trailAlpha = 0.45f;
    [SerializeField, Range(0f, 1f)] private float trailSizeScale = 0.72f;

    private readonly List<Particle> particles = new();
    private float playbackTime;
    private float previousPlaybackTime;
    private float emissionAccumulator;
    private Vector2 localOrigin;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    private sealed class Particle
    {
        public GameObject GameObject;
        public RectTransform Rect;
        public RawImage Image;
        public readonly List<RawImage> TrailImages = new();
        public readonly List<RectTransform> TrailRects = new();
        public bool Active;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public float Lifetime;
        public float Length;
        public float Thickness;
        public float Rotation;
        public float AngularVelocity;
        public Color Color;
    }

    private void Awake()
    {
        EnsurePool();
        HideAll();
    }

    private void OnEnable()
    {
        if (playOnEnable && Application.isPlaying)
            Play();
    }

    private void OnDisable()
    {
        Stop(clear: true);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || showGizmosOnlyWhenSelected)
            return;

        DrawEmitterGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || !showGizmosOnlyWhenSelected)
            return;

        DrawEmitterGizmos();
    }

    private void Update()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        Simulate(deltaTime);
    }

    public void Simulate(float deltaTime)
    {
        if (!isPlaying || deltaTime <= 0f)
            return;

        previousPlaybackTime = playbackTime;
        playbackTime += deltaTime;
        EmitScheduledBursts();
        EmitRate(deltaTime);
        UpdateParticles(deltaTime);

        if (!looping && playbackTime >= duration && !HasActiveParticles())
            isPlaying = false;
    }

    public void Play()
    {
        PlayAt(Vector2.zero);
    }

    public void PlayAt(Vector2 anchoredPosition)
    {
        localOrigin = anchoredPosition + emitterOffset;
        playbackTime = 0f;
        previousPlaybackTime = -0.0001f;
        emissionAccumulator = 0f;
        isPlaying = true;

        EnsurePool();
        if (clearOnPlay)
            HideAll();

        EmitScheduledBursts();
        previousPlaybackTime = playbackTime;
    }

    public void PlayAtWorldPosition(Vector3 worldPosition)
    {
        RectTransform root = ResolveParticleRoot();
        Vector2 localPosition = root != null
            ? root.InverseTransformPoint(worldPosition)
            : Vector2.zero;
        PlayAt(localPosition);
    }

    public void EmitBurst(int count)
    {
        EnsurePool();
        Emit(count, 0f);
    }

    public void Stop(bool clear = true)
    {
        isPlaying = false;
        playbackTime = 0f;
        emissionAccumulator = 0f;

        if (clear)
            HideAll();
    }

#if UNITY_EDITOR
    public void DestroyEditorPreviewObjects()
    {
        if (Application.isPlaying)
            return;

        for (int i = 0; i < particles.Count; i++)
        {
            Particle particle = particles[i];
            if (particle == null)
                continue;

            for (int j = 0; j < particle.TrailImages.Count; j++)
            {
                RawImage trailImage = particle.TrailImages[j];
                if (trailImage != null)
                    DestroyImmediate(trailImage.gameObject);
            }

            if (particle.GameObject != null)
                DestroyImmediate(particle.GameObject);
        }

        particles.Clear();
    }
#endif

    private void EmitScheduledBursts()
    {
        if (bursts == null || bursts.Length == 0)
            return;

        for (int i = 0; i < bursts.Length; i++)
        {
            UIParticleBurst burst = bursts[i];
            if (burst.count <= 0)
                continue;

            int cycles = Mathf.Max(1, burst.cycles);
            float interval = Mathf.Max(0f, burst.interval);
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                float burstTime = burst.time + interval * cycle;
                if (WasCrossedThisFrame(burstTime))
                    Emit(burst.count, burstTime);
            }
        }
    }

    private void EmitRate(float deltaTime)
    {
        if (rateOverTime <= 0f)
            return;

        if (!looping && playbackTime > duration)
            return;

        emissionAccumulator += rateOverTime * deltaTime;
        int count = Mathf.FloorToInt(emissionAccumulator);
        if (count <= 0)
            return;

        emissionAccumulator -= count;
        Emit(count, playbackTime);
    }

    private void Emit(int count, float burstTime)
    {
        count = Mathf.Min(Mathf.Max(0, count), maxParticles);
        for (int i = 0; i < count; i++)
        {
            Particle particle = GetFreeParticle();
            if (particle == null)
                return;

            InitializeParticle(particle, i, count, burstTime);
        }
    }

    private void InitializeParticle(Particle particle, int indexInBurst, int burstCount, float burstTime)
    {
        float angle = ResolveEmissionAngle(indexInBurst, burstCount);
        Vector2 direction = AngleToVector(angle);
        Vector2 spawnOffset = ResolveShapeOffset(direction);
        float speed = RandomRange(startSpeed);

        particle.Active = true;
        particle.Age = 0f;
        particle.Lifetime = Mathf.Max(0.001f, RandomRange(startLifetime));
        particle.Position = localOrigin + spawnOffset;
        particle.Velocity = direction * speed;
        particle.Length = Mathf.Max(0f, RandomRange(startLength));
        particle.Thickness = Mathf.Max(0f, RandomRange(startThickness));
        particle.Rotation = angle + RandomRange(startRotation);
        particle.AngularVelocity = RandomRange(angularVelocity);
        particle.Color = ResolveStartColor();
        particle.GameObject.SetActive(true);

        ApplyParticleVisual(particle, 0f);
    }

    private void UpdateParticles(float deltaTime)
    {
        for (int i = 0; i < particles.Count; i++)
        {
            Particle particle = particles[i];
            if (!particle.Active)
                continue;

            particle.Age += deltaTime;
            if (particle.Age >= particle.Lifetime)
            {
                HideParticle(particle);
                continue;
            }

            float t = Mathf.Clamp01(particle.Age / particle.Lifetime);
            Vector2 velocity = particle.Velocity;

            if (velocityOverLifetime)
            {
                velocity += acceleration * deltaTime;
                velocity *= Mathf.Max(0f, speedMultiplier.Evaluate(t));
            }

            if (gravityEnabled)
                velocity += gravity * gravityScale * deltaTime;

            if (limitVelocityOverLifetime && maxSpeed > 0f)
            {
                float speed = velocity.magnitude;
                if (speed > maxSpeed)
                    velocity = Vector2.Lerp(velocity, velocity.normalized * maxSpeed, velocityDampen);
            }

            particle.Velocity = velocity;
            particle.Position += velocity * deltaTime;
            particle.Rotation += particle.AngularVelocity * deltaTime;
            ApplyParticleVisual(particle, t);
        }
    }

    private void ApplyParticleVisual(Particle particle, float t)
    {
        float lengthScale = sizeOverLifetime ? Mathf.Max(0f, lengthOverLifetime.Evaluate(t)) : 1f;
        float thicknessScale = sizeOverLifetime ? Mathf.Max(0f, thicknessOverLifetime.Evaluate(t)) : 1f;
        Color color = colorOverLifetime ? lifetimeColor.Evaluate(t) * particle.Color : particle.Color;

        particle.Rect.anchoredPosition = particle.Position;
        particle.Rect.localRotation = Quaternion.Euler(0f, 0f, particle.Rotation);
        particle.Rect.sizeDelta = new Vector2(
            particle.Length * lengthScale,
            particle.Thickness * thicknessScale);
        particle.Image.color = color;

        ApplyTrailVisual(particle, t, color, lengthScale, thicknessScale);
    }

    private void ApplyTrailVisual(Particle particle, float t, Color color, float lengthScale, float thicknessScale)
    {
        EnsureTrailPool(particle);

        if (!trailsEnabled || particle.TrailImages.Count == 0)
        {
            SetTrailsActive(particle, false);
            return;
        }

        Vector2 direction = particle.Velocity.sqrMagnitude > 0.0001f
            ? particle.Velocity.normalized
            : AngleToVector(particle.Rotation);

        float rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        for (int i = 0; i < particle.TrailImages.Count; i++)
        {
            float segmentT = (i + 1f) / (particle.TrailImages.Count + 1f);
            RawImage image = particle.TrailImages[i];
            RectTransform rect = particle.TrailRects[i];

            image.gameObject.SetActive(true);
            rect.anchoredPosition = particle.Position - direction * (trailSpacing * (i + 1));
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            rect.sizeDelta = new Vector2(
                particle.Length * lengthScale * Mathf.Lerp(1f, trailSizeScale, segmentT),
                particle.Thickness * thicknessScale * Mathf.Lerp(1f, trailSizeScale, segmentT));

            Color trailColor = color;
            trailColor.a *= trailAlpha * (1f - segmentT) * (1f - t);
            image.color = trailColor;
        }
    }

    private void EnsurePool()
    {
        RectTransform root = ResolveParticleRoot();
        if (root == null)
            return;

        for (int i = particles.Count; i < maxParticles; i++)
            particles.Add(CreateParticle(root, i));

        ApplyGraphicSettings();
    }

    private Particle CreateParticle(RectTransform root, int index)
    {
        GameObject particleObject = new GameObject($"UIParticle_{index:00}", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
        RectTransform rect = particleObject.GetComponent<RectTransform>();
        rect.SetParent(root, worldPositionStays: false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;

        LayoutElement layoutElement = particleObject.GetComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        RawImage image = particleObject.GetComponent<RawImage>();
        image.raycastTarget = false;
        image.color = Color.clear;
        image.texture = particleTexture;
        image.material = particleMaterial;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            particleObject.hideFlags = HideFlags.HideAndDontSave;
#endif
        particleObject.SetActive(false);

        return new Particle
        {
            GameObject = particleObject,
            Rect = rect,
            Image = image,
        };
    }

    private void EnsureTrailPool(Particle particle)
    {
        if (!trailsEnabled)
            return;

        RectTransform root = ResolveParticleRoot();
        if (root == null)
            return;

        for (int i = particle.TrailImages.Count; i < trailSegmentCount; i++)
        {
            GameObject trailObject = new GameObject($"{particle.GameObject.name}_Trail_{i:00}", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            RectTransform rect = trailObject.GetComponent<RectTransform>();
            rect.SetParent(root, worldPositionStays: false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;

            LayoutElement layoutElement = trailObject.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            RawImage image = trailObject.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.color = Color.clear;
            image.texture = particleTexture;
            image.material = particleMaterial;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                trailObject.hideFlags = HideFlags.HideAndDontSave;
#endif
            trailObject.SetActive(false);

            particle.TrailImages.Add(image);
            particle.TrailRects.Add(rect);
        }
    }

    private RectTransform ResolveParticleRoot()
    {
        if (particleRoot != null)
            return particleRoot;

        particleRoot = transform as RectTransform;
        return particleRoot;
    }

    private Color ResolveStartColor()
    {
        return startColorMode == UIParticleStartColorMode.RandomBetweenTwoColors
            ? Color.Lerp(startColor, startColorB, UnityEngine.Random.value)
            : startColor;
    }

    private void DrawEmitterGizmos()
    {
        RectTransform root = particleRoot != null
            ? particleRoot
            : transform as RectTransform;
        if (root == null)
            return;

        Vector3 origin = root.TransformPoint(emitterOffset);
        float rootScale = ResolveRootScale(root);
        float radius = Mathf.Max(0f, shapeRadius * rootScale);
        float speedPreviewLength = Mathf.Max(24f, Mathf.Max(startSpeed.x, startSpeed.y) * 0.16f * rootScale);

        Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.45f);
        DrawRectGizmo(root);

        Gizmos.color = new Color(1f, 0.76f, 0.2f, 1f);
        DrawShapeGizmo(root, origin, radius);

        Gizmos.color = new Color(1f, 0.35f, 0.08f, 1f);
        DrawDirectionGizmo(root, origin, directionAngle, spreadAngle, speedPreviewLength);

        Gizmos.color = new Color(1f, 0.92f, 0.2f, 1f);
        Gizmos.DrawSphere(origin, ResolveHandleSize(origin) * 0.025f);
    }

    private void DrawShapeGizmo(RectTransform root, Vector3 origin, float radius)
    {
        switch (shape)
        {
            case UIParticleShape.Circle:
            case UIParticleShape.Ring:
                DrawCircleGizmo(root, origin, radius, 64);
                break;

            case UIParticleShape.Point:
            default:
                float size = ResolveHandleSize(origin) * 0.12f;
                Gizmos.DrawLine(origin - root.right * size, origin + root.right * size);
                Gizmos.DrawLine(origin - root.up * size, origin + root.up * size);
                break;
        }
    }

    private static void DrawRectGizmo(RectTransform root)
    {
        Vector3[] corners = new Vector3[4];
        root.GetWorldCorners(corners);
        Gizmos.DrawLine(corners[0], corners[1]);
        Gizmos.DrawLine(corners[1], corners[2]);
        Gizmos.DrawLine(corners[2], corners[3]);
        Gizmos.DrawLine(corners[3], corners[0]);
    }

    private static void DrawCircleGizmo(RectTransform root, Vector3 origin, float radius, int segments)
    {
        if (radius <= 0f)
            return;

        segments = Mathf.Max(8, segments);
        Vector3 previous = origin + root.right * radius;
        for (int i = 1; i <= segments; i++)
        {
            float radians = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 point = origin
                + root.right * (Mathf.Cos(radians) * radius)
                + root.up * (Mathf.Sin(radians) * radius);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }

    private static void DrawDirectionGizmo(
        RectTransform root,
        Vector3 origin,
        float angle,
        float spread,
        float length)
    {
        Vector3 centerDirection = AngleToWorldVector(angle, root);
        DrawArrowGizmo(origin, centerDirection, length);

        float halfSpread = spread * 0.5f;
        if (spread > 0.1f && spread < 359.9f)
        {
            Vector3 leftDirection = AngleToWorldVector(angle - halfSpread, root);
            Vector3 rightDirection = AngleToWorldVector(angle + halfSpread, root);
            float spreadLength = Mathf.Max(18f, length * 0.78f);
            Gizmos.DrawLine(origin, origin + leftDirection * spreadLength);
            Gizmos.DrawLine(origin, origin + rightDirection * spreadLength);
        }
        else if (spread >= 359.9f)
        {
            DrawCircleGizmo(root, origin, Mathf.Max(16f, length * 0.55f), 48);
        }
    }

    private static void DrawArrowGizmo(Vector3 origin, Vector3 direction, float length)
    {
        Vector3 end = origin + direction * length;
        Gizmos.DrawLine(origin, end);

        float headSize = Mathf.Max(length * 0.16f, 8f);
        Vector3 left = Quaternion.Euler(0f, 0f, 150f) * direction;
        Vector3 right = Quaternion.Euler(0f, 0f, -150f) * direction;
        Gizmos.DrawLine(end, end + left * headSize);
        Gizmos.DrawLine(end, end + right * headSize);
    }

    private static Vector3 AngleToWorldVector(float angleDegrees, RectTransform root)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        Vector3 direction = root.right * Mathf.Cos(radians) + root.up * Mathf.Sin(radians);
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : root.right;
    }

    private static float ResolveRootScale(RectTransform root)
    {
        Vector3 lossyScale = root.lossyScale;
        return Mathf.Max(0.0001f, (Mathf.Abs(lossyScale.x) + Mathf.Abs(lossyScale.y)) * 0.5f);
    }

    private static float ResolveHandleSize(Vector3 worldPosition)
    {
        Camera camera = Camera.current;
        if (camera == null)
            return 24f;

        float distance = Vector3.Distance(camera.transform.position, worldPosition);
        return Mathf.Max(12f, distance * 0.04f);
    }

    private Particle GetFreeParticle()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            if (!particles[i].Active)
                return particles[i];
        }

        return null;
    }

    private bool HasActiveParticles()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            if (particles[i].Active)
                return true;
        }

        return false;
    }

    private void HideAll()
    {
        for (int i = 0; i < particles.Count; i++)
            HideParticle(particles[i]);
    }

    private void HideParticle(Particle particle)
    {
        if (particle == null)
            return;

        particle.Active = false;
        particle.Image.color = Color.clear;
        particle.GameObject.SetActive(false);
        SetTrailsActive(particle, false);
    }

    private void SetTrailsActive(Particle particle, bool active)
    {
        for (int i = 0; i < particle.TrailImages.Count; i++)
        {
            RawImage image = particle.TrailImages[i];
            if (image == null)
                continue;

            image.color = Color.clear;
            image.gameObject.SetActive(active);
        }
    }

    private void ApplyGraphicSettings()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            Particle particle = particles[i];
            if (particle == null)
                continue;

            particle.Image.texture = particleTexture;
            particle.Image.material = particleMaterial;

            for (int j = 0; j < particle.TrailImages.Count; j++)
            {
                RawImage image = particle.TrailImages[j];
                if (image == null)
                    continue;

                image.texture = particleTexture;
                image.material = particleMaterial;
            }
        }
    }

    private bool WasCrossedThisFrame(float time)
    {
        if (looping && duration > 0f)
        {
            float current = Mathf.Repeat(playbackTime, duration);
            float previous = Mathf.Repeat(previousPlaybackTime, duration);
            return previous > current
                ? time > previous || time <= current
                : time > previous && time <= current;
        }

        return time > previousPlaybackTime && time <= playbackTime;
    }

    private float ResolveEmissionAngle(int indexInBurst, int burstCount)
    {
        float halfSpread = spreadAngle * 0.5f;
        float baseAngle;

        if (distributeBurstEvenly && burstCount > 1)
        {
            float t = burstCount <= 1 ? 0.5f : indexInBurst / (burstCount - 1f);
            baseAngle = directionAngle - halfSpread + spreadAngle * t;
        }
        else
        {
            baseAngle = directionAngle + UnityEngine.Random.Range(-halfSpread, halfSpread);
        }

        return baseAngle + UnityEngine.Random.Range(-burstAngleJitter, burstAngleJitter);
    }

    private Vector2 ResolveShapeOffset(Vector2 direction)
    {
        switch (shape)
        {
            case UIParticleShape.Circle:
            {
                Vector2 random = UnityEngine.Random.insideUnitCircle * shapeRadius;
                return random;
            }

            case UIParticleShape.Ring:
                return direction * shapeRadius;

            case UIParticleShape.Point:
            default:
                return Vector2.zero;
        }
    }

    private static Vector2 AngleToVector(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private static float RandomRange(Vector2 range)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        return Mathf.Approximately(min, max) ? min : UnityEngine.Random.Range(min, max);
    }

    private static Gradient CreateDefaultColorGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.82f, 0.35f), 0f),
                new GradientColorKey(new Color(1f, 0.35f, 0.1f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }
}

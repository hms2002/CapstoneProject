using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum UIParticleShape
{
    Point,
    Circle,
    Ring,
    Line,
    Rectangle,
    RectangleEdge,
    Arc,
    ArcFilled,
    Ellipse,
    EllipseEdge,
}

public enum UIParticleStartColorMode
{
    Color,
    [InspectorName("Between Two Value")]
    RandomBetweenTwoColors,
}

public enum UIParticleSimulationSpace
{
    Local,
    World,
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
    [SerializeField] private UIParticleSimulationSpace simulationSpace;
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
    [SerializeField] private Vector2 shapeSize = new(80f, 40f);
    [SerializeField] private Vector2 emitterOffset;
    [SerializeField, Range(-180f, 180f)] private float directionAngle;
    [SerializeField, Range(0f, 360f)] private float spreadAngle = 360f;
    [SerializeField] private bool distributeBurstEvenly = true;
    [SerializeField, Range(0f, 180f)] private float burstAngleJitter = 14f;

    [Header("Start Lifetime")]
    [SerializeField] private Vector2 startLifetime = new(0.18f, 0.28f);
    [SerializeField] private Vector2 startSpeed = new(360f, 520f);
    [SerializeField] private Vector2 startSize = Vector2.one;
    [SerializeField] private Vector2 startLength = new(22f, 34f);
    [SerializeField] private Vector2 startThickness = new(3f, 5f);
    [SerializeField] private Vector2 startRotation = Vector2.zero;
    [SerializeField] private Vector2 angularVelocity = Vector2.zero;
    [SerializeField] private UIParticleStartColorMode startColorMode;
    [SerializeField] private Color startColor = new(1f, 0.76f, 0.32f, 0.95f);
    [SerializeField] private Color startColorB = new(1f, 0.35f, 0.1f, 0.95f);

    [Header("Gizmos")]
#pragma warning disable CS0414 // Read by UIParticleEmitterEditor through SerializedObject.
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool showGizmosOnlyWhenSelected;
#pragma warning restore CS0414

    [Header("Velocity Over Lifetime")]
    [SerializeField] private bool velocityOverLifetime;
    [SerializeField] private Vector2 acceleration;
    [SerializeField] private AnimationCurve speedMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Header("Limit Velocity Over Lifetime")]
    [SerializeField] private bool limitVelocityOverLifetime;
    [SerializeField] private bool separateAxes;
    [SerializeField, Min(0f)] private float maxSpeed = 520f;
    [SerializeField] private Vector2 maxSpeedAxes = new(520f, 520f);
    [SerializeField, InspectorName("Limit Multiplier")] private AnimationCurve maxSpeedMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float velocityDampen = 0.7f;
    [SerializeField, Min(0f)] private float drag;
    [SerializeField, InspectorName("Drag Multiplier")] private AnimationCurve dragMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [SerializeField] private bool multiplyDragBySize;
    [SerializeField] private bool multiplyDragByVelocity;

    [Header("Gravity")]
    [SerializeField] private bool gravityEnabled;
    [SerializeField] private Vector2 gravity = new(0f, -980f);
    [SerializeField] private float gravityScale = 1f;

    [Header("Color Over Lifetime")]
    [SerializeField] private bool colorOverLifetime = true;
    [SerializeField] private Gradient lifetimeColor = CreateDefaultColorGradient();

    [Header("Size Over Lifetime")]
    [SerializeField] private bool sizeOverLifetime = true;
    [SerializeField] private AnimationCurve sizeOverLifetimeMultiplier = AnimationCurve.Linear(0f, 1f, 1f, 1f);
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
    private bool isEmitting;
#if UNITY_EDITOR
    private bool editorPreviewMode;
#endif

    public bool IsPlaying => isPlaying;
    public bool IsEmitting => isEmitting;

    public void SetParticleRoot(RectTransform root, bool clearExisting = true)
    {
        if (particleRoot == root)
            return;

        if (clearExisting)
            Stop(clear: true);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyEditorPreviewObjects();
#endif

        particles.Clear();
        particleRoot = root;
        EnsurePool();
    }

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
        public float Size;
        public float Length;
        public float Thickness;
        public float Rotation;
        public float AngularVelocity;
        public Color Color;
        public Vector3 WorldPosition;
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

        if (isEmitting)
        {
            EmitScheduledBursts();
            EmitRate(deltaTime);

            if (!looping && playbackTime >= duration)
                isEmitting = false;
        }

        UpdateParticles(deltaTime);

        if (!isEmitting && !HasActiveParticles())
            isPlaying = false;
    }

    public void Play()
    {
        PlayAtWorldPosition(transform.position);
    }

    public void PlayAt(Vector2 anchoredPosition)
    {
        PlayAt(anchoredPosition, clearExisting: clearOnPlay);
    }

    public void PlayAt(Vector2 anchoredPosition, bool clearExisting)
    {
        localOrigin = anchoredPosition + emitterOffset;
        playbackTime = 0f;
        previousPlaybackTime = -0.0001f;
        emissionAccumulator = 0f;
        isPlaying = true;
        isEmitting = true;

        EnsurePool();
        if (clearExisting)
            HideAll();

        EmitScheduledBursts();
        previousPlaybackTime = playbackTime;
    }

    public void PlayAtWorldPosition(Vector3 worldPosition)
    {
        PlayAtWorldPosition(worldPosition, clearExisting: clearOnPlay);
    }

    public void PlayAtWorldPosition(Vector3 worldPosition, bool clearExisting)
    {
        RectTransform root = ResolveParticleRoot();
        Vector2 localPosition = root != null
            ? root.InverseTransformPoint(worldPosition)
            : Vector2.zero;
        PlayAt(localPosition, clearExisting);
    }

    public void EmitBurst(int count)
    {
        EnsurePool();
        Emit(count, 0f);
    }

    public void Stop(bool clear = true)
    {
        if (!clear)
        {
            StopEmitting();
            return;
        }

        isPlaying = false;
        isEmitting = false;
        playbackTime = 0f;
        emissionAccumulator = 0f;

        if (clear)
            HideAll();
    }

    public void StopEmitting()
    {
        isEmitting = false;
        emissionAccumulator = 0f;

        if (!HasActiveParticles())
            isPlaying = false;
    }

#if UNITY_EDITOR
    public void PrepareEditorPreview()
    {
        if (Application.isPlaying)
            return;

        Stop(clear: true);
        DestroyEditorPreviewObjects();
        editorPreviewMode = true;
    }

    public void EndEditorPreview()
    {
        if (Application.isPlaying)
            return;

        editorPreviewMode = false;
    }

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
        DestroyOrphanEditorPreviewObjects();
    }

    private void DestroyOrphanEditorPreviewObjects()
    {
        RectTransform root = ResolveParticleRoot();
        DestroyOrphanEditorPreviewObjects(root);

        if (transform is RectTransform emitterRect && emitterRect != root)
            DestroyOrphanEditorPreviewObjects(emitterRect);
    }

    private static void DestroyOrphanEditorPreviewObjects(RectTransform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child == null || !child.name.StartsWith("UIParticle_", StringComparison.Ordinal))
                continue;

            DestroyImmediate(child.gameObject);
        }
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
        RectTransform root = ResolveParticleRoot();
        Vector2 position = localOrigin + spawnOffset;

        particle.Active = true;
        particle.Age = 0f;
        particle.Lifetime = Mathf.Max(0.001f, RandomRange(startLifetime));
        particle.Position = position;
        particle.WorldPosition = root != null ? root.TransformPoint(position) : (Vector3)position;
        particle.Velocity = direction * speed;
        particle.Size = Mathf.Max(0f, RandomRange(startSize));
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

            velocity = ApplyLimitVelocityOverLifetime(particle, velocity, deltaTime, t);

            particle.Velocity = velocity;
            if (simulationSpace == UIParticleSimulationSpace.World)
            {
                RectTransform root = ResolveParticleRoot();
                Vector3 worldVelocity = root != null ? root.TransformVector(velocity) : (Vector3)velocity;
                particle.WorldPosition += worldVelocity * deltaTime;
            }
            else
            {
                particle.Position += velocity * deltaTime;
            }

            particle.Rotation += particle.AngularVelocity * deltaTime;
            ApplyParticleVisual(particle, t);
        }
    }

    private Vector2 ApplyLimitVelocityOverLifetime(Particle particle, Vector2 velocity, float deltaTime, float lifetimeProgress)
    {
        if (!limitVelocityOverLifetime)
            return velocity;

        float limitMultiplier = EvaluateNonNegative(maxSpeedMultiplier, lifetimeProgress);
        Vector2 limitedVelocity = separateAxes
            ? LimitVelocityByAxes(velocity, limitMultiplier)
            : LimitVelocityByMagnitude(velocity, limitMultiplier);

        velocity = Vector2.Lerp(velocity, limitedVelocity, Mathf.Clamp01(velocityDampen));
        return ApplyVelocityDrag(particle, velocity, deltaTime, lifetimeProgress);
    }

    private Vector2 LimitVelocityByMagnitude(Vector2 velocity, float limitMultiplier)
    {
        float speed = velocity.magnitude;
        float limit = Mathf.Max(0f, maxSpeed) * Mathf.Max(0f, limitMultiplier);

        if (speed <= limit)
            return velocity;

        if (limit <= 0f || speed <= 0f)
            return Vector2.zero;

        return velocity / speed * limit;
    }

    private Vector2 LimitVelocityByAxes(Vector2 velocity, float limitMultiplier)
    {
        Vector2 axisLimits = ResolvePositiveVector(maxSpeedAxes) * Mathf.Max(0f, limitMultiplier);
        return new Vector2(
            ClampSignedMagnitude(velocity.x, axisLimits.x),
            ClampSignedMagnitude(velocity.y, axisLimits.y));
    }

    private Vector2 ApplyVelocityDrag(Particle particle, Vector2 velocity, float deltaTime, float lifetimeProgress)
    {
        float effectiveDrag = Mathf.Max(0f, drag) * EvaluateNonNegative(dragMultiplier, lifetimeProgress);
        if (effectiveDrag <= 0f || deltaTime <= 0f)
            return velocity;

        if (multiplyDragBySize && particle != null)
            effectiveDrag *= Mathf.Max(0f, particle.Size);

        if (multiplyDragByVelocity)
            effectiveDrag *= velocity.magnitude;

        float dragFactor = Mathf.Clamp01(effectiveDrag * deltaTime);
        return Vector2.Lerp(velocity, Vector2.zero, dragFactor);
    }

    private void ApplyParticleVisual(Particle particle, float t)
    {
        float sizeScale = particle.Size * (sizeOverLifetime ? Mathf.Max(0f, sizeOverLifetimeMultiplier.Evaluate(t)) : 1f);
        float lengthScale = sizeOverLifetime ? Mathf.Max(0f, lengthOverLifetime.Evaluate(t)) : 1f;
        float thicknessScale = sizeOverLifetime ? Mathf.Max(0f, thicknessOverLifetime.Evaluate(t)) : 1f;
        Color color = colorOverLifetime ? lifetimeColor.Evaluate(t) * particle.Color : particle.Color;
        Vector2 visualPosition = ResolveVisualPosition(particle);

        particle.Rect.anchoredPosition = visualPosition;
        particle.Rect.localRotation = Quaternion.Euler(0f, 0f, particle.Rotation);
        particle.Rect.sizeDelta = new Vector2(
            particle.Length * sizeScale * lengthScale,
            particle.Thickness * sizeScale * thicknessScale);
        particle.Image.color = color;

        ApplyTrailVisual(particle, t, color, sizeScale, lengthScale, thicknessScale, visualPosition);
    }

    private Vector2 ResolveVisualPosition(Particle particle)
    {
        if (simulationSpace != UIParticleSimulationSpace.World)
            return particle.Position;

        RectTransform root = ResolveParticleRoot();
        return root != null ? root.InverseTransformPoint(particle.WorldPosition) : particle.Position;
    }

    private void ApplyTrailVisual(
        Particle particle,
        float t,
        Color color,
        float sizeScale,
        float lengthScale,
        float thicknessScale,
        Vector2 visualPosition)
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
            rect.anchoredPosition = visualPosition - direction * (trailSpacing * (i + 1));
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            rect.sizeDelta = new Vector2(
                particle.Length * sizeScale * lengthScale * Mathf.Lerp(1f, trailSizeScale, segmentT),
                particle.Thickness * sizeScale * thicknessScale * Mathf.Lerp(1f, trailSizeScale, segmentT));

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
            particleObject.hideFlags = ResolveEditorPreviewHideFlags();
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
                trailObject.hideFlags = ResolveEditorPreviewHideFlags();
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

        if (transform is RectTransform rectTransform)
        {
            particleRoot = rectTransform;
            return particleRoot;
        }

        return GetComponentInParent<RectTransform>();
    }

    private Color ResolveStartColor()
    {
        return startColorMode == UIParticleStartColorMode.RandomBetweenTwoColors
            ? Color.Lerp(startColor, startColorB, UnityEngine.Random.value)
            : startColor;
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

            case UIParticleShape.Line:
                return RandomPointOnLine(ResolveShapeSize().x);

            case UIParticleShape.Rectangle:
                return RandomPointInRect(ResolveShapeSize());

            case UIParticleShape.RectangleEdge:
                return PointOnRectEdge(ResolveShapeSize(), direction);

            case UIParticleShape.Ellipse:
                return RandomPointInEllipse(ResolveShapeSize());

            case UIParticleShape.EllipseEdge:
                return PointOnEllipseEdge(ResolveShapeSize(), direction);

            case UIParticleShape.Arc:
                return direction * shapeRadius;

            case UIParticleShape.ArcFilled:
                return direction * (Mathf.Sqrt(UnityEngine.Random.value) * shapeRadius);

            case UIParticleShape.Point:
            default:
                return Vector2.zero;
        }
    }

    private Vector2 ResolveShapeSize()
    {
        return new Vector2(
            Mathf.Max(0f, shapeSize.x),
            Mathf.Max(0f, shapeSize.y));
    }

    private static Vector2 RandomPointOnLine(float length)
    {
        if (length <= 0f)
            return Vector2.zero;

        return new Vector2(UnityEngine.Random.Range(length * -0.5f, length * 0.5f), 0f);
    }

    private static Vector2 RandomPointInRect(Vector2 size)
    {
        if (size.x <= 0f && size.y <= 0f)
            return Vector2.zero;

        return new Vector2(
            UnityEngine.Random.Range(size.x * -0.5f, size.x * 0.5f),
            UnityEngine.Random.Range(size.y * -0.5f, size.y * 0.5f));
    }

    private static Vector2 PointOnRectEdge(Vector2 size, Vector2 direction)
    {
        if (size.x <= 0f && size.y <= 0f)
            return Vector2.zero;

        if (size.y <= 0f)
            return direction.x >= 0f
                ? Vector2.right * size.x * 0.5f
                : Vector2.left * size.x * 0.5f;

        if (size.x <= 0f)
            return direction.y >= 0f
                ? Vector2.up * size.y * 0.5f
                : Vector2.down * size.y * 0.5f;

        if (direction.sqrMagnitude <= 0.0001f)
            return Vector2.right * size.x * 0.5f;

        direction.Normalize();
        Vector2 halfSize = size * 0.5f;
        float scaleX = Mathf.Abs(direction.x) > 0.0001f
            ? halfSize.x / Mathf.Abs(direction.x)
            : float.PositiveInfinity;
        float scaleY = Mathf.Abs(direction.y) > 0.0001f
            ? halfSize.y / Mathf.Abs(direction.y)
            : float.PositiveInfinity;
        float scale = Mathf.Min(scaleX, scaleY);

        if (float.IsInfinity(scale) || scale <= 0f)
            return Vector2.zero;

        return direction * scale;
    }

    private static Vector2 RandomPointInEllipse(Vector2 size)
    {
        if (size.x <= 0f && size.y <= 0f)
            return Vector2.zero;

        if (size.y <= 0f)
            return RandomPointOnLine(size.x);

        if (size.x <= 0f)
            return new Vector2(0f, UnityEngine.Random.Range(size.y * -0.5f, size.y * 0.5f));

        Vector2 point = UnityEngine.Random.insideUnitCircle;
        return new Vector2(point.x * size.x * 0.5f, point.y * size.y * 0.5f);
    }

    private static Vector2 RandomPointOnEllipse(Vector2 size)
    {
        if (size.x <= 0f && size.y <= 0f)
            return Vector2.zero;

        if (size.y <= 0f)
            return RandomPointOnLine(size.x);

        if (size.x <= 0f)
            return new Vector2(0f, UnityEngine.Random.Range(size.y * -0.5f, size.y * 0.5f));

        float radians = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        return new Vector2(
            Mathf.Cos(radians) * size.x * 0.5f,
            Mathf.Sin(radians) * size.y * 0.5f);
    }

    private static Vector2 PointOnEllipseEdge(Vector2 size, Vector2 direction)
    {
        if (size.x <= 0f && size.y <= 0f)
            return Vector2.zero;

        if (size.y <= 0f)
            return direction.x >= 0f
                ? Vector2.right * size.x * 0.5f
                : Vector2.left * size.x * 0.5f;

        if (size.x <= 0f)
            return direction.y >= 0f
                ? Vector2.up * size.y * 0.5f
                : Vector2.down * size.y * 0.5f;

        if (direction.sqrMagnitude <= 0.0001f)
            return Vector2.right * size.x * 0.5f;

        direction.Normalize();
        Vector2 halfSize = size * 0.5f;
        float denominator = Mathf.Sqrt(
            direction.x * direction.x / (halfSize.x * halfSize.x)
            + direction.y * direction.y / (halfSize.y * halfSize.y));

        if (denominator <= 0.0001f)
            return Vector2.zero;

        return direction / denominator;
    }

    private static Vector2 AngleToVector(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private static Vector2 ResolvePositiveVector(Vector2 value)
    {
        return new Vector2(
            Mathf.Max(0f, value.x),
            Mathf.Max(0f, value.y));
    }

    private static float ClampSignedMagnitude(float value, float limit)
    {
        limit = Mathf.Max(0f, limit);
        if (limit <= 0f)
            return 0f;

        return Mathf.Clamp(value, -limit, limit);
    }

    private static float EvaluateNonNegative(AnimationCurve curve, float time)
    {
        return Mathf.Max(0f, curve != null ? curve.Evaluate(time) : 1f);
    }

    private static float RandomRange(Vector2 range)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        return Mathf.Approximately(min, max) ? min : UnityEngine.Random.Range(min, max);
    }

#if UNITY_EDITOR
    private HideFlags ResolveEditorPreviewHideFlags()
    {
        return editorPreviewMode
            ? HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            : HideFlags.HideAndDontSave;
    }
#endif

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

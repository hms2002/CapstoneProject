using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 쉐이더 장판 본체 위에 얹히는 술 거품 ParticleSystem과 불꽃 ParticleSystem들을 제어한다.
    /// - 장판의 본체 색/형태/흡수 연출은 소유하지 않고, 상태에 맞는 보조 파티클 재생과 정렬만 담당한다.
    /// - 술 장판의 버프 영역을 나타내는 상승 화살표를 재사용하고 상태 전환 시 숨긴다.
    /// - 선택적으로 화살표 대신 주황색 그라데이션 상승선을 표시하며 같은 생명주기를 적용한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PuddleParticleVisual : MonoBehaviour, IPuddleParticleVisual
    {
        [Header("Particles")]
        [SerializeField] private ParticleSystem bubbleParticles;
        [SerializeField] private ParticleSystem[] flameParticleSystems = new ParticleSystem[4];

        [Header("Sorting")]
        [SerializeField] private bool driveParticleRendererSorting = true;
        [SerializeField] private string particleSortingLayerName = "Entity";
        [SerializeField] private int particleSortingOrder = 2;

        [Header("Alcohol Bubble Layer")]
        [SerializeField] private bool driveAlcoholBubbleLayer = true;
        [SerializeField, Min(0f)] private float bubbleLayerRadiusScale = 0.82f;
        [SerializeField, Min(0f)] private float alcoholBubbleEmissionRate = 7f;
        [SerializeField] private bool stopBubbleLayerWhenNotAlcohol = true;

        [Header("Fire Flame Layer")]
        [SerializeField] private bool driveFlameLayer = true;
        [SerializeField, Min(0f)] private float flameLayerRadiusScale = 0.9f;
        [SerializeField, Min(0f)] private float fireFlameEmissionRate = 28f;
        [SerializeField, Min(0f)] private float ignitingFlameEmissionRate = 16f;
        [SerializeField] private bool clearFlamesWhenDisabled = true;

        [Header("Approved Pixel Artwork")]
        [Tooltip("Authored sprite emission: flame index0 is near-ground, index1 is airborne.")]
        [SerializeField] private bool usePixelArtwork;
        [SerializeField] private Vector2 pixelFlameEmissionRates = new Vector2(62.5f, 15f);
        private float ignitionProgress;
        private bool ignitionSparkEmitted;
        private readonly ParticleSystem.Particle[] bubbleBuffer = new ParticleSystem.Particle[32];

        private PuddleElementType elementType = PuddleElementType.Alcohol;
        private PuddleAreaMode currentMode = PuddleAreaMode.Ground;
        private float surfaceRadius = 1.35f;
        private ParticleSystemRenderer bubbleParticleRenderer;
        private ParticleSystemRenderer[] flameParticleRenderers;

        [Header("Alcohol Buff Arrows")]
        [SerializeField] private Sprite buffArrowSprite;
        [SerializeField] private bool flipBuffArrowY = true;
        [SerializeField, Range(1, 8)] private int buffArrowCount = 6;
        [SerializeField, Min(0.1f)] private float buffArrowLifetime = 0.85f;
        [SerializeField, Min(0.001f)] private float buffArrowWidth = 0.22f;
        [SerializeField, Min(0f)] private float buffArrowRise = 0.4f;
        [SerializeField] private Color buffArrowColor = new Color(0.65f, 1f, 0.8f, 0.7f);
        private SpriteRenderer[] buffArrows;
        private Vector2[] arrowOrigins;
        private float[] arrowAges;
        private System.Random arrowRandom;
        private Transform arrowSpace;
        [Header("Alcohol Rising Streaks")]
        [SerializeField] private bool useRisingStreaks;
        [SerializeField, Min(0.001f)] private float streakWidth = 0.045f;
        [SerializeField] private Vector2 streakLengthRange = new Vector2(0.25f, 0.45f);
        [SerializeField] private Color streakTailColor = new Color(1f, 0.25f, 0.02f, 0f);
        [SerializeField] private Color streakTipColor = new Color(1f, 0.7f, 0.2f, 0.85f);
        private LineRenderer[] streaks;
        private float[] streakLengths;

        private bool ShouldShowBuffArrows => isActiveAndEnabled && (useRisingStreaks || buffArrowSprite != null) &&
            elementType == PuddleElementType.Alcohol && currentMode == PuddleAreaMode.Ground;

        private void Update()
        {
            if (usePixelArtwork && currentMode == PuddleAreaMode.Igniting)
                RemoveBurnedBubbles();
            if (!ShouldShowBuffArrows) return;
            EnsureBuffArrows();
            float lifetime = Mathf.Max(0.1f, buffArrowLifetime);
            for (int i = 0; i < buffArrows.Length; i++)
            {
                arrowAges[i] += Time.deltaTime;
                if (arrowAges[i] >= lifetime)
                {
                    arrowAges[i] %= lifetime;
                    arrowOrigins[i] = NextArrowOrigin();
                    streakLengths[i] = NextStreakLength();
                }
                float t = arrowAges[i] / lifetime;
                SpriteRenderer arrow = buffArrows[i];
                float margin = useRisingStreaks ? streakLengths[i] * 0.5f + streakWidth :
                    buffArrowWidth * buffArrowSprite.bounds.size.magnitude /
                    Mathf.Max(0.001f, buffArrowSprite.bounds.size.x);
                float innerRadius = Mathf.Max(0f, surfaceRadius * 0.65f - margin);
                float rise = Mathf.Min(buffArrowRise, innerRadius);
                float spawnRadius = Mathf.Min(
                    Mathf.Max(0f, innerRadius - rise) * 1.5f,
                    Mathf.Max(0f, surfaceRadius - margin - rise));
                Vector2 ground = arrowOrigins[i] * spawnRadius;
                arrow.transform.localPosition = ground + Vector2.up * (rise * t);
                float fade = Mathf.Min(1f, t / 0.15f) *
                    (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, t)));
                if (useRisingStreaks)
                {
                    if (arrow.enabled) arrow.enabled = false;
                    if (!arrow.transform.localScale.Equals(Vector3.one))
                        arrow.transform.localScale = Vector3.one;
                    LineRenderer streak = streaks[i];
                    float length = streakLengths[i] * Mathf.Lerp(1f, 0.7f, t);
                    streak.SetPosition(0, Vector3.down * length * 0.5f);
                    streak.SetPosition(1, Vector3.up * length * 0.5f);
                    float startWidth = streakWidth * 0.35f;
                    if (!streak.startWidth.Equals(startWidth)) streak.startWidth = startWidth;
                    if (!streak.endWidth.Equals(streakWidth)) streak.endWidth = streakWidth;
                    Color tail = streakTailColor;
                    Color tip = streakTipColor;
                    tail.a *= fade;
                    tip.a *= fade;
                    streak.startColor = tail;
                    streak.endColor = tip;
                    if (!streak.enabled) streak.enabled = true;
                    continue;
                }
                if (streaks[i].enabled) streaks[i].enabled = false;
                float scale = buffArrowWidth / Mathf.Max(0.001f, buffArrowSprite.bounds.size.x);
                arrow.transform.localScale = Vector3.one * scale * Mathf.Lerp(1f, 0.65f, t);
                Color color = buffArrowColor;
                color.a *= fade;
                arrow.color = color;
                if (!arrow.enabled) arrow.enabled = true;
            }
        }

        private Vector2 NextArrowOrigin()
        {
            float angle = (float)arrowRandom.NextDouble() * Mathf.PI * 2f;
            float radius = Mathf.Sqrt((float)arrowRandom.NextDouble());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private float NextStreakLength() => Mathf.Lerp(Mathf.Max(0.01f, streakLengthRange.x),
            Mathf.Max(0.01f, streakLengthRange.y), (float)arrowRandom.NextDouble());

        private void EnsureBuffArrows()
        {
            if (buffArrows != null) return;
            int count = Mathf.Clamp(buffArrowCount, 1, 8);
            buffArrows = new SpriteRenderer[count];
            arrowOrigins = new Vector2[count];
            arrowAges = new float[count];
            streaks = new LineRenderer[count];
            streakLengths = new float[count];
            arrowRandom = new System.Random(GetInstanceID());
            PuddleAreaBase puddle = GetComponentInParent<PuddleAreaBase>();
            arrowSpace = puddle != null ? puddle.transform : transform;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("AlcoholBuffArrow");
                go.layer = gameObject.layer;
                go.transform.SetParent(arrowSpace, false);
                SpriteRenderer arrow = go.AddComponent<SpriteRenderer>();
                arrow.sprite = buffArrowSprite;
                arrow.flipY = flipBuffArrowY;
                arrow.sortingLayerName = particleSortingLayerName;
                arrow.sortingOrder = particleSortingOrder + 1;
                if (bubbleParticleRenderer != null) arrow.renderingLayerMask = bubbleParticleRenderer.renderingLayerMask;
                arrow.enabled = false;
                LineRenderer streak = go.AddComponent<LineRenderer>();
                streak.useWorldSpace = false;
                streak.positionCount = 2;
                streak.sharedMaterial = arrow.sharedMaterial;
                streak.sortingLayerID = arrow.sortingLayerID;
                streak.sortingOrder = arrow.sortingOrder;
                streak.renderingLayerMask = arrow.renderingLayerMask;
                streak.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                streak.receiveShadows = false;
                streak.numCapVertices = 2;
                streak.enabled = false;
                streaks[i] = streak;
                streakLengths[i] = NextStreakLength();
                buffArrows[i] = arrow;
                arrowOrigins[i] = NextArrowOrigin();
                arrowAges[i] = Mathf.Max(0.1f, buffArrowLifetime) * i / count;
            }
        }

        private void HideBuffArrows()
        {
            if (streaks != null)
                foreach (LineRenderer streak in streaks)
                    if (streak != null) streak.enabled = false;
            if (buffArrows == null) return;
            foreach (SpriteRenderer arrow in buffArrows)
                if (arrow != null) arrow.enabled = false;
        }

        private void OnDisable()
        {
            HideBuffArrows();
            if (usePixelArtwork) { StopBubbleLayer(true); StopFlameLayer(true); }
        }

        private void OnDestroy()
        {
            if (buffArrows == null) return;
            foreach (SpriteRenderer arrow in buffArrows)
                if (arrow != null) Destroy(arrow.gameObject);
        }

        private void Awake()
        {
            ConfigureBubbleLayer();
            ConfigureFlameLayer();
            ApplyParticleRendererSorting();
            ApplyBubbleLayerMode();
            ApplyFlameLayerMode();
        }

        private void OnEnable()
        {
            ApplyBubbleLayerMode();
            ApplyFlameLayerMode();
        }

        private void OnValidate()
        {
            bubbleLayerRadiusScale = Mathf.Max(0f, bubbleLayerRadiusScale);
            alcoholBubbleEmissionRate = Mathf.Max(0f, alcoholBubbleEmissionRate);
            flameLayerRadiusScale = Mathf.Max(0f, flameLayerRadiusScale);
            fireFlameEmissionRate = Mathf.Max(0f, fireFlameEmissionRate);
            ignitingFlameEmissionRate = Mathf.Max(0f, ignitingFlameEmissionRate);
            ConfigureBubbleLayer();
            ConfigureFlameLayer();
            ApplyParticleRendererSorting();
        }

        public void SetElementType(PuddleElementType newElementType)
        {
            elementType = newElementType;
            if (!ShouldShowBuffArrows) HideBuffArrows();
            ApplyBubbleLayerMode();
            ApplyFlameLayerMode();
        }

        public void SetSurfaceRadius(float radius)
        {
            surfaceRadius = Mathf.Max(0.01f, radius);
            ConfigureBubbleLayer();
            ConfigureFlameLayer();
        }

        public void ApplyMode(PuddleAreaMode mode)
        {
            if (mode != currentMode && mode == PuddleAreaMode.Igniting)
            {
                ignitionProgress = 0f;
                ignitionSparkEmitted = false;
            }
            currentMode = mode;
            if (!ShouldShowBuffArrows) HideBuffArrows();
            ApplyBubbleLayerMode();
            ApplyFlameLayerMode();
        }

        public void SetIgnitionProgress(float normalizedProgress)
        {
            ignitionProgress = Mathf.Clamp01(normalizedProgress);
            if (!usePixelArtwork || currentMode != PuddleAreaMode.Igniting) return;
            ConfigureFlameLayer();
            ApplyFlameLayerMode();
            if (!ignitionSparkEmitted && ignitionProgress > 0f && flameParticleSystems != null &&
                flameParticleSystems.Length > 0 && flameParticleSystems[0] != null)
            {
                var spark = new ParticleSystem.EmitParams
                {
                    position = Vector3.zero,
                    velocity = Vector3.zero,
                    applyShapeToPosition = false
                };
                flameParticleSystems[0].Emit(spark, 1);
                ignitionSparkEmitted = true;
            }
            RemoveBurnedBubbles();
        }

        private void RemoveBurnedBubbles()
        {
            if (bubbleParticles == null || ignitionProgress <= 0f) return;
            int count = bubbleParticles.GetParticles(bubbleBuffer);
            float pixelSize = surfaceRadius * 2.2f / 64f;
            float front = 27.2383446f * (Mathf.Floor(ignitionProgress * 15f + 0.0001f) / 15f);
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = bubbleBuffer[i].position;
                // Local particle space is the authored ground plane; image Y points down.
                float x = position.x / pixelSize, y = -position.y / pixelSize;
                float angle = Mathf.Atan2(y, x);
                float distance = Mathf.Sqrt(x * x + y * y) *
                    (1f + .065f * Mathf.Sin(angle * 5f + .4f) + .035f * Mathf.Sin(angle * 9f + 1.2f));
                if (distance > front && ignitionProgress < 1f) continue;
                bubbleBuffer[i].remainingLifetime = 0f;
                changed = true;
            }
            if (changed) bubbleParticles.SetParticles(bubbleBuffer, count);
        }

        private void ConfigureBubbleLayer()
        {
            if (bubbleParticles == null)
                return;

            ParticleSystem.MainModule main = bubbleParticles.main;
            main.loop = true;
            main.playOnAwake = false;

            ParticleSystem.ShapeModule shape = bubbleParticles.shape;
            if (shape.enabled)
            {
                shape.radius = Mathf.Max(0.01f, surfaceRadius * bubbleLayerRadiusScale);
                if (usePixelArtwork) shape.scale = Vector3.one * surfaceRadius * 2.2f * bubbleLayerRadiusScale;
            }
            if (usePixelArtwork) main.startSize = surfaceRadius * 2.2f * 8f / 64f;

            ParticleSystem.EmissionModule emission = bubbleParticles.emission;
            emission.rateOverTime = driveAlcoholBubbleLayer ? alcoholBubbleEmissionRate : 0f;
        }

        private void ConfigureFlameLayer()
        {
            if (flameParticleSystems == null)
                return;

            for (int i = 0; i < flameParticleSystems.Length; i++)
            {
                ParticleSystem flameParticleSystem = flameParticleSystems[i];
                if (flameParticleSystem == null)
                    continue;

                ParticleSystem.MainModule main = flameParticleSystem.main;
                main.loop = true;
                main.playOnAwake = false;

                ParticleSystem.ShapeModule shape = flameParticleSystem.shape;
                if (shape.enabled)
                {
                    shape.radius = Mathf.Max(0.01f, surfaceRadius * flameLayerRadiusScale);
                    if (usePixelArtwork)
                    {
                        float spread = currentMode == PuddleAreaMode.Igniting ? ignitionProgress : 1f;
                        shape.scale = Vector3.one * surfaceRadius * 2.2f * flameLayerRadiusScale * Mathf.Max(.01f, spread);
                    }
                }
                if (usePixelArtwork) main.startSize = surfaceRadius * 2.2f * (i == 0 ? 8f : 12f) / 64f;
            }
        }

        private void ApplyParticleRendererSorting()
        {
            if (!driveParticleRendererSorting)
                return;

            CacheParticleRenderers();

            if (bubbleParticleRenderer != null)
            {
                bubbleParticleRenderer.sortingLayerName = particleSortingLayerName;
                bubbleParticleRenderer.sortingOrder = particleSortingOrder;
            }

            if (flameParticleRenderers == null)
                return;

            for (int i = 0; i < flameParticleRenderers.Length; i++)
            {
                ParticleSystemRenderer flameRenderer = flameParticleRenderers[i];
                if (flameRenderer == null)
                    continue;

                flameRenderer.sortingLayerName = particleSortingLayerName;
                flameRenderer.sortingOrder = particleSortingOrder + 1 + i;
            }
        }

        private void ApplyBubbleLayerMode()
        {
            if (bubbleParticles == null)
                return;

            ConfigureBubbleLayer();

            bool shouldPlay =
                driveAlcoholBubbleLayer &&
                elementType == PuddleElementType.Alcohol &&
                (currentMode == PuddleAreaMode.Ground || currentMode == PuddleAreaMode.Igniting);

            if (shouldPlay)
            {
                ParticleSystem.EmissionModule emission = bubbleParticles.emission;
                emission.enabled = true;
                emission.rateOverTime = alcoholBubbleEmissionRate;

                if (!bubbleParticles.isPlaying)
                    bubbleParticles.Play();

                return;
            }

            if (stopBubbleLayerWhenNotAlcohol ||
                currentMode == PuddleAreaMode.AbsorbPreparing ||
                currentMode == PuddleAreaMode.AbsorbProjectile ||
                currentMode == PuddleAreaMode.Consumed)
            {
                StopBubbleLayer(currentMode == PuddleAreaMode.Consumed);
            }
        }

        private void ApplyFlameLayerMode()
        {
            if (flameParticleSystems == null)
                return;

            ConfigureFlameLayer();

            bool isFireState =
                elementType == PuddleElementType.Fire ||
                currentMode == PuddleAreaMode.Igniting;
            bool shouldPlay =
                driveFlameLayer &&
                isFireState &&
                (currentMode == PuddleAreaMode.Ground || currentMode == PuddleAreaMode.Igniting);

            if (shouldPlay)
            {
                float emissionRate = currentMode == PuddleAreaMode.Igniting
                    ? ignitingFlameEmissionRate
                    : fireFlameEmissionRate;
                int activeCount = CountAssignedFlameParticleSystems();
                float emissionRatePerSystem = activeCount > 0 ? emissionRate / activeCount : emissionRate;

                for (int i = 0; i < flameParticleSystems.Length; i++)
                {
                    ParticleSystem flameParticleSystem = flameParticleSystems[i];
                    if (flameParticleSystem == null)
                        continue;

                    ParticleSystem.EmissionModule emission = flameParticleSystem.emission;
                    emission.enabled = true;
                    float authoredRate = i == 0 ? pixelFlameEmissionRates.x : pixelFlameEmissionRates.y;
                    float coverage = currentMode == PuddleAreaMode.Igniting ? ignitionProgress * ignitionProgress : 1f;
                    emission.rateOverTime = usePixelArtwork ? authoredRate * coverage : emissionRatePerSystem;

                    if (!flameParticleSystem.isPlaying)
                    {
                        if (usePixelArtwork)
                        {
                            var main = flameParticleSystem.main;
                            main.prewarm = Application.isPlaying && currentMode == PuddleAreaMode.Ground && elementType == PuddleElementType.Fire;
                        }
                        flameParticleSystem.Play();
                    }
                }

                return;
            }

            StopFlameLayer(clearFlamesWhenDisabled || currentMode == PuddleAreaMode.Consumed);
        }

        private void StopBubbleLayer(bool clear = false)
        {
            if (bubbleParticles == null)
                return;

            ParticleSystem.EmissionModule emission = bubbleParticles.emission;
            emission.enabled = false;

            if (clear)
                bubbleParticles.Clear();

            bubbleParticles.Stop(false, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        }

        private void StopFlameLayer(bool clear = false)
        {
            if (flameParticleSystems == null)
                return;

            for (int i = 0; i < flameParticleSystems.Length; i++)
            {
                ParticleSystem flameParticleSystem = flameParticleSystems[i];
                if (flameParticleSystem == null)
                    continue;

                ParticleSystem.EmissionModule emission = flameParticleSystem.emission;
                emission.enabled = false;

                if (clear)
                    flameParticleSystem.Clear();

                flameParticleSystem.Stop(false, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private int CountAssignedFlameParticleSystems()
        {
            if (flameParticleSystems == null)
                return 0;

            int count = 0;
            for (int i = 0; i < flameParticleSystems.Length; i++)
            {
                if (flameParticleSystems[i] != null)
                    count++;
            }

            return count;
        }

        private void CacheParticleRenderers()
        {
            bubbleParticleRenderer = bubbleParticles != null
                ? bubbleParticles.GetComponent<ParticleSystemRenderer>()
                : null;

            if (flameParticleSystems == null)
            {
                flameParticleRenderers = null;
                return;
            }

            if (flameParticleRenderers == null || flameParticleRenderers.Length != flameParticleSystems.Length)
                flameParticleRenderers = new ParticleSystemRenderer[flameParticleSystems.Length];

            for (int i = 0; i < flameParticleSystems.Length; i++)
            {
                ParticleSystem flameParticleSystem = flameParticleSystems[i];
                flameParticleRenderers[i] = flameParticleSystem != null
                    ? flameParticleSystem.GetComponent<ParticleSystemRenderer>()
                    : null;
            }
        }
    }
}

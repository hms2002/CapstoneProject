using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 쉐이더 장판 본체 위에 얹히는 술 거품 ParticleSystem과 불꽃 ParticleSystem들을 제어한다.
    /// - 장판의 본체 색/형태/흡수 연출은 소유하지 않고, 상태에 맞는 보조 파티클 재생과 정렬만 담당한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PuddleParticleVisual : MonoBehaviour
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

        private PuddleElementType elementType = PuddleElementType.Alcohol;
        private PuddleAreaMode currentMode = PuddleAreaMode.Ground;
        private float surfaceRadius = 1.35f;
        private ParticleSystemRenderer bubbleParticleRenderer;
        private ParticleSystemRenderer[] flameParticleRenderers;

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
            currentMode = mode;
            ApplyBubbleLayerMode();
            ApplyFlameLayerMode();
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
                shape.radius = Mathf.Max(0.01f, surfaceRadius * bubbleLayerRadiusScale);

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
                    shape.radius = Mathf.Max(0.01f, surfaceRadius * flameLayerRadiusScale);
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
                    emission.rateOverTime = emissionRatePerSystem;

                    if (!flameParticleSystem.isPlaying)
                        flameParticleSystem.Play();
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

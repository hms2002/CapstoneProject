using UnityEngine;

namespace CapstonePresentation
{
    /// <summary>Owns visuals only. All objects/references are authored; no collision or damage callbacks.</summary>
    [DisallowMultipleComponent]
    public sealed class CrimsonBoundaryChargePresentation : MonoBehaviour, ICrimsonBoundaryChargePresentation
    {
        [Header("Charge")]
        [SerializeField] private Transform warning;
        [SerializeField] private SpriteRenderer innerCircle;
        [SerializeField] private SpriteRenderer outerCircle;
        [SerializeField] private SpriteRenderer centerCircle;
        [SerializeField] private float circleRotationSpeed = 37.5f;
        [SerializeField] private float warningFadeSeconds = 0.25f;
        [SerializeField] private ChargeGaugePresentation chargeGauge;

        [Header("Flight sprites and independent world-space particles")]
        [SerializeField] private Transform meteor;
        [SerializeField] private GameObject meteorSprites;
        [SerializeField] private SpriteRenderer flame;
        [SerializeField] private Sprite[] flameFrames;
        [SerializeField] private float flameFrameSeconds = 0.07f;
        [SerializeField] private ParticleSystem[] trailParticles;

        [Header("Impact")]
        [SerializeField] private Transform impact;
        [SerializeField] private SpriteRenderer flash;
        [SerializeField] private Sprite[] flashFrames;
        [SerializeField] private float[] flashFrameSeconds = { 0.04f, 0.05f, 0.06f };
        [SerializeField] private SpriteRenderer scorch;
        [SerializeField] private ParticleSystem[] impactParticles;
        [SerializeField] private TopDownDebrisBounceEmitter2D debris;
        [SerializeField] private float nominalDiameter = 6f;
        [SerializeField] private float impactLifetime = 2.1f;

        private CrimsonBoundaryRuntimeState owner;
        private float phaseTime;
        private float circleAngle;
        private float centerCircleAngle;
        private Phase phase;
        private enum Phase { Charge, Flight, Impact }

        private void Awake()
        {
            innerCircle.enabled = false;
            outerCircle.enabled = false;
            centerCircle.enabled = false;
            innerCircle.transform.localScale = Vector3.zero;
            outerCircle.transform.localScale = Vector3.zero;
            centerCircle.transform.localScale = Vector3.zero;
            meteor.gameObject.SetActive(false);
            impact.gameObject.SetActive(false);
        }

        public void BindOwner(CrimsonBoundaryRuntimeState value) => owner = value;

        public void ShowCharge(Vector3 ownerPosition, Vector3 target, float ratio, float diameter)
        {
            warning.position = target;
            innerCircle.color = outerCircle.color = centerCircle.color = Color.white;
            chargeGauge.ShowCharge(ownerPosition, ratio);
            float progress = Mathf.Clamp01(ratio);
            float innerFraction = CrimsonBoundaryChargeData.InnerCircleFraction;
            float rangeFraction = progress <= 0.5f ? innerFraction * progress * 2f
                : Mathf.Lerp(innerFraction, 1f, progress * 2f - 1f);
            float maximumDiameter = rangeFraction > 0f ? diameter / rangeFraction : 0f;
            // The 192px artwork has a 128px visible inner diameter; gameplay keeps its original range ratio.
            SetCircleDiameter(innerCircle, maximumDiameter * innerFraction * Mathf.Clamp01(progress * 2f), 128f / 192f);
            // Both first-stage layers share a centered 192px canvas, preserving their tangent outlines.
            centerCircle.transform.localScale = innerCircle.transform.localScale;
            centerCircle.enabled = innerCircle.enabled;
            SetCircleDiameter(outerCircle, progress > 0.5f ? diameter : 0f, 1f);
        }

        public void BeginFlight(Vector3 start, Vector3 target)
        {
            phase = Phase.Flight;
            phaseTime = 0f;
            warning.position = target;
            innerCircle.color = outerCircle.color = centerCircle.color = Color.white;
            chargeGauge.Hide();
            meteor.position = start;
            Vector3 direction = target - start;
            // The source sprite points southwest (-135 degrees).
            meteor.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 135f);
            meteor.gameObject.SetActive(true);
            foreach (ParticleSystem particles in trailParticles) particles.Play(false);
        }

        public void MoveMeteor(Vector3 position)
        {
            meteor.position = position;
        }

        public void PlayImpact(Vector3 position, float diameter)
        {
            // Keep the released footprint through flight; only alpha fades after impact.
            phase = Phase.Impact;
            phaseTime = 0f;
            meteorSprites.SetActive(false);
            // Leave emitted world-space puffs alive; only stop creating new ones.
            foreach (ParticleSystem particles in trailParticles)
                particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            impact.position = position;
            impact.localScale = Vector3.one * (diameter / Mathf.Max(0.01f, nominalDiameter));
            impact.gameObject.SetActive(true);
            flash.sprite = flashFrames[0];
            foreach (ParticleSystem particles in impactParticles) particles.Play(false);
            debris.Play();
        }

        private void Update() => Tick(Time.deltaTime);

        private void Tick(float deltaTime)
        {
            phaseTime += deltaTime;
            if (warning.gameObject.activeSelf)
            {
                circleAngle = Mathf.Repeat(circleAngle + circleRotationSpeed * deltaTime, 360f);
                innerCircle.transform.localRotation = Quaternion.Euler(0f, 0f, circleAngle);
                outerCircle.transform.localRotation = Quaternion.Euler(0f, 0f, -circleAngle);
                centerCircleAngle = Mathf.Repeat(centerCircleAngle - circleRotationSpeed * 1.5f * deltaTime, 360f);
                centerCircle.transform.localRotation = Quaternion.Euler(0f, 0f, centerCircleAngle);
                if (phase == Phase.Impact)
                {
                    float warningAlpha = Mathf.Clamp01(1f - phaseTime / Mathf.Max(0.01f, warningFadeSeconds));
                    innerCircle.color = outerCircle.color = centerCircle.color = new Color(1f, 1f, 1f, warningAlpha);
                    if (warningAlpha <= 0f) warning.gameObject.SetActive(false);
                }
            }
            if (phase == Phase.Flight)
            {
                flame.sprite = flameFrames[(int)(phaseTime / flameFrameSeconds) % flameFrames.Length];
            }
            else if (phase == Phase.Impact)
            {
                float frameEnd = 0f;
                int frame = 0;
                for (; frame < flashFrameSeconds.Length; frame++)
                {
                    frameEnd += flashFrameSeconds[frame];
                    if (phaseTime < frameEnd) break;
                }
                flash.enabled = frame < flashFrames.Length;
                if (flash.enabled) flash.sprite = flashFrames[frame];
                float alpha = Mathf.Clamp01(phaseTime / 0.1f) * Mathf.Clamp01((2f - phaseTime) / 0.7f) * 0.65f;
                scorch.color = new Color(1f, 1f, 1f, alpha);
                if (phaseTime >= impactLifetime) Destroy(gameObject);
            }
        }

        private static void SetCircleDiameter(SpriteRenderer circle, float diameter, float visibleFraction)
        {
            float sourceDiameter = circle.sprite.rect.width / circle.sprite.pixelsPerUnit * visibleFraction;
            circle.transform.localScale = Vector3.one * (Mathf.Max(0f, diameter) / sourceDiameter);
            circle.enabled = diameter > 0f;
        }

#if UNITY_EDITOR
        // Native Editor captures use the same phase/flipbook code and authored particle modules.
        public void StepEditorPreview(float deltaTime)
        {
            Tick(deltaTime);
            foreach (ParticleSystem p in trailParticles)
                if (p.gameObject.activeInHierarchy) p.Simulate(deltaTime, false, false, false);
            if (phase != Phase.Impact) return;
            foreach (ParticleSystem p in impactParticles) p.Simulate(deltaTime, false, false, false);
            debris.StepEditorPreview(deltaTime);
        }
#endif

        private void OnDestroy()
        {
            if (owner != null) owner.Forget(gameObject);
        }
    }
}

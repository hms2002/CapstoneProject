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
        [SerializeField] private Sprite relicContour;
        [SerializeField] private float circleRotationSpeed = 24f;
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
        private bool showsGauge = true;
        private float phaseTime;
        private float circleAngle;
        private float rotationDirection = -1f;
        private Phase phase;
        private enum Phase { Charge, Flight, Impact }

        private void Awake()
        {
            innerCircle.enabled = false;
            outerCircle.enabled = false;
            innerCircle.transform.localScale = Vector3.zero;
            outerCircle.transform.localScale = Vector3.zero;
            meteor.gameObject.SetActive(false);
            impact.gameObject.SetActive(false);
        }

        public void BindOwner(CrimsonBoundaryRuntimeState value, bool showGauge = true, Vector2 outward = default)
        {
            owner = value;
            showsGauge = showGauge;
            if (!showsGauge) chargeGauge.Hide();
            if (outward == Vector2.zero) return;

            circleAngle = Vector2.SignedAngle(Vector2.up, outward);
            rotationDirection = outward.x < 0f ? 1f : -1f;
            outerCircle.sprite = relicContour;
            outerCircle.transform.localRotation = Quaternion.Euler(0f, 0f, circleAngle);
            innerCircle.transform.localRotation = outerCircle.transform.localRotation;
        }

        public void ShowCharge(Vector3 ownerPosition, Vector3 target, float ratio, float diameter)
        {
            warning.position = target;
            innerCircle.color = outerCircle.color = Color.white;
            if (showsGauge) chargeGauge.ShowCharge(ownerPosition, ratio);
            // The outline and trim share the centered 192px canvas and attack diameter.
            float sourceDiameter = outerCircle.sprite.rect.width / outerCircle.sprite.pixelsPerUnit;
            Vector3 scale = Vector3.one * (diameter / sourceDiameter);
            innerCircle.transform.localScale = outerCircle.transform.localScale = scale;
            innerCircle.enabled = outerCircle.enabled = diameter > 0f;
        }

        public void BeginFlight(Vector3 start, Vector3 target)
        {
            phase = Phase.Flight;
            phaseTime = 0f;
            warning.position = target;
            innerCircle.color = outerCircle.color = Color.white;
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
                circleAngle = Mathf.Repeat(circleAngle + rotationDirection * circleRotationSpeed * deltaTime, 360f);
                innerCircle.transform.localRotation = Quaternion.Euler(0f, 0f, circleAngle);
                if (phase == Phase.Impact)
                {
                    float warningAlpha = Mathf.Clamp01(1f - phaseTime / Mathf.Max(0.01f, warningFadeSeconds));
                    innerCircle.color = outerCircle.color = new Color(1f, 1f, 1f, warningAlpha);
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

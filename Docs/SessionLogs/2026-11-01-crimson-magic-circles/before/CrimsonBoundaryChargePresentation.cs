using UnityEngine;

namespace CapstonePresentation
{
    /// <summary>Owns visuals only. All objects/references are authored; no collision or damage callbacks.</summary>
    [DisallowMultipleComponent]
    public sealed class CrimsonBoundaryChargePresentation : MonoBehaviour, ICrimsonBoundaryChargePresentation
    {
        [Header("Charge")]
        [SerializeField] private Transform warning;
        [SerializeField] private MeshFilter disc;
        [SerializeField] private MeshFilter dashes;
        [SerializeField] private Transform gauge;
        [SerializeField] private UnityEngine.UI.Image gaugeFill;
        [SerializeField] private UnityEngine.UI.Image gaugeHighlight;
        [SerializeField] private UnityEngine.UI.Image gaugeShadow;
        [SerializeField] private GameObject gaugeFullFrame;
        [SerializeField] private Vector3 gaugeOffset = new(0f, 1.3f, 0f);
        [SerializeField] private Color chargingColor = new Color32(221, 46, 70, 255);
        [SerializeField] private Color readyColor = new Color32(240, 120, 38, 255);
        [SerializeField] private Color fullColor = new Color32(255, 157, 65, 255);
        [SerializeField] private Color chargingHighlight = new Color32(255, 102, 91, 255);
        [SerializeField] private Color readyHighlight = new Color32(255, 171, 86, 255);
        [SerializeField] private Color fullHighlight = new Color32(255, 235, 171, 255);
        [SerializeField] private Color chargingShadow = new Color32(128, 23, 48, 255);
        [SerializeField] private Color readyShadow = new Color32(158, 60, 33, 255);
        [SerializeField] private Color fullShadow = new Color32(207, 70, 41, 255);
        [SerializeField] private float dashThickness = 0.055f;
        [SerializeField] private float dashRotationSpeed = 35f;

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
        private Mesh discMesh;
        private Mesh dashMesh;
        private float lastRadius = -1f;
        private Vector3 flightTarget;
        private float flightDistance;
        private float flightWarningRadius;
        private float phaseTime;
        private float dashAngle;
        private Phase phase;
        private enum Phase { Charge, Flight, Impact }
        private const int Segments = 96;
        private const int DashCount = 24;
        private readonly Vector3[] discVertices = new Vector3[Segments + 1];
        private readonly int[] discTriangles = new int[Segments * 3];
        private readonly Vector3[] dashVertices = new Vector3[DashCount * 8];
        private readonly int[] dashTriangles = new int[DashCount * 18];

        private void Awake()
        {
            discMesh = new Mesh { name = "Crimson charge disc (instance)" };
            dashMesh = new Mesh { name = "Crimson constant-width dashes (instance)" };
            discMesh.MarkDynamic();
            dashMesh.MarkDynamic();
            disc.sharedMesh = discMesh;
            dashes.sharedMesh = dashMesh;
            for (int i = 0; i < Segments; i++)
            {
                discTriangles[i * 3] = 0;
                discTriangles[i * 3 + 1] = (i + 1) % Segments + 1;
                discTriangles[i * 3 + 2] = i + 1;
            }
            for (int i = 0; i < DashCount; i++)
                for (int j = 0; j < 3; j++)
                {
                    int v = i * 8 + j * 2;
                    int t = i * 18 + j * 6;
                    dashTriangles[t] = v;
                    dashTriangles[t + 1] = v + 2;
                    dashTriangles[t + 2] = v + 1;
                    dashTriangles[t + 3] = v + 1;
                    dashTriangles[t + 4] = v + 2;
                    dashTriangles[t + 5] = v + 3;
                }
            meteor.gameObject.SetActive(false);
            impact.gameObject.SetActive(false);
        }

        public void BindOwner(CrimsonBoundaryRuntimeState value) => owner = value;

        public void ShowCharge(Vector3 ownerPosition, Vector3 target, float ratio, float diameter)
        {
            warning.position = target;
            gauge.position = ownerPosition + gaugeOffset;
            gaugeFill.fillAmount = Mathf.Clamp01(ratio);
            gaugeHighlight.fillAmount = gaugeFill.fillAmount;
            gaugeShadow.fillAmount = gaugeFill.fillAmount;
            gaugeFill.color = ratio >= 1f ? fullColor : ratio >= 0.5f ? readyColor : chargingColor;
            gaugeHighlight.color = ratio >= 1f ? fullHighlight : ratio >= 0.5f ? readyHighlight : chargingHighlight;
            gaugeShadow.color = ratio >= 1f ? fullShadow : ratio >= 0.5f ? readyShadow : chargingShadow;
            gaugeFullFrame.SetActive(ratio >= 1f);
            RebuildWarning(Mathf.Max(0f, diameter * 0.5f));
        }

        public void BeginFlight(Vector3 start, Vector3 target)
        {
            phase = Phase.Flight;
            phaseTime = 0f;
            flightTarget = target;
            flightDistance = Vector3.Distance(start, target);
            flightWarningRadius = Mathf.Max(0f, lastRadius);
            gauge.gameObject.SetActive(false);
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
            if (phase != Phase.Flight) return;
            // Follow the actual flight progress without a second timer or changing gameplay's impact radius.
            float remaining = flightDistance > 0f ? Mathf.Clamp01(Vector3.Distance(position, flightTarget) / flightDistance) : 0f;
            RebuildWarning(flightWarningRadius * remaining);
        }

        public void PlayImpact(Vector3 position, float diameter)
        {
            // The shrinking warning stays centered on the landing point until impact.
            warning.gameObject.SetActive(false);
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
            if (phase != Phase.Impact)
            {
                dashAngle -= dashRotationSpeed * deltaTime;
                dashes.transform.localRotation = Quaternion.Euler(0f, 0f, dashAngle);
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

        private void RebuildWarning(float radius)
        {
            if (Mathf.Approximately(radius, lastRadius)) return;
            lastRadius = radius;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                discVertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            discMesh.vertices = discVertices;
            discMesh.triangles = discTriangles;
            discMesh.RecalculateBounds();
            float inner = Mathf.Max(0f, radius - dashThickness);
            for (int i = 0; i < DashCount; i++)
                for (int j = 0; j < 4; j++)
                {
                    float angle = (i + j / 3f * 0.55f) * Mathf.PI * 2f / DashCount;
                    Vector3 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                    dashVertices[i * 8 + j * 2] = direction * inner;
                    dashVertices[i * 8 + j * 2 + 1] = direction * radius;
                }
            dashMesh.vertices = dashVertices;
            dashMesh.triangles = dashTriangles;
            dashMesh.RecalculateBounds();
        }

#if UNITY_EDITOR
        public void DisposeEditorPreview()
        {
            if (discMesh != null) DestroyImmediate(discMesh);
            if (dashMesh != null) DestroyImmediate(dashMesh);
            discMesh = null;
            dashMesh = null;
        }

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
            if (discMesh != null) Destroy(discMesh);
            if (dashMesh != null) Destroy(dashMesh);
        }
    }
}

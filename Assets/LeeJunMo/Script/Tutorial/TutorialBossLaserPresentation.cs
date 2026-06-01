using System;
using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class TutorialBossLaserPresentation : MonoBehaviour
{
    [Serializable]
    public sealed class IntEvent : UnityEvent<int>
    {
    }

    [Serializable]
    public sealed class LaserStep
    {
        public Transform origin;
        public Vector2 direction = Vector2.right;
        [Min(0.01f)] public float length = 10f;
        [Min(0.01f)] public float width = 0.6f;
        [Min(0f)] public float warningSeconds = 0.55f;
        [Min(0f)] public float attackSeconds = 0.45f;
        [Min(0f)] public float postDelaySeconds = 0.12f;
        public bool spawnOppositeRay;
        public bool showPrimitiveWarning = true;
        public DemonKingEgoLaserVfx laserVfxPrefab;
        public Color warningColor = new(1f, 0.1f, 0.1f, 0.35f);
        public Color fallbackAttackColor = new(1f, 0.05f, 0.05f, 0.65f);
    }

    [Header("Laser")]
    [SerializeField] private DemonKingEgoLaserVfx defaultLaserVfxPrefab;
    [SerializeField] private LaserStep[] steps;

    [Header("Sound")]
    [SerializeField] private SoundRef laserFireSound = SoundRef.FromKey("sound_boss_darklord_laser");

    [Header("Presentation HP")]
    [SerializeField] private TutorialPresentationHpView presentationHpView;
    [SerializeField] private bool reduceHpOnEachStep = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onSequenceStarted = new();
    [SerializeField] private UnityEvent onSequenceCompleted = new();
    [SerializeField] private IntEvent onStepStarted = new();
    [SerializeField] private IntEvent onStepHit = new();

    private Coroutine runningRoutine;
    private bool cancelRequested;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;
    public UnityEvent OnSequenceStarted => onSequenceStarted;
    public UnityEvent OnSequenceCompleted => onSequenceCompleted;
    public IntEvent OnStepStarted => onStepStarted;
    public IntEvent OnStepHit => onStepHit;

    private void OnDisable()
    {
        Cancel();
    }

    public void Play()
    {
        Cancel();
        runningRoutine = StartCoroutine(PlayRoutine());
    }

    public void Cancel()
    {
        cancelRequested = true;

        if (runningRoutine != null)
        {
            StopCoroutine(runningRoutine);
            runningRoutine = null;
        }

        isPlaying = false;
    }

    public IEnumerator PlayRoutine()
    {
        if (isPlaying)
        {
            while (isPlaying)
                yield return null;

            yield break;
        }

        isPlaying = true;
        cancelRequested = false;
        onSequenceStarted?.Invoke();

        LaserStep[] resolvedSteps = steps ?? Array.Empty<LaserStep>();
        for (int i = 0; i < resolvedSteps.Length; i++)
        {
            if (cancelRequested)
                break;

            LaserStep step = resolvedSteps[i];
            if (step == null)
                continue;

            yield return PlayStepRoutine(step, i);
        }

        if (!cancelRequested)
            onSequenceCompleted?.Invoke();

        isPlaying = false;
        runningRoutine = null;
    }

    private IEnumerator PlayStepRoutine(LaserStep step, int stepIndex)
    {
        Vector2 origin = ResolveOrigin(step);
        Vector2 direction = ResolveDirection(step.direction);
        float length = Mathf.Max(0.01f, step.length);
        float width = Mathf.Max(0.01f, step.width);
        float warningSeconds = Mathf.Max(0f, step.warningSeconds);
        float attackSeconds = Mathf.Max(0f, step.attackSeconds);

        onStepStarted?.Invoke(stepIndex);

        if (step.showPrimitiveWarning && warningSeconds > 0f)
        {
            SpawnPrimitiveLaser(origin, direction, length, width, warningSeconds, step.warningColor, "TutorialBossLaserWarning");
            if (step.spawnOppositeRay)
                SpawnPrimitiveLaser(origin, -direction, length, width, warningSeconds, step.warningColor, "TutorialBossLaserWarning");
        }

        yield return WaitSeconds(warningSeconds);
        if (cancelRequested)
            yield break;

        if (reduceHpOnEachStep && presentationHpView != null)
            presentationHpView.ReduceOne();

        onStepHit?.Invoke(stepIndex);
        PlayLaserFireSound(origin);

        DemonKingEgoLaserVfx primaryVfx = SpawnLaserVfx(step, origin, direction, length, width, attackSeconds);
        DemonKingEgoLaserVfx oppositeVfx = step.spawnOppositeRay
            ? SpawnLaserVfx(step, origin, -direction, length, width, attackSeconds)
            : null;

        if (primaryVfx == null)
            SpawnPrimitiveLaser(origin, direction, length, width, attackSeconds, step.fallbackAttackColor, "TutorialBossLaserFallback");

        if (step.spawnOppositeRay && oppositeVfx == null)
            SpawnPrimitiveLaser(origin, -direction, length, width, attackSeconds, step.fallbackAttackColor, "TutorialBossLaserFallback");

        yield return WaitForAttackRoutine(primaryVfx, oppositeVfx, attackSeconds);
        yield return WaitSeconds(step.postDelaySeconds);
    }

    private DemonKingEgoLaserVfx SpawnLaserVfx(
        LaserStep step,
        Vector2 origin,
        Vector2 direction,
        float length,
        float width,
        float attackSeconds)
    {
        DemonKingEgoLaserVfx prefab = step.laserVfxPrefab != null
            ? step.laserVfxPrefab
            : defaultLaserVfxPrefab;
        if (prefab == null)
            return null;

        DemonKingEgoLaserVfx instance = Instantiate(prefab);
        instance.Play(origin, direction, length, width, attackSeconds);
        return instance;
    }

    private void PlayLaserFireSound(Vector2 origin)
    {
        SoundPlaybackUtility.Play(
            laserFireSound,
            causer: gameObject,
            position: new Vector3(origin.x, origin.y, transform.position.z),
            sourceObject: this);
    }

    private IEnumerator WaitForAttackRoutine(
        DemonKingEgoLaserVfx primaryVfx,
        DemonKingEgoLaserVfx oppositeVfx,
        float fallbackSeconds)
    {
        if (primaryVfx == null && oppositeVfx == null)
        {
            yield return WaitSeconds(fallbackSeconds);
            yield break;
        }

        float minimumWaitSeconds = Mathf.Max(0f, fallbackSeconds);
        float maxWaitSeconds = Mathf.Max(0.1f, fallbackSeconds + 2f);
        float elapsed = 0f;
        bool sawPlaying = false;
        while (!cancelRequested && elapsed < maxWaitSeconds)
        {
            bool primaryPlaying = primaryVfx != null && primaryVfx.IsPlaying;
            bool oppositePlaying = oppositeVfx != null && oppositeVfx.IsPlaying;
            if (primaryPlaying || oppositePlaying)
                sawPlaying = true;

            if (elapsed >= minimumWaitSeconds && sawPlaying && !primaryPlaying && !oppositePlaying)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static void SpawnPrimitiveLaser(
        Vector2 origin,
        Vector2 direction,
        float length,
        float width,
        float duration,
        Color color,
        string objectName)
    {
        Vector2 safeDirection = ResolveDirection(direction);
        Vector2 center = origin + safeDirection * (length * 0.5f);
        float rotationDeg = Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg;
        DemonKingPrimitiveVisual.SpawnSquare(
            center,
            new Vector2(Mathf.Max(0.01f, length), Mathf.Max(0.01f, width)),
            rotationDeg,
            Mathf.Max(0f, duration),
            color,
            objectName);
    }

    private IEnumerator WaitSeconds(float seconds)
    {
        float duration = Mathf.Max(0f, seconds);
        if (duration <= 0f)
            yield break;

        float elapsed = 0f;
        while (!cancelRequested && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private Vector2 ResolveOrigin(LaserStep step)
    {
        if (step != null && step.origin != null)
            return step.origin.position;

        return transform.position;
    }

    private static Vector2 ResolveDirection(Vector2 direction)
    {
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
    }
}

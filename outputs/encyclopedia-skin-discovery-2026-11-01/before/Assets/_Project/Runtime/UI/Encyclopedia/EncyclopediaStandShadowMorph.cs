using UnityEngine;

/// <summary>Projects book state onto local shadow poses and layers a non-accumulating periodic scale effect without owning interaction.</summary>
[DisallowMultipleComponent]
public sealed class EncyclopediaStandShadowMorph : MonoBehaviour
{
    /// <summary>Selects discrete size changes or continuous interpolation for the repeating effect only.</summary>
    private enum PulseMode { Step = 0, Smooth = 1 }

    /// <summary>Stores a shadow pose relative to its existing parent.</summary>
    [System.Serializable]
    private struct ShadowPose
    {
        public Vector3 position;
        public Vector3 eulerAngles;
        public Vector3 scale;

        public static ShadowPose Capture(Transform target) => new ShadowPose
        {
            position = target.localPosition,
            eulerAngles = target.localEulerAngles,
            scale = target.localScale
        };
    }

    [SerializeField] private BookWorldSpriteSequencePresentation book;
    [SerializeField] private Transform shadow;
    [SerializeField] private ShadowPose bookClosed = new ShadowPose { scale = Vector3.one };
    [SerializeField] private ShadowPose bookOpen = new ShadowPose { scale = Vector3.one };
    [SerializeField, Min(0f)] private float duration = 0.25f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Repeating Scale")]
    [SerializeField] private bool pulseEnabled = true;
    [SerializeField] private PulseMode pulseMode = PulseMode.Step;
    [SerializeField] private Vector3 minScaleMultiplier = new Vector3(0.95f, 0.95f, 1f);
    [SerializeField] private Vector3 maxScaleMultiplier = new Vector3(1.05f, 1.05f, 1f);
    [Tooltip("Step: hold Min. Smooth: travel from Min to Max.")]
    [SerializeField, Min(0.01f)] private float minSeconds = 0.3f;
    [Tooltip("Step: hold Max. Smooth: travel from Max to Min.")]
    [SerializeField, Min(0.01f)] private float maxSeconds = 0.3f;
    [SerializeField] private bool startAtMax;
    [Tooltip("Restart at the selected endpoint when the book enters a different animation state. Idle loops do not reset.")]
    [SerializeField] private bool resetPulseOnAnimationChange = true;

    private bool initialized;
    private bool targetOpen;
    private bool moving;
    private float elapsed;
    private ShadowPose start;
    private Vector3 baseScale;
    private float pulseTime;
    private bool hasAnimationState;
    private int lastAnimationState;

    private void Awake()
    {
        if (book == null) book = GetComponent<BookWorldSpriteSequencePresentation>();
    }

    private void OnEnable() { initialized = false; pulseTime = 0f; hasAnimationState = false; }
    private void OnDisable()
    {
        if (initialized && shadow != null) shadow.localScale = baseScale;
        moving = false;
        initialized = false;
    }

    private void LateUpdate()
    {
        if (book == null || shadow == null) return;
        if (!initialized)
        {
            initialized = true;
            targetOpen = book.IsOpen;
            Apply(targetOpen ? bookOpen : bookClosed);
            moving = false;
        }
        if (targetOpen != book.IsOpen)
        {
            targetOpen = book.IsOpen;
            start = ShadowPose.Capture(shadow);
            // Retarget from the unmodulated pose, never from the previous pulse result.
            start.scale = baseScale;
            elapsed = 0f;
            moving = true;
        }
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (moving) UpdatePose(deltaTime);
        SyncPulseAnimationState();
        ApplyPulse();
        float period = Mathf.Max(0.01f, minSeconds) + Mathf.Max(0.01f, maxSeconds);
        if (pulseEnabled) pulseTime = Mathf.Repeat(pulseTime + deltaTime, period);
    }

    private void SyncPulseAnimationState()
    {
        if (!book.TryGetAnimationStateHash(out int state))
        {
            hasAnimationState = false;
            return;
        }

        if (resetPulseOnAnimationChange && (!hasAnimationState || lastAnimationState != state))
            pulseTime = 0f;
        // Track even with reset disabled, so toggling the option does not invent a transition.
        lastAnimationState = state;
        hasAnimationState = true;
    }

    private void UpdatePose(float deltaTime)
    {
        elapsed += deltaTime;
        ShadowPose target = targetOpen ? bookOpen : bookClosed;
        float progress = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
        if (progress >= 1f)
        {
            Apply(target);
            moving = false;
            return;
        }
        float t = curve == null ? progress : Mathf.Clamp01(curve.Evaluate(progress));
        shadow.localPosition = Vector3.Lerp(start.position, target.position, t);
        shadow.localRotation = Quaternion.Slerp(Quaternion.Euler(start.eulerAngles), Quaternion.Euler(target.eulerAngles), t);
        baseScale = Vector3.Lerp(start.scale, target.scale, t);
    }

    private void ApplyPulse()
    {
        if (!pulseEnabled)
        {
            shadow.localScale = baseScale;
            return;
        }
        float minTime = Mathf.Max(0.01f, minSeconds);
        float maxTime = Mathf.Max(0.01f, maxSeconds);
        float phase = Mathf.Repeat(pulseTime + (startAtMax ? minTime : 0f), minTime + maxTime);
        float weight;
        if (pulseMode == PulseMode.Step)
            weight = phase < minTime ? 0f : 1f;
        else
            weight = phase < minTime
                ? Mathf.SmoothStep(0f, 1f, phase / minTime)
                : Mathf.SmoothStep(1f, 0f, (phase - minTime) / maxTime);
        shadow.localScale = Vector3.Scale(baseScale, Vector3.Lerp(minScaleMultiplier, maxScaleMultiplier, weight));
    }

    private void Apply(ShadowPose pose)
    {
        shadow.localPosition = pose.position;
        shadow.localRotation = Quaternion.Euler(pose.eulerAngles);
        shadow.localScale = pose.scale;
        baseScale = pose.scale;
    }

#if UNITY_EDITOR
    [ContextMenu("Capture Shadow As Book Closed")]
    private void CaptureClosed()
    {
        if (shadow == null) return;
        UnityEditor.Undo.RecordObject(this, "Capture closed shadow pose");
        bookClosed = ShadowPose.Capture(shadow);
        if (Application.isPlaying && initialized) bookClosed.scale = baseScale;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

    [ContextMenu("Capture Shadow As Book Open")]
    private void CaptureOpen()
    {
        if (shadow == null) return;
        UnityEditor.Undo.RecordObject(this, "Capture open shadow pose");
        bookOpen = ShadowPose.Capture(shadow);
        if (Application.isPlaying && initialized) bookOpen.scale = baseScale;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }
#endif
}

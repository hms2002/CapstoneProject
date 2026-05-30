using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueTextAnimationProfile", menuName = "Dialogue/Text Animation Profile")]
public sealed class DialogueTextAnimationProfileSO : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/LeeJunMo/Datas/Resources/Dialogue/DefaultDialogueTextAnimationProfile.asset";
    public const string DefaultResourcesPath = "Dialogue/DefaultDialogueTextAnimationProfile";

    private static DialogueTextAnimationProfileSO cachedDefaultProfile;
    private static DialogueTextAnimationProfileSO runtimeFallbackProfile;

    [Header("Inline Text Effects")]
    [SerializeField, Min(0f)] private float textEffectSettleSeconds = 0.18f;
    [SerializeField] private InlineMotionSettings shake = InlineMotionSettings.CreateShake();
    [SerializeField] private InlineMotionSettings tremble = InlineMotionSettings.CreateTremble();
    [SerializeField] private InlineMotionSettings slowShake = InlineMotionSettings.CreateSlowShake();
    [SerializeField] private WaveMotionSettings wave = WaveMotionSettings.CreateWave();
    [SerializeField] private WaveMotionSettings @float = WaveMotionSettings.CreateFloat();
    [SerializeField] private PunchMotionSettings punch = PunchMotionSettings.CreateDefault();
    [SerializeField] private RandomSizeSettings randomSize = RandomSizeSettings.CreateDefault();

    [Header("Line CameraShake Text Motion")]
    [SerializeField] private DialogueCameraShakeSettings cameraShake = DialogueCameraShakeSettings.CreateDefault();

    public float TextEffectSettleSeconds => Mathf.Max(0f, textEffectSettleSeconds);
    public InlineMotionSettings Shake => shake.Validated();
    public InlineMotionSettings Tremble => tremble.Validated();
    public InlineMotionSettings SlowShake => slowShake.Validated();
    public WaveMotionSettings Wave => wave.Validated();
    public WaveMotionSettings Float => @float.Validated();
    public PunchMotionSettings Punch => punch.Validated();
    public RandomSizeSettings RandomSize => randomSize.Validated();
    public DialogueCameraShakeSettings CameraShake => cameraShake.Validated();

    public static DialogueTextAnimationProfileSO Resolve(DialogueTextAnimationProfileSO overrideProfile)
    {
        return overrideProfile != null ? overrideProfile : LoadDefaultOrFallback();
    }

    public static DialogueTextAnimationProfileSO LoadDefaultOrFallback()
    {
        if (cachedDefaultProfile == null)
            cachedDefaultProfile = Resources.Load<DialogueTextAnimationProfileSO>(DefaultResourcesPath);

        if (cachedDefaultProfile != null)
            return cachedDefaultProfile;

        if (runtimeFallbackProfile == null)
        {
            runtimeFallbackProfile = CreateInstance<DialogueTextAnimationProfileSO>();
            runtimeFallbackProfile.name = "RuntimeFallbackDialogueTextAnimationProfile";
            runtimeFallbackProfile.hideFlags = HideFlags.HideAndDontSave;
            runtimeFallbackProfile.ResetToDefaults();
        }

        return runtimeFallbackProfile;
    }

    public void ResetToDefaults()
    {
        textEffectSettleSeconds = 0.18f;
        shake = InlineMotionSettings.CreateShake();
        tremble = InlineMotionSettings.CreateTremble();
        slowShake = InlineMotionSettings.CreateSlowShake();
        wave = WaveMotionSettings.CreateWave();
        @float = WaveMotionSettings.CreateFloat();
        punch = PunchMotionSettings.CreateDefault();
        randomSize = RandomSizeSettings.CreateDefault();
        cameraShake = DialogueCameraShakeSettings.CreateDefault();
    }

    public void ResetShakeToDefault()
    {
        shake = InlineMotionSettings.CreateShake();
    }

    public void ResetTrembleToDefault()
    {
        tremble = InlineMotionSettings.CreateTremble();
    }

    public void ResetSlowShakeToDefault()
    {
        slowShake = InlineMotionSettings.CreateSlowShake();
    }

    public void ResetWaveToDefault()
    {
        wave = WaveMotionSettings.CreateWave();
    }

    public void ResetFloatToDefault()
    {
        @float = WaveMotionSettings.CreateFloat();
    }

    public void ResetPunchToDefault()
    {
        punch = PunchMotionSettings.CreateDefault();
    }

    public void ResetRandomSizeToDefault()
    {
        randomSize = RandomSizeSettings.CreateDefault();
    }

    public void ResetCameraShakeLowToDefault()
    {
        cameraShake = cameraShake.WithDefaultLow();
    }

    public void ResetCameraShakeMiddleToDefault()
    {
        cameraShake = cameraShake.WithDefaultMiddle();
    }

    public void ResetCameraShakeHighToDefault()
    {
        cameraShake = cameraShake.WithDefaultHigh();
    }

    public void ResetCameraShakeToDefault()
    {
        cameraShake = DialogueCameraShakeSettings.CreateDefault();
    }

    public bool TryResolveCameraShakeMotion(
        DialogueCameraShakePreset preset,
        out DialogueCameraShakeMotionSettings motion)
    {
        return CameraShake.TryResolvePreset(preset, out motion);
    }
}

[Serializable]
public struct InlineMotionSettings
{
    [SerializeField, Min(0f)] private float amplitudeX;
    [SerializeField, Min(0f)] private float amplitudeY;
    [SerializeField, Min(0f)] private float speedX;
    [SerializeField, Min(0f)] private float speedY;
    [SerializeField] private float characterPhaseX;
    [SerializeField] private float characterPhaseY;
    [SerializeField] private float phaseOffsetX;
    [SerializeField] private float phaseOffsetY;

    public float AmplitudeX => Mathf.Max(0f, amplitudeX);
    public float AmplitudeY => Mathf.Max(0f, amplitudeY);
    public float SpeedX => Mathf.Max(0f, speedX);
    public float SpeedY => Mathf.Max(0f, speedY);
    public float CharacterPhaseX => characterPhaseX;
    public float CharacterPhaseY => characterPhaseY;
    public float PhaseOffsetX => phaseOffsetX;
    public float PhaseOffsetY => phaseOffsetY;

    public static InlineMotionSettings CreateShake()
    {
        return new InlineMotionSettings
        {
            amplitudeX = 2.2f,
            amplitudeY = 1.6f,
            speedX = 58.1f,
            speedY = 71.7f,
            characterPhaseX = 23.115f,
            characterPhaseY = 28.147f,
            phaseOffsetX = 0f,
            phaseOffsetY = 17.798f,
        };
    }

    public static InlineMotionSettings CreateTremble()
    {
        return new InlineMotionSettings
        {
            amplitudeX = 0.9f,
            amplitudeY = 0.7f,
            speedX = 42.3f,
            speedY = 49.5f,
            characterPhaseX = 17.269f,
            characterPhaseY = 19.933f,
            phaseOffsetX = 0f,
            phaseOffsetY = 8.09f,
        };
    }

    public static InlineMotionSettings CreateSlowShake()
    {
        return new InlineMotionSettings
        {
            amplitudeX = 0.65f,
            amplitudeY = 0.45f,
            speedX = 4.2f,
            speedY = 3.3f,
            characterPhaseX = 1.71f,
            characterPhaseY = 2.13f,
            phaseOffsetX = 0f,
            phaseOffsetY = 0f,
        };
    }

    public InlineMotionSettings Validated()
    {
        amplitudeX = Mathf.Max(0f, amplitudeX);
        amplitudeY = Mathf.Max(0f, amplitudeY);
        speedX = Mathf.Max(0f, speedX);
        speedY = Mathf.Max(0f, speedY);
        return this;
    }

    public Vector2 Evaluate(float elapsedSeconds, int characterIndex)
    {
        return new Vector2(
            Mathf.Sin(elapsedSeconds * SpeedX + characterIndex * CharacterPhaseX + PhaseOffsetX) * AmplitudeX,
            Mathf.Sin(elapsedSeconds * SpeedY + characterIndex * CharacterPhaseY + PhaseOffsetY) * AmplitudeY);
    }
}

[Serializable]
public struct WaveMotionSettings
{
    [SerializeField, Min(0f)] private float amplitudeY;
    [SerializeField, Min(0f)] private float speed;
    [SerializeField] private float characterPhase;

    public float AmplitudeY => Mathf.Max(0f, amplitudeY);
    public float Speed => Mathf.Max(0f, speed);
    public float CharacterPhase => characterPhase;

    public static WaveMotionSettings CreateWave()
    {
        return new WaveMotionSettings
        {
            amplitudeY = 1.7f,
            speed = 7f,
            characterPhase = 0.55f,
        };
    }

    public static WaveMotionSettings CreateFloat()
    {
        return new WaveMotionSettings
        {
            amplitudeY = 1.2f,
            speed = 3.5f,
            characterPhase = 0.45f,
        };
    }

    public WaveMotionSettings Validated()
    {
        amplitudeY = Mathf.Max(0f, amplitudeY);
        speed = Mathf.Max(0f, speed);
        return this;
    }

    public float EvaluateOffsetY(float elapsedSeconds, int characterIndex)
    {
        return Mathf.Sin(elapsedSeconds * Speed + characterIndex * CharacterPhase) * AmplitudeY;
    }
}

[Serializable]
public struct PunchMotionSettings
{
    [SerializeField, Min(0f)] private float scaleAmplitude;
    [SerializeField, Min(0f)] private float verticalAmplitude;
    [SerializeField, Min(0f)] private float speed;
    [SerializeField] private float characterPhase;

    public float ScaleAmplitude => Mathf.Max(0f, scaleAmplitude);
    public float VerticalAmplitude => Mathf.Max(0f, verticalAmplitude);
    public float Speed => Mathf.Max(0f, speed);
    public float CharacterPhase => characterPhase;

    public static PunchMotionSettings CreateDefault()
    {
        return new PunchMotionSettings
        {
            scaleAmplitude = 0.08f,
            verticalAmplitude = 1.1f,
            speed = 18f,
            characterPhase = -0.22f,
        };
    }

    public PunchMotionSettings Validated()
    {
        scaleAmplitude = Mathf.Max(0f, scaleAmplitude);
        verticalAmplitude = Mathf.Max(0f, verticalAmplitude);
        speed = Mathf.Max(0f, speed);
        return this;
    }

    public float EvaluatePulse(float elapsedSeconds, int characterIndex)
    {
        return Mathf.Max(0f, Mathf.Sin(elapsedSeconds * Speed + characterIndex * CharacterPhase));
    }
}

[Serializable]
public struct RandomSizeSettings
{
    [SerializeField, Min(0.01f)] private float defaultMinScale;
    [SerializeField, Min(0.01f)] private float defaultMaxScale;
    [SerializeField, Min(0.01f)] private float clampMinScale;
    [SerializeField, Min(0.01f)] private float clampMaxScale;

    public float DefaultMinScale => Mathf.Clamp(Mathf.Min(defaultMinScale, defaultMaxScale), ClampMinScale, ClampMaxScale);
    public float DefaultMaxScale => Mathf.Clamp(Mathf.Max(defaultMinScale, defaultMaxScale), ClampMinScale, ClampMaxScale);
    public float ClampMinScale => Mathf.Max(0.01f, Mathf.Min(clampMinScale, clampMaxScale));
    public float ClampMaxScale => Mathf.Max(ClampMinScale, Mathf.Max(clampMinScale, clampMaxScale));

    public static RandomSizeSettings CreateDefault()
    {
        return new RandomSizeSettings
        {
            defaultMinScale = 0.95f,
            defaultMaxScale = 1.10f,
            clampMinScale = 0.80f,
            clampMaxScale = 1.20f,
        };
    }

    public RandomSizeSettings Validated()
    {
        float lowerClamp = Mathf.Max(0.01f, Mathf.Min(clampMinScale, clampMaxScale));
        float upperClamp = Mathf.Max(lowerClamp, Mathf.Max(clampMinScale, clampMaxScale));
        clampMinScale = lowerClamp;
        clampMaxScale = upperClamp;
        defaultMinScale = Mathf.Clamp(defaultMinScale, clampMinScale, clampMaxScale);
        defaultMaxScale = Mathf.Clamp(defaultMaxScale, clampMinScale, clampMaxScale);
        return this;
    }

    public void ResolveRange(float requestedMin, float requestedMax, out float minScale, out float maxScale)
    {
        minScale = Mathf.Clamp(Mathf.Min(requestedMin, requestedMax), ClampMinScale, ClampMaxScale);
        maxScale = Mathf.Clamp(Mathf.Max(requestedMin, requestedMax), ClampMinScale, ClampMaxScale);
    }
}

[Serializable]
public struct DialogueCameraShakeSettings
{
    [SerializeField, Min(0f)] private float intensityMultiplier;
    [SerializeField] private DialogueCameraShakeProfileSettings low;
    [SerializeField] private DialogueCameraShakeProfileSettings middle;
    [SerializeField] private DialogueCameraShakeProfileSettings high;

    public float IntensityMultiplier => Mathf.Max(0f, intensityMultiplier);
    public DialogueCameraShakeProfileSettings Low => low.Validated();
    public DialogueCameraShakeProfileSettings Middle => middle.Validated();
    public DialogueCameraShakeProfileSettings High => high.Validated();

    public static DialogueCameraShakeSettings CreateDefault()
    {
        return new DialogueCameraShakeSettings
        {
            intensityMultiplier = 10f,
            low = DialogueCameraShakeProfileSettings.Create(0.12f, new Vector2(8f, 2f), 2.5f, 1.5f, 0.10f, 12, 70f),
            middle = DialogueCameraShakeProfileSettings.Create(0.18f, new Vector2(16f, 4f), 5f, 3f, 0.20f, 16, 75f),
            high = DialogueCameraShakeProfileSettings.Create(0.26f, new Vector2(28f, 7f), 8f, 5f, 0.35f, 22, 80f),
        };
    }

    public DialogueCameraShakeSettings Validated()
    {
        intensityMultiplier = Mathf.Max(0f, intensityMultiplier);
        low = low.Validated();
        middle = middle.Validated();
        high = high.Validated();
        return this;
    }

    public DialogueCameraShakeSettings WithDefaultLow()
    {
        low = CreateDefault().low;
        return this;
    }

    public DialogueCameraShakeSettings WithDefaultMiddle()
    {
        middle = CreateDefault().middle;
        return this;
    }

    public DialogueCameraShakeSettings WithDefaultHigh()
    {
        high = CreateDefault().high;
        return this;
    }

    public bool TryResolvePreset(DialogueCameraShakePreset preset, out DialogueCameraShakeMotionSettings motion)
    {
        motion = default;
        DialogueCameraShakeProfileSettings profile = preset switch
        {
            DialogueCameraShakePreset.Low => Low,
            DialogueCameraShakePreset.Middle => Middle,
            DialogueCameraShakePreset.High => High,
            _ => default,
        };

        if (preset == DialogueCameraShakePreset.None)
            return false;

        motion = profile.ToMotionSettings(IntensityMultiplier);
        return motion.Duration > 0f ||
               motion.PanelStrength != Vector2.zero ||
               motion.CharacterImpactOffset > 0f ||
               motion.TextMaxOffset > 0f ||
               motion.CameraAmplitude > 0f;
    }
}

[Serializable]
public struct DialogueCameraShakeProfileSettings
{
    [SerializeField, Min(0f)] private float duration;
    [SerializeField] private Vector2 panelStrength;
    [SerializeField, Min(0f)] private float textMaxOffset;
    [SerializeField, Min(0f)] private float characterImpactOffset;
    [SerializeField, Min(0f)] private float cameraAmplitude;
    [SerializeField, Min(1)] private int vibrato;
    [SerializeField, Min(0f)] private float randomness;
    [SerializeField, Min(0f)] private float textInertiaScale;
    [SerializeField, Min(0.0001f)] private float textSmoothTime;
    [SerializeField, Min(0f)] private float textSettleDuration;
    [SerializeField, Min(0f)] private float cameraMinIntervalSeconds;

    public float Duration => Mathf.Max(0f, duration);
    public Vector2 PanelStrength => panelStrength;
    public float TextMaxOffset => Mathf.Max(0f, textMaxOffset);
    public float CharacterImpactOffset => Mathf.Max(0f, characterImpactOffset);
    public float CameraAmplitude => Mathf.Max(0f, cameraAmplitude);
    public int Vibrato => Mathf.Max(1, vibrato);
    public float Randomness => Mathf.Max(0f, randomness);
    public float TextInertiaScale => Mathf.Max(0f, textInertiaScale);
    public float TextSmoothTime => Mathf.Max(0.0001f, textSmoothTime);
    public float TextSettleDuration => Mathf.Max(0f, textSettleDuration);
    public float CameraMinIntervalSeconds => Mathf.Max(0f, cameraMinIntervalSeconds);

    public static DialogueCameraShakeProfileSettings Create(
        float duration,
        Vector2 panelStrength,
        float textMaxOffset,
        float characterImpactOffset,
        float cameraAmplitude,
        int vibrato,
        float randomness)
    {
        return new DialogueCameraShakeProfileSettings
        {
            duration = duration,
            panelStrength = panelStrength,
            textMaxOffset = textMaxOffset,
            characterImpactOffset = characterImpactOffset,
            cameraAmplitude = cameraAmplitude,
            vibrato = vibrato,
            randomness = randomness,
            textInertiaScale = 0.45f,
            textSmoothTime = 0.035f,
            textSettleDuration = 0.12f,
            cameraMinIntervalSeconds = 0.03f,
        };
    }

    public DialogueCameraShakeProfileSettings Validated()
    {
        duration = Mathf.Max(0f, duration);
        textMaxOffset = Mathf.Max(0f, textMaxOffset);
        characterImpactOffset = Mathf.Max(0f, characterImpactOffset);
        cameraAmplitude = Mathf.Max(0f, cameraAmplitude);
        vibrato = Mathf.Max(1, vibrato);
        randomness = Mathf.Max(0f, randomness);
        textInertiaScale = Mathf.Max(0f, textInertiaScale);
        textSmoothTime = Mathf.Max(0.0001f, textSmoothTime);
        textSettleDuration = Mathf.Max(0f, textSettleDuration);
        cameraMinIntervalSeconds = Mathf.Max(0f, cameraMinIntervalSeconds);
        return this;
    }

    public DialogueCameraShakeMotionSettings ToMotionSettings(float intensityMultiplier)
    {
        float multiplier = Mathf.Max(0f, intensityMultiplier);
        return new DialogueCameraShakeMotionSettings(
            Duration,
            PanelStrength * multiplier,
            TextMaxOffset * multiplier,
            CharacterImpactOffset * multiplier,
            CameraAmplitude * multiplier,
            Mathf.Max(1, Mathf.RoundToInt(Vibrato * multiplier)),
            Randomness,
            TextInertiaScale * multiplier,
            TextSmoothTime,
            TextSettleDuration,
            CameraMinIntervalSeconds);
    }
}

public readonly struct DialogueCameraShakeMotionSettings
{
    public DialogueCameraShakeMotionSettings(
        float duration,
        Vector2 panelStrength,
        float textMaxOffset,
        float characterImpactOffset,
        float cameraAmplitude,
        int vibrato,
        float randomness,
        float textInertiaScale,
        float textSmoothTime,
        float textSettleDuration,
        float cameraMinIntervalSeconds)
    {
        Duration = Mathf.Max(0f, duration);
        PanelStrength = panelStrength;
        TextMaxOffset = Mathf.Max(0f, textMaxOffset);
        CharacterImpactOffset = Mathf.Max(0f, characterImpactOffset);
        CameraAmplitude = Mathf.Max(0f, cameraAmplitude);
        Vibrato = Mathf.Max(1, vibrato);
        Randomness = Mathf.Max(0f, randomness);
        TextInertiaScale = Mathf.Max(0f, textInertiaScale);
        TextSmoothTime = Mathf.Max(0.0001f, textSmoothTime);
        TextSettleDuration = Mathf.Max(0f, textSettleDuration);
        CameraMinIntervalSeconds = Mathf.Max(0f, cameraMinIntervalSeconds);
    }

    public float Duration { get; }
    public Vector2 PanelStrength { get; }
    public float TextMaxOffset { get; }
    public float CharacterImpactOffset { get; }
    public float CameraAmplitude { get; }
    public int Vibrato { get; }
    public float Randomness { get; }
    public float TextInertiaScale { get; }
    public float TextSmoothTime { get; }
    public float TextSettleDuration { get; }
    public float CameraMinIntervalSeconds { get; }
}

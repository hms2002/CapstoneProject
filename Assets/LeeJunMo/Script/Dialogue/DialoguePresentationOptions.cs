using UnityEngine;

/// <summary>
/// 대화 시작/종료 시 어떤 프레젠테이션 단계를 사용할지 전달하는 값 객체입니다.
/// </summary>
public readonly struct DialoguePresentationOptions
{
    public static DialoguePresentationOptions Default => default;

    public static DialoguePresentationOptions WithoutPortraits => new DialoguePresentationOptions(
        suppressPortraitIntro: true,
        suppressPortraitOutro: true,
        forceDialogueBoxOnly: true,
        skipBossPrelude: true,
        suppressOpeningIntroSound: true);

    public DialoguePresentationOptions(
        bool suppressPortraitIntro = false,
        bool suppressPortraitOutro = false,
        bool useFastSilhouetteIntro = false,
        float fastSilhouetteFadeSeconds = 0.35f,
        string fastSilhouettePosition = "center",
        bool fastSilhouetteColorize = false,
        bool forceDialogueBoxOnly = false,
        bool skipBossPrelude = false,
        bool suppressOpeningIntroSound = false)
    {
        SuppressPortraitIntro = suppressPortraitIntro;
        SuppressPortraitOutro = suppressPortraitOutro;
        UseFastSilhouetteIntro = useFastSilhouetteIntro;
        FastSilhouetteFadeSeconds = fastSilhouetteFadeSeconds;
        FastSilhouettePosition = fastSilhouettePosition;
        FastSilhouetteColorize = fastSilhouetteColorize;
        ForceDialogueBoxOnly = forceDialogueBoxOnly;
        SkipBossPrelude = skipBossPrelude;
        SuppressOpeningIntroSound = suppressOpeningIntroSound;
    }

    public bool SuppressPortraitIntro { get; }
    public bool SuppressPortraitOutro { get; }
    public bool UseFastSilhouetteIntro { get; }
    public float FastSilhouetteFadeSeconds { get; }
    public string FastSilhouettePosition { get; }
    public bool FastSilhouetteColorize { get; }
    public bool ForceDialogueBoxOnly { get; }
    public bool SkipBossPrelude { get; }
    public bool SuppressOpeningIntroSound { get; }

    public float ResolvedFastSilhouetteFadeSeconds => Mathf.Max(0f, FastSilhouetteFadeSeconds);

    public string ResolvedFastSilhouettePosition =>
        string.IsNullOrWhiteSpace(FastSilhouettePosition) ? "center" : FastSilhouettePosition.Trim();

    public static DialoguePresentationOptions FastSilhouette(
        float fadeSeconds,
        string position = "center",
        bool colorize = false,
        bool forceDialogueBoxOnly = false)
    {
        return new DialoguePresentationOptions(
            useFastSilhouetteIntro: true,
            fastSilhouetteFadeSeconds: fadeSeconds,
            fastSilhouettePosition: position,
            fastSilhouetteColorize: colorize,
            forceDialogueBoxOnly: forceDialogueBoxOnly,
            skipBossPrelude: true);
    }
}

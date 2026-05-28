using UnityEngine;

public readonly struct DialoguePresentationOptions
{
    public static DialoguePresentationOptions Default => default;

    public static DialoguePresentationOptions WithoutPortraits => new DialoguePresentationOptions(
        suppressPortraitIntro: true,
        suppressPortraitOutro: true,
        forceDialogueBoxOnly: true,
        skipBossPrelude: true);

    public DialoguePresentationOptions(
        bool suppressPortraitIntro = false,
        bool suppressPortraitOutro = false,
        bool useFastSilhouetteIntro = false,
        float fastSilhouetteFadeSeconds = 0.35f,
        string fastSilhouettePosition = "center",
        bool fastSilhouetteColorize = false,
        bool forceDialogueBoxOnly = false,
        bool skipBossPrelude = false)
    {
        SuppressPortraitIntro = suppressPortraitIntro;
        SuppressPortraitOutro = suppressPortraitOutro;
        UseFastSilhouetteIntro = useFastSilhouetteIntro;
        FastSilhouetteFadeSeconds = fastSilhouetteFadeSeconds;
        FastSilhouettePosition = fastSilhouettePosition;
        FastSilhouetteColorize = fastSilhouetteColorize;
        ForceDialogueBoxOnly = forceDialogueBoxOnly;
        SkipBossPrelude = skipBossPrelude;
    }

    public bool SuppressPortraitIntro { get; }
    public bool SuppressPortraitOutro { get; }
    public bool UseFastSilhouetteIntro { get; }
    public float FastSilhouetteFadeSeconds { get; }
    public string FastSilhouettePosition { get; }
    public bool FastSilhouetteColorize { get; }
    public bool ForceDialogueBoxOnly { get; }
    public bool SkipBossPrelude { get; }

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

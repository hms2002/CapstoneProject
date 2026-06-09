using TMPro;

public static class DialogueTextAnimationUtility
{
    public static DialogueTextRevealPlan BuildPlan(
        string rawText,
        DialogueTextAnimationProfileSO textAnimationProfile = null)
    {
        return DialogueTextRevealUtility.BuildPlan(rawText, textAnimationProfile);
    }

    public static bool TryParseAnimType(string value, out DialogueAnimType animType)
    {
        return DialogueTextRevealUtility.TryParseAnimType(value, out animType);
    }

    public static DialogueTextRevealProfile ResolveProfile(DialogueAnimType animType)
    {
        return DialogueTextRevealUtility.ResolveProfile(animType);
    }

    public static float GetPauseBeforeCharacter(DialogueTextRevealPlan plan, int visibleCharacterIndex)
    {
        return DialogueTextRevealUtility.GetPauseBeforeCharacter(plan, visibleCharacterIndex);
    }

    public static float GetPostCharacterDelay(
        DialogueTextRevealProfile profile,
        TMP_TextInfo textInfo,
        int visibleCharacterIndex)
    {
        return DialogueTextRevealUtility.GetPostCharacterDelay(profile, textInfo, visibleCharacterIndex);
    }

    public static bool HasTextEffects(DialogueTextRevealPlan plan)
    {
        return DialogueTextRevealUtility.HasTextEffects(plan);
    }

    public static float GetTextEffectSettleSeconds(
        DialogueTextRevealPlan plan,
        DialogueTextAnimationProfileSO textAnimationProfile = null)
    {
        return DialogueTextRevealUtility.GetTextEffectSettleSeconds(plan, textAnimationProfile);
    }

    public static void ApplyTextEffects(
        TMP_Text text,
        DialogueTextRevealPlan plan,
        int visibleCharacterCount,
        float elapsedSeconds,
        DialogueTextAnimationProfileSO textAnimationProfile = null)
    {
        DialogueTextRevealUtility.ApplyTextEffects(
            text,
            plan,
            visibleCharacterCount,
            elapsedSeconds,
            default(DialogueTextImpactState),
            textAnimationProfile);
    }

    public static void ApplyTextEffects(
        TMP_Text text,
        DialogueTextRevealPlan plan,
        int visibleCharacterCount,
        float elapsedSeconds,
        DialogueTextImpactState impactState,
        DialogueTextAnimationProfileSO textAnimationProfile = null)
    {
        DialogueTextRevealUtility.ApplyTextEffects(
            text,
            plan,
            visibleCharacterCount,
            elapsedSeconds,
            impactState,
            textAnimationProfile);
    }

    public static void ResetTextEffects(TMP_Text text)
    {
        DialogueTextRevealUtility.ResetTextEffects(text);
    }
}

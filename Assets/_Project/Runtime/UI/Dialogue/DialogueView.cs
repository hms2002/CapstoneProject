using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CapstoneAudio;

public enum DialogueCameraShakePreset
{
    None,
    Low,
    Middle,
    High
}

// 책임: 대사 텍스트 특정 문자 위치에서 적용할 inline pause 시간을 보관한다.
public readonly struct DialogueInlinePause
{
    public DialogueInlinePause(int characterIndex, float seconds)
    {
        CharacterIndex = Mathf.Max(0, characterIndex);
        Seconds = Mathf.Max(0f, seconds);
    }

    public int CharacterIndex { get; }
    public float Seconds { get; }
}

public enum DialogueTextEffectType
{
    Shake,
    Tremble,
    Punch,
    Wave,
    Float,
    RandomSize,
    SlowShake
}

// 책임: 대사 텍스트 효과가 적용될 문자 범위와 효과 파라미터를 보관한다.
public readonly struct DialogueTextEffectRange
{
    public DialogueTextEffectRange(
        int startCharacterIndex,
        int endCharacterIndex,
        DialogueTextEffectType effectType,
        float randomSizeMinScale = 1f,
        float randomSizeMaxScale = 1f,
        float randomSizeClampMinScale = 0.8f,
        float randomSizeClampMaxScale = 1.2f)
    {
        float clampMin = Mathf.Max(0.01f, Mathf.Min(randomSizeClampMinScale, randomSizeClampMaxScale));
        float clampMax = Mathf.Max(clampMin, Mathf.Max(randomSizeClampMinScale, randomSizeClampMaxScale));
        StartCharacterIndex = Mathf.Max(0, startCharacterIndex);
        EndCharacterIndex = Mathf.Max(StartCharacterIndex, endCharacterIndex);
        EffectType = effectType;
        RandomSizeMinScale = Mathf.Clamp(Mathf.Min(randomSizeMinScale, randomSizeMaxScale), clampMin, clampMax);
        RandomSizeMaxScale = Mathf.Clamp(Mathf.Max(randomSizeMinScale, randomSizeMaxScale), clampMin, clampMax);
    }

    public int StartCharacterIndex { get; }
    public int EndCharacterIndex { get; }
    public DialogueTextEffectType EffectType { get; }
    public float RandomSizeMinScale { get; }
    public float RandomSizeMaxScale { get; }

    public bool Contains(int characterIndex)
    {
        return characterIndex >= StartCharacterIndex && characterIndex < EndCharacterIndex;
    }
}

// 책임: 태그를 제거한 표시 문자열과 inline pause/effect 범위를 묶어 전달한다.
public readonly struct DialogueTextRevealPlan
{
    public DialogueTextRevealPlan(
        string displayText,
        List<DialogueInlinePause> pauses,
        List<DialogueTextEffectRange> effects)
    {
        DisplayText = displayText ?? string.Empty;
        Pauses = pauses ?? new List<DialogueInlinePause>();
        Effects = effects ?? new List<DialogueTextEffectRange>();
    }

    public string DisplayText { get; }
    public List<DialogueInlinePause> Pauses { get; }
    public List<DialogueTextEffectRange> Effects { get; }
}

// 책임: 대사 타입별 문자 출력 지연과 문장부호 pause 배율을 보관한다.
public readonly struct DialogueTextRevealProfile
{
    public DialogueTextRevealProfile(float characterDelay, float punctuationPauseScale)
    {
        CharacterDelay = Mathf.Max(0f, characterDelay);
        PunctuationPauseScale = Mathf.Max(0f, punctuationPauseScale);
    }

    public float CharacterDelay { get; }
    public float PunctuationPauseScale { get; }
}

// 책임: 대사 충격 연출의 시작 시간, 지속 시간, 문자 흔들림 파라미터를 보관한다.
public readonly struct DialogueTextImpactState
{
    public DialogueTextImpactState(
        float startTime,
        float duration,
        float settleDuration,
        float characterImpactOffset,
        int vibrato,
        float randomness)
    {
        StartTime = startTime;
        Duration = Mathf.Max(0f, duration);
        SettleDuration = Mathf.Max(0f, settleDuration);
        CharacterImpactOffset = Mathf.Max(0f, characterImpactOffset);
        Vibrato = Mathf.Max(1, vibrato);
        Randomness = Mathf.Max(0f, randomness);
    }

    public float StartTime { get; }
    public float Duration { get; }
    public float SettleDuration { get; }
    public float CharacterImpactOffset { get; }
    public int Vibrato { get; }
    public float Randomness { get; }

    public float TotalDuration => Duration + SettleDuration;

    public bool IsActiveAt(float elapsedSeconds)
    {
        return CharacterImpactOffset > 0f &&
               TotalDuration > 0f &&
               elapsedSeconds >= StartTime &&
               elapsedSeconds < StartTime + TotalDuration;
    }

    public float ResolveEnvelope(float elapsedSeconds)
    {
        if (!IsActiveAt(elapsedSeconds))
            return 0f;

        float normalized = Mathf.Clamp01((elapsedSeconds - StartTime) / TotalDuration);
        return 1f - Mathf.SmoothStep(0f, 1f, normalized);
    }
}

// 책임: 대사 원문 태그를 표시 문자열, pause, 텍스트 효과 계획으로 파싱하고 적용한다.
public static class DialogueTextRevealUtility
{
    private const string PauseTagPrefix = "[pause=";

    // 책임: 파싱 중 아직 닫히지 않은 텍스트 효과 태그의 시작 위치와 랜덤 크기 값을 보관한다.
    private readonly struct ActiveEffectTag
    {
        public ActiveEffectTag(
            DialogueTextEffectType effectType,
            int startCharacterIndex,
            float randomSizeMinScale,
            float randomSizeMaxScale)
        {
            EffectType = effectType;
            StartCharacterIndex = startCharacterIndex;
            RandomSizeMinScale = randomSizeMinScale;
            RandomSizeMaxScale = randomSizeMaxScale;
        }

        public DialogueTextEffectType EffectType { get; }
        public int StartCharacterIndex { get; }
        public float RandomSizeMinScale { get; }
        public float RandomSizeMaxScale { get; }
    }

    public static DialogueTextRevealPlan BuildPlan(string rawText)
    {
        return BuildPlan(rawText, null);
    }

    public static DialogueTextRevealPlan BuildPlan(
        string rawText,
        DialogueTextAnimationProfileSO textAnimationProfile)
    {
        if (string.IsNullOrEmpty(rawText))
        {
            return new DialogueTextRevealPlan(
                string.Empty,
                new List<DialogueInlinePause>(),
                new List<DialogueTextEffectRange>());
        }

        DialogueTextAnimationProfileSO profile =
            DialogueTextAnimationProfileSO.Resolve(textAnimationProfile);
        System.Text.StringBuilder displayBuilder = new System.Text.StringBuilder(rawText.Length);
        List<DialogueInlinePause> pauses = new List<DialogueInlinePause>();
        List<DialogueTextEffectRange> effects = new List<DialogueTextEffectRange>();
        List<ActiveEffectTag> activeEffects = new List<ActiveEffectTag>();
        int visibleCharacterIndex = 0;

        for (int i = 0; i < rawText.Length;)
        {
            if (TryReadPauseTag(rawText, i, out int consumed, out float pauseSeconds))
            {
                pauses.Add(new DialogueInlinePause(visibleCharacterIndex, pauseSeconds));
                i += consumed;
                continue;
            }

            if (TryReadEffectTag(
                    rawText,
                    i,
                    visibleCharacterIndex,
                    activeEffects,
                    effects,
                    profile,
                    out consumed))
            {
                i += consumed;
                continue;
            }

            char c = rawText[i];
            if (c == '<' && TryCopyRichTextTag(rawText, i, displayBuilder, out consumed))
            {
                i += consumed;
                continue;
            }

            displayBuilder.Append(c);
            visibleCharacterIndex++;
            i++;
        }

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveEffectTag activeEffect = activeEffects[i];
            if (visibleCharacterIndex > activeEffect.StartCharacterIndex)
            {
                effects.Add(new DialogueTextEffectRange(
                    activeEffect.StartCharacterIndex,
                    visibleCharacterIndex,
                    activeEffect.EffectType,
                    activeEffect.RandomSizeMinScale,
                    activeEffect.RandomSizeMaxScale,
                    profile.RandomSize.ClampMinScale,
                    profile.RandomSize.ClampMaxScale));
            }
        }

        return new DialogueTextRevealPlan(displayBuilder.ToString(), pauses, effects);
    }

    public static bool TryParseAnimType(string value, out DialogueAnimType animType)
    {
        animType = DialogueAnimType.Normal;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "normal":
                animType = DialogueAnimType.Normal;
                return true;
            case "slow":
                animType = DialogueAnimType.Slow;
                return true;
            case "angry":
                animType = DialogueAnimType.Angry;
                return true;
            case "whisper":
                animType = DialogueAnimType.Whisper;
                return true;
            case "cold":
                animType = DialogueAnimType.Cold;
                return true;
            default:
                return false;
        }
    }

    public static DialogueTextRevealProfile ResolveProfile(DialogueAnimType animType)
    {
        return animType switch
        {
            DialogueAnimType.Slow => new DialogueTextRevealProfile(0.045f, 1.3f),
            DialogueAnimType.Angry => new DialogueTextRevealProfile(0.016f, 0.8f),
            DialogueAnimType.Whisper => new DialogueTextRevealProfile(0.04f, 1.2f),
            DialogueAnimType.Cold => new DialogueTextRevealProfile(0.025f, 0.65f),
            _ => new DialogueTextRevealProfile(0.03f, 1f),
        };
    }

    public static float GetPauseBeforeCharacter(DialogueTextRevealPlan plan, int visibleCharacterIndex)
    {
        if (plan.Pauses == null || plan.Pauses.Count == 0)
            return 0f;

        float total = 0f;
        for (int i = 0; i < plan.Pauses.Count; i++)
        {
            DialogueInlinePause pause = plan.Pauses[i];
            if (pause.CharacterIndex == visibleCharacterIndex)
                total += pause.Seconds;
        }

        return total;
    }

    public static float GetPostCharacterDelay(
        DialogueTextRevealProfile profile,
        TMP_TextInfo textInfo,
        int visibleCharacterIndex)
    {
        if (textInfo == null || visibleCharacterIndex < 0 || visibleCharacterIndex >= textInfo.characterCount)
            return profile.CharacterDelay;

        TMP_CharacterInfo characterInfo = textInfo.characterInfo[visibleCharacterIndex];
        char c = characterInfo.character;
        if (char.IsWhiteSpace(c))
            return profile.CharacterDelay;

        float punctuationPause = ResolvePunctuationPause(textInfo, visibleCharacterIndex, c);
        return profile.CharacterDelay + punctuationPause * profile.PunctuationPauseScale;
    }

    public static bool HasTextEffects(DialogueTextRevealPlan plan)
    {
        return plan.Effects != null && plan.Effects.Count > 0;
    }

    public static float GetTextEffectSettleSeconds(DialogueTextRevealPlan plan)
    {
        return GetTextEffectSettleSeconds(plan, null);
    }

    public static float GetTextEffectSettleSeconds(
        DialogueTextRevealPlan plan,
        DialogueTextAnimationProfileSO textAnimationProfile)
    {
        DialogueTextAnimationProfileSO profile =
            DialogueTextAnimationProfileSO.Resolve(textAnimationProfile);
        return HasTextEffects(plan) ? profile.TextEffectSettleSeconds : 0f;
    }

    public static void ApplyTextEffects(
        TMP_Text text,
        DialogueTextRevealPlan plan,
        int visibleCharacterCount,
        float elapsedSeconds)
    {
        ApplyTextEffects(
            text,
            plan,
            visibleCharacterCount,
            elapsedSeconds,
            default(DialogueTextImpactState),
            null);
    }

    public static void ApplyTextEffects(
        TMP_Text text,
        DialogueTextRevealPlan plan,
        int visibleCharacterCount,
        float elapsedSeconds,
        DialogueTextImpactState impactState)
    {
        ApplyTextEffects(
            text,
            plan,
            visibleCharacterCount,
            elapsedSeconds,
            impactState,
            null);
    }

    public static void ApplyTextEffects(
        TMP_Text text,
        DialogueTextRevealPlan plan,
        int visibleCharacterCount,
        float elapsedSeconds,
        DialogueTextImpactState impactState,
        DialogueTextAnimationProfileSO textAnimationProfile)
    {
        bool hasInlineEffects = HasTextEffects(plan);
        bool hasImpact = impactState.IsActiveAt(elapsedSeconds);
        if (text == null || (!hasInlineEffects && !hasImpact))
            return;

        DialogueTextAnimationProfileSO profile =
            DialogueTextAnimationProfileSO.Resolve(textAnimationProfile);
        text.ForceMeshUpdate();

        TMP_TextInfo textInfo = text.textInfo;
        if (textInfo == null || textInfo.characterCount <= 0)
            return;

        int clampedVisibleCount = Mathf.Clamp(visibleCharacterCount, 0, textInfo.characterCount);
        for (int i = 0; i < clampedVisibleCount; i++)
        {
            TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];
            if (!characterInfo.isVisible)
                continue;

            Vector3 offset = Vector3.zero;
            float scale = 1f;

            if (hasInlineEffects)
            {
                for (int effectIndex = 0; effectIndex < plan.Effects.Count; effectIndex++)
                {
                    DialogueTextEffectRange range = plan.Effects[effectIndex];
                    if (!range.Contains(i))
                        continue;

                    AccumulateTextEffect(range, i, elapsedSeconds, profile, ref offset, ref scale);
                }
            }

            if (hasImpact)
                AccumulateImpactEffect(impactState, i, elapsedSeconds, ref offset);

            if (offset == Vector3.zero && Mathf.Approximately(scale, 1f))
                continue;

            ApplyCharacterTransform(textInfo, characterInfo, offset, scale);
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    public static void ResetTextEffects(TMP_Text text)
    {
        if (text == null)
            return;

        text.ForceMeshUpdate();
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    private static float ResolvePunctuationPause(TMP_TextInfo textInfo, int visibleCharacterIndex, char c)
    {
        if (c == '\u2026')
            return 0.45f;

        if (c == '.')
        {
            bool previousDot = visibleCharacterIndex > 0 &&
                               textInfo.characterInfo[visibleCharacterIndex - 1].character == '.';
            bool nextDot = visibleCharacterIndex + 1 < textInfo.characterCount &&
                           textInfo.characterInfo[visibleCharacterIndex + 1].character == '.';

            if (previousDot && !nextDot)
                return 0.45f;

            if (nextDot || previousDot)
                return 0f;

            return 0.2f;
        }

        return c switch
        {
            ',' or ':' or ';' or '\uFF0C' or '\u3001' => 0.12f,
            '?' or '\uFF1F' => 0.2f,
            '!' or '\uFF01' => 0.15f,
            '\u3002' => 0.2f,
            _ => 0f,
        };
    }

    private static void AccumulateTextEffect(
        DialogueTextEffectRange range,
        int characterIndex,
        float elapsedSeconds,
        DialogueTextAnimationProfileSO textAnimationProfile,
        ref Vector3 offset,
        ref float scale)
    {
        switch (range.EffectType)
        {
            case DialogueTextEffectType.Shake:
            {
                Vector2 motion = textAnimationProfile.Shake.Evaluate(elapsedSeconds, characterIndex);
                offset.x += motion.x;
                offset.y += motion.y;
                break;
            }

            case DialogueTextEffectType.Tremble:
            {
                Vector2 motion = textAnimationProfile.Tremble.Evaluate(elapsedSeconds, characterIndex);
                offset.x += motion.x;
                offset.y += motion.y;
                break;
            }

            case DialogueTextEffectType.Punch:
            {
                PunchMotionSettings settings = textAnimationProfile.Punch;
                float pulse = settings.EvaluatePulse(elapsedSeconds, characterIndex);
                scale *= 1f + pulse * settings.ScaleAmplitude;
                offset.y += pulse * settings.VerticalAmplitude;
                break;
            }

            case DialogueTextEffectType.Wave:
                offset.y += textAnimationProfile.Wave.EvaluateOffsetY(elapsedSeconds, characterIndex);
                break;

            case DialogueTextEffectType.Float:
                offset.y += textAnimationProfile.Float.EvaluateOffsetY(elapsedSeconds, characterIndex);
                break;

            case DialogueTextEffectType.RandomSize:
                scale *= ResolveRandomSizeScale(range, characterIndex);
                break;

            case DialogueTextEffectType.SlowShake:
            {
                Vector2 motion = textAnimationProfile.SlowShake.Evaluate(elapsedSeconds, characterIndex);
                offset.x += motion.x;
                offset.y += motion.y;
                break;
            }
        }
    }

    private static void AccumulateImpactEffect(
        DialogueTextImpactState impactState,
        int characterIndex,
        float elapsedSeconds,
        ref Vector3 offset)
    {
        float envelope = impactState.ResolveEnvelope(elapsedSeconds);
        if (envelope <= 0f)
            return;

        float localTime = Mathf.Max(0f, elapsedSeconds - impactState.StartTime);
        float activeDuration = Mathf.Max(impactState.Duration, 0.0001f);
        float samplePosition = Mathf.Clamp01(localTime / activeDuration) * impactState.Vibrato;
        int sampleIndex = Mathf.FloorToInt(samplePosition);
        float sampleBlend = Mathf.SmoothStep(0f, 1f, samplePosition - sampleIndex);
        Vector2 currentSample = ResolveShakeSample(characterIndex, sampleIndex, impactState.Randomness);
        Vector2 nextSample = ResolveShakeSample(characterIndex, sampleIndex + 1, impactState.Randomness);
        Vector2 shakeOffset = Vector2.Lerp(currentSample, nextSample, sampleBlend) *
                              impactState.CharacterImpactOffset *
                              envelope;

        offset.x += shakeOffset.x;
        offset.y += shakeOffset.y;
    }

    private static float Hash01(int seed)
    {
        float value = Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    private static float HashSigned(int seed)
    {
        return Hash01(seed) * 2f - 1f;
    }

    private static Vector2 ResolveShakeSample(int characterIndex, int sampleIndex, float randomness)
    {
        int randomnessSeed = Mathf.RoundToInt(randomness * 10f);
        int baseSeed = characterIndex * 73856093 ^ sampleIndex * 19349663 ^ randomnessSeed * 83492791;
        Vector2 direction = new Vector2(
            HashSigned(baseSeed + 17),
            HashSigned(baseSeed + 53));

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.right;
        else
            direction.Normalize();

        float magnitude = Mathf.Lerp(0.55f, 1f, Hash01(baseSeed + 89));
        return direction * magnitude;
    }

    private static float ResolveRandomSizeScale(DialogueTextEffectRange range, int characterIndex)
    {
        int seed = unchecked(
            characterIndex * 73856093 ^
            range.StartCharacterIndex * 19349663 ^
            range.EndCharacterIndex * 83492791);
        float t = Hash01(seed);
        return Mathf.Lerp(range.RandomSizeMinScale, range.RandomSizeMaxScale, t);
    }

    private static void ApplyCharacterTransform(
        TMP_TextInfo textInfo,
        TMP_CharacterInfo characterInfo,
        Vector3 offset,
        float scale)
    {
        int materialIndex = characterInfo.materialReferenceIndex;
        int vertexIndex = characterInfo.vertexIndex;

        if (materialIndex < 0 || materialIndex >= textInfo.meshInfo.Length)
            return;

        Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
        if (vertices == null || vertexIndex < 0 || vertexIndex + 3 >= vertices.Length)
            return;

        Vector3 center = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
        for (int i = 0; i < 4; i++)
        {
            int currentVertexIndex = vertexIndex + i;
            vertices[currentVertexIndex] =
                center +
                (vertices[currentVertexIndex] - center) * scale +
                offset;
        }
    }

    private static bool TryReadPauseTag(string text, int startIndex, out int consumed, out float seconds)
    {
        consumed = 0;
        seconds = 0f;

        if (startIndex < 0 ||
            startIndex + PauseTagPrefix.Length >= text.Length ||
            !string.Equals(
                text.Substring(startIndex, PauseTagPrefix.Length),
                PauseTagPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int closeIndex = text.IndexOf(']', startIndex + PauseTagPrefix.Length);
        if (closeIndex < 0)
            return false;

        string value = text.Substring(
            startIndex + PauseTagPrefix.Length,
            closeIndex - startIndex - PauseTagPrefix.Length);

        if (!float.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out seconds))
        {
            return false;
        }

        consumed = closeIndex - startIndex + 1;
        seconds = Mathf.Max(0f, seconds);
        return true;
    }

    private static bool TryReadEffectTag(
        string text,
        int startIndex,
        int visibleCharacterIndex,
        List<ActiveEffectTag> activeEffects,
        List<DialogueTextEffectRange> effects,
        DialogueTextAnimationProfileSO textAnimationProfile,
        out int consumed)
    {
        consumed = 0;

        if (startIndex < 0 || startIndex >= text.Length || text[startIndex] != '[')
            return false;

        int closeIndex = text.IndexOf(']', startIndex + 1);
        if (closeIndex < 0)
            return false;

        string tag = text.Substring(startIndex + 1, closeIndex - startIndex - 1).Trim();
        bool isClosingTag = tag.StartsWith("/", StringComparison.Ordinal);
        string effectExpression = isClosingTag ? tag.Substring(1).Trim() : tag;
        string effectName = ExtractEffectName(effectExpression);

        if (!TryParseEffectType(effectName, out DialogueTextEffectType effectType))
            return false;

        consumed = closeIndex - startIndex + 1;
        if (!isClosingTag)
        {
            ResolveRandomSizeRange(
                effectExpression,
                effectType,
                textAnimationProfile,
                out float minScale,
                out float maxScale);
            activeEffects.Add(new ActiveEffectTag(effectType, visibleCharacterIndex, minScale, maxScale));
            return true;
        }

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveEffectTag activeEffect = activeEffects[i];
            if (activeEffect.EffectType != effectType)
                continue;

            activeEffects.RemoveAt(i);
            if (visibleCharacterIndex > activeEffect.StartCharacterIndex)
            {
                effects.Add(new DialogueTextEffectRange(
                    activeEffect.StartCharacterIndex,
                    visibleCharacterIndex,
                    effectType,
                    activeEffect.RandomSizeMinScale,
                    activeEffect.RandomSizeMaxScale,
                    textAnimationProfile.RandomSize.ClampMinScale,
                    textAnimationProfile.RandomSize.ClampMaxScale));
            }

            return true;
        }

        return true;
    }

    private static bool TryParseEffectType(string value, out DialogueTextEffectType effectType)
    {
        effectType = DialogueTextEffectType.Tremble;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "shake":
                effectType = DialogueTextEffectType.Shake;
                return true;
            case "tremble":
            case "jitter":
                effectType = DialogueTextEffectType.Tremble;
                return true;
            case "punch":
            case "pop":
            case "emphasis":
                effectType = DialogueTextEffectType.Punch;
                return true;
            case "wave":
            case "wobble":
                effectType = DialogueTextEffectType.Wave;
                return true;
            case "float":
            case "drift":
                effectType = DialogueTextEffectType.Float;
                return true;
            case "rand_size":
            case "random_size":
            case "randomsize":
            case "size_jitter":
            case "sizejitter":
            case "drunk_size":
            case "drunksize":
                effectType = DialogueTextEffectType.RandomSize;
                return true;
            case "slowshake":
            case "slow_shake":
            case "drunkshake":
            case "drunk_shake":
                effectType = DialogueTextEffectType.SlowShake;
                return true;
            default:
                return false;
        }
    }

    private static string ExtractEffectName(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return string.Empty;

        int equalsIndex = expression.IndexOf('=');
        return equalsIndex >= 0
            ? expression.Substring(0, equalsIndex).Trim()
            : expression.Trim();
    }

    private static void ResolveRandomSizeRange(
        string expression,
        DialogueTextEffectType effectType,
        DialogueTextAnimationProfileSO textAnimationProfile,
        out float minScale,
        out float maxScale)
    {
        RandomSizeSettings settings = textAnimationProfile.RandomSize;
        minScale = settings.DefaultMinScale;
        maxScale = settings.DefaultMaxScale;

        if (effectType != DialogueTextEffectType.RandomSize ||
            string.IsNullOrWhiteSpace(expression))
        {
            return;
        }

        int equalsIndex = expression.IndexOf('=');
        if (equalsIndex < 0 || equalsIndex + 1 >= expression.Length)
            return;

        string value = expression.Substring(equalsIndex + 1);
        string[] parts = value.Split(',', ';', '|', '~');
        if (parts.Length < 2)
            return;

        if (!TryParseScale(parts[0], out float parsedMin) ||
            !TryParseScale(parts[1], out float parsedMax))
        {
            return;
        }

        settings.ResolveRange(parsedMin, parsedMax, out minScale, out maxScale);
    }

    private static bool TryParseScale(string value, out float scale)
    {
        scale = 1f;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string trimmed = value.Trim();
        if (trimmed.EndsWith("%", StringComparison.Ordinal))
            trimmed = trimmed.Substring(0, trimmed.Length - 1);

        if (!float.TryParse(
                trimmed,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float parsed))
        {
            return false;
        }

        scale = parsed > 2f ? parsed / 100f : parsed;
        return scale > 0f;
    }

    private static bool TryCopyRichTextTag(
        string text,
        int startIndex,
        System.Text.StringBuilder displayBuilder,
        out int consumed)
    {
        consumed = 0;
        int closeIndex = text.IndexOf('>', startIndex + 1);
        if (closeIndex < 0)
            return false;

        consumed = closeIndex - startIndex + 1;
        displayBuilder.Append(text, startIndex, consumed);
        return true;
    }
}

// 책임: 대화 UI의 텍스트, 선택지, 캐릭터 이미지, 카메라 흔들림/타이핑 연출을 표시한다.
public class DialogueView : MonoBehaviour
{
    private static readonly SoundRef TalkUiIntroSound = SoundRef.FromKey("sound_ui_TalkUIIntro");

    private readonly struct DialogueCameraShakeMotionProfile
    {
        public DialogueCameraShakeMotionProfile(
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

    // 책임: 대사 카메라 흔들림 preset별 패널/텍스트/카메라 흔들림 설정을 직렬화한다.
    [Serializable]
    private sealed class DialogueCameraShakeProfileSettings
    {
        [SerializeField, Min(0f)] private float duration = 0.12f;
        [SerializeField] private Vector2 panelStrength = new Vector2(8f, 2f);
        [SerializeField, Min(0f)] private float textMaxOffset = 2.5f;
        [SerializeField, Min(0f)] private float characterImpactOffset = 1.5f;
        [SerializeField, Min(0f)] private float cameraAmplitude = 0.10f;
        [SerializeField, Min(1)] private int vibrato = 12;
        [SerializeField, Min(0f)] private float randomness = 70f;
        [SerializeField, Min(0f)] private float textInertiaScale = 0.45f;
        [SerializeField, Min(0.0001f)] private float textSmoothTime = 0.035f;
        [SerializeField, Min(0f)] private float textSettleDuration = 0.12f;
        [SerializeField, Min(0f)] private float cameraMinIntervalSeconds = 0.03f;

        public DialogueCameraShakeProfileSettings()
        {
        }

        private DialogueCameraShakeProfileSettings(
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
            this.duration = duration;
            this.panelStrength = panelStrength;
            this.textMaxOffset = textMaxOffset;
            this.characterImpactOffset = characterImpactOffset;
            this.cameraAmplitude = cameraAmplitude;
            this.vibrato = vibrato;
            this.randomness = randomness;
            this.textInertiaScale = textInertiaScale;
            this.textSmoothTime = textSmoothTime;
            this.textSettleDuration = textSettleDuration;
            this.cameraMinIntervalSeconds = cameraMinIntervalSeconds;
        }

        public static DialogueCameraShakeProfileSettings Create(
            float duration,
            Vector2 panelStrength,
            float textMaxOffset,
            float characterImpactOffset,
            float cameraAmplitude,
            int vibrato,
            float randomness)
        {
            return new DialogueCameraShakeProfileSettings(
                duration,
                panelStrength,
                textMaxOffset,
                characterImpactOffset,
                cameraAmplitude,
                vibrato,
                randomness,
                0.45f,
                0.035f,
                0.12f,
                0.03f);
        }

        public DialogueCameraShakeMotionProfile ToMotionProfile(float intensityMultiplier)
        {
            float multiplier = Mathf.Max(0f, intensityMultiplier);
            return new DialogueCameraShakeMotionProfile(
                duration,
                panelStrength * multiplier,
                textMaxOffset * multiplier,
                characterImpactOffset * multiplier,
                cameraAmplitude * multiplier,
                Mathf.Max(1, Mathf.RoundToInt(vibrato * multiplier)),
                randomness,
                textInertiaScale * multiplier,
                textSmoothTime,
                textSettleDuration,
                cameraMinIntervalSeconds);
        }
    }

    [Header("UI Groups (CanvasGroup required)")]
    [SerializeField] private CanvasGroup textBoxGroup;
    [SerializeField] private CanvasGroup dialogueUpperFrameGroup;
    [SerializeField] private CanvasGroup affectionGroup;
    [SerializeField] private AffectionUI affectionUI;

    [Header("UI Presentation")]
    [SerializeField] private UISlideFadePresentation textBoxPresentation;
    [SerializeField] private UISlideFadePresentation dialogueUpperFramePresentation;
    [SerializeField] private UISlideFadePresentation affectionPresentation;

    [Header("Opening Header Presentation")]
    [SerializeField, Min(0f)] private float openingHeaderBeatInterval = 0.06f;
    [SerializeField, Min(0f)] private float openingHeartScaleDuration = 0.14f;

    [Header("Text Components")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("Continue Icon")]
    [SerializeField] private GameObject continueIcon;
    [SerializeField] private RectTransform continueLeftArrow;
    [SerializeField] private RectTransform continueRightArrow;
    [SerializeField, Min(0f)] private float continueArrowMoveDistance = 6f;
    [SerializeField, Min(0.01f)] private float continueArrowMoveDuration = 0.45f;

    [Header("Choice UI")]
    [SerializeField] private Transform choiceContainer;
    [SerializeField] private GameObject choiceButtonPrefab;
    [SerializeField, Min(0f)] private float choiceEnterMoveDistance = 24f;
    [SerializeField, Min(0f)] private float choiceEnterDuration = 0.12f;
    [SerializeField, Min(0f)] private float choiceEnterStagger = 0.05f;
    [SerializeField, Min(0f)] private float unselectedChoiceExitDuration = 0.08f;
    [SerializeField, Min(0f)] private float selectedChoiceExitMoveDistance = 24f;
    [SerializeField, Min(0f)] private float selectedChoiceExitDuration = 0.16f;

    [Header("Theme")]
    [SerializeField] private Graphic[] continueIconThemeTargets;
    [SerializeField] private Graphic dimPanelGraphic;
    [SerializeField] private float dimFadeDuration = 0.25f;
    [SerializeField] private float dialogueEffectIntroFallbackDuration = 0.5f;
    [SerializeField] private Animator dialogueEffectAnimator;
    [SerializeField] private Graphic dialogueEffectGraphic;
    [SerializeField, Min(0f)] private float dialogueEffectFadeDuration = 0.25f;
    [SerializeField] private string dialogueEffectIdleState = "Idle";
    [SerializeField] private string dialogueEffectIntroState = "Intro";

    [Header("Typing Audio")]
    [SerializeField] private bool playTypingSound = true;
    [SerializeField, Min(0f)] private float typingSoundInterval = 0.035f;

    [Header("Text Animation")]
    [SerializeField] private DialogueTextAnimationProfileSO textAnimationProfileOverride;

    [SerializeField, HideInInspector, Min(0f)] private float cameraShakeIntensityMultiplier = 10f;
    [SerializeField, HideInInspector] private DialogueCameraShakeProfileSettings lowCameraShake = DialogueCameraShakeProfileSettings.Create(0.12f, new Vector2(8f, 2f), 2.5f, 1.5f, 0.10f, 12, 70f);
    [SerializeField, HideInInspector] private DialogueCameraShakeProfileSettings middleCameraShake = DialogueCameraShakeProfileSettings.Create(0.18f, new Vector2(16f, 4f), 5f, 3f, 0.20f, 16, 75f);
    [SerializeField, HideInInspector] private DialogueCameraShakeProfileSettings highCameraShake = DialogueCameraShakeProfileSettings.Create(0.26f, new Vector2(28f, 7f), 8f, 5f, 0.35f, 22, 80f);

    private Coroutine typingRoutine;
    private Coroutine textEffectRoutine;
    private Coroutine dialogueCameraShakeInertiaRoutine;
    private Tween continueIconTween;
    private Tween dialoguePanelShakeTween;
    private Sequence choiceTransitionSequence;
    private RectTransform dialoguePanelShakeRoot;
    private RectTransform dialogueTextRect;
    private Vector2 continueLeftArrowBaseAnchoredPosition;
    private Vector2 continueRightArrowBaseAnchoredPosition;
    private Vector2 dialoguePanelShakeBaseAnchoredPosition;
    private Vector2 dialogueTextBaseAnchoredPosition;
    private DialogueTextImpactState dialogueCharacterImpactState;
    private bool hasContinueArrowBasePositions;
    private bool hasDialoguePanelShakeBasePosition;
    private bool hasDialogueTextBaseAnchoredPosition;
    private readonly List<GameObject> activeChoiceButtons = new List<GameObject>();
    private readonly Dictionary<Graphic, Color> originalThemeColors = new Dictionary<Graphic, Color>();

    private DialogueThemeSO currentTheme;
    private DialogueThemeSO currentEffectTheme;
    private RuntimeAnimatorController defaultEffectController;
    private Color defaultNameTextColor;
    private float defaultDimPanelAlpha;
    private float defaultDialogueEffectAlpha = 1f;
    private bool isUiVisible;
    private bool choiceInputEnabled;
    private bool choicePresentationReady;
    private bool choiceExitInProgress;
    private bool openingHeaderRevealPending;
    private bool openingHeaderRevealInProgress;
    private bool openingHeartRevealPending;
    private int currentChoiceIndex;
    private Action<int> onChoiceSelectedCallback;
    private int lastTypedCharacterCount;
    private float nextTypingSoundTime;

    private void Awake()
    {
        AutoResolveThemeTargets();
        CacheThemeDefaults();
        if (nameText != null)
            defaultNameTextColor = nameText.color;
        if (dimPanelGraphic != null)
        {
            defaultDimPanelAlpha = dimPanelGraphic.color.a;
            SetDimPanelVisible(false, true);
        }

        if (dialogueEffectGraphic != null)
            defaultDialogueEffectAlpha = dialogueEffectGraphic.color.a;

        ResetDialogueEffectToHiddenIdle();
        if (dialogueEffectAnimator != null)
            dialogueEffectAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

        ResolveGroupPresentations();
        ResolveAffectionUI();
        SnapGroupClosed(textBoxGroup, textBoxPresentation);
        SnapGroupClosed(dialogueUpperFrameGroup, dialogueUpperFramePresentation);
        SnapGroupClosed(affectionGroup, affectionPresentation);

        SetContinueIconVisible(false);
        CacheContinueIconArrowTransforms();

        ClearChoices();
        ClearText();
    }

    private DialogueTextAnimationProfileSO GetTextAnimationProfile()
    {
        return DialogueTextAnimationProfileSO.Resolve(textAnimationProfileOverride);
    }

    private void OnDisable()
    {
        SetContinueIconVisible(false);
        StopTypingRoutine();
        CompleteOpeningHeaderReveal();
        ClearChoices();
        StopTextEffectRoutine(true);
        StopDialogueCameraShake(true);
    }

    private void OnDestroy()
    {
        StopRuntimeTweens();
    }

    public void ClearText()
    {
        StopTypingRoutine();
        StopTextEffectRoutine(true);
        StopDialogueCameraShake(true);
        ResetTypingAudioTracking();

        if (nameText != null)
        {
            nameText.text = string.Empty;
            nameText.maxVisibleCharacters = int.MaxValue;
        }

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
            dialogueText.maxVisibleCharacters = int.MaxValue;
        }
    }

    public void ApplyTheme(DialogueThemeSO theme, bool updateEffectTheme = false)
    {
        AutoResolveThemeTargets();
        CacheThemeDefaults();
        currentTheme = theme;
        if (updateEffectTheme)
            currentEffectTheme = theme;

        RefreshThemePresentation(false);
    }

    public void ApplyDialogueEffectTheme(DialogueThemeSO theme)
    {
        AutoResolveThemeTargets();
        currentEffectTheme = theme;
        RefreshDialogueEffectOverride();
    }

    public void ResetTheme()
    {
        currentTheme = null;
        currentEffectTheme = null;
        RestoreThemeVisuals();
        RefreshActiveChoiceTheme();
        ResetDialogueEffectOverride();
        ResetDialogueEffectToHiddenIdle();
    }

    public void ShowUI(bool isBoss, Action onComplete = null)
    {
        CompleteOpeningHeaderReveal();
        openingHeaderRevealPending = true;
        openingHeartRevealPending = isBoss && ResolveAffectionUI() != null;
        if (nameText != null)
            nameText.maxVisibleCharacters = 0;

        if (openingHeartRevealPending)
            affectionUI.PrepareOpeningReveal();

        isUiVisible = true;
        RefreshThemePresentation(false);
        ResolveGroupPresentations();

        int pendingAnimations = 0;
        bool didComplete = false;
        bool startedAllAnimations = false;

        void RegisterAnimation()
        {
            pendingAnimations++;
        }

        void CompleteAnimation()
        {
            pendingAnimations--;
            if (pendingAnimations > 0 || didComplete || !startedAllAnimations)
                return;

            didComplete = true;
            onComplete?.Invoke();
        }

        if (textBoxGroup != null)
        {
            RegisterAnimation();
            PlayGroupOpen(textBoxGroup, textBoxPresentation, CompleteAnimation);
        }

        if (dialogueUpperFrameGroup != null)
        {
            RegisterAnimation();
            PlayGroupOpen(dialogueUpperFrameGroup, dialogueUpperFramePresentation, CompleteAnimation);
        }

        if (isBoss && affectionGroup != null)
        {
            if (IsAffectionNestedInTextBox())
                SnapGroupOpen(affectionGroup, affectionPresentation);
            else
            {
                RegisterAnimation();
                PlayGroupOpen(affectionGroup, affectionPresentation, CompleteAnimation);
            }
        }
        else
        {
            SnapGroupClosed(affectionGroup, affectionPresentation);
        }

        startedAllAnimations = true;
        if (pendingAnimations == 0 && !didComplete)
        {
            didComplete = true;
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// 책임 : 대화 UI가 실제 텍스트 표시로 넘어가기 전, 진입 연출 시작음을 재생한다.
    /// </summary>
    public void PlayOpeningIntroSound()
    {
        if (isUiVisible)
            return;

        SoundPlaybackUtility.Play(TalkUiIntroSound, sourceObject: this);
    }

    public void PlayBossPrelude(Action onComplete = null)
    {
        RefreshThemePresentation(false);

        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true);
        float effectDuration = GetDialogueEffectIntroDuration();
        if (effectDuration <= 0f)
            effectDuration = dialogueEffectIntroFallbackDuration;

        if (dimPanelGraphic != null)
        {
            SetDimPanelVisible(true, true);
            seq.Append(dimPanelGraphic.DOFade(defaultDimPanelAlpha, dimFadeDuration).SetUpdate(true));
        }

        seq.AppendCallback(() =>
        {
            SetDialogueEffectAlpha(0f);
            SetDialogueEffectVisible(true);
            PlayDialogueEffectIntro();
        });

        float fadeDuration = dialogueEffectGraphic != null
            ? Mathf.Max(0f, dialogueEffectFadeDuration)
            : 0f;
        if (fadeDuration > 0f)
        {
            dialogueEffectGraphic.DOKill();
            seq.Append(dialogueEffectGraphic
                .DOFade(defaultDialogueEffectAlpha, fadeDuration)
                .SetUpdate(true));
        }
        else
        {
            seq.AppendCallback(() => SetDialogueEffectAlpha(defaultDialogueEffectAlpha));
        }

        seq.AppendInterval(Mathf.Max(0f, effectDuration - fadeDuration));
        seq.OnComplete(() => onComplete?.Invoke());
    }

    public void TypeText(string speakerName, string text, Action onComplete = null)
    {
        TypeText(speakerName, text, DialogueAnimType.Normal, DialogueCameraShakePreset.None, onComplete);
    }

    public void TypeText(
        string speakerName,
        string text,
        DialogueAnimType animType,
        Action onComplete = null)
    {
        TypeText(speakerName, text, animType, DialogueCameraShakePreset.None, onComplete);
    }

    internal void TypeText(
        string speakerName,
        string text,
        DialogueAnimType animType,
        DialogueCameraShakePreset cameraShakePreset,
        Action onComplete = null)
    {
        SetContinueIconVisible(false);

        StopTypingRoutine();
        StopTextEffectRoutine(true);
        StopDialogueCameraShake(true);
        ResetTypingAudioTracking();

        if (nameText != null)
        {
            nameText.text = speakerName;
            nameText.ForceMeshUpdate();
            nameText.maxVisibleCharacters = openingHeaderRevealPending ? 0 : int.MaxValue;
        }

        if (dialogueText == null)
            return;

        DialogueTextAnimationProfileSO textAnimationProfile = GetTextAnimationProfile();
        DialogueTextRevealPlan revealPlan = DialogueTextAnimationUtility.BuildPlan(text, textAnimationProfile);
        string lineText = revealPlan.DisplayText;
        dialogueText.richText = true;
        dialogueText.text = lineText;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();
        PlayDialogueCameraShake(cameraShakePreset);

        int visibleCharacterCount = dialogueText.textInfo != null
            ? dialogueText.textInfo.characterCount
            : 0;

        if (openingHeaderRevealPending)
        {
            typingRoutine = StartCoroutine(PlayOpeningHeaderThenTypeTextRoutine(
                revealPlan,
                animType,
                visibleCharacterCount,
                textAnimationProfile,
                onComplete));
            return;
        }

        if (visibleCharacterCount <= 0)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;

            SetContinueIconVisible(true);

            onComplete?.Invoke();
            return;
        }

        typingRoutine = StartCoroutine(TypeTextRoutine(
            revealPlan,
            animType,
            visibleCharacterCount,
            textAnimationProfile,
            onComplete));
    }

    private IEnumerator PlayOpeningHeaderThenTypeTextRoutine(
        DialogueTextRevealPlan revealPlan,
        DialogueAnimType animType,
        int visibleCharacterCount,
        DialogueTextAnimationProfileSO textAnimationProfile,
        Action onComplete)
    {
        openingHeaderRevealInProgress = true;

        int speakerCharacterCount = 0;
        if (nameText != null)
        {
            nameText.ForceMeshUpdate();
            speakerCharacterCount = nameText.textInfo != null ? nameText.textInfo.characterCount : 0;
            nameText.maxVisibleCharacters = 0;
        }

        float heartRevealDuration = 0f;
        if (openingHeartRevealPending && ResolveAffectionUI() != null)
        {
            heartRevealDuration = affectionUI.PlayOpeningReveal(
                openingHeaderBeatInterval,
                openingHeartScaleDuration);
        }

        float safeBeatInterval = Mathf.Max(0f, openingHeaderBeatInterval);
        float speakerRevealDuration = speakerCharacterCount > 0
            ? (speakerCharacterCount - 1) * safeBeatInterval
            : 0f;
        float revealDuration = Mathf.Max(speakerRevealDuration, heartRevealDuration);
        float elapsed = 0f;

        while (elapsed < revealDuration)
        {
            if (nameText != null && speakerCharacterCount > 0)
            {
                int visibleCount = safeBeatInterval <= 0f
                    ? speakerCharacterCount
                    : Mathf.Clamp(Mathf.FloorToInt(elapsed / safeBeatInterval) + 1, 0, speakerCharacterCount);
                nameText.maxVisibleCharacters = visibleCount;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        CompleteOpeningHeaderReveal();

        if (visibleCharacterCount <= 0)
        {
            typingRoutine = null;
            if (dialogueText != null)
                dialogueText.maxVisibleCharacters = int.MaxValue;

            SetContinueIconVisible(true);
            onComplete?.Invoke();
            yield break;
        }

        yield return TypeTextRoutine(
            revealPlan,
            animType,
            visibleCharacterCount,
            textAnimationProfile,
            onComplete);
    }

    public void SkipTyping(string fullText)
    {
        StopTypingRoutine();
        CompleteOpeningHeaderReveal();

        if (dialogueText != null)
        {
            DialogueTextAnimationProfileSO textAnimationProfile = GetTextAnimationProfile();
            DialogueTextRevealPlan revealPlan = DialogueTextAnimationUtility.BuildPlan(fullText, textAnimationProfile);
            dialogueText.richText = true;
            dialogueText.text = revealPlan.DisplayText;
            dialogueText.ForceMeshUpdate();
            dialogueText.maxVisibleCharacters = dialogueText.textInfo != null
                ? dialogueText.textInfo.characterCount
                : int.MaxValue;

            StartTextEffectRoutine(
                revealPlan,
                GetCurrentVisibleDialogueCharacterCount(),
                textAnimationProfile);
        }

        lastTypedCharacterCount = GetCurrentVisibleDialogueCharacterCount();

        SetContinueIconVisible(true);
    }

    private IEnumerator TypeTextRoutine(
        DialogueTextRevealPlan revealPlan,
        DialogueAnimType animType,
        int visibleCharacterCount,
        DialogueTextAnimationProfileSO textAnimationProfile,
        Action onComplete)
    {
        DialogueTextRevealProfile profile = DialogueTextAnimationUtility.ResolveProfile(animType);

        for (int i = 0; i < visibleCharacterCount; i++)
        {
            float explicitPause = DialogueTextAnimationUtility.GetPauseBeforeCharacter(revealPlan, i);
            if (explicitPause > 0f)
                yield return WaitForTextRevealDelay(revealPlan, i, explicitPause, textAnimationProfile);

            if (dialogueText == null)
                yield break;

            dialogueText.maxVisibleCharacters = i + 1;
            HandleTypingTweenUpdated();
            DialogueTextAnimationUtility.ApplyTextEffects(
                dialogueText,
                revealPlan,
                i + 1,
                Time.unscaledTime,
                dialogueCharacterImpactState,
                textAnimationProfile);

            float delay = DialogueTextAnimationUtility.GetPostCharacterDelay(
                profile,
                dialogueText.textInfo,
                i);

            if (delay > 0f)
                yield return WaitForTextRevealDelay(revealPlan, i + 1, delay, textAnimationProfile);
        }

        float settleSeconds = DialogueTextAnimationUtility.GetTextEffectSettleSeconds(
            revealPlan,
            textAnimationProfile);
        if (settleSeconds > 0f)
            yield return WaitForTextRevealDelay(
                revealPlan,
                visibleCharacterCount,
                settleSeconds,
                textAnimationProfile);

        typingRoutine = null;

        if (dialogueText != null)
        {
            dialogueText.maxVisibleCharacters = visibleCharacterCount;
            StartTextEffectRoutine(revealPlan, visibleCharacterCount, textAnimationProfile);
            HandleTypingTweenUpdated();
        }

        SetContinueIconVisible(true);

        onComplete?.Invoke();
    }

    private IEnumerator WaitForTextRevealDelay(
        DialogueTextRevealPlan revealPlan,
        int visibleCharacterCount,
        float seconds,
        DialogueTextAnimationProfileSO textAnimationProfile)
    {
        if (seconds <= 0f)
            yield break;

        bool hasInlineEffects = DialogueTextAnimationUtility.HasTextEffects(revealPlan);
        if (!hasInlineEffects && !HasActiveDialogueCharacterImpact())
        {
            yield return new WaitForSecondsRealtime(seconds);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (dialogueText == null)
                yield break;

            if (!hasInlineEffects && !HasActiveDialogueCharacterImpact())
            {
                DialogueTextAnimationUtility.ResetTextEffects(dialogueText);
                float remainingSeconds = seconds - elapsed;
                if (remainingSeconds > 0f)
                    yield return new WaitForSecondsRealtime(remainingSeconds);

                yield break;
            }

            DialogueTextAnimationUtility.ApplyTextEffects(
                dialogueText,
                revealPlan,
                visibleCharacterCount,
                Time.unscaledTime,
                dialogueCharacterImpactState,
                textAnimationProfile);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!hasInlineEffects && !HasActiveDialogueCharacterImpact())
            DialogueTextAnimationUtility.ResetTextEffects(dialogueText);
    }

    public bool ShowChoices(List<Ink.Runtime.Choice> choices, Action<int> onChoiceSelected)
    {
        ClearChoices();

        SetContinueIconVisible(false);

        if (choiceContainer == null || choiceButtonPrefab == null)
        {
            Debug.LogError("[DialogueView] choiceContainer or choiceButtonPrefab is missing. Cannot display dialogue choices.", this);
            return false;
        }

        onChoiceSelectedCallback = onChoiceSelected;
        choiceInputEnabled = false;
        choicePresentationReady = false;
        choiceExitInProgress = false;
        currentChoiceIndex = -1;
        EventSystem.current?.SetSelectedGameObject(null);

        choiceContainer.gameObject.SetActive(true);
        LayoutGroup choiceLayoutGroup = choiceContainer.GetComponent<LayoutGroup>();
        if (choiceLayoutGroup != null)
            choiceLayoutGroup.enabled = true;

        foreach (Ink.Runtime.Choice choice in choices)
        {
            GameObject btnObj = Instantiate(choiceButtonPrefab, choiceContainer);
            if (btnObj != null && !btnObj.activeSelf)
                btnObj.SetActive(true);

            activeChoiceButtons.Add(btnObj);
            int listIndex = activeChoiceButtons.Count - 1;

            TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
                btnText.text = choice.text;

            DialogueChoiceHighlightPresentation choiceHighlight = btnObj.GetComponent<DialogueChoiceHighlightPresentation>();
            if (choiceHighlight != null)
            {
                ApplyThemeToChoice(choiceHighlight);
                choiceHighlight.SetSelected(false, true);
            }

            DialogueChoiceInputRelay inputRelay = btnObj.GetComponent<DialogueChoiceInputRelay>();
            if (inputRelay != null)
                inputRelay.Bind(this, listIndex);

            DialogueChoiceKeyGlyph keyGlyph = btnObj.GetComponent<DialogueChoiceKeyGlyph>();
            if (keyGlyph != null)
                keyGlyph.Bind(listIndex);

            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick = new Button.ButtonClickedEvent();
                Navigation navigation = btn.navigation;
                navigation.mode = Navigation.Mode.None;
                btn.navigation = navigation;

                int index = choice.index;
                btn.onClick.AddListener(() =>
                {
                    TryCommitChoice(listIndex, index);
                });
            }
        }

        HighlightChoice(currentChoiceIndex);
        Canvas.ForceUpdateCanvases();
        if (choiceContainer is RectTransform choiceContainerRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(choiceContainerRect);

        foreach (GameObject choiceButton in activeChoiceButtons)
        {
            DialogueChoiceHighlightPresentation presentation =
                choiceButton != null ? choiceButton.GetComponent<DialogueChoiceHighlightPresentation>() : null;
            presentation?.CaptureLayoutPosition();
        }

        if (choiceLayoutGroup != null)
            choiceLayoutGroup.enabled = false;

        PlayChoiceEnterPresentation();
        return true;
    }

    public void ChangeChoiceSelection(int direction)
    {
        if (!CanAcceptChoiceInput() || activeChoiceButtons.Count == 0)
            return;

        if (currentChoiceIndex < 0)
        {
            currentChoiceIndex = direction < 0 ? 0 : Mathf.Min(1, activeChoiceButtons.Count - 1);
            HighlightChoice(currentChoiceIndex);
            return;
        }

        currentChoiceIndex += direction;

        if (currentChoiceIndex < 0)
            currentChoiceIndex = activeChoiceButtons.Count - 1;
        else if (currentChoiceIndex >= activeChoiceButtons.Count)
            currentChoiceIndex = 0;

        HighlightChoice(currentChoiceIndex);
    }

    public void ConfirmChoice()
    {
        if (!CanAcceptChoiceInput() || activeChoiceButtons.Count <= 0)
            return;

        if (currentChoiceIndex < 0 || currentChoiceIndex >= activeChoiceButtons.Count)
            return;

        Button selectedBtn = activeChoiceButtons[currentChoiceIndex].GetComponent<Button>();
        selectedBtn?.onClick.Invoke();
    }

    public void ConfirmChoiceAt(int index)
    {
        if (!CanAcceptChoiceInput() || index < 0 || index >= activeChoiceButtons.Count)
            return;

        currentChoiceIndex = index;
        HighlightChoice(currentChoiceIndex);

        Button selectedBtn = activeChoiceButtons[currentChoiceIndex].GetComponent<Button>();
        selectedBtn?.onClick.Invoke();
    }

    public void SetChoiceInputEnabled(bool enabled)
    {
        choiceInputEnabled = enabled;
        RefreshChoiceInteraction();
    }

    public void SelectChoiceFromPointer(int index)
    {
        if (!CanAcceptChoiceInput())
            return;

        SelectChoice(index);
    }

    private void SelectChoice(int index)
    {
        if (index < 0 || index >= activeChoiceButtons.Count)
            return;

        currentChoiceIndex = index;
        HighlightChoice(currentChoiceIndex);
    }

    public void ClearChoices()
    {
        if (choiceTransitionSequence != null)
        {
            choiceTransitionSequence.Kill(false);
            choiceTransitionSequence = null;
        }

        choiceInputEnabled = false;
        choicePresentationReady = false;
        choiceExitInProgress = false;
        currentChoiceIndex = -1;

        LayoutGroup choiceLayoutGroup = choiceContainer != null
            ? choiceContainer.GetComponent<LayoutGroup>()
            : null;
        if (choiceLayoutGroup != null)
            choiceLayoutGroup.enabled = true;

        foreach (GameObject btn in activeChoiceButtons)
        {
            if (btn != null)
            {
                DialogueChoiceHighlightPresentation choiceHighlight =
                    btn.GetComponent<DialogueChoiceHighlightPresentation>();
                if (choiceHighlight != null)
                {
                    choiceHighlight.ResetPresentation(true);
                    choiceHighlight.SetSelected(false, true);
                }

                btn.SetActive(false);
                Destroy(btn);
            }
        }

        activeChoiceButtons.Clear();
        onChoiceSelectedCallback = null;
        EventSystem.current?.SetSelectedGameObject(null);

        if (choiceContainer != null)
            choiceContainer.gameObject.SetActive(false);
    }

    private void PlayChoiceEnterPresentation()
    {
        if (activeChoiceButtons.Count == 0)
        {
            choicePresentationReady = true;
            RefreshChoiceInteraction();
            return;
        }

        choiceTransitionSequence = DOTween.Sequence().SetUpdate(true);
        int animatedChoiceCount = 0;

        for (int i = 0; i < activeChoiceButtons.Count; i++)
        {
            GameObject choiceButton = activeChoiceButtons[i];
            DialogueChoiceHighlightPresentation presentation =
                choiceButton != null ? choiceButton.GetComponent<DialogueChoiceHighlightPresentation>() : null;
            if (presentation == null)
                continue;

            Tween enterTween = presentation.CreateEnterTween(choiceEnterMoveDistance, choiceEnterDuration);
            choiceTransitionSequence.Insert(i * choiceEnterStagger, enterTween);
            animatedChoiceCount++;
        }

        if (animatedChoiceCount == 0)
        {
            choiceTransitionSequence.Kill(false);
            choiceTransitionSequence = null;
            choicePresentationReady = true;
            RefreshChoiceInteraction();
            return;
        }

        choiceTransitionSequence.OnComplete(() =>
        {
            choiceTransitionSequence = null;
            choicePresentationReady = true;
            RefreshChoiceInteraction();
        });
    }

    private void TryCommitChoice(int listIndex, int storyChoiceIndex)
    {
        if (!CanAcceptChoiceInput() || listIndex < 0 || listIndex >= activeChoiceButtons.Count)
            return;

        currentChoiceIndex = listIndex;
        HighlightChoice(currentChoiceIndex);

        choiceInputEnabled = false;
        choicePresentationReady = false;
        choiceExitInProgress = true;
        RefreshChoiceInteraction();

        Action<int> callback = onChoiceSelectedCallback;
        onChoiceSelectedCallback = null;

        if (choiceTransitionSequence != null)
        {
            choiceTransitionSequence.Kill(false);
            choiceTransitionSequence = null;
        }

        choiceTransitionSequence = DOTween.Sequence().SetUpdate(true);
        int animatedChoiceCount = 0;

        for (int i = 0; i < activeChoiceButtons.Count; i++)
        {
            GameObject choiceButton = activeChoiceButtons[i];
            DialogueChoiceHighlightPresentation presentation =
                choiceButton != null ? choiceButton.GetComponent<DialogueChoiceHighlightPresentation>() : null;
            if (presentation == null)
                continue;

            bool isSelected = i == listIndex;
            float duration = isSelected ? selectedChoiceExitDuration : unselectedChoiceExitDuration;
            Tween exitTween = presentation.CreateExitTween(
                isSelected,
                selectedChoiceExitMoveDistance,
                duration);
            choiceTransitionSequence.Insert(0f, exitTween);
            animatedChoiceCount++;
        }

        void CompleteSelection()
        {
            choiceTransitionSequence = null;
            ClearChoices();
            callback?.Invoke(storyChoiceIndex);
        }

        if (animatedChoiceCount == 0)
        {
            choiceTransitionSequence.Kill(false);
            CompleteSelection();
            return;
        }

        choiceTransitionSequence.OnComplete(CompleteSelection);
    }

    private bool CanAcceptChoiceInput()
    {
        return choiceInputEnabled && choicePresentationReady && !choiceExitInProgress;
    }

    private void RefreshChoiceInteraction()
    {
        bool enabled = CanAcceptChoiceInput();
        foreach (GameObject choiceButton in activeChoiceButtons)
        {
            DialogueChoiceHighlightPresentation presentation =
                choiceButton != null ? choiceButton.GetComponent<DialogueChoiceHighlightPresentation>() : null;
            presentation?.SetInteractionEnabled(enabled);
        }
    }

    private void SetContinueIconVisible(bool visible)
    {
        if (continueIcon == null)
            return;

        if (!visible)
        {
            StopContinueIconMotion(true);
            continueIcon.SetActive(false);
            return;
        }

        continueIcon.SetActive(true);
        StartContinueIconMotion();
    }

    private void CacheContinueIconArrowTransforms()
    {
        if (continueLeftArrow == null || continueRightArrow == null)
            return;

        continueLeftArrowBaseAnchoredPosition = continueLeftArrow.anchoredPosition;
        continueRightArrowBaseAnchoredPosition = continueRightArrow.anchoredPosition;
        hasContinueArrowBasePositions = true;
    }

    private void StartContinueIconMotion()
    {
        if (!hasContinueArrowBasePositions)
            CacheContinueIconArrowTransforms();

        if (!hasContinueArrowBasePositions || continueLeftArrow == null || continueRightArrow == null)
            return;

        StopContinueIconMotion(true);

        Sequence arrowSequence = DOTween.Sequence();
        arrowSequence.SetUpdate(true);
        arrowSequence.Append(continueLeftArrow
            .DOAnchorPosX(continueLeftArrowBaseAnchoredPosition.x - continueArrowMoveDistance, continueArrowMoveDuration)
            .SetEase(Ease.InOutSine));
        arrowSequence.Join(continueRightArrow
            .DOAnchorPosX(continueRightArrowBaseAnchoredPosition.x + continueArrowMoveDistance, continueArrowMoveDuration)
            .SetEase(Ease.InOutSine));
        arrowSequence.SetLoops(-1, LoopType.Yoyo);
        continueIconTween = arrowSequence;
    }

    private void StopContinueIconMotion(bool resetPosition)
    {
        continueIconTween?.Kill();
        continueIconTween = null;

        if (continueLeftArrow != null)
            continueLeftArrow.DOKill();

        if (continueRightArrow != null)
            continueRightArrow.DOKill();

        if (resetPosition && hasContinueArrowBasePositions)
        {
            if (continueLeftArrow != null)
                continueLeftArrow.anchoredPosition = continueLeftArrowBaseAnchoredPosition;

            if (continueRightArrow != null)
                continueRightArrow.anchoredPosition = continueRightArrowBaseAnchoredPosition;
        }
    }

    private void PlayDialogueCameraShake(DialogueCameraShakePreset preset)
    {
        if (preset == DialogueCameraShakePreset.None ||
            !TryResolveDialogueCameraShakeProfile(preset, out DialogueCameraShakeMotionProfile profile))
        {
            return;
        }

        PlayDialoguePanelShake(profile);
        PlayDialogueTextInertia(profile);
        PlayDialogueCharacterImpact(profile);
        PlayDialogueWorldCameraShake(profile);
    }

    private bool TryResolveDialogueCameraShakeProfile(
        DialogueCameraShakePreset preset,
        out DialogueCameraShakeMotionProfile profile)
    {
        DialogueTextAnimationProfileSO textAnimationProfile = GetTextAnimationProfile();
        if (textAnimationProfile != null &&
            textAnimationProfile.TryResolveCameraShakeMotion(
                preset,
                out DialogueCameraShakeMotionSettings motionSettings))
        {
            profile = new DialogueCameraShakeMotionProfile(
                motionSettings.Duration,
                motionSettings.PanelStrength,
                motionSettings.TextMaxOffset,
                motionSettings.CharacterImpactOffset,
                motionSettings.CameraAmplitude,
                motionSettings.Vibrato,
                motionSettings.Randomness,
                motionSettings.TextInertiaScale,
                motionSettings.TextSmoothTime,
                motionSettings.TextSettleDuration,
                motionSettings.CameraMinIntervalSeconds);
            return true;
        }

        DialogueCameraShakeProfileSettings settings = preset switch
        {
            DialogueCameraShakePreset.Low => lowCameraShake,
            DialogueCameraShakePreset.Middle => middleCameraShake,
            DialogueCameraShakePreset.High => highCameraShake,
            _ => null,
        };

        if (settings == null)
        {
            profile = default;
            return false;
        }

        profile = settings.ToMotionProfile(cameraShakeIntensityMultiplier);
        return true;
    }

    private void PlayDialoguePanelShake(in DialogueCameraShakeMotionProfile profile)
    {
        RectTransform panelRoot = ResolveDialoguePanelShakeRoot();
        if (panelRoot == null || profile.Duration <= 0f)
            return;

        dialoguePanelShakeBaseAnchoredPosition = panelRoot.anchoredPosition;
        hasDialoguePanelShakeBasePosition = true;
        dialoguePanelShakeTween = panelRoot
            .DOShakeAnchorPos(
                profile.Duration,
                profile.PanelStrength,
                profile.Vibrato,
                profile.Randomness,
                false,
                true)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                dialoguePanelShakeTween = null;
                if (panelRoot != null && hasDialoguePanelShakeBasePosition)
                    panelRoot.anchoredPosition = dialoguePanelShakeBaseAnchoredPosition;
            });
    }

    private void PlayDialogueTextInertia(in DialogueCameraShakeMotionProfile profile)
    {
        RectTransform panelRoot = ResolveDialoguePanelShakeRoot();
        RectTransform textRoot = ResolveDialogueTextRect();
        if (panelRoot == null || textRoot == null || profile.TextMaxOffset <= 0f)
            return;

        dialogueTextBaseAnchoredPosition = textRoot.anchoredPosition;
        hasDialogueTextBaseAnchoredPosition = true;
        dialogueCameraShakeInertiaRoutine = StartCoroutine(PlayDialogueTextInertiaRoutine(panelRoot, textRoot, profile));
    }

    private IEnumerator PlayDialogueTextInertiaRoutine(
        RectTransform panelRoot,
        RectTransform textRoot,
        DialogueCameraShakeMotionProfile profile)
    {
        float elapsed = 0f;
        float totalDuration = profile.Duration + profile.TextSettleDuration;
        Vector2 textOffset = Vector2.zero;
        Vector2 velocity = Vector2.zero;

        while (elapsed < totalDuration && panelRoot != null && textRoot != null)
        {
            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            Vector2 panelOffset = hasDialoguePanelShakeBasePosition
                ? panelRoot.anchoredPosition - dialoguePanelShakeBaseAnchoredPosition
                : Vector2.zero;
            Vector2 targetOffset = Vector2.ClampMagnitude(
                -panelOffset * profile.TextInertiaScale,
                profile.TextMaxOffset);

            textOffset = Vector2.SmoothDamp(
                textOffset,
                targetOffset,
                ref velocity,
                profile.TextSmoothTime,
                Mathf.Infinity,
                deltaTime);

            float settleFade = elapsed <= profile.Duration || profile.TextSettleDuration <= 0f
                ? 1f
                : 1f - Mathf.Clamp01((elapsed - profile.Duration) / profile.TextSettleDuration);
            textRoot.anchoredPosition = dialogueTextBaseAnchoredPosition + textOffset * settleFade;

            elapsed += deltaTime;
            yield return null;
        }

        if (textRoot != null && hasDialogueTextBaseAnchoredPosition)
            textRoot.anchoredPosition = dialogueTextBaseAnchoredPosition;

        dialogueCameraShakeInertiaRoutine = null;
    }

    private void PlayDialogueWorldCameraShake(in DialogueCameraShakeMotionProfile profile)
    {
        if (profile.CameraAmplitude <= 0f)
            return;

        CameraShakePlayback.Play(new CameraShakeRequest(
            profile.CameraAmplitude,
            Vector3.up,
            gameObject,
            profile.CameraMinIntervalSeconds,
            "Dialogue.CameraShake"));
    }

    private void PlayDialogueCharacterImpact(in DialogueCameraShakeMotionProfile profile)
    {
        dialogueCharacterImpactState = new DialogueTextImpactState(
            Time.unscaledTime,
            profile.Duration,
            profile.TextSettleDuration,
            profile.CharacterImpactOffset,
            profile.Vibrato,
            profile.Randomness);
    }

    private bool HasActiveDialogueCharacterImpact()
    {
        return dialogueCharacterImpactState.IsActiveAt(Time.unscaledTime);
    }

    private void StopDialogueCameraShake(bool resetPosition)
    {
        dialoguePanelShakeTween?.Kill();
        dialoguePanelShakeTween = null;

        if (dialogueCameraShakeInertiaRoutine != null)
        {
            StopCoroutine(dialogueCameraShakeInertiaRoutine);
            dialogueCameraShakeInertiaRoutine = null;
        }

        if (dialoguePanelShakeRoot != null)
            dialoguePanelShakeRoot.DOKill();

        if (dialogueTextRect != null)
            dialogueTextRect.DOKill();

        if (resetPosition && dialoguePanelShakeRoot != null && hasDialoguePanelShakeBasePosition)
            dialoguePanelShakeRoot.anchoredPosition = dialoguePanelShakeBaseAnchoredPosition;

        if (resetPosition && dialogueTextRect != null && hasDialogueTextBaseAnchoredPosition)
            dialogueTextRect.anchoredPosition = dialogueTextBaseAnchoredPosition;

        dialogueCharacterImpactState = default;
        if (resetPosition)
            DialogueTextAnimationUtility.ResetTextEffects(dialogueText);

        hasDialoguePanelShakeBasePosition = false;
        hasDialogueTextBaseAnchoredPosition = false;
    }

    private RectTransform ResolveDialoguePanelShakeRoot()
    {
        if (dialoguePanelShakeRoot != null)
            return dialoguePanelShakeRoot;

        if (textBoxGroup == null)
            return null;

        dialoguePanelShakeRoot = textBoxGroup.transform as RectTransform;
        return dialoguePanelShakeRoot;
    }

    private RectTransform ResolveDialogueTextRect()
    {
        if (dialogueTextRect != null)
            return dialogueTextRect;

        if (dialogueText == null)
            return null;

        dialogueTextRect = dialogueText.rectTransform;
        return dialogueTextRect;
    }

    private void StopRuntimeTweens()
    {
        StopTypingRoutine();
        StopTextEffectRoutine(true);
        StopDialogueCameraShake(true);

        if (choiceTransitionSequence != null)
        {
            choiceTransitionSequence.Kill(false);
            choiceTransitionSequence = null;
        }

        StopContinueIconMotion(true);

        if (dialogueText != null)
            dialogueText.DOKill();

        if (textBoxGroup != null)
            textBoxGroup.DOKill();

        if (dialogueUpperFrameGroup != null)
            dialogueUpperFrameGroup.DOKill();

        if (affectionGroup != null)
            affectionGroup.DOKill();

        if (dimPanelGraphic != null)
            dimPanelGraphic.DOKill();

        if (dialogueEffectGraphic != null)
            dialogueEffectGraphic.DOKill();

        foreach (GameObject choiceButton in activeChoiceButtons)
        {
            if (choiceButton != null)
            {
                DialogueChoiceHighlightPresentation presentation =
                    choiceButton.GetComponent<DialogueChoiceHighlightPresentation>();
                presentation?.KillMotion();
            }
        }
    }

    private void StopTypingRoutine()
    {
        if (typingRoutine == null)
            return;

        bool wasOpeningHeaderReveal = openingHeaderRevealInProgress;
        StopCoroutine(typingRoutine);
        typingRoutine = null;

        if (wasOpeningHeaderReveal)
            CompleteOpeningHeaderReveal();
    }

    private void CompleteOpeningHeaderReveal()
    {
        if (!openingHeaderRevealPending &&
            !openingHeaderRevealInProgress &&
            !openingHeartRevealPending)
        {
            return;
        }

        if (nameText != null)
            nameText.maxVisibleCharacters = int.MaxValue;

        if (openingHeartRevealPending && ResolveAffectionUI() != null)
            affectionUI.CompleteOpeningReveal();

        openingHeaderRevealPending = false;
        openingHeaderRevealInProgress = false;
        openingHeartRevealPending = false;
    }

    private void StartTextEffectRoutine(
        DialogueTextRevealPlan revealPlan,
        int visibleCharacterCount,
        DialogueTextAnimationProfileSO textAnimationProfile)
    {
        StopTextEffectRoutine(true);

        if (dialogueText == null ||
            (!DialogueTextAnimationUtility.HasTextEffects(revealPlan) && !HasActiveDialogueCharacterImpact()))
        {
            return;
        }

        textEffectRoutine = StartCoroutine(PlayTextEffectRoutine(
            revealPlan,
            visibleCharacterCount,
            textAnimationProfile));
    }

    private IEnumerator PlayTextEffectRoutine(
        DialogueTextRevealPlan revealPlan,
        int visibleCharacterCount,
        DialogueTextAnimationProfileSO textAnimationProfile)
    {
        bool hasInlineEffects = DialogueTextAnimationUtility.HasTextEffects(revealPlan);
        while (dialogueText != null)
        {
            bool hasImpact = HasActiveDialogueCharacterImpact();
            if (!hasInlineEffects && !hasImpact)
                break;

            DialogueTextAnimationUtility.ApplyTextEffects(
                dialogueText,
                revealPlan,
                visibleCharacterCount,
                Time.unscaledTime,
                dialogueCharacterImpactState,
                textAnimationProfile);

            yield return null;
        }

        textEffectRoutine = null;
        if (dialogueText != null && !hasInlineEffects)
            DialogueTextAnimationUtility.ResetTextEffects(dialogueText);
    }

    private void StopTextEffectRoutine(bool resetText)
    {
        if (textEffectRoutine != null)
        {
            StopCoroutine(textEffectRoutine);
            textEffectRoutine = null;
        }

        if (resetText)
            DialogueTextAnimationUtility.ResetTextEffects(dialogueText);
    }

    public void HideUI(Action onComplete = null)
    {
        ClearChoices();
        StopTypingRoutine();
        CompleteOpeningHeaderReveal();
        StopTextEffectRoutine(true);
        StopDialogueCameraShake(true);

        SetContinueIconVisible(false);

        ResolveGroupPresentations();

        int pendingAnimations = 0;
        bool didComplete = false;
        bool startedAllAnimations = false;

        void FinishHide()
        {
            isUiVisible = false;

            if (IsAffectionNestedInTextBox())
                SnapGroupClosed(affectionGroup, affectionPresentation);

            SetDimPanelVisible(false, true);
            ResetDialogueEffectToHiddenIdle();
            onComplete?.Invoke();
        }

        void FinishFrameExit()
        {
            if (IsDialogueEffectVisible())
            {
                PlayDialogueEffectFadeOut(FinishHide);
                return;
            }

            FinishHide();
        }

        void RegisterAnimation()
        {
            pendingAnimations++;
        }

        void CompleteAnimation()
        {
            pendingAnimations--;
            if (pendingAnimations > 0 || didComplete || !startedAllAnimations)
                return;

            didComplete = true;
            FinishFrameExit();
        }

        if (textBoxGroup != null && textBoxGroup.gameObject.activeSelf)
        {
            RegisterAnimation();
            PlayGroupClose(textBoxGroup, textBoxPresentation, CompleteAnimation);
        }

        if (dialogueUpperFrameGroup != null && dialogueUpperFrameGroup.gameObject.activeSelf)
        {
            RegisterAnimation();
            PlayGroupClose(dialogueUpperFrameGroup, dialogueUpperFramePresentation, CompleteAnimation);
        }

        if (!IsAffectionNestedInTextBox() && affectionGroup != null && affectionGroup.gameObject.activeSelf)
        {
            RegisterAnimation();
            PlayGroupClose(affectionGroup, affectionPresentation, CompleteAnimation);
        }

        startedAllAnimations = true;
        if (pendingAnimations == 0 && !didComplete)
        {
            didComplete = true;
            FinishFrameExit();
        }
    }

    private void PlayGroupOpen(CanvasGroup group, UISlideFadePresentation presentation, Action onComplete)
    {
        if (presentation != null)
        {
            presentation.PlayOpen(onComplete);
            return;
        }

        if (group == null)
        {
            onComplete?.Invoke();
            return;
        }

        group.DOKill();
        group.gameObject.SetActive(true);
        group.alpha = 0f;
        group.DOFade(1f, 0.25f)
            .SetUpdate(true)
            .OnComplete(() => onComplete?.Invoke());
    }

    private void PlayGroupClose(CanvasGroup group, UISlideFadePresentation presentation, Action onComplete)
    {
        if (presentation != null)
        {
            presentation.PlayClose(onComplete);
            return;
        }

        if (group == null || !group.gameObject.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }

        group.DOKill();
        group.DOFade(0f, 0.25f)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                group.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
    }

    private void SnapGroupClosed(CanvasGroup group, UISlideFadePresentation presentation)
    {
        if (presentation != null)
        {
            presentation.SnapClosed();
            return;
        }

        if (group == null)
            return;

        group.DOKill();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        group.gameObject.SetActive(false);
    }

    private void SnapGroupOpen(CanvasGroup group, UISlideFadePresentation presentation)
    {
        if (presentation != null)
        {
            presentation.SnapOpen();
            return;
        }

        if (group == null)
            return;

        group.DOKill();
        group.gameObject.SetActive(true);
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    private void ResolveGroupPresentations()
    {
        if (textBoxPresentation == null)
            textBoxPresentation = ResolveGroupPresentation(textBoxGroup);

        if (dialogueUpperFramePresentation == null)
            dialogueUpperFramePresentation = ResolveGroupPresentation(dialogueUpperFrameGroup);

        if (affectionPresentation == null)
            affectionPresentation = ResolveGroupPresentation(affectionGroup);
    }

    private bool IsAffectionNestedInTextBox()
    {
        return affectionGroup != null &&
               textBoxGroup != null &&
               affectionGroup.transform.IsChildOf(textBoxGroup.transform);
    }

    private AffectionUI ResolveAffectionUI()
    {
        if (affectionUI != null)
            return affectionUI;

        if (affectionGroup != null)
            affectionUI = affectionGroup.GetComponent<AffectionUI>();

        return affectionUI;
    }

    private UISlideFadePresentation ResolveGroupPresentation(CanvasGroup group)
    {
        if (group == null)
            return null;

        return group.GetComponent<UISlideFadePresentation>();
    }

    private void HighlightChoice(int index)
    {
        for (int i = 0; i < activeChoiceButtons.Count; i++)
        {
            GameObject choiceButton = activeChoiceButtons[i];
            if (choiceButton == null)
                continue;

            bool isSelected = index >= 0 && i == index;

            DialogueChoiceHighlightPresentation choiceHighlight =
                choiceButton.GetComponent<DialogueChoiceHighlightPresentation>();
            if (choiceHighlight != null)
                choiceHighlight.SetSelected(isSelected);
        }
    }

    private void AutoResolveThemeTargets()
    {
        if (dialogueEffectAnimator == null)
        {
            Transform effectTransform = FindChildRecursive("DialogueEffect");
            if (effectTransform != null)
                dialogueEffectAnimator = effectTransform.GetComponent<Animator>();
        }

        if (dialogueEffectAnimator != null && defaultEffectController == null)
            defaultEffectController = dialogueEffectAnimator.runtimeAnimatorController;

        if (dialogueEffectGraphic == null && dialogueEffectAnimator != null)
            dialogueEffectGraphic = dialogueEffectAnimator.GetComponentInChildren<Graphic>(true);

        if (dimPanelGraphic == null)
            dimPanelGraphic = FindGraphicByName("DimPanel");

        if (continueIconThemeTargets == null || continueIconThemeTargets.Length == 0)
        {
            continueIconThemeTargets = new[]
            {
                FindGraphicByName("LeftArrow"),
                FindGraphicByName("MiddleDot"),
                FindGraphicByName("RightArrow")
            };
        }
    }

    private void CacheThemeDefaults()
    {
        foreach (Graphic graphic in EnumerateThemeTargets())
        {
            if (graphic == null)
                continue;

            if (!originalThemeColors.ContainsKey(graphic))
                originalThemeColors[graphic] = graphic.color;
        }
    }

    private IEnumerable<Graphic> EnumerateThemeTargets()
    {
        if (continueIconThemeTargets != null)
        {
            HashSet<Graphic> uniqueTargets = new HashSet<Graphic>();
            foreach (Graphic graphic in continueIconThemeTargets)
            {
                if (graphic != null && uniqueTargets.Add(graphic))
                    yield return graphic;
            }
        }
    }

    private void ApplyAccentColor(Color accentColor)
    {
        if (nameText != null)
            nameText.color = accentColor;

        foreach (Graphic graphic in EnumerateThemeTargets())
        {
            if (graphic == null)
                continue;

            graphic.color = accentColor;
        }

        RefreshActiveChoiceTheme();
    }

    private void RestoreThemeVisuals()
    {
        foreach (Graphic graphic in EnumerateThemeTargets())
        {
            if (graphic == null)
                continue;

            if (originalThemeColors.TryGetValue(graphic, out Color originalColor))
                graphic.color = originalColor;
        }

        if (nameText != null)
            nameText.color = defaultNameTextColor;
    }

    private void RefreshThemePresentation(bool restartEffect)
    {
        if (currentTheme == null)
        {
            RestoreThemeVisuals();
            RefreshActiveChoiceTheme();

            RefreshDialogueEffectOverride();
            if (restartEffect)
                ResetDialogueEffectToHiddenIdle();
            return;
        }

        ApplyAccentColor(currentTheme.accentColor);

        RefreshDialogueEffectOverride();

        if (restartEffect)
            PlayDialogueEffectIntro();
    }

    private void RefreshActiveChoiceTheme()
    {
        foreach (GameObject choiceButton in activeChoiceButtons)
        {
            if (choiceButton == null)
                continue;

            DialogueChoiceHighlightPresentation choiceHighlight =
                choiceButton.GetComponent<DialogueChoiceHighlightPresentation>();
            if (choiceHighlight != null)
                ApplyThemeToChoice(choiceHighlight);
        }
    }

    private void ApplyThemeToChoice(DialogueChoiceHighlightPresentation choiceHighlight)
    {
        if (choiceHighlight == null)
            return;

        if (currentTheme != null)
            choiceHighlight.SetThemeColor(currentTheme.accentColor);
        else
            choiceHighlight.ResetThemeColor();
    }

    private void ApplyDialogueEffectOverride(AnimatorOverrideController overrideController)
    {
        if (dialogueEffectAnimator == null)
            return;

        if (defaultEffectController == null)
            defaultEffectController = dialogueEffectAnimator.runtimeAnimatorController;

        RuntimeAnimatorController targetController = overrideController != null
            ? overrideController
            : defaultEffectController;

        if (dialogueEffectAnimator.runtimeAnimatorController == targetController)
            return;

        dialogueEffectAnimator.runtimeAnimatorController = targetController;
        dialogueEffectAnimator.Rebind();
        UpdateDialogueEffectAnimatorImmediately();
    }

    private void ResetDialogueEffectOverride()
    {
        if (dialogueEffectAnimator == null || defaultEffectController == null)
            return;

        if (dialogueEffectAnimator.runtimeAnimatorController == defaultEffectController)
            return;

        dialogueEffectAnimator.runtimeAnimatorController = defaultEffectController;
        dialogueEffectAnimator.Rebind();
        UpdateDialogueEffectAnimatorImmediately();
    }

    private void RefreshDialogueEffectOverride()
    {
        if (currentEffectTheme != null)
            ApplyDialogueEffectOverride(currentEffectTheme.effectOverride);
        else
            ResetDialogueEffectOverride();
    }

    private void PlayDialogueEffectIntro()
    {
        PlayDialogueEffectState(dialogueEffectIntroState);
    }

    private void PlayDialogueEffectIdle()
    {
        PlayDialogueEffectState(dialogueEffectIdleState);
    }

    private void ResetDialogueEffectToHiddenIdle()
    {
        if (dialogueEffectAnimator == null)
            return;

        if (dialogueEffectGraphic != null)
            dialogueEffectGraphic.DOKill();

        SetDialogueEffectAlpha(0f);
        SetDialogueEffectVisible(true);
        PlayDialogueEffectIdle();
        SetDialogueEffectVisible(false);
    }

    private bool IsDialogueEffectVisible()
    {
        return dialogueEffectAnimator != null && dialogueEffectAnimator.gameObject.activeSelf;
    }

    private void PlayDialogueEffectFadeOut(Action onComplete)
    {
        if (!IsDialogueEffectVisible())
        {
            onComplete?.Invoke();
            return;
        }

        if (dialogueEffectGraphic == null || dialogueEffectFadeDuration <= 0f)
        {
            SetDialogueEffectAlpha(0f);
            onComplete?.Invoke();
            return;
        }

        dialogueEffectGraphic.DOKill();
        dialogueEffectGraphic
            .DOFade(0f, dialogueEffectFadeDuration)
            .SetUpdate(true)
            .OnComplete(() => onComplete?.Invoke());
    }

    private void SetDialogueEffectAlpha(float alpha)
    {
        if (dialogueEffectGraphic == null)
            return;

        Color color = dialogueEffectGraphic.color;
        color.a = alpha;
        dialogueEffectGraphic.color = color;
    }

    private void SetDialogueEffectVisible(bool visible)
    {
        if (dialogueEffectAnimator == null)
            return;

        GameObject effectObject = dialogueEffectAnimator.gameObject;
        if (effectObject != null && effectObject.activeSelf != visible)
            effectObject.SetActive(visible);
    }

    private void PlayDialogueEffectState(string stateName)
    {
        if (dialogueEffectAnimator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        int stateHash = Animator.StringToHash(stateName);
        if (!dialogueEffectAnimator.HasState(0, stateHash))
            return;

        dialogueEffectAnimator.Play(stateHash, 0, 0f);
        UpdateDialogueEffectAnimatorImmediately();
    }

    private void UpdateDialogueEffectAnimatorImmediately()
    {
        if (dialogueEffectAnimator != null && dialogueEffectAnimator.gameObject.activeInHierarchy)
            dialogueEffectAnimator.Update(0f);
    }

    /// <summary>
    /// 책임 :
    /// - DialogueEffect 인트로 클립 길이를 상태 변경 없이 조회해 프리루드의 대기 시간을 계산한다.
    /// </summary>
    private float GetDialogueEffectIntroDuration()
    {
        if (dialogueEffectAnimator == null || string.IsNullOrWhiteSpace(dialogueEffectIntroState))
            return 0f;

        AnimationClip introClip = ResolveDialogueEffectClip(dialogueEffectIntroState);
        return introClip != null ? introClip.length : 0f;
    }

    private void ResetTypingAudioTracking()
    {
        lastTypedCharacterCount = 0;
        nextTypingSoundTime = 0f;
    }

    private void HandleTypingTweenUpdated()
    {
        if (!playTypingSound || dialogueText == null)
            return;

        int currentCharacterCount = GetCurrentVisibleDialogueCharacterCount();
        if (currentCharacterCount <= lastTypedCharacterCount)
            return;

        if (Time.unscaledTime >= nextTypingSoundTime)
        {
            TypingAudioUtility.PlayBossTalking(this, gameObject);
            nextTypingSoundTime = Time.unscaledTime + typingSoundInterval;
        }

        lastTypedCharacterCount = currentCharacterCount;
    }

    private int GetCurrentVisibleDialogueCharacterCount()
    {
        if (dialogueText == null || dialogueText.textInfo == null)
            return 0;

        int totalCharacterCount = dialogueText.textInfo.characterCount;
        if (totalCharacterCount <= 0)
            return 0;

        return Mathf.Clamp(dialogueText.maxVisibleCharacters, 0, totalCharacterCount);
    }

    private AnimationClip ResolveDialogueEffectClip(string stateOrClipName)
    {
        RuntimeAnimatorController controller = dialogueEffectAnimator != null
            ? dialogueEffectAnimator.runtimeAnimatorController
            : null;

        if (controller == null)
            return null;

        if (controller is AnimatorOverrideController overrideController)
        {
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);

            foreach (KeyValuePair<AnimationClip, AnimationClip> pair in overrides)
            {
                if (!MatchesDialogueEffectClipHint(pair.Key, stateOrClipName))
                    continue;

                return pair.Value != null ? pair.Value : pair.Key;
            }

            controller = overrideController.runtimeAnimatorController;
        }

        return controller.animationClips
            .FirstOrDefault(clip => MatchesDialogueEffectClipHint(clip, stateOrClipName));
    }

    private static bool MatchesDialogueEffectClipHint(AnimationClip clip, string stateOrClipName)
    {
        if (clip == null || string.IsNullOrWhiteSpace(stateOrClipName))
            return false;

        return string.Equals(clip.name, stateOrClipName, StringComparison.OrdinalIgnoreCase)
               || clip.name.IndexOf(stateOrClipName, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SetDimPanelVisible(bool visible, bool immediate)
    {
        if (dimPanelGraphic == null)
            return;

        dimPanelGraphic.DOKill();
        dimPanelGraphic.gameObject.SetActive(visible);

        Color color = dimPanelGraphic.color;
        color.a = visible ? (immediate ? defaultDimPanelAlpha : color.a) : 0f;
        dimPanelGraphic.color = color;
    }

    private Graphic FindGraphicByName(string targetName)
    {
        return GetComponentsInChildren<Graphic>(true)
            .FirstOrDefault(graphic => string.Equals(graphic.gameObject.name, targetName, StringComparison.Ordinal));
    }

    private Transform FindChildRecursive(string targetName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, targetName, StringComparison.Ordinal))
                return child;
        }

        return null;
    }
}

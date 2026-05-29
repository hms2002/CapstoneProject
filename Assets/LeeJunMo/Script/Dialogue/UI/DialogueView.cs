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

public enum DialogueAnimType
{
    Normal,
    Slow,
    Angry,
    Whisper,
    Cold
}

internal readonly struct DialogueInlinePause
{
    public DialogueInlinePause(int characterIndex, float seconds)
    {
        CharacterIndex = Mathf.Max(0, characterIndex);
        Seconds = Mathf.Max(0f, seconds);
    }

    public int CharacterIndex { get; }
    public float Seconds { get; }
}

internal enum DialogueTextEffectType
{
    Shake,
    Tremble,
    Punch,
    Wave,
    Float
}

internal readonly struct DialogueTextEffectRange
{
    public DialogueTextEffectRange(int startCharacterIndex, int endCharacterIndex, DialogueTextEffectType effectType)
    {
        StartCharacterIndex = Mathf.Max(0, startCharacterIndex);
        EndCharacterIndex = Mathf.Max(StartCharacterIndex, endCharacterIndex);
        EffectType = effectType;
    }

    public int StartCharacterIndex { get; }
    public int EndCharacterIndex { get; }
    public DialogueTextEffectType EffectType { get; }

    public bool Contains(int characterIndex)
    {
        return characterIndex >= StartCharacterIndex && characterIndex < EndCharacterIndex;
    }
}

internal readonly struct DialogueTextRevealPlan
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

internal readonly struct DialogueTextRevealProfile
{
    public DialogueTextRevealProfile(float characterDelay, float punctuationPauseScale)
    {
        CharacterDelay = Mathf.Max(0f, characterDelay);
        PunctuationPauseScale = Mathf.Max(0f, punctuationPauseScale);
    }

    public float CharacterDelay { get; }
    public float PunctuationPauseScale { get; }
}

internal static class DialogueTextRevealUtility
{
    private const string PauseTagPrefix = "[pause=";
    private const float TextEffectSettleSeconds = 0.18f;

    private readonly struct ActiveEffectTag
    {
        public ActiveEffectTag(DialogueTextEffectType effectType, int startCharacterIndex)
        {
            EffectType = effectType;
            StartCharacterIndex = startCharacterIndex;
        }

        public DialogueTextEffectType EffectType { get; }
        public int StartCharacterIndex { get; }
    }

    public static DialogueTextRevealPlan BuildPlan(string rawText)
    {
        if (string.IsNullOrEmpty(rawText))
        {
            return new DialogueTextRevealPlan(
                string.Empty,
                new List<DialogueInlinePause>(),
                new List<DialogueTextEffectRange>());
        }

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
                    activeEffect.EffectType));
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
        return HasTextEffects(plan) ? TextEffectSettleSeconds : 0f;
    }

    public static void ApplyTextEffects(
        TMP_Text text,
        DialogueTextRevealPlan plan,
        int visibleCharacterCount,
        float elapsedSeconds)
    {
        if (text == null || !HasTextEffects(plan))
            return;

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

            for (int effectIndex = 0; effectIndex < plan.Effects.Count; effectIndex++)
            {
                DialogueTextEffectRange range = plan.Effects[effectIndex];
                if (!range.Contains(i))
                    continue;

                AccumulateTextEffect(range.EffectType, i, elapsedSeconds, ref offset, ref scale);
            }

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
        DialogueTextEffectType effectType,
        int characterIndex,
        float elapsedSeconds,
        ref Vector3 offset,
        ref float scale)
    {
        float phase = elapsedSeconds + characterIndex * 0.37f;
        switch (effectType)
        {
            case DialogueTextEffectType.Shake:
                offset.x += SignedWave(phase, 58.1f, characterIndex) * 2.2f;
                offset.y += SignedWave(phase, 71.7f, characterIndex + 11) * 1.6f;
                break;

            case DialogueTextEffectType.Tremble:
                offset.x += SignedWave(phase, 42.3f, characterIndex) * 0.9f;
                offset.y += SignedWave(phase, 49.5f, characterIndex + 5) * 0.7f;
                break;

            case DialogueTextEffectType.Punch:
                scale *= 1f + Mathf.Max(0f, Mathf.Sin(elapsedSeconds * 18f - characterIndex * 0.22f)) * 0.08f;
                offset.y += Mathf.Max(0f, Mathf.Sin(elapsedSeconds * 18f - characterIndex * 0.22f)) * 1.1f;
                break;

            case DialogueTextEffectType.Wave:
                offset.y += Mathf.Sin(elapsedSeconds * 7f + characterIndex * 0.55f) * 1.7f;
                break;

            case DialogueTextEffectType.Float:
                offset.y += Mathf.Sin(elapsedSeconds * 3.5f + characterIndex * 0.45f) * 1.2f;
                break;
        }
    }

    private static float SignedWave(float phase, float speed, int seed)
    {
        return Mathf.Sin(phase * speed + seed * 1.618f);
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
        string effectName = isClosingTag ? tag.Substring(1).Trim() : tag;

        if (!TryParseEffectType(effectName, out DialogueTextEffectType effectType))
            return false;

        consumed = closeIndex - startIndex + 1;
        if (!isClosingTag)
        {
            activeEffects.Add(new ActiveEffectTag(effectType, visibleCharacterIndex));
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
                    effectType));
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
            default:
                return false;
        }
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

public class DialogueView : MonoBehaviour
{
    private static readonly SoundRef TalkUiIntroSound = SoundRef.FromKey("sound_ui_TalkUIIntro");

    [Header("UI Groups (CanvasGroup required)")]
    [SerializeField] private CanvasGroup textBoxGroup;
    [SerializeField] private CanvasGroup affectionGroup;

    [Header("UI Presentation")]
    [SerializeField] private UISlideFadePresentation textBoxPresentation;
    [SerializeField] private UISlideFadePresentation affectionPresentation;

    [Header("Text Components")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("Continue Icon")]
    [SerializeField] private GameObject continueIcon;

    [Header("Choice UI")]
    [SerializeField] private Transform choiceContainer;
    [SerializeField] private GameObject choiceButtonPrefab;
    [SerializeField] private Color normalChoiceColor = Color.gray;
    [SerializeField] private Color selectedChoiceColor = Color.white;

    [Header("Theme")]
    [SerializeField] private Graphic[] textBoxThemeTargets;
    [SerializeField] private Graphic[] speakerFrameThemeTargets;
    [SerializeField] private Color defaultTextBoxFillColor = new Color(0f, 0f, 0f, 0.85f);
    [SerializeField] private Graphic dimPanelGraphic;
    [SerializeField] private float dimFadeDuration = 0.25f;
    [SerializeField] private float dialogueEffectIntroFallbackDuration = 0.5f;
    [SerializeField] private Animator dialogueEffectAnimator;
    [SerializeField] private string dialogueEffectIdleState = "Idle";
    [SerializeField] private string dialogueEffectIntroState = "Intro";

    [Header("Typing Audio")]
    [SerializeField] private bool playTypingSound = true;
    [SerializeField, Min(0f)] private float typingSoundInterval = 0.035f;

    private Coroutine typingRoutine;
    private Coroutine textEffectRoutine;
    private Tween continueIconTween;
    private RectTransform continueIconRect;
    private Vector2 continueIconBaseAnchoredPosition;
    private bool hasContinueIconBasePosition;
    private readonly List<GameObject> activeChoiceButtons = new List<GameObject>();
    private readonly Dictionary<Graphic, Material> originalThemeMaterials = new Dictionary<Graphic, Material>();
    private readonly Dictionary<Graphic, Color> originalThemeColors = new Dictionary<Graphic, Color>();
    private readonly Dictionary<Outline, Color> originalOutlineColors = new Dictionary<Outline, Color>();
    private readonly Dictionary<Graphic, Material> runtimeThemeMaterials = new Dictionary<Graphic, Material>();

    private DialogueThemeSO currentTheme;
    private DialogueThemeSO currentEffectTheme;
    private RuntimeAnimatorController defaultEffectController;
    private Color defaultNameTextColor;
    private float defaultDimPanelAlpha;
    private bool isUiVisible;
    private bool choiceInputEnabled;
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

        ResetDialogueEffectToHiddenIdle();
        if (dialogueEffectAnimator != null)
            dialogueEffectAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

        ResolveGroupPresentations();
        SnapGroupClosed(textBoxGroup, textBoxPresentation);
        SnapGroupClosed(affectionGroup, affectionPresentation);

        if (continueIcon != null)
        {
            continueIcon.SetActive(false);
            CacheContinueIconTransform();
        }

        ClearChoices();
        ClearText();
    }

    private void OnEnable()
    {
        StartContinueIconMotion();
    }

    private void OnDisable()
    {
        StopContinueIconMotion(true);
    }

    private void OnDestroy()
    {
        StopRuntimeTweens();

        foreach (Material runtimeMaterial in runtimeThemeMaterials.Values)
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }

        runtimeThemeMaterials.Clear();
    }

    public void ClearText()
    {
        StopTypingRoutine();
        StopTextEffectRoutine(true);
        ResetTypingAudioTracking();

        if (nameText != null)
            nameText.text = string.Empty;

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

        RestoreThemeVisuals();

        if (theme == null)
        {
            RefreshDialogueEffectOverride();
            return;
        }

        ApplyThemeToTargets(textBoxThemeTargets, defaultTextBoxFillColor, theme.outlineColor);
        ApplyThemeToTargets(speakerFrameThemeTargets, theme.speakerFrameFillColor, theme.outlineColor);
        if (nameText != null)
            nameText.color = theme.outlineColor;
        RefreshDialogueEffectOverride();
    }

    public void ResetTheme()
    {
        currentTheme = null;
        currentEffectTheme = null;
        RestoreThemeVisuals();
        if (nameText != null)
            nameText.color = defaultNameTextColor;
        ResetDialogueEffectOverride();
        ResetDialogueEffectToHiddenIdle();
    }

    public void ShowUI(bool isBoss, Action onComplete = null)
    {
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

        if (isBoss && affectionGroup != null)
        {
            RegisterAnimation();
            PlayGroupOpen(affectionGroup, affectionPresentation, CompleteAnimation);
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
            SetDialogueEffectVisible(true);
            PlayDialogueEffectIntro();
        });

        seq.AppendInterval(effectDuration);
        seq.OnComplete(() => onComplete?.Invoke());
    }

    public void TypeText(string speakerName, string text, Action onComplete = null)
    {
        TypeText(speakerName, text, DialogueAnimType.Normal, onComplete);
    }

    public void TypeText(
        string speakerName,
        string text,
        DialogueAnimType animType,
        Action onComplete = null)
    {
        if (nameText != null)
            nameText.text = speakerName;

        if (continueIcon != null)
            continueIcon.SetActive(false);

        StopTypingRoutine();
        StopTextEffectRoutine(true);
        ResetTypingAudioTracking();

        if (dialogueText == null)
            return;

        DialogueTextRevealPlan revealPlan = DialogueTextRevealUtility.BuildPlan(text);
        string lineText = revealPlan.DisplayText;
        dialogueText.richText = true;
        dialogueText.text = lineText;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();

        int visibleCharacterCount = dialogueText.textInfo != null
            ? dialogueText.textInfo.characterCount
            : 0;

        if (visibleCharacterCount <= 0)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;

            if (continueIcon != null)
                continueIcon.SetActive(true);

            onComplete?.Invoke();
            return;
        }

        typingRoutine = StartCoroutine(TypeTextRoutine(revealPlan, animType, visibleCharacterCount, onComplete));
    }

    public void SkipTyping(string fullText)
    {
        StopTypingRoutine();

        if (dialogueText != null)
        {
            DialogueTextRevealPlan revealPlan = DialogueTextRevealUtility.BuildPlan(fullText);
            dialogueText.richText = true;
            dialogueText.text = revealPlan.DisplayText;
            dialogueText.ForceMeshUpdate();
            dialogueText.maxVisibleCharacters = dialogueText.textInfo != null
                ? dialogueText.textInfo.characterCount
                : int.MaxValue;

            StartTextEffectRoutine(revealPlan, GetCurrentVisibleDialogueCharacterCount());
        }

        lastTypedCharacterCount = GetCurrentVisibleDialogueCharacterCount();

        if (continueIcon != null)
            continueIcon.SetActive(true);
    }

    private IEnumerator TypeTextRoutine(
        DialogueTextRevealPlan revealPlan,
        DialogueAnimType animType,
        int visibleCharacterCount,
        Action onComplete)
    {
        DialogueTextRevealProfile profile = DialogueTextRevealUtility.ResolveProfile(animType);

        for (int i = 0; i < visibleCharacterCount; i++)
        {
            float explicitPause = DialogueTextRevealUtility.GetPauseBeforeCharacter(revealPlan, i);
            if (explicitPause > 0f)
                yield return WaitForTextRevealDelay(revealPlan, i, explicitPause);

            if (dialogueText == null)
                yield break;

            dialogueText.maxVisibleCharacters = i + 1;
            HandleTypingTweenUpdated();
            DialogueTextRevealUtility.ApplyTextEffects(
                dialogueText,
                revealPlan,
                i + 1,
                Time.unscaledTime);

            float delay = DialogueTextRevealUtility.GetPostCharacterDelay(
                profile,
                dialogueText.textInfo,
                i);

            if (delay > 0f)
                yield return WaitForTextRevealDelay(revealPlan, i + 1, delay);
        }

        float settleSeconds = DialogueTextRevealUtility.GetTextEffectSettleSeconds(revealPlan);
        if (settleSeconds > 0f)
            yield return WaitForTextRevealDelay(revealPlan, visibleCharacterCount, settleSeconds);

        typingRoutine = null;

        if (dialogueText != null)
        {
            dialogueText.maxVisibleCharacters = visibleCharacterCount;
            StartTextEffectRoutine(revealPlan, visibleCharacterCount);
            HandleTypingTweenUpdated();
        }

        if (continueIcon != null)
            continueIcon.SetActive(true);

        onComplete?.Invoke();
    }

    private IEnumerator WaitForTextRevealDelay(
        DialogueTextRevealPlan revealPlan,
        int visibleCharacterCount,
        float seconds)
    {
        if (seconds <= 0f)
            yield break;

        if (!DialogueTextRevealUtility.HasTextEffects(revealPlan))
        {
            yield return new WaitForSecondsRealtime(seconds);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (dialogueText == null)
                yield break;

            DialogueTextRevealUtility.ApplyTextEffects(
                dialogueText,
                revealPlan,
                visibleCharacterCount,
                Time.unscaledTime);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    public bool ShowChoices(List<Ink.Runtime.Choice> choices, Action<int> onChoiceSelected)
    {
        ClearChoices();

        if (continueIcon != null)
            continueIcon.SetActive(false);

        if (choiceContainer == null || choiceButtonPrefab == null)
        {
            Debug.LogError("[DialogueView] choiceContainer or choiceButtonPrefab is missing. Cannot display dialogue choices.", this);
            return false;
        }

        onChoiceSelectedCallback = onChoiceSelected;
        choiceInputEnabled = false;
        currentChoiceIndex = -1;
        EventSystem.current?.SetSelectedGameObject(null);

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
                choiceHighlight.SetSelected(false, true);

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
                    if (!choiceInputEnabled)
                        return;

                    Action<int> callback = onChoiceSelectedCallback;
                    ClearChoices();
                    callback?.Invoke(index);
                });
            }
        }

        HighlightChoice(currentChoiceIndex);
        return true;
    }

    public void ChangeChoiceSelection(int direction)
    {
        if (activeChoiceButtons.Count == 0)
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
        if (!choiceInputEnabled || activeChoiceButtons.Count <= 0)
            return;

        if (currentChoiceIndex < 0 || currentChoiceIndex >= activeChoiceButtons.Count)
            return;

        Button selectedBtn = activeChoiceButtons[currentChoiceIndex].GetComponent<Button>();
        selectedBtn?.onClick.Invoke();
    }

    public void ConfirmChoiceAt(int index)
    {
        if (!choiceInputEnabled || index < 0 || index >= activeChoiceButtons.Count)
            return;

        currentChoiceIndex = index;
        HighlightChoice(currentChoiceIndex);

        Button selectedBtn = activeChoiceButtons[currentChoiceIndex].GetComponent<Button>();
        selectedBtn?.onClick.Invoke();
    }

    public void SetChoiceInputEnabled(bool enabled)
    {
        choiceInputEnabled = enabled;
    }

    public void SelectChoiceFromPointer(int index)
    {
        if (!choiceInputEnabled)
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
        choiceInputEnabled = false;
        currentChoiceIndex = -1;

        foreach (GameObject btn in activeChoiceButtons)
        {
            if (btn != null)
            {
                DialogueChoiceHighlightPresentation choiceHighlight =
                    btn.GetComponent<DialogueChoiceHighlightPresentation>();
                if (choiceHighlight != null)
                    choiceHighlight.SetSelected(false, true);

                Destroy(btn);
            }
        }

        activeChoiceButtons.Clear();
        onChoiceSelectedCallback = null;
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private void CacheContinueIconTransform()
    {
        if (continueIcon == null)
            return;

        continueIconRect = continueIcon.GetComponent<RectTransform>();
        if (continueIconRect == null)
            return;

        continueIconBaseAnchoredPosition = continueIconRect.anchoredPosition;
        hasContinueIconBasePosition = true;
    }

    private void StartContinueIconMotion()
    {
        if (continueIconRect == null)
            CacheContinueIconTransform();

        if (continueIconRect == null)
            return;

        StopContinueIconMotion(true);
        continueIconTween = continueIconRect
            .DOAnchorPosY(continueIconBaseAnchoredPosition.y - 10f, 0.5f)
            .SetUpdate(true)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }

    private void StopContinueIconMotion(bool resetPosition)
    {
        continueIconTween?.Kill();
        continueIconTween = null;

        if (continueIconRect != null)
            continueIconRect.DOKill();

        if (resetPosition && continueIconRect != null && hasContinueIconBasePosition)
            continueIconRect.anchoredPosition = continueIconBaseAnchoredPosition;
    }

    private void StopRuntimeTweens()
    {
        StopTypingRoutine();
        StopTextEffectRoutine(true);

        StopContinueIconMotion(true);

        if (dialogueText != null)
            dialogueText.DOKill();

        if (textBoxGroup != null)
            textBoxGroup.DOKill();

        if (affectionGroup != null)
            affectionGroup.DOKill();

        if (dimPanelGraphic != null)
            dimPanelGraphic.DOKill();

        foreach (GameObject choiceButton in activeChoiceButtons)
        {
            if (choiceButton != null)
                choiceButton.transform.DOKill();
        }
    }

    private void StopTypingRoutine()
    {
        if (typingRoutine == null)
            return;

        StopCoroutine(typingRoutine);
        typingRoutine = null;
    }

    private void StartTextEffectRoutine(DialogueTextRevealPlan revealPlan, int visibleCharacterCount)
    {
        StopTextEffectRoutine(true);

        if (!DialogueTextRevealUtility.HasTextEffects(revealPlan) || dialogueText == null)
            return;

        textEffectRoutine = StartCoroutine(PlayTextEffectRoutine(revealPlan, visibleCharacterCount));
    }

    private IEnumerator PlayTextEffectRoutine(DialogueTextRevealPlan revealPlan, int visibleCharacterCount)
    {
        while (dialogueText != null)
        {
            DialogueTextRevealUtility.ApplyTextEffects(
                dialogueText,
                revealPlan,
                visibleCharacterCount,
                Time.unscaledTime);

            yield return null;
        }
    }

    private void StopTextEffectRoutine(bool resetText)
    {
        if (textEffectRoutine != null)
        {
            StopCoroutine(textEffectRoutine);
            textEffectRoutine = null;
        }

        if (resetText)
            DialogueTextRevealUtility.ResetTextEffects(dialogueText);
    }

    public void HideUI(Action onComplete = null)
    {
        ClearChoices();

        if (continueIcon != null)
            continueIcon.SetActive(false);

        ResolveGroupPresentations();

        int pendingAnimations = 0;
        bool didComplete = false;
        bool startedAllAnimations = false;

        void FinishHide()
        {
            isUiVisible = false;

            SetDimPanelVisible(false, true);
            ResetDialogueEffectToHiddenIdle();
            onComplete?.Invoke();
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
            FinishHide();
        }

        if (textBoxGroup != null && textBoxGroup.gameObject.activeSelf)
        {
            RegisterAnimation();
            PlayGroupClose(textBoxGroup, textBoxPresentation, CompleteAnimation);
        }

        if (affectionGroup != null && affectionGroup.gameObject.activeSelf)
        {
            RegisterAnimation();
            PlayGroupClose(affectionGroup, affectionPresentation, CompleteAnimation);
        }

        startedAllAnimations = true;
        if (pendingAnimations == 0 && !didComplete)
        {
            didComplete = true;
            FinishHide();
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

    private void ResolveGroupPresentations()
    {
        if (textBoxPresentation == null)
            textBoxPresentation = ResolveGroupPresentation(textBoxGroup);

        if (affectionPresentation == null)
            affectionPresentation = ResolveGroupPresentation(affectionGroup);
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

            TextMeshProUGUI btnText = choiceButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
                btnText.color = isSelected ? selectedChoiceColor : normalChoiceColor;

            DialogueChoiceHighlightPresentation choiceHighlight =
                choiceButton.GetComponent<DialogueChoiceHighlightPresentation>();
            if (choiceHighlight != null)
                choiceHighlight.SetSelected(isSelected);

            choiceButton.transform.DOScale(isSelected ? 1.05f : 1.0f, 0.1f).SetUpdate(true);
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

        if (dimPanelGraphic == null)
            dimPanelGraphic = FindGraphicByName("DimPanel");

        if (textBoxThemeTargets == null || textBoxThemeTargets.Length == 0)
        {
            Graphic textBoxGraphic = FindGraphicByName("TextBoxGroup");
            if (textBoxGraphic != null)
                textBoxThemeTargets = new[] { textBoxGraphic };
        }

        if (speakerFrameThemeTargets == null || speakerFrameThemeTargets.Length == 0)
        {
            Graphic speakerFrameGraphic = FindGraphicByName("SpeakerFrame");
            if (speakerFrameGraphic != null)
                speakerFrameThemeTargets = new[] { speakerFrameGraphic };
        }
    }

    private void CacheThemeDefaults()
    {
        foreach (Graphic graphic in EnumerateThemeTargets())
        {
            if (graphic == null)
                continue;

            if (!originalThemeMaterials.ContainsKey(graphic))
                originalThemeMaterials[graphic] = graphic.material;

            if (!originalThemeColors.ContainsKey(graphic))
                originalThemeColors[graphic] = graphic.color;

            foreach (Outline outline in graphic.GetComponents<Outline>())
            {
                if (outline != null && !originalOutlineColors.ContainsKey(outline))
                    originalOutlineColors[outline] = outline.effectColor;
            }
        }
    }

    private IEnumerable<Graphic> EnumerateThemeTargets()
    {
        HashSet<Graphic> uniqueTargets = new HashSet<Graphic>();

        if (textBoxThemeTargets != null)
        {
            foreach (Graphic graphic in textBoxThemeTargets)
            {
                if (graphic != null && uniqueTargets.Add(graphic))
                    yield return graphic;
            }
        }

        if (speakerFrameThemeTargets != null)
        {
            foreach (Graphic graphic in speakerFrameThemeTargets)
            {
                if (graphic != null && uniqueTargets.Add(graphic))
                    yield return graphic;
            }
        }
    }

    private void ApplyThemeToTargets(Graphic[] targets, Color fillColor, Color outlineColor)
    {
        if (targets == null)
            return;

        foreach (Graphic graphic in targets)
        {
            if (graphic == null)
                continue;

            Material themedMaterial = GetOrCreateThemeMaterial(graphic);
            if (themedMaterial != null)
            {
                if (themedMaterial.HasProperty("_OutlineColor"))
                    themedMaterial.SetColor("_OutlineColor", outlineColor);

                graphic.material = themedMaterial;
            }

            graphic.color = fillColor;

            foreach (Outline outline in graphic.GetComponents<Outline>())
            {
                if (outline != null)
                    outline.effectColor = outlineColor;
            }
        }
    }

    private Material GetOrCreateThemeMaterial(Graphic graphic)
    {
        if (graphic == null)
            return null;

        if (runtimeThemeMaterials.TryGetValue(graphic, out Material cachedMaterial) && cachedMaterial != null)
            return cachedMaterial;

        originalThemeMaterials.TryGetValue(graphic, out Material originalMaterial);
        Material themeMaterial = null;

        if (originalMaterial != null && originalMaterial.HasProperty("_OutlineColor"))
        {
            themeMaterial = new Material(originalMaterial);
        }
        else
        {
            Shader outlineShader = Shader.Find("UI/Alpha Outline");
            if (outlineShader != null)
                themeMaterial = new Material(outlineShader);
            else if (originalMaterial != null)
                themeMaterial = new Material(originalMaterial);
        }

        if (themeMaterial != null)
            runtimeThemeMaterials[graphic] = themeMaterial;

        return themeMaterial;
    }

    private void RestoreThemeVisuals()
    {
        foreach (Graphic graphic in EnumerateThemeTargets())
        {
            if (graphic == null)
                continue;

            if (originalThemeMaterials.TryGetValue(graphic, out Material originalMaterial))
                graphic.material = originalMaterial;

            if (originalThemeColors.TryGetValue(graphic, out Color originalColor))
                graphic.color = originalColor;

            foreach (Outline outline in graphic.GetComponents<Outline>())
            {
                if (outline != null && originalOutlineColors.TryGetValue(outline, out Color originalOutlineColor))
                    outline.effectColor = originalOutlineColor;
            }
        }
    }

    private void RefreshThemePresentation(bool restartEffect)
    {
        if (currentTheme == null)
        {
            RestoreThemeVisuals();
            if (nameText != null)
                nameText.color = defaultNameTextColor;

            RefreshDialogueEffectOverride();
            if (restartEffect)
                ResetDialogueEffectToHiddenIdle();
            return;
        }

        ApplyThemeToTargets(textBoxThemeTargets, defaultTextBoxFillColor, currentTheme.outlineColor);
        ApplyThemeToTargets(speakerFrameThemeTargets, currentTheme.speakerFrameFillColor, currentTheme.outlineColor);

        if (nameText != null)
            nameText.color = currentTheme.outlineColor;

        RefreshDialogueEffectOverride();

        if (restartEffect)
            PlayDialogueEffectIntro();
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
        dialogueEffectAnimator.Update(0f);
    }

    private void ResetDialogueEffectOverride()
    {
        if (dialogueEffectAnimator == null || defaultEffectController == null)
            return;

        if (dialogueEffectAnimator.runtimeAnimatorController == defaultEffectController)
            return;

        dialogueEffectAnimator.runtimeAnimatorController = defaultEffectController;
        dialogueEffectAnimator.Rebind();
        dialogueEffectAnimator.Update(0f);
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

        SetDialogueEffectVisible(true);
        PlayDialogueEffectIdle();
        SetDialogueEffectVisible(false);
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

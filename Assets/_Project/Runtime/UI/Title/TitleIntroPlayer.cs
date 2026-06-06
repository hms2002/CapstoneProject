using System;
using System.Collections;
using CapstoneAudio;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TitleIntroPlayer : MonoBehaviour
{
    private const float TypingSoundSkipFadeOutSeconds = 0.2f;

    [Header("Data")]
    [SerializeField] private TitleIntroSequenceSO sequence;

    [Header("View")]
    [SerializeField] private TitleIntroView view;
    [SerializeField] private bool hideViewOnAwake = true;
    [SerializeField] private bool hideViewWhenCompleted;

    [Header("Input")]
    [SerializeField] private KeyCode skipKey = KeyCode.Space;

    [Header("Typing Audio")]
    [SerializeField] private bool playTypingSound = true;
    [SerializeField, Min(0f)] private float typingSoundInterval = 0.035f;

    private Coroutine playRoutine;
    private float skipHoldElapsed;
    private bool skipKeyWasHeld;
    private bool skipIntroRequested;
    private float nextTypingSoundTime;
    private bool typingStartSoundPlayed;
    private AudioHandle typingSoundHandle;
    private int consumedAdvanceInputFrame = -1;
    private bool hasHiddenCursor;

    public bool IsPlaying => playRoutine != null;

    public bool CanPlay
    {
        get
        {
            return isActiveAndEnabled &&
                   sequence != null &&
                   sequence.SlideCount > 0 &&
                   view != null &&
                   view.IsReady;
        }
    }

    private void Awake()
    {
        if (hideViewOnAwake)
            HideViewIfAlive();
    }

    private void OnDisable()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        StopTypingSoundImmediate();
        ResetInputState();
        ReleaseCursorHidden();
        HideViewIfAlive();
    }

    public bool TryPlay(Action onCompleted, bool keepViewVisibleOnCompleted = false)
    {
        if (IsPlaying)
            return false;

        if (!CanPlay)
        {
            Debug.LogWarning(
                "[TitleIntroPlayer] Intro cannot play because sequence or view references are missing.",
                this);
            return false;
        }

        playRoutine = StartCoroutine(PlayRoutine(onCompleted, keepViewVisibleOnCompleted));
        return true;
    }

    public void HideViewImmediate()
    {
        HideViewIfAlive();
    }

    private IEnumerator PlayRoutine(Action onCompleted, bool keepViewVisibleOnCompleted)
    {
        skipIntroRequested = false;
        ResetInputState();
        AcquireCursorHidden();
        view.Show(skipKey);
        view.ApplySkipFillColor(sequence.SkipFillColor);
        view.SetRootAlpha(0f);
        view.SetSkipPromptAlpha(0f);

        yield return FadeIntroRootAsync(1f, sequence.IntroStartFadeDuration);
        if (skipIntroRequested)
        {
            CompletePlayback(onCompleted, keepViewVisibleOnCompleted);
            yield break;
        }

        for (int i = 0; i < sequence.SlideCount; i++)
        {
            TitleIntroSlide slide = sequence.GetSlide(i);
            if (slide == null)
                continue;

            view.SetSlideSprite(slide.Image);
            view.SetSlideAlpha(0f);

            string text = slide.Text;
            float fadeInDuration = i == 0
                ? sequence.InitialImageFadeDuration
                : sequence.ImageFadeDuration;
            yield return TypeTextAndFadeInAsync(
                text,
                fadeInDuration,
                ignoreAdvanceUntilFadeComplete: i == 0,
                fadeSkipPromptWithSlide: i == 0);
            if (skipIntroRequested)
                break;

            yield return WaitAfterTextAsync(sequence.GetPostTextWaitSeconds(text));
            if (skipIntroRequested)
                break;

            bool isLastSlide = i >= sequence.SlideCount - 1;
            if (!isLastSlide)
            {
                yield return FadeSlideAsync(0f, sequence.ImageFadeDuration);
                if (skipIntroRequested)
                    break;
            }
        }

        CompletePlayback(onCompleted, keepViewVisibleOnCompleted);
    }

    private IEnumerator FadeIntroRootAsync(float targetAlpha, float duration)
    {
        float startAlpha = view.RootAlpha;
        if (duration <= 0f)
        {
            view.SetRootAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            TitleIntroInputCommand command = PollInput();
            if (command == TitleIntroInputCommand.SkipIntro)
            {
                skipIntroRequested = true;
                FadeTypingSoundOnSkipRequest();
                view.SetRootAlpha(targetAlpha);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            view.SetRootAlpha(Mathf.Lerp(startAlpha, targetAlpha, normalized));
            yield return null;
        }

        view.SetRootAlpha(targetAlpha);
    }

    private IEnumerator TypeTextAndFadeInAsync(
        string text,
        float fadeDuration,
        bool ignoreAdvanceUntilFadeComplete,
        bool fadeSkipPromptWithSlide)
    {
        text ??= string.Empty;
        view.SetText(string.Empty);
        nextTypingSoundTime = 0f;
        typingStartSoundPlayed = false;

        float secondsPerCharacter = sequence.SecondsPerCharacter;
        bool textComplete = text.Length == 0;
        if (!textComplete && secondsPerCharacter <= 0f)
        {
            view.SetText(text);
            TryPlayTypingSound();
            textComplete = true;
        }

        int visibleCharacters = 0;
        float elapsedForNextCharacter = 0f;
        float fadeElapsed = 0f;
        float startAlpha = view.SlideAlpha;
        bool fadeComplete = fadeDuration <= 0f;
        if (fadeComplete)
        {
            view.SetSlideAlpha(1f);
            if (fadeSkipPromptWithSlide)
                view.SetSkipPromptAlpha(1f);
        }

        while (!textComplete || !fadeComplete)
        {
            TitleIntroInputCommand command = PollInput();
            if (command == TitleIntroInputCommand.SkipIntro)
            {
                skipIntroRequested = true;
                FadeTypingSoundOnSkipRequest();
                view.SetText(text);
                view.SetSlideAlpha(1f);
                if (fadeSkipPromptWithSlide)
                    view.SetSkipPromptAlpha(1f);
                yield break;
            }

            if (command == TitleIntroInputCommand.Advance)
            {
                bool canAdvance = !ignoreAdvanceUntilFadeComplete || fadeComplete;
                if (canAdvance)
                {
                    view.SetText(text);
                    view.SetSlideAlpha(1f);
                    if (fadeSkipPromptWithSlide)
                        view.SetSkipPromptAlpha(1f);
                    yield break;
                }
            }

            float deltaTime = Time.unscaledDeltaTime;
            if (!textComplete)
            {
                elapsedForNextCharacter += deltaTime;
                bool changed = false;
                while (elapsedForNextCharacter >= secondsPerCharacter && visibleCharacters < text.Length)
                {
                    elapsedForNextCharacter -= secondsPerCharacter;
                    visibleCharacters++;
                    changed = true;
                }

                if (changed)
                {
                    view.SetText(text.Substring(0, visibleCharacters));
                    TryPlayTypingSound();
                }

                textComplete = visibleCharacters >= text.Length;
            }

            if (!fadeComplete)
            {
                fadeElapsed += deltaTime;
                float normalized = Mathf.Clamp01(fadeElapsed / fadeDuration);
                view.SetSlideAlpha(Mathf.Lerp(startAlpha, 1f, normalized));
                if (fadeSkipPromptWithSlide)
                    view.SetSkipPromptAlpha(normalized);
                fadeComplete = fadeElapsed >= fadeDuration;
            }

            yield return null;
        }

        view.SetText(text);
        view.SetSlideAlpha(1f);
        if (fadeSkipPromptWithSlide)
            view.SetSkipPromptAlpha(1f);
    }

    private IEnumerator WaitAfterTextAsync(float waitSeconds)
    {
        float elapsed = 0f;
        while (elapsed < waitSeconds)
        {
            TitleIntroInputCommand command = PollInput();
            if (command == TitleIntroInputCommand.SkipIntro)
            {
                skipIntroRequested = true;
                FadeTypingSoundOnSkipRequest();
                yield break;
            }

            if (command == TitleIntroInputCommand.Advance)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator FadeSlideAsync(float targetAlpha, float duration)
    {
        float startAlpha = view.SlideAlpha;
        if (duration <= 0f)
        {
            view.SetSlideAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            TitleIntroInputCommand command = PollInput();
            if (command == TitleIntroInputCommand.SkipIntro)
            {
                skipIntroRequested = true;
                FadeTypingSoundOnSkipRequest();
                yield break;
            }

            if (command == TitleIntroInputCommand.Advance)
                break;

            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            view.SetSlideAlpha(Mathf.Lerp(startAlpha, targetAlpha, normalized));
            yield return null;
        }

        view.SetSlideAlpha(targetAlpha);
    }

    private TitleIntroInputCommand PollInput()
    {
        if (InputKeyCompatibility.IsPressed(skipKey))
        {
            skipKeyWasHeld = true;
            skipHoldElapsed += Time.unscaledDeltaTime;

            float skipHoldSeconds = sequence != null ? sequence.SkipHoldSeconds : 0f;
            if (skipHoldSeconds <= 0f)
            {
                view.SetSkipFill(1f);
                return TitleIntroInputCommand.SkipIntro;
            }

            float normalized = Mathf.Clamp01(skipHoldElapsed / skipHoldSeconds);
            view.SetSkipFill(normalized);
            if (skipHoldElapsed >= skipHoldSeconds)
                return TitleIntroInputCommand.SkipIntro;
        }
        else
        {
            bool wasIntroSpacePress = skipKeyWasHeld;
            bool wasReleased = InputKeyCompatibility.WasReleasedThisFrame(skipKey);
            ResetHoldInputState();

            if (wasIntroSpacePress && wasReleased && !WasAdvanceInputConsumedThisFrame())
                return ConsumeAdvanceInput();
        }

        if (Input.GetMouseButtonDown(0) && !WasAdvanceInputConsumedThisFrame())
            return ConsumeAdvanceInput();

        return TitleIntroInputCommand.None;
    }

    private void ResetInputState()
    {
        consumedAdvanceInputFrame = -1;
        ResetHoldInputState();
    }

    private void ResetHoldInputState()
    {
        skipHoldElapsed = 0f;
        skipKeyWasHeld = false;
        SetSkipFillIfViewAlive(0f);
    }

    private bool WasAdvanceInputConsumedThisFrame()
    {
        return consumedAdvanceInputFrame == Time.frameCount;
    }

    private TitleIntroInputCommand ConsumeAdvanceInput()
    {
        consumedAdvanceInputFrame = Time.frameCount;
        return TitleIntroInputCommand.Advance;
    }

    private void TryPlayTypingSound()
    {
        if (!playTypingSound || typingStartSoundPlayed || Time.unscaledTime < nextTypingSoundTime)
            return;

        typingStartSoundPlayed = true;
        typingSoundHandle = TypingAudioUtility.PlayIntroOutroPencil(this, gameObject);
        nextTypingSoundTime = Time.unscaledTime + Mathf.Max(0f, typingSoundInterval);
    }

    private void FadeTypingSoundOnSkipRequest()
    {
        if (!typingSoundHandle.IsValid)
            return;

        SoundPlaybackUtility.Stop(typingSoundHandle, TypingSoundSkipFadeOutSeconds);
        typingSoundHandle = AudioHandle.Invalid;
    }

    private void StopTypingSoundImmediate()
    {
        if (!typingSoundHandle.IsValid)
            return;

        SoundPlaybackUtility.Stop(typingSoundHandle);
        typingSoundHandle = AudioHandle.Invalid;
    }

    private void AcquireCursorHidden()
    {
        MouseCursorService service = MouseCursorService.EnsureInstance();
        if (service == null)
            return;

        service.SetHidden(this, true);
        hasHiddenCursor = true;
    }

    private void ReleaseCursorHidden()
    {
        if (!hasHiddenCursor)
            return;

        MouseCursorService.Instance?.SetHidden(this, false);
        hasHiddenCursor = false;
    }

    private void CompletePlayback(Action onCompleted, bool keepViewVisibleOnCompleted)
    {
        playRoutine = null;
        ResetInputState();
        ReleaseCursorHidden();

        if (hideViewWhenCompleted && !keepViewVisibleOnCompleted)
            HideViewIfAlive();

        onCompleted?.Invoke();
    }

    private bool HasLiveView()
    {
        return view != null;
    }

    private void HideViewIfAlive()
    {
        if (HasLiveView())
            view.HideImmediate();
    }

    private void SetSkipFillIfViewAlive(float normalized)
    {
        if (HasLiveView())
            view.SetSkipFill(normalized);
    }

    private enum TitleIntroInputCommand
    {
        None,
        Advance,
        SkipIntro
    }
}

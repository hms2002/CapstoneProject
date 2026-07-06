using System;
using System.Collections;
using CapstoneAudio;
using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// 책임 : 엔딩 아웃트로 시퀀스 재생, 스킵 입력, view 탐색과 표시 상태를 관리한다.
/// </summary>
public sealed class EndingOutroPlayer : MonoBehaviour
{
    private const float TypingSoundSkipFadeOutSeconds = 0.2f;

    [Header("Data")]
    [SerializeField] private EndingOutroSequenceSO sequence;

    [Header("View")]
    [SerializeField] private MonoBehaviour view;
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
    private bool skipHoldWasPressedByKey;
    private bool skipHoldWasPressedByMouse;
    private bool skipOutroRequested;
    private float nextTypingSoundTime;
    private bool typingStartSoundPlayed;
    private AudioHandle typingSoundHandle;
    private int consumedAdvanceInputFrame = -1;
    private bool hasHiddenCursor;

    public bool IsPlaying => playRoutine != null;
    private IEndingOutroView View => view != null ? view as IEndingOutroView : null;

    public bool CanPlay
    {
        get
        {
            ResolveRuntimeReferences();
            IEndingOutroView resolvedView = View;
            return isActiveAndEnabled &&
                   sequence != null &&
                   sequence.SlideCount > 0 &&
                   resolvedView != null &&
                   resolvedView.IsReady;
        }
    }

    private void Awake()
    {
        ResolveRuntimeReferences();

        if (hideViewOnAwake)
            HideViewIfAlive();
    }

    private void OnDisable()
    {
        StopPlayback(hideView: true);
    }

    public bool TryPlay(Action onCompleted, bool keepViewVisibleOnCompleted = false)
    {
        if (IsPlaying)
            return false;

        ResolveRuntimeReferences();

        if (!CanPlay)
        {
            Debug.LogWarning(
                "[EndingOutroPlayer] Outro cannot play because sequence or view references are missing.",
                this);
            return false;
        }

        playRoutine = StartCoroutine(PlayRoutine(onCompleted, keepViewVisibleOnCompleted));
        return true;
    }

    public void StopPlayback(bool hideView)
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        StopTypingSoundImmediate();
        ResetInputState();
        ReleaseCursorHidden();

        if (hideView)
            HideViewIfAlive();
    }

    public void HideViewImmediate()
    {
        HideViewIfAlive();
    }

    public bool ResolveRuntimeReferences()
    {
        IEndingOutroView resolvedView = View;
        if (resolvedView == null || !resolvedView.IsReady)
            view = FindGlobalRootOutroView();

        resolvedView = View;
        if (resolvedView == null || !resolvedView.IsReady)
            view = FindSingleSceneOutroView();

        resolvedView = View;
        return resolvedView != null && resolvedView.IsReady;
    }

    private IEnumerator PlayRoutine(Action onCompleted, bool keepViewVisibleOnCompleted)
    {
        IEndingOutroView resolvedView = View;
        if (resolvedView == null)
            yield break;

        skipOutroRequested = false;
        ResetInputState();
        AcquireCursorHidden();
        resolvedView.Show(skipKey);
        resolvedView.ApplySkipFillColor(sequence.SkipFillColor);
        resolvedView.SetRootAlpha(0f);
        resolvedView.SetSkipPromptAlpha(0f);

        yield return FadeOutroRootAsync(1f, sequence.OutroStartFadeDuration);
        if (skipOutroRequested)
        {
            CompletePlayback(onCompleted, keepViewVisibleOnCompleted);
            yield break;
        }

        for (int i = 0; i < sequence.SlideCount; i++)
        {
            EndingOutroSlide slide = sequence.GetSlide(i);
            if (slide == null)
                continue;

            resolvedView.SetSlideSprite(slide.Image);
            resolvedView.SetSlideAlpha(0f);

            string text = slide.Text;
            float fadeInDuration = i == 0
                ? sequence.InitialImageFadeDuration
                : sequence.ImageFadeDuration;
            yield return TypeTextAndFadeInAsync(
                text,
                fadeInDuration,
                ignoreAdvanceUntilFadeComplete: i == 0,
                fadeSkipPromptWithSlide: i == 0);
            if (skipOutroRequested)
                break;

            yield return WaitAfterTextAsync(sequence.GetPostTextWaitSeconds(text));
            if (skipOutroRequested)
                break;

            bool isLastSlide = i >= sequence.SlideCount - 1;
            if (!isLastSlide)
            {
                yield return FadeSlideAsync(0f, sequence.ImageFadeDuration);
                if (skipOutroRequested)
                    break;
            }
        }

        CompletePlayback(onCompleted, keepViewVisibleOnCompleted);
    }

    private IEnumerator FadeOutroRootAsync(float targetAlpha, float duration)
    {
        IEndingOutroView resolvedView = View;
        if (resolvedView == null)
            yield break;

        float startAlpha = resolvedView.RootAlpha;
        if (duration <= 0f)
        {
            resolvedView.SetRootAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            EndingOutroInputCommand command = PollInput();
            if (command == EndingOutroInputCommand.SkipOutro)
            {
                skipOutroRequested = true;
                FadeTypingSoundOnSkipRequest();
                resolvedView.SetRootAlpha(targetAlpha);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            resolvedView.SetRootAlpha(Mathf.Lerp(startAlpha, targetAlpha, normalized));
            yield return null;
        }

        resolvedView.SetRootAlpha(targetAlpha);
    }

    private IEnumerator TypeTextAndFadeInAsync(
        string text,
        float fadeDuration,
        bool ignoreAdvanceUntilFadeComplete,
        bool fadeSkipPromptWithSlide)
    {
        IEndingOutroView resolvedView = View;
        if (resolvedView == null)
            yield break;

        text ??= string.Empty;
        resolvedView.SetText(string.Empty);
        nextTypingSoundTime = 0f;
        typingStartSoundPlayed = false;

        float secondsPerCharacter = sequence.SecondsPerCharacter;
        bool textComplete = text.Length == 0;
        if (!textComplete && secondsPerCharacter <= 0f)
        {
            resolvedView.SetText(text);
            TryPlayTypingSound();
            textComplete = true;
        }

        int visibleCharacters = 0;
        float elapsedForNextCharacter = 0f;
        float fadeElapsed = 0f;
        float startAlpha = resolvedView.SlideAlpha;
        bool fadeComplete = fadeDuration <= 0f;
        if (fadeComplete)
        {
            resolvedView.SetSlideAlpha(1f);
            if (fadeSkipPromptWithSlide)
                resolvedView.SetSkipPromptAlpha(1f);
        }

        while (!textComplete || !fadeComplete)
        {
            EndingOutroInputCommand command = PollInput();
            if (command == EndingOutroInputCommand.SkipOutro)
            {
                skipOutroRequested = true;
                FadeTypingSoundOnSkipRequest();
                resolvedView.SetText(text);
                resolvedView.SetSlideAlpha(1f);
                if (fadeSkipPromptWithSlide)
                    resolvedView.SetSkipPromptAlpha(1f);
                yield break;
            }

            if (command == EndingOutroInputCommand.Advance)
            {
                bool canAdvance = !ignoreAdvanceUntilFadeComplete || fadeComplete;
                if (canAdvance)
                {
                    resolvedView.SetText(text);
                    resolvedView.SetSlideAlpha(1f);
                    if (fadeSkipPromptWithSlide)
                        resolvedView.SetSkipPromptAlpha(1f);
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
                    resolvedView.SetText(text.Substring(0, visibleCharacters));
                    TryPlayTypingSound();
                }

                textComplete = visibleCharacters >= text.Length;
            }

            if (!fadeComplete)
            {
                fadeElapsed += deltaTime;
                float normalized = Mathf.Clamp01(fadeElapsed / fadeDuration);
                resolvedView.SetSlideAlpha(Mathf.Lerp(startAlpha, 1f, normalized));
                if (fadeSkipPromptWithSlide)
                    resolvedView.SetSkipPromptAlpha(normalized);
                fadeComplete = fadeElapsed >= fadeDuration;
            }

            yield return null;
        }

        resolvedView.SetText(text);
        resolvedView.SetSlideAlpha(1f);
        if (fadeSkipPromptWithSlide)
            resolvedView.SetSkipPromptAlpha(1f);
    }

    private IEnumerator WaitAfterTextAsync(float waitSeconds)
    {
        float elapsed = 0f;
        while (elapsed < waitSeconds)
        {
            EndingOutroInputCommand command = PollInput();
            if (command == EndingOutroInputCommand.SkipOutro)
            {
                skipOutroRequested = true;
                FadeTypingSoundOnSkipRequest();
                yield break;
            }

            if (command == EndingOutroInputCommand.Advance)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator FadeSlideAsync(float targetAlpha, float duration)
    {
        IEndingOutroView resolvedView = View;
        if (resolvedView == null)
            yield break;

        float startAlpha = resolvedView.SlideAlpha;
        if (duration <= 0f)
        {
            resolvedView.SetSlideAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            EndingOutroInputCommand command = PollInput();
            if (command == EndingOutroInputCommand.SkipOutro)
            {
                skipOutroRequested = true;
                FadeTypingSoundOnSkipRequest();
                yield break;
            }

            if (command == EndingOutroInputCommand.Advance)
                break;

            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            resolvedView.SetSlideAlpha(Mathf.Lerp(startAlpha, targetAlpha, normalized));
            yield return null;
        }

        resolvedView.SetSlideAlpha(targetAlpha);
    }

    private EndingOutroInputCommand PollInput()
    {
        IEndingOutroView resolvedView = View;
        bool keyHoldPressed = IsSkipKeyOrAdvancePressed();
        bool mouseHoldPressed = IsSkipPromptMouseHoldPressed();
        if (keyHoldPressed || mouseHoldPressed)
        {
            skipKeyWasHeld = true;
            skipHoldWasPressedByKey |= keyHoldPressed;
            skipHoldWasPressedByMouse |= mouseHoldPressed;
            skipHoldElapsed += Time.unscaledDeltaTime;

            float skipHoldSeconds = sequence != null ? sequence.SkipHoldSeconds : 0f;
            if (skipHoldSeconds <= 0f)
            {
                resolvedView?.SetSkipFill(1f);
                return EndingOutroInputCommand.SkipOutro;
            }

            float normalized = Mathf.Clamp01(skipHoldElapsed / skipHoldSeconds);
            resolvedView?.SetSkipFill(normalized);
            if (skipHoldElapsed >= skipHoldSeconds)
                return EndingOutroInputCommand.SkipOutro;
        }
        else
        {
            bool wasOutroSpacePress = skipKeyWasHeld;
            bool wasReleased =
                (skipHoldWasPressedByKey && WasSkipKeyOrAdvanceReleasedThisFrame()) ||
                (skipHoldWasPressedByMouse && Input.GetMouseButtonUp(0));
            ResetHoldInputState();

            if (wasOutroSpacePress && wasReleased && !WasAdvanceInputConsumedThisFrame())
                return ConsumeAdvanceInput();
        }

        if (Input.GetMouseButtonDown(0) && !WasAdvanceInputConsumedThisFrame())
            return ConsumeAdvanceInput();

        return EndingOutroInputCommand.None;
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
        skipHoldWasPressedByKey = false;
        skipHoldWasPressedByMouse = false;
        SetSkipFillIfViewAlive(0f);
    }

    private bool IsSkipKeyOrAdvancePressed()
    {
        if (InputActionQuery.IsKeyPressed(skipKey))
            return true;

        return InputActionQuery.IsPressed(InputActionId.DialogueAdvance);
    }

    private bool WasSkipKeyOrAdvanceReleasedThisFrame()
    {
        if (InputActionQuery.WasKeyReleasedThisFrame(skipKey))
            return true;

        return InputActionQuery.WasReleasedThisFrame(InputActionId.DialogueAdvance);
    }

    private bool IsSkipPromptMouseHoldPressed()
    {
        IEndingOutroView resolvedView = View;
        return Input.GetMouseButton(0) &&
               resolvedView != null &&
               resolvedView.ContainsSkipPromptScreenPoint(Input.mousePosition);
    }

    private bool WasAdvanceInputConsumedThisFrame()
    {
        return consumedAdvanceInputFrame == Time.frameCount;
    }

    private EndingOutroInputCommand ConsumeAdvanceInput()
    {
        consumedAdvanceInputFrame = Time.frameCount;
        return EndingOutroInputCommand.Advance;
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
        hasHiddenCursor = MouseCursorPlayback.SetHidden(this, true);
    }

    private void ReleaseCursorHidden()
    {
        if (!hasHiddenCursor)
            return;

        MouseCursorPlayback.SetHidden(this, false);
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
        return View != null;
    }

    private void HideViewIfAlive()
    {
        IEndingOutroView resolvedView = View;
        if (resolvedView != null)
            resolvedView.HideImmediate();
    }

    private void SetSkipFillIfViewAlive(float normalized)
    {
        IEndingOutroView resolvedView = View;
        if (resolvedView != null)
            resolvedView.SetSkipFill(normalized);
    }

    private enum EndingOutroInputCommand
    {
        None,
        Advance,
        SkipOutro
    }

    private static MonoBehaviour FindGlobalRootOutroView()
    {
        MonoBehaviour[] behaviours = GlobalCanvasPlayback.GetComponentsInRoot<MonoBehaviour>(includeInactive: true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour candidate = behaviours[i];
            if (candidate is IEndingOutroView endingView && endingView.IsReady)
                return candidate;
        }

        return null;
    }

    private static MonoBehaviour FindSingleSceneOutroView()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);

        MonoBehaviour found = null;
        int foundCount = 0;
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour candidate = behaviours[i];
            if (candidate is not IEndingOutroView endingView || !endingView.IsReady)
                continue;

            found = candidate;
            foundCount++;
        }

        return foundCount == 1 ? found : null;
    }
}

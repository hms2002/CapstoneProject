using System;
using System.Collections;
using CapstoneAudio;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EndingOutroPlayer : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EndingOutroSequenceSO sequence;

    [Header("View")]
    [SerializeField] private EndingOutroView view;
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
    private int consumedAdvanceInputFrame = -1;
    private bool hasHiddenCursor;

    public bool IsPlaying => playRoutine != null;

    public bool CanPlay
    {
        get
        {
            ResolveRuntimeReferences();
            return isActiveAndEnabled &&
                   sequence != null &&
                   sequence.SlideCount > 0 &&
                   view != null &&
                   view.IsReady;
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
        if (view == null || !view.IsReady)
            view = FindGlobalRootOutroView();

        if (view == null || !view.IsReady)
            view = FindSingleSceneOutroView();

        return view != null && view.IsReady;
    }

    private IEnumerator PlayRoutine(Action onCompleted, bool keepViewVisibleOnCompleted)
    {
        skipOutroRequested = false;
        ResetInputState();
        AcquireCursorHidden();
        view.Show(skipKey);
        view.ApplySkipFillColor(sequence.SkipFillColor);
        view.SetRootAlpha(0f);
        view.SetSkipPromptAlpha(0f);

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
        float startAlpha = view.RootAlpha;
        if (duration <= 0f)
        {
            view.SetRootAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            EndingOutroInputCommand command = PollInput();
            if (command == EndingOutroInputCommand.SkipOutro)
            {
                skipOutroRequested = true;
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

        float secondsPerCharacter = sequence.SecondsPerCharacter;
        bool textComplete = text.Length == 0;
        if (!textComplete && secondsPerCharacter <= 0f)
        {
            view.SetText(text);
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
            EndingOutroInputCommand command = PollInput();
            if (command == EndingOutroInputCommand.SkipOutro)
            {
                skipOutroRequested = true;
                view.SetText(text);
                view.SetSlideAlpha(1f);
                if (fadeSkipPromptWithSlide)
                    view.SetSkipPromptAlpha(1f);
                yield break;
            }

            if (command == EndingOutroInputCommand.Advance)
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
            EndingOutroInputCommand command = PollInput();
            if (command == EndingOutroInputCommand.SkipOutro)
            {
                skipOutroRequested = true;
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
        float startAlpha = view.SlideAlpha;
        if (duration <= 0f)
        {
            view.SetSlideAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            EndingOutroInputCommand command = PollInput();
            if (command == EndingOutroInputCommand.SkipOutro)
            {
                skipOutroRequested = true;
                yield break;
            }

            if (command == EndingOutroInputCommand.Advance)
                break;

            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            view.SetSlideAlpha(Mathf.Lerp(startAlpha, targetAlpha, normalized));
            yield return null;
        }

        view.SetSlideAlpha(targetAlpha);
    }

    private EndingOutroInputCommand PollInput()
    {
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
                view.SetSkipFill(1f);
                return EndingOutroInputCommand.SkipOutro;
            }

            float normalized = Mathf.Clamp01(skipHoldElapsed / skipHoldSeconds);
            view.SetSkipFill(normalized);
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
        if (InputKeyCompatibility.IsPressed(skipKey))
            return true;

        InputBindingService input = InputBindingService.Instance;
        return input != null && input.IsPressed(InputActionId.DialogueAdvance);
    }

    private bool WasSkipKeyOrAdvanceReleasedThisFrame()
    {
        if (InputKeyCompatibility.WasReleasedThisFrame(skipKey))
            return true;

        InputBindingService input = InputBindingService.Instance;
        return input != null && input.WasReleasedThisFrame(InputActionId.DialogueAdvance);
    }

    private bool IsSkipPromptMouseHoldPressed()
    {
        return Input.GetMouseButton(0) &&
               view != null &&
               view.ContainsSkipPromptScreenPoint(Input.mousePosition);
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
        if (!playTypingSound || Time.unscaledTime < nextTypingSoundTime)
            return;

        TypingAudioUtility.PlayBossTalking(this, gameObject);
        nextTypingSoundTime = Time.unscaledTime + Mathf.Max(0f, typingSoundInterval);
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

    private enum EndingOutroInputCommand
    {
        None,
        Advance,
        SkipOutro
    }

    private static EndingOutroView FindGlobalRootOutroView()
    {
        GlobalUIRoot root = GlobalUIRoot.Instance;
        if (root == null)
            return null;

        EndingOutroView[] views = root.GetComponentsInChildren<EndingOutroView>(true);
        for (int i = 0; i < views.Length; i++)
        {
            EndingOutroView candidate = views[i];
            if (candidate != null && candidate.IsReady)
                return candidate;
        }

        return null;
    }

    private static EndingOutroView FindSingleSceneOutroView()
    {
        EndingOutroView[] views = FindObjectsByType<EndingOutroView>(FindObjectsInactive.Include);

        EndingOutroView found = null;
        int foundCount = 0;
        for (int i = 0; i < views.Length; i++)
        {
            EndingOutroView candidate = views[i];
            if (candidate == null || !candidate.IsReady)
                continue;

            found = candidate;
            foundCount++;
        }

        return foundCount == 1 ? found : null;
    }
}

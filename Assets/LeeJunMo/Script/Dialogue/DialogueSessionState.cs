public sealed class DialogueSessionState
{
    public bool IsPlaying { get; private set; }
    public bool IsTyping { get; private set; }
    public bool IsChoosing { get; private set; }
    public bool IsTransitioning { get; private set; }
    public bool IsWaitingForCallback { get; private set; }
    public string CurrentText { get; private set; } = string.Empty;

    public void BeginSession()
    {
        IsPlaying = true;
        IsTyping = false;
        IsChoosing = false;
        IsTransitioning = true;
        IsWaitingForCallback = false;
        CurrentText = string.Empty;
    }

    public void BeginTransition() => IsTransitioning = true;
    public void EndTransition() => IsTransitioning = false;
    public void BeginWaiting() => IsWaitingForCallback = true;
    public void EndWaiting() => IsWaitingForCallback = false;
    public void BeginChoosing() => IsChoosing = true;
    public void EndChoosing() => IsChoosing = false;
    public void EndTyping() => IsTyping = false;

    public void BeginTyping(string text)
    {
        CurrentText = text ?? string.Empty;
        IsTyping = true;
    }

    public void ResetInteractionFlags()
    {
        IsWaitingForCallback = false;
        IsTyping = false;
        IsChoosing = false;
    }

    public void EndSession()
    {
        IsPlaying = false;
        IsTyping = false;
        IsChoosing = false;
        IsTransitioning = false;
        IsWaitingForCallback = false;
        CurrentText = string.Empty;
    }
}

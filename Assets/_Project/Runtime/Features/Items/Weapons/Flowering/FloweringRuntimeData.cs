public sealed class FloweringRuntimeData : WeaponRuntimeData
{
    public bool IsBloomActive { get; private set; }
    public float RemainingSeconds { get; private set; }
    public float DisplayMaxSeconds { get; private set; }

    public void BeginBloom(float durationSeconds)
    {
        IsBloomActive = true;
        RemainingSeconds = UnityEngine.Mathf.Max(0f, durationSeconds);
        DisplayMaxSeconds = UnityEngine.Mathf.Max(0.0001f, RemainingSeconds);
    }

    public void TickBloom(float deltaTime)
    {
        if (!IsBloomActive)
            return;

        RemainingSeconds -= UnityEngine.Mathf.Max(0f, deltaTime);
        if (RemainingSeconds <= 0f)
            EndBloom();
    }

    public void ExtendBloom(float seconds)
    {
        if (!IsBloomActive || seconds <= 0f)
            return;

        RemainingSeconds += seconds;
        DisplayMaxSeconds = UnityEngine.Mathf.Max(DisplayMaxSeconds, RemainingSeconds);
    }

    public void EndBloom()
    {
        IsBloomActive = false;
        RemainingSeconds = 0f;
        DisplayMaxSeconds = 0f;
    }
}

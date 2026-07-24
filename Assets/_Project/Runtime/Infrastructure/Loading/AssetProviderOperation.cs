using UnityEngine;

public class AssetProviderOperation : CustomYieldInstruction
{
    internal AssetProviderOperation(string label = null)
    {
        Label = string.IsNullOrWhiteSpace(label) ? "AssetProviderOperation" : label;
        StartedRealtimeSeconds = Time.realtimeSinceStartup;
        Progress01 = 0f;
        ProgressUnits = 1f;
    }

    public string Label { get; }
    public float StartedRealtimeSeconds { get; }
    public float CompletedRealtimeSeconds { get; private set; }
    public bool IsDone { get; private set; }
    public bool Succeeded => IsDone && string.IsNullOrEmpty(ErrorMessage);
    public string ErrorMessage { get; private set; }
    public float Progress01 { get; private set; }
    public float ProgressUnits { get; private set; }
    public float ElapsedSeconds => Mathf.Max(
        0f,
        (IsDone ? CompletedRealtimeSeconds : Time.realtimeSinceStartup) - StartedRealtimeSeconds);
    public override bool keepWaiting => !IsDone;

    public static AssetProviderOperation Completed(string label = null)
    {
        var operation = new AssetProviderOperation(label);
        operation.Complete();
        return operation;
    }

    public static AssetProviderOperation Failed(string errorMessage, string label = null)
    {
        var operation = new AssetProviderOperation(label);
        operation.Complete(errorMessage);
        return operation;
    }

    internal bool Complete(string errorMessage = null)
    {
        if (IsDone)
            return false;

        ErrorMessage = errorMessage;
        IsDone = true;
        Progress01 = 1f;
        CompletedRealtimeSeconds = Time.realtimeSinceStartup;
        return true;
    }

    internal bool ReportProgress(float progress01)
    {
        if (IsDone)
            return false;

        float clampedProgress = Mathf.Clamp01(progress01);
        if (Mathf.Approximately(Progress01, clampedProgress))
            return false;

        Progress01 = clampedProgress;
        return true;
    }

    internal void SetProgressUnits(float progressUnits)
    {
        ProgressUnits = Mathf.Max(0.0001f, progressUnits);
    }
}

public sealed class AssetResolveOperation<T> : AssetProviderOperation where T : Object
{
    internal AssetResolveOperation(string label = null) : base(label)
    {
    }

    public T Asset { get; private set; }

    public static AssetResolveOperation<T> Completed(T asset, string label = null)
    {
        var operation = new AssetResolveOperation<T>(label);
        operation.Complete(asset);
        return operation;
    }

    public static AssetResolveOperation<T> Failed(string errorMessage, T asset = null, string label = null)
    {
        var operation = new AssetResolveOperation<T>(label);
        operation.Complete(asset, errorMessage);
        return operation;
    }

    internal bool Complete(T asset, string errorMessage = null)
    {
        if (IsDone)
            return false;

        Asset = asset;
        return base.Complete(errorMessage);
    }
}

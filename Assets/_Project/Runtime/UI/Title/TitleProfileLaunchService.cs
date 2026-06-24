using System;
using UnityEngine;

public enum TitleProfileLaunchAction
{
    None = 0,
    StartNewRun = 1,
    ContinueRun = 2
}

[Serializable]
public struct TitleProfileLaunchRequest
{
    [SerializeField] private int slotIndex;
    [SerializeField] private TitleProfileLaunchAction action;
    [SerializeField] private string targetSceneName;

    public TitleProfileLaunchRequest(int slotIndex, TitleProfileLaunchAction action, string targetSceneName)
    {
        this.slotIndex = slotIndex;
        this.action = action;
        this.targetSceneName = targetSceneName;
    }

    public int SlotIndex => slotIndex;
    public TitleProfileLaunchAction Action => action;
    public string TargetSceneName => targetSceneName;
    public bool IsValid => action != TitleProfileLaunchAction.None && !string.IsNullOrWhiteSpace(targetSceneName);
}

internal readonly struct TitleProfileLaunchResult
{
    public readonly bool Succeeded;
    public readonly string TargetSceneName;

    public TitleProfileLaunchResult(bool succeeded, string targetSceneName)
    {
        Succeeded = succeeded;
        TargetSceneName = targetSceneName;
    }
}

internal static class TitleProfileLaunchService
{
    public static bool PreparePreloadWindow(
        TitleProfileLaunchRequest request,
        GameDataManager gameDataManager)
    {
        if (!request.IsValid)
            return false;

        if (gameDataManager != null)
        {
            PresentationPreloadService.EnsureInstance();
            gameDataManager.LoadSlot(request.SlotIndex);
        }

        return true;
    }

    public static TitleProfileLaunchResult PrepareLaunch(
        TitleProfileLaunchRequest request,
        GameDataManager gameDataManager)
    {
        if (!request.IsValid)
            return new TitleProfileLaunchResult(false, null);

        if (gameDataManager != null)
        {
            PresentationPreloadService.EnsureInstance();
            gameDataManager.LoadSlot(request.SlotIndex);
            gameDataManager.EnsureData().hasInitializedProfile = true;
            gameDataManager.SaveData();
        }

        if (SceneDomainScenePolicy.IsHubSceneName(request.TargetSceneName))
            GamePlayDataManager.EnsureInstance()?.RequestPendingHubLoadFullHeal();

        return new TitleProfileLaunchResult(true, request.TargetSceneName);
    }
}

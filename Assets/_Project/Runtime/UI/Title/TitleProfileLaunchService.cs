using System;
using UnityEngine;

/// <summary>
/// 책임 : 타이틀 화면의 프로필 실행 요청이 새 런/이어하기 중 어떤 의미인지 표현한다.
/// </summary>
public enum TitleProfileLaunchAction
{
    None = 0,
    StartNewRun = 1,
    ContinueRun = 2
}

/// <summary>
/// 책임 : 타이틀 화면에서 선택한 슬롯, 실행 동작, 이동 대상 씬 이름을 전달하는 요청 데이터이다.
/// </summary>
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

/// <summary>
/// 책임 : 타이틀 프로필 실행 준비 결과와 실제 이동할 씬 이름을 반환한다.
/// </summary>
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

/// <summary>
/// 책임 : 타이틀 프로필 실행 전 저장 슬롯 로드, preload 창 준비, 허브 진입 회복 예약을 조율한다.
/// </summary>
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

        if (SceneDomainNamePolicy.IsHubSceneName(request.TargetSceneName))
            GamePlayDataManager.EnsureInstance()?.RequestPendingHubLoadFullHeal();

        return new TitleProfileLaunchResult(true, request.TargetSceneName);
    }
}

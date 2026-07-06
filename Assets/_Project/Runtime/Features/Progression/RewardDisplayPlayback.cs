using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 보상 표시 UI 구현이 gameplay 보상 흐름에 제공해야 하는 표시 backend 계약이다.
/// </summary>
public interface IRewardDisplayBackend
{
    Component BackendComponent { get; }
    void ShowUpgradeReward(UpgradeNodeSO upgradeNode, Action callback = null);
    void ShowFlowOwnedReward(List<UpgradeEffectSO> upgradeEffects, List<AffectionEffect> affectionEffects, Action callback = null);
}

/// <summary>
/// 책임 : 호감도/업그레이드 gameplay가 구체 보상 표시 서비스 없이 보상 표시를 요청하게 한다.
/// </summary>
public static class RewardDisplayPlayback
{
    private static IRewardDisplayBackend backend;

    public static void RegisterBackend(IRewardDisplayBackend newBackend)
    {
        backend = newBackend;
    }

    public static void UnregisterBackend(IRewardDisplayBackend oldBackend)
    {
        if (ReferenceEquals(backend, oldBackend))
            backend = null;
    }

    public static bool ShowUpgradeReward(UpgradeNodeSO upgradeNode, Action callback = null)
    {
        if (!IsBackendAlive(backend))
            return false;

        backend.ShowUpgradeReward(upgradeNode, callback);
        return true;
    }

    public static bool ShowFlowOwnedReward(
        List<UpgradeEffectSO> upgradeEffects,
        List<AffectionEffect> affectionEffects,
        Action callback = null)
    {
        if (!IsBackendAlive(backend))
        {
            callback?.Invoke();
            return false;
        }

        backend.ShowFlowOwnedReward(upgradeEffects, affectionEffects, callback);
        return true;
    }

    private static bool IsBackendAlive(IRewardDisplayBackend candidate)
    {
        return candidate != null && candidate.BackendComponent != null;
    }
}

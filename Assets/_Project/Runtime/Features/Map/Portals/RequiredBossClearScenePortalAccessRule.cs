using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 책임 : 지정된 보스 route theme들이 영구 클리어된 경우에만 ScenePortal 이동을 허용하고, 실패 시 교체 가능한 이벤트를 발행한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RequiredBossClearScenePortalAccessRule : MonoBehaviour, IScenePortalAccessRule
{
    [SerializeField] private List<CorridorBossRouteSetSO> requiredBossRouteSets = new();
    [SerializeField] private List<string> requiredBossThemeIds = new();
    [SerializeField] private string blockedMessage = "세 보스를 모두 처치해야 이동할 수 있습니다.";
    [SerializeField] private bool showDefaultPopup = true;
    [SerializeField, Min(0f)] private float popupDuration = 1.5f;
    [SerializeField] private UnityEvent onAccessDenied = new();

    public event Action<RequiredBossClearScenePortalAccessRule, ScenePortal, IPlayerInteractor> AccessDenied;

    public IReadOnlyList<CorridorBossRouteSetSO> RequiredBossRouteSets => requiredBossRouteSets;
    public IReadOnlyList<string> RequiredBossThemeIds => requiredBossThemeIds;

    public bool CanAccess(ScenePortal portal, IPlayerInteractor player)
    {
        return AreAllRequirementsCleared();
    }

    public void HandleAccessDenied(ScenePortal portal, IPlayerInteractor player)
    {
        if (showDefaultPopup)
            ShowBlockedPopup();

        onAccessDenied?.Invoke();
        AccessDenied?.Invoke(this, portal, player);
    }

    private bool AreAllRequirementsCleared()
    {
        bool hasRequirement = false;

        if (requiredBossRouteSets != null)
        {
            for (int i = 0; i < requiredBossRouteSets.Count; i++)
            {
                CorridorBossRouteSetSO routeSet = requiredBossRouteSets[i];
                if (routeSet == null)
                    continue;

                hasRequirement = true;
                if (!IsBossCleared(routeSet.StableThemeId))
                    return false;
            }
        }

        if (requiredBossThemeIds != null)
        {
            for (int i = 0; i < requiredBossThemeIds.Count; i++)
            {
                string bossThemeId = requiredBossThemeIds[i];
                if (string.IsNullOrWhiteSpace(bossThemeId))
                    continue;

                hasRequirement = true;
                if (!IsBossCleared(bossThemeId))
                    return false;
            }
        }

        return hasRequirement;
    }

    private static bool IsBossCleared(string bossThemeId)
    {
        return BossClearProgressStore.HasClearedBoss(bossThemeId) ||
               RunProgressPlayback.IsBossDefeatedThisRun(bossThemeId);
    }

    private void ShowBlockedPopup()
    {
        if (string.IsNullOrWhiteSpace(blockedMessage))
            return;

        if (popupDuration > 0f)
            WarningPopupPlayback.ShowMessage(blockedMessage, popupDuration);
        else
            WarningPopupPlayback.ShowMessage(blockedMessage);
    }
}

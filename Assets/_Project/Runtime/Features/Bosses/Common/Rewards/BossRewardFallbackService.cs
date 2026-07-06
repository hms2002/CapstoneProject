using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 책임 : 보스 보상/포탈 처리 이벤트가 소비되지 않았을 때 개발용 fallback 경고를 기록한다.
/// </summary>
public static class BossRewardFallbackService
{
    public static void HandleUnhandledFallbacks(BossRewardContext context)
    {
        if (context == null)
            return;

        if (!context.RewardsHandled)
            LogFallback(
                context,
                "Boss rewards were not handled. Add or verify a scene-authored BossBattleEndHandler with a boss reference, physical-drop handling, and a TreasureChest reference for non-final boss routes.");

        if (!context.PortalHandled)
            LogFallback(
                context,
                "Boss portal was not handled. Add or verify a scene-authored BossBattleEndHandler with boss and exitPortal references.");
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    private static void LogFallback(BossRewardContext context, string message)
    {
        Object logContext = context != null && context.Boss != null
            ? context.Boss
            : null;
        Debug.LogWarning($"[BossRewardFallbackService] {message}", logContext);
    }
}

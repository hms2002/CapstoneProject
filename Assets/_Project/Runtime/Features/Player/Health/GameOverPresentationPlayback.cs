using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임 : 게임오버/승리 화면이 어떤 런 종료 원인으로 표시되는지 분류한다.
/// </summary>
public enum GameOverCauseKind
{
    Monster,
    Trap,
    TimeOver
}

/// <summary>
/// 책임 : Gameplay가 게임오버/승리 프레젠테이션에 필요한 런 종료 문맥을 UI 구현에 전달하는 요청 데이터이다.
/// </summary>
public struct GameOverPresentationRequest
{
    private const string VictoryTitleText = "\uC2B9\uB9AC?";
    private const string VictoryMessageText = "\uC2B9\uB9AC\uD558\uC600\uC9C0\uB9CC, \uC774\uAC83\uC73C\uB85C \uCDA9\uBD84\uD588\uC744\uAE4C?";

    public GameOverCauseKind CauseKind;
    public string CauseName;
    public float RemainingSeconds;
    public string LocationName;
    public string HubSceneName;
    public Transform PlayerTransform;
    public bool EndRunOnReturn;
    public RunEndReason EndRunReason;
    public bool UseSceneTransitionService;
    public string ReturnButtonLabel;
    public string MessageTextOverride;
    public bool HideTimeText;
    public bool IsVictory;
    public int MagicStoneRewardAmount;
    public bool AllowInventoryDuringPresentation;
    public bool ShowInventoryKeyHint;
    public bool UseStandingPlayerSnapshot;
    public string TitleTextOverride;
    public bool HasTitleColorOverride;
    public Color TitleColorOverride;

    public static GameOverPresentationRequest Defeat(
        Transform playerTransform,
        string causeName,
        GameOverCauseKind causeKind,
        string hubSceneName,
        bool useSceneTransitionService)
    {
        return new GameOverPresentationRequest
        {
            CauseKind = causeKind == GameOverCauseKind.TimeOver ? GameOverCauseKind.Monster : causeKind,
            CauseName = causeName,
            RemainingSeconds = ResolveRemainingSeconds(),
            LocationName = ResolveCurrentLocationName(),
            HubSceneName = hubSceneName,
            PlayerTransform = playerTransform,
            EndRunOnReturn = true,
            EndRunReason = RunEndReason.Defeat,
            UseSceneTransitionService = useSceneTransitionService,
            AllowInventoryDuringPresentation = true,
            ShowInventoryKeyHint = true
        };
    }

    public static GameOverPresentationRequest TimeOver(
        Transform playerTransform,
        string hubSceneName,
        bool useSceneTransitionService)
    {
        return new GameOverPresentationRequest
        {
            CauseKind = GameOverCauseKind.TimeOver,
            CauseName = "\uB9C8\uC655\uC758 \uC778\uB0B4\uC2EC",
            RemainingSeconds = ResolveRemainingSeconds(),
            LocationName = ResolveCurrentLocationName(),
            HubSceneName = hubSceneName,
            PlayerTransform = playerTransform,
            EndRunOnReturn = true,
            EndRunReason = RunEndReason.TimeOver,
            UseSceneTransitionService = useSceneTransitionService,
            AllowInventoryDuringPresentation = true,
            ShowInventoryKeyHint = true
        };
    }

    public static GameOverPresentationRequest Victory(
        Transform playerTransform,
        int magicStoneRewardAmount,
        string hubSceneName,
        bool useSceneTransitionService)
    {
        return new GameOverPresentationRequest
        {
            CauseKind = GameOverCauseKind.Monster,
            CauseName = string.Empty,
            RemainingSeconds = ResolveRemainingSeconds(),
            LocationName = string.Empty,
            HubSceneName = hubSceneName,
            PlayerTransform = playerTransform,
            EndRunOnReturn = true,
            EndRunReason = RunEndReason.Victory,
            UseSceneTransitionService = useSceneTransitionService,
            MessageTextOverride = VictoryMessageText,
            IsVictory = true,
            MagicStoneRewardAmount = Mathf.Max(0, magicStoneRewardAmount),
            AllowInventoryDuringPresentation = true,
            ShowInventoryKeyHint = true,
            UseStandingPlayerSnapshot = true,
            TitleTextOverride = VictoryTitleText,
            HasTitleColorOverride = true,
            TitleColorOverride = new Color(0.35f, 1f, 0.35f, 1f)
        };
    }

    private static float ResolveRemainingSeconds()
    {
        if (RunTimeLimitSystem.Instance != null)
            return Mathf.Max(0f, RunTimeLimitSystem.Instance.RemainingSeconds);

        return RunSessionStore.GetRunRemainingSeconds();
    }

    private static string ResolveCurrentLocationName()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrWhiteSpace(sceneName))
            return "\uC54C \uC218 \uC5C6\uB294 \uC7A5\uC18C";

        string normalized = sceneName.ToLowerInvariant();
        bool isBossRoom = normalized.Contains("boss");

        if (TryResolveRouteSetLocationName(sceneName, out string routeLocationName))
            return routeLocationName;

        if (normalized.Contains("demon") || normalized.Contains("king"))
            return "\uB9C8\uC655\uC758 \uC54C\uD604\uC2E4";

        if (normalized.Contains("dragon") || normalized.Contains("dragon") ||
            sceneName == "ProtoTypeCorridor 1" || sceneName == "ProtoTypeBoss 2")
        {
            return isBossRoom ? "\uB4DC\uB798\uACE4\uC758 \uBC29" : "\uBCF4\uBB3C\uCC3D\uACE0";
        }

        if (normalized.Contains("shadow") || normalized.Contains("chloe") ||
            sceneName == "ProtoTypeCorridor 2" || sceneName == "ProtoTypeBoss 3")
        {
            return isBossRoom ? "\uD074\uB85C\uC5D0\uC758 \uBC29" : "\uADF8\uB9BC\uC790 \uAC10\uC625";
        }

        if (normalized.Contains("slime") || normalized.Contains("melta") ||
            sceneName == "ProtoTypeCorridor 3" || sceneName == "ProtoTypeBoss 4")
        {
            return isBossRoom ? "\uBA5C\uD0C0\uC758 \uBC29" : "\uC2AC\uB77C\uC784 \uC655\uAD6D";
        }

        if (sceneName.StartsWith("ProtoTypeCorridor", System.StringComparison.OrdinalIgnoreCase))
            return "\uBCF4\uBB3C\uCC3D\uACE0";

        if (sceneName.StartsWith("ProtoTypeBoss", System.StringComparison.OrdinalIgnoreCase))
            return "\uBCF4\uC2A4\uB8F8";

        return sceneName;
    }

    private static bool TryResolveRouteSetLocationName(string sceneName, out string locationName)
    {
        locationName = null;

        return RunRoutePlayback.TryResolveCurrentLocationName(sceneName, out locationName);
    }
}

/// <summary>
/// 책임 : Gameplay 요청을 구체 게임오버 UI 구현에 전달하는 backend 계약이다.
/// </summary>
public interface IGameOverPresentationBackend
{
    bool IsShowing { get; }
    bool TryShow(GameOverPresentationRequest request);
}

/// <summary>
/// 책임 : Gameplay 코드가 구체 게임오버 UI 컨트롤러 없이 게임오버/승리 프레젠테이션을 요청하게 한다.
/// </summary>
public static class GameOverPresentationPlayback
{
    private static IGameOverPresentationBackend backend;

    public static bool IsAvailable => backend != null;
    public static bool IsShowing => backend != null && backend.IsShowing;

    public static void RegisterBackend(IGameOverPresentationBackend presentationBackend)
    {
        backend = presentationBackend;
    }

    public static bool TryShow(GameOverPresentationRequest request)
    {
        if (backend != null)
            return backend.TryShow(request);

        Debug.LogWarning("[GameOverPresentationPlayback] No game-over presentation backend is registered.");
        return false;
    }
}

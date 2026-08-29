using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 하나의 연결 에셋에서 출발 endpoint가 어느 쪽인지를 안정적인 직렬화 값으로 표현한다.
/// </summary>
public enum SceneConnectionEndpointSide
{
    A = 0,
    B = 1
}

/// <summary>
/// 책임 : 연결 이동이 현재 런을 시작하거나 종료해야 하는지를 경로 해석과 분리해 전달한다.
/// </summary>
public enum SceneTravelRunAction
{
    None = 0,
    StartRun = 1,
    EndRun = 2
}

/// <summary>
/// 책임 : 연결 방향을 사용할 수 있는지 검사할 런 진행 조건의 종류를 정의한다.
/// </summary>
public enum SceneTravelGateKind
{
    None = 0,
    BossNotDefeatedThisRun = 1,
    BossDefeatedThisRun = 2
}

/// <summary>
/// 책임 : 절차 던전에 다시 들어올 때 레이아웃과 콘텐츠 상태를 어느 범위까지 재사용할지 정의한다.
/// </summary>
public enum DungeonReentryPolicy
{
    RegenerateOnEntry = 0,
    ResetContentsKeepLayout = 1,
    PreserveDuringRun = 2
}

/// <summary>
/// 책임 : 연결 에셋이 참조하는 한쪽 endpoint의 씬 이름과 씬 내부 식별자를 보관한다.
/// </summary>
[Serializable]
public struct SceneConnectionEndpointData
{
    [SerializeField] private string sceneName;
    [SerializeField] private string endpointId;

    public string SceneName => sceneName;
    public string EndpointId => endpointId;
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(sceneName) &&
        !string.IsNullOrWhiteSpace(endpointId);

    public bool Matches(string candidateSceneName, string candidateEndpointId)
    {
        return IsValid &&
               string.Equals(sceneName, candidateSceneName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(endpointId, candidateEndpointId, StringComparison.Ordinal);
    }
}

/// <summary>
/// 책임 : 연결 방향을 차단할 한 가지 런 조건과 실패 시 표시할 공통 경고 코드를 보관한다.
/// </summary>
[Serializable]
public struct SceneTravelGateData
{
    [SerializeField] private SceneTravelGateKind kind;
    [SerializeField] private string subjectId;
    [SerializeField] private WarningPopupCode failureWarning;

    public SceneTravelGateKind Kind => kind;
    public string SubjectId => subjectId;
    public WarningPopupCode FailureWarning => failureWarning;
    public bool IsConfigured => kind != SceneTravelGateKind.None;
}

/// <summary>
/// 책임 : 연결의 한 방향에 적용할 활성 여부, 런 수명 변경, 상태 복원 정책, gate와 연출 프로필을 보관한다.
/// </summary>
[Serializable]
public struct SceneTravelDirectionData
{
    [SerializeField] private bool enabled;
    [SerializeField] private SceneTravelRunAction runAction;
    [SerializeField] private RunEndReason runEndReason;
    [SerializeField] private bool preservePlayerRuntimeState;

    [Header("Player Restore Policy")]
    [SerializeField] private bool fullyHealPlayer;
    [SerializeField] private bool resetCooldowns;
    [SerializeField] private bool clearAllEffects;
    [SerializeField] private bool clearCombatOnlyEffects;

    [Header("Availability Gates")]
    [SerializeField] private List<SceneTravelGateData> gates;

    [Header("Presentation")]
    [SerializeField] private SceneTravelPresentationProfileSO presentationProfile;

    public bool Enabled => enabled;
    public SceneTravelRunAction RunAction => runAction;
    public RunEndReason RunEndReason => runEndReason;
    public bool PreservePlayerRuntimeState => preservePlayerRuntimeState;
    public bool FullyHealPlayer => fullyHealPlayer;
    public bool ResetCooldowns => resetCooldowns;
    public bool ClearAllEffects => clearAllEffects;
    public bool ClearCombatOnlyEffects => clearCombatOnlyEffects;
    public IReadOnlyList<SceneTravelGateData> Gates => gates;
    public SceneTravelPresentationProfileSO PresentationProfile => presentationProfile;

    public static SceneTravelDirectionData CreateEnabledDefault()
    {
        return new SceneTravelDirectionData
        {
            enabled = true,
            preservePlayerRuntimeState = true,
            gates = new List<SceneTravelGateData>()
        };
    }
}

/// <summary>
/// 책임 : SceneConnectionSO가 출발 방향을 해석한 뒤 실행 계층에 전달할 출발·도착 endpoint와 방향 설정을 묶는다.
/// </summary>
public readonly struct ResolvedSceneTravelDirection
{
    public SceneConnectionEndpointSide SourceSide { get; }
    public SceneConnectionEndpointData Source { get; }
    public SceneConnectionEndpointData Destination { get; }
    public SceneTravelDirectionData Direction { get; }

    public bool IsValid =>
        Source.IsValid &&
        Destination.IsValid &&
        Direction.Enabled;

    public ResolvedSceneTravelDirection(
        SceneConnectionEndpointSide sourceSide,
        SceneConnectionEndpointData source,
        SceneConnectionEndpointData destination,
        SceneTravelDirectionData direction)
    {
        SourceSide = sourceSide;
        Source = source;
        Destination = destination;
        Direction = direction;
    }
}

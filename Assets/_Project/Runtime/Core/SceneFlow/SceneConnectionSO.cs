using UnityEngine;

/// <summary>
/// 책임 : 두 씬 endpoint 사이의 양방향 목적지와 방향별 gate·런 정책·연출을 하나의 데이터 에셋으로 제공한다.
/// </summary>
[CreateAssetMenu(
    fileName = "SceneConnection",
    menuName = "Capstone/Scene Management/Scene Connection")]
public sealed class SceneConnectionSO : ScriptableObject
{
    [SerializeField] private string connectionId;
    [SerializeField] private SceneConnectionEndpointData endpointA;
    [SerializeField] private SceneConnectionEndpointData endpointB;
    [SerializeField] private SceneTravelDirectionData aToB = SceneTravelDirectionData.CreateEnabledDefault();
    [SerializeField] private SceneTravelDirectionData bToA = SceneTravelDirectionData.CreateEnabledDefault();

    public string ConnectionId => connectionId;
    public SceneConnectionEndpointData EndpointA => endpointA;
    public SceneConnectionEndpointData EndpointB => endpointB;
    public SceneTravelDirectionData AToB => aToB;
    public SceneTravelDirectionData BToA => bToA;

    public bool TryResolve(
        SceneConnectionEndpointSide sourceSide,
        string sourceSceneName,
        string sourceEndpointId,
        out ResolvedSceneTravelDirection resolved)
    {
        SceneConnectionEndpointData source = sourceSide == SceneConnectionEndpointSide.A
            ? endpointA
            : endpointB;
        SceneConnectionEndpointData destination = sourceSide == SceneConnectionEndpointSide.A
            ? endpointB
            : endpointA;
        SceneTravelDirectionData direction = sourceSide == SceneConnectionEndpointSide.A
            ? aToB
            : bToA;

        resolved = new ResolvedSceneTravelDirection(
            sourceSide,
            source,
            destination,
            direction);

        return !string.IsNullOrWhiteSpace(connectionId) &&
               source.Matches(sourceSceneName, sourceEndpointId) &&
               resolved.IsValid;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        connectionId = name;
        aToB = SceneTravelDirectionData.CreateEnabledDefault();
        bToA = SceneTravelDirectionData.CreateEnabledDefault();
    }

    private void OnValidate()
    {
        connectionId ??= string.Empty;
        if (string.IsNullOrWhiteSpace(connectionId))
            connectionId = name;
    }
#endif
}

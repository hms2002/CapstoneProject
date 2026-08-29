using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 씬 또는 절차 방에 배치된 이동 endpoint의 식별자, 연결 방향, 출발·도착 anchor와 이동 전 정리 태그를 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SceneTravelEndpoint : MonoBehaviour
{
    [SerializeField] private string endpointId = "Default";
    [SerializeField] private string proceduralSlotId;
    [SerializeField] private SceneConnectionSO connection;
    [SerializeField] private SceneConnectionEndpointSide connectionSide;
    [SerializeField] private Transform departureAnchor;
    [SerializeField] private Transform arrivalAnchor;
    [SerializeField] private List<GameplayTagSet> sceneTravelCleanupTagSets = new();

    private bool isTravelReserved;
    private bool isRegistered;

    public string EndpointId => endpointId;
    public string ProceduralSlotId => proceduralSlotId;
    public SceneConnectionSO Connection => connection;
    public SceneConnectionEndpointSide ConnectionSide => connectionSide;
    public Transform DepartureAnchor => departureAnchor != null ? departureAnchor : transform;
    public Transform ArrivalAnchor => arrivalAnchor != null ? arrivalAnchor : transform;
    public IReadOnlyList<GameplayTagSet> SceneTravelCleanupTagSets => sceneTravelCleanupTagSets;
    public bool IsTravelReserved => isTravelReserved;
    public bool IsBound => connection != null && !string.IsNullOrWhiteSpace(endpointId);

    private void OnEnable()
    {
        if (Application.isPlaying)
            Register();
    }

    private void OnDisable()
    {
        Unregister();
        isTravelReserved = false;
    }

    public bool TryResolveDirection(out ResolvedSceneTravelDirection direction)
    {
        direction = default;
        if (connection == null)
            return false;

        return connection.TryResolve(
            connectionSide,
            gameObject.scene.name,
            endpointId,
            out direction);
    }

    public bool TryReserveTravel()
    {
        if (isTravelReserved)
            return false;

        isTravelReserved = true;
        return true;
    }

    public void ReleaseTravelReservation()
    {
        isTravelReserved = false;
    }

    /// <summary>
    /// 책임 : 절차 방의 slot Id를 씬별 연결 데이터에 결합하고 도착 registry의 실제 endpoint Id를 갱신한다.
    /// </summary>
    public bool BindRuntime(SceneConnectionSO targetConnection, SceneConnectionEndpointSide targetSide)
    {
        if (targetConnection == null)
            return false;

        SceneConnectionEndpointData source = targetSide == SceneConnectionEndpointSide.A
            ? targetConnection.EndpointA
            : targetConnection.EndpointB;
        if (!source.IsValid ||
            !string.Equals(source.SceneName, gameObject.scene.name, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Unregister();
        connection = targetConnection;
        connectionSide = targetSide;
        endpointId = source.EndpointId;
        Register();
        return isRegistered;
    }

    /// <summary>
    /// 책임 : 절차 방 구현기가 템플릿의 이동 슬롯 Id를 런타임 endpoint에 기록하되 씬 연결 결합은 별도 단계로 남긴다.
    /// </summary>
    public void ConfigureRuntimeSlot(string slotId)
    {
        proceduralSlotId = slotId ?? string.Empty;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        string id,
        string slotId,
        SceneConnectionSO targetConnection,
        SceneConnectionEndpointSide targetSide)
    {
        endpointId = id ?? string.Empty;
        proceduralSlotId = slotId ?? string.Empty;
        connection = targetConnection;
        connectionSide = targetSide;
    }

    private void OnValidate()
    {
        endpointId ??= string.Empty;
        proceduralSlotId ??= string.Empty;
    }
#endif

    private void Register()
    {
        if (isRegistered || !IsBound)
            return;

        isRegistered = SceneTravelEndpointRegistry.Register(this);
    }

    private void Unregister()
    {
        if (!isRegistered)
            return;

        SceneTravelEndpointRegistry.Unregister(this);
        isRegistered = false;
    }
}

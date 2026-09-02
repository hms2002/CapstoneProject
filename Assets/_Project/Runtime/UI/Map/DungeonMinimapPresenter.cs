using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 책임 : 활성 절차 던전의 발견 그래프를 디자이너 제작 미니맵 프리팹의 노드·연결선 템플릿과 지역명 슬롯에 투영한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonMinimapPresenter : MonoBehaviour
{
    /// <summary>
    /// 책임 : 프리팹에서 복제된 연결선과 양 끝 방 배치 Id를 묶어 발견 상태에 따라 표시를 갱신한다.
    /// </summary>
    private sealed class ConnectionView
    {
        public int FirstRoomPlacementId { get; }
        public int SecondRoomPlacementId { get; }
        public Image Line { get; }

        public ConnectionView(int firstRoomPlacementId, int secondRoomPlacementId, Image line)
        {
            FirstRoomPlacementId = firstRoomPlacementId;
            SecondRoomPlacementId = secondRoomPlacementId;
            Line = line;
        }
    }

    [Header("View Slots")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform mapContent;
    [SerializeField] private RectTransform connectionRoot;
    [SerializeField] private RectTransform roomRoot;
    [SerializeField] private DungeonMinimapNodeView roomTemplate;
    [SerializeField] private Image connectionTemplate;
    [SerializeField] private TMP_Text locationLabel;

    [Header("Graph Layout")]
    [SerializeField, Min(0f)] private float contentPadding = 10f;

    private readonly Dictionary<int, DungeonMinimapNodeView> roomViews = new();
    private readonly List<ConnectionView> connectionViews = new();

    private DungeonMinimapIconSetSO iconSet;
    private DungeonMapRuntimeController runtime;

    public void Configure(DungeonMinimapIconSetSO style)
    {
        iconSet = style;
        ResolveReferences();

        DungeonMapRuntimeController activeRuntime = DungeonMapRuntimeController.Active;
        if (runtime == activeRuntime)
            RebuildGraphViews();
        else
            AttachRuntime(activeRuntime);
    }

    private void Awake()
    {
        ResolveReferences();
        SetVisible(false);
    }

    private void OnEnable()
    {
        DungeonMapRuntimeController.ActiveChanged += HandleActiveRuntimeChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        AttachRuntime(DungeonMapRuntimeController.Active);
    }

    private void OnDisable()
    {
        DungeonMapRuntimeController.ActiveChanged -= HandleActiveRuntimeChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        AttachRuntime(null);
    }

    private void HandleActiveRuntimeChanged(DungeonMapRuntimeController activeRuntime)
    {
        AttachRuntime(activeRuntime);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachRuntime(DungeonMapRuntimeController.Active);
        RefreshLocationLabel();
    }

    private void AttachRuntime(DungeonMapRuntimeController targetRuntime)
    {
        if (runtime == targetRuntime)
        {
            RefreshPresentation();
            return;
        }

        if (runtime != null)
            runtime.Changed -= HandleRuntimeChanged;

        runtime = targetRuntime;
        if (runtime != null)
            runtime.Changed += HandleRuntimeChanged;

        RebuildGraphViews();
    }

    private void HandleRuntimeChanged()
    {
        RefreshPresentation();
    }

    private void RebuildGraphViews()
    {
        ClearGraphViews();
        if (!HasValidView() ||
            iconSet == null ||
            runtime?.Graph == null ||
            runtime.Graph.Rooms.Count == 0)
        {
            SetVisible(false);
            return;
        }

        IReadOnlyList<DungeonMapRoomNode> rooms = runtime.Graph.Rooms;
        ResolveWorldBounds(rooms, out Vector2 center, out Vector2 span);
        Vector2 mapSize = ResolveRectSize(mapContent);
        Vector2 availableSize = new(
            Mathf.Max(1f, mapSize.x - contentPadding * 2f),
            Mathf.Max(1f, mapSize.y - contentPadding * 2f));
        float scaleX = span.x > 0.001f ? availableSize.x / span.x : float.MaxValue;
        float scaleY = span.y > 0.001f ? availableSize.y / span.y : float.MaxValue;
        float graphScale = Mathf.Min(scaleX, scaleY);
        if (float.IsInfinity(graphScale) || graphScale == float.MaxValue)
            graphScale = 1f;

        for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            DungeonMapRoomNode room = rooms[roomIndex];
            DungeonMinimapNodeView roomView = Instantiate(roomTemplate, roomRoot, false);
            roomView.name = $"Room_{room.PlacementId}_{room.RoomType}";
            roomView.ConfigureIdentity(room.PlacementId, room.RoomType);
            roomView.ConfigureShape(room.ShapeRectangles, room.ShapeGridSize);
            roomView.NodeRect.anchoredPosition = (room.WorldCenter - center) * graphScale;
            roomView.NodeRect.sizeDelta = new Vector2(
                Mathf.Max(1f, room.WorldBounds.width * graphScale),
                Mathf.Max(1f, room.WorldBounds.height * graphScale));
            roomView.gameObject.SetActive(true);
            roomViews[room.PlacementId] = roomView;
        }

        IReadOnlyList<DungeonMapConnection> connections = runtime.Graph.Connections;
        for (int connectionIndex = 0;
             connectionIndex < connections.Count;
             connectionIndex++)
        {
            DungeonMapConnection connection = connections[connectionIndex];
            if (!roomViews.TryGetValue(
                    connection.FirstRoomPlacementId,
                    out DungeonMinimapNodeView first) ||
                !roomViews.TryGetValue(
                    connection.SecondRoomPlacementId,
                    out DungeonMinimapNodeView second))
            {
                continue;
            }

            connectionViews.Add(CreateConnectionView(
                connection,
                center,
                graphScale,
                first,
                second));
        }

        RefreshPresentation();
    }

    private ConnectionView CreateConnectionView(
        DungeonMapConnection connection,
        Vector2 graphCenter,
        float graphScale,
        DungeonMinimapNodeView first,
        DungeonMinimapNodeView second)
    {
        Image line = Instantiate(connectionTemplate, connectionRoot, false);
        line.name =
            $"Connection_{connection.FirstRoomPlacementId}_{connection.SecondRoomPlacementId}";
        RectTransform lineRect = line.rectTransform;
        Vector2 firstPosition = connection.HasSocketEndpoints
            ? (connection.FirstWorldSocketCenter - graphCenter) * graphScale
            : first.NodeRect.anchoredPosition;
        Vector2 secondPosition = connection.HasSocketEndpoints
            ? (connection.SecondWorldSocketCenter - graphCenter) * graphScale
            : second.NodeRect.anchoredPosition;
        Vector2 delta = secondPosition - firstPosition;
        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.pivot = new Vector2(0f, 0.5f);
        lineRect.anchoredPosition = firstPosition;
        lineRect.sizeDelta = new Vector2(delta.magnitude, lineRect.sizeDelta.y);
        lineRect.localRotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        line.gameObject.SetActive(true);

        return new ConnectionView(
            connection.FirstRoomPlacementId,
            connection.SecondRoomPlacementId,
            line);
    }

    private void RefreshPresentation()
    {
        if (!HasValidView() ||
            iconSet == null ||
            runtime?.Graph == null ||
            roomViews.Count == 0)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        foreach (DungeonMinimapNodeView roomView in roomViews.Values)
        {
            DungeonMapRoomVisibility visibility =
                runtime.GetVisibility(roomView.PlacementId);
            bool isCurrent = runtime.CurrentRoomPlacementId == roomView.PlacementId;
            iconSet.TryGetRoomIcon(
                roomView.RoomType,
                out DungeonMinimapRoomIconData iconData);
            roomView.Apply(visibility, isCurrent, iconData, iconSet);
        }

        for (int connectionIndex = 0;
             connectionIndex < connectionViews.Count;
             connectionIndex++)
        {
            ConnectionView connection = connectionViews[connectionIndex];
            DungeonMapRoomVisibility firstVisibility =
                runtime.GetVisibility(connection.FirstRoomPlacementId);
            DungeonMapRoomVisibility secondVisibility =
                runtime.GetVisibility(connection.SecondRoomPlacementId);
            bool isVisible = firstVisibility != DungeonMapRoomVisibility.Unknown &&
                             secondVisibility != DungeonMapRoomVisibility.Unknown;
            connection.Line.gameObject.SetActive(isVisible);
            if (!isVisible)
                continue;

            bool bothVisited = firstVisibility == DungeonMapRoomVisibility.Visited &&
                               secondVisibility == DungeonMapRoomVisibility.Visited;
            connection.Line.color = bothVisited
                ? iconSet.VisitedConnectionColor
                : iconSet.RevealedConnectionColor;
        }

        RefreshLocationLabel();
    }

    private void RefreshLocationLabel()
    {
        if (locationLabel == null || runtime == null)
            return;

        string sceneName = SceneManager.GetActiveScene().name;
        locationLabel.text = RunRoutePlayback.TryResolveCurrentLocationName(
            sceneName,
            out string locationName)
            ? locationName
            : sceneName;
    }

    private void ClearGraphViews()
    {
        roomViews.Clear();
        connectionViews.Clear();
        DestroyGeneratedChildren(roomRoot, roomTemplate != null ? roomTemplate.transform : null);
        DestroyGeneratedChildren(
            connectionRoot,
            connectionTemplate != null ? connectionTemplate.transform : null);
    }

    private bool HasValidView()
    {
        ResolveReferences();
        return canvasGroup != null &&
               mapContent != null &&
               connectionRoot != null &&
               roomRoot != null &&
               roomTemplate != null &&
               roomTemplate.NodeRect != null &&
               connectionTemplate != null &&
               locationLabel != null;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = visible ? 1f : 0f;
    }

    private void ResolveReferences()
    {
        canvasGroup ??= GetComponent<CanvasGroup>();
    }

    private static Vector2 ResolveRectSize(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return Vector2.zero;

        Vector2 rectSize = rectTransform.rect.size;
        if (rectSize.x <= 0f)
            rectSize.x = Mathf.Abs(rectTransform.sizeDelta.x);
        if (rectSize.y <= 0f)
            rectSize.y = Mathf.Abs(rectTransform.sizeDelta.y);
        return rectSize;
    }

    private static void ResolveWorldBounds(
        IReadOnlyList<DungeonMapRoomNode> rooms,
        out Vector2 center,
        out Vector2 span)
    {
        Vector2 minimum = rooms[0].WorldBounds.min;
        Vector2 maximum = rooms[0].WorldBounds.max;
        for (int roomIndex = 1; roomIndex < rooms.Count; roomIndex++)
        {
            minimum = Vector2.Min(minimum, rooms[roomIndex].WorldBounds.min);
            maximum = Vector2.Max(maximum, rooms[roomIndex].WorldBounds.max);
        }

        center = (minimum + maximum) * 0.5f;
        span = maximum - minimum;
    }

    private static void DestroyGeneratedChildren(Transform parent, Transform template)
    {
        if (parent == null)
            return;

        for (int childIndex = parent.childCount - 1; childIndex >= 0; childIndex--)
        {
            Transform child = parent.GetChild(childIndex);
            if (child == template)
                continue;

            child.gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        CanvasGroup visibilityGroup,
        RectTransform content,
        RectTransform lines,
        RectTransform rooms,
        DungeonMinimapNodeView nodeTemplate,
        Image lineTemplate,
        TMP_Text areaLabel,
        float padding)
    {
        canvasGroup = visibilityGroup;
        mapContent = content;
        connectionRoot = lines;
        roomRoot = rooms;
        roomTemplate = nodeTemplate;
        connectionTemplate = lineTemplate;
        locationLabel = areaLabel;
        contentPadding = Mathf.Max(0f, padding);
    }
#endif
}

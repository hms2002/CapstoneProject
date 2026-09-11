using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 : 미니맵 방 노드 프리팹의 배경, RoomType 아이콘과 현재 위치 마커를 한 표시 단위로 갱신한다.
/// 방문한 방의 콘텐츠 배지를 역할 아이콘과 함께 배치하되 게임플레이 상태를 변경하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonMinimapNodeView : MonoBehaviour
{
    [SerializeField] private RectTransform nodeRect;
    [SerializeField] private DungeonMinimapRoomShapeGraphic background;
    [SerializeField] private Image roomIcon;
    [SerializeField] private DungeonMinimapRoomShapeGraphic currentMarker;
    [SerializeField] private RectTransform iconGroup;
    [SerializeField] private DungeonMinimapContentBadgeView closedChestBadge;
    [SerializeField] private DungeonMinimapContentBadgeView openedChestBadge;
    [SerializeField] private DungeonMinimapContentBadgeView heartBadge;
    [SerializeField, Min(1f)] private float iconSize = 10f;
    [SerializeField, Min(0f)] private float iconSpacing = 2f;
    private readonly List<RectTransform> visibleIcons = new(4);
    private Vector2 safeIconSize = Vector2.one;

    public int PlacementId { get; private set; } = -1;
    public RoomType RoomType { get; private set; }
    public RectTransform NodeRect => nodeRect;

    public void ConfigureIdentity(int placementId, RoomType roomType)
    {
        PlacementId = placementId;
        RoomType = roomType;
        ResolveReferences();
    }

    public void ConfigureShape(
        IReadOnlyList<RectInt> shapeRectangles,
        Vector2Int shapeGridSize,
        Vector2? iconAnchor = null)
    {
        ResolveReferences();
        background?.ConfigureShape(shapeRectangles, shapeGridSize);
        currentMarker?.ConfigureShape(shapeRectangles, shapeGridSize);
        Vector2 autoAnchor = DungeonMapRoomShapeBuilder.ResolveInteriorAnchor(
            shapeRectangles, shapeGridSize, out safeIconSize);
        PositionIconInsideShape(iconAnchor ?? autoAnchor);
        if (iconAnchor.HasValue && (iconAnchor.Value - autoAnchor).sqrMagnitude > 0.000001f)
            safeIconSize = Vector2.zero;
    }

    public void Apply(
        DungeonMapRoomVisibility visibility,
        bool isCurrent,
        DungeonMinimapRoomIconData iconData,
        DungeonMinimapIconSetSO iconSet,
        DungeonMapRoomContents contents = default)
    {
        ResolveReferences();
        bool isVisible = visibility != DungeonMapRoomVisibility.Unknown;
        gameObject.SetActive(isVisible);
        if (!isVisible || iconSet == null)
            return;

        bool isVisited = visibility == DungeonMapRoomVisibility.Visited;
        if (background != null)
        {
            background.color = isCurrent
                ? iconSet.CurrentNodeColor
                : isVisited
                    ? iconSet.VisitedNodeColor
                    : iconSet.RevealedNodeColor;
        }

        if (roomIcon != null)
        {
            Sprite sprite = isVisited ? iconData.NormalIcon : iconData.SilhouetteIcon;
            roomIcon.sprite = sprite;
            roomIcon.color = isVisited
                ? iconData.NormalIconTint
                : iconData.SilhouetteIconTint;
            roomIcon.gameObject.SetActive(sprite != null);
        }

        if (currentMarker != null)
            currentMarker.gameObject.SetActive(isCurrent);

        visibleIcons.Clear();
        if (roomIcon != null && roomIcon.gameObject.activeSelf)
            visibleIcons.Add(roomIcon.rectTransform);
        AddContentBadge(closedChestBadge, DungeonMapContentKind.ClosedChest, contents, isVisited, iconSet);
        AddContentBadge(openedChestBadge, DungeonMapContentKind.OpenedChest, contents, isVisited, iconSet);
        AddContentBadge(heartBadge, DungeonMapContentKind.Heart, contents, isVisited, iconSet);
        LayoutIcons();
    }

    private void AddContentBadge(DungeonMinimapContentBadgeView badge, DungeonMapContentKind kind,
        DungeonMapRoomContents contents, bool isVisited, DungeonMinimapIconSetSO iconSet)
    {
        if (badge != null && badge.Apply(isVisited ? contents.GetCount(kind) : 0, iconSet.GetContentIcon(kind)))
            visibleIcons.Add(badge.Rect);
    }

    private void LayoutIcons()
    {
        if (iconGroup == null || nodeRect == null || visibleIcons.Count == 0)
            return;
        int columns = Mathf.Min(2, visibleIcons.Count);
        int rows = (visibleIcons.Count + columns - 1) / columns;
        float step = iconSize + iconSpacing;
        Vector2 size = new(columns * step - iconSpacing, rows * step - iconSpacing);
        Vector2 anchor = iconGroup.anchorMin;
        Vector2 available = new(nodeRect.rect.width * 2f * Mathf.Min(anchor.x, 1f - anchor.x),
            nodeRect.rect.height * 2f * Mathf.Min(anchor.y, 1f - anchor.y));
        if (safeIconSize.x > 0f && safeIconSize.y > 0f)
            available = Vector2.Min(available, nodeRect.rect.size * safeIconSize);
        float scale = Mathf.Clamp01(Mathf.Min(available.x / size.x, available.y / size.y) * 0.9f);
        iconGroup.sizeDelta = size;
        iconGroup.localScale = Vector3.one * scale;
        for (int i = 0; i < visibleIcons.Count; i++)
        {
            RectTransform icon = visibleIcons[i];
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = Vector2.one * iconSize;
            int row = i / columns;
            int rowCount = Mathf.Min(columns, visibleIcons.Count - row * columns);
            icon.anchoredPosition = new Vector2((i % columns - (rowCount - 1) * 0.5f) * step,
                ((rows - 1) * 0.5f - row) * step);
        }
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        nodeRect ??= transform as RectTransform;
        background ??= GetComponent<DungeonMinimapRoomShapeGraphic>();

        if (roomIcon == null)
        {
            Transform iconTransform = transform.Find("Icon");
            roomIcon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        }

        if (currentMarker == null)
        {
            Transform markerTransform = transform.Find("CurrentMarker");
            currentMarker = markerTransform != null
                ? markerTransform.GetComponent<DungeonMinimapRoomShapeGraphic>()
                : null;
        }
    }

    private void PositionIconInsideShape(Vector2 normalizedAnchor)
    {
        if (roomIcon == null && iconGroup == null)
            return;

        RectTransform iconRect = iconGroup != null ? iconGroup : roomIcon.rectTransform;
        iconRect.anchorMin = normalizedAnchor;
        iconRect.anchorMax = normalizedAnchor;
        iconRect.anchoredPosition = Vector2.zero;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        RectTransform rect,
        DungeonMinimapRoomShapeGraphic nodeBackground,
        Image icon,
        DungeonMinimapRoomShapeGraphic marker)
    {
        nodeRect = rect;
        background = nodeBackground;
        roomIcon = icon;
        currentMarker = marker;
    }
#endif
}

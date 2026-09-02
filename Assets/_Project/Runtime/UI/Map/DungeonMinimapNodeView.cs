using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 : 미니맵 방 노드 프리팹의 배경, RoomType 아이콘과 현재 위치 마커를 한 표시 단위로 갱신한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonMinimapNodeView : MonoBehaviour
{
    [SerializeField] private RectTransform nodeRect;
    [SerializeField] private DungeonMinimapRoomShapeGraphic background;
    [SerializeField] private Image roomIcon;
    [SerializeField] private DungeonMinimapRoomShapeGraphic currentMarker;

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
        Vector2Int shapeGridSize)
    {
        ResolveReferences();
        background?.ConfigureShape(shapeRectangles, shapeGridSize);
        currentMarker?.ConfigureShape(shapeRectangles, shapeGridSize);
        PositionIconInsideShape(shapeRectangles, shapeGridSize);
    }

    public void Apply(
        DungeonMapRoomVisibility visibility,
        bool isCurrent,
        DungeonMinimapRoomIconData iconData,
        DungeonMinimapIconSetSO iconSet)
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

    private void PositionIconInsideShape(
        IReadOnlyList<RectInt> shapeRectangles,
        Vector2Int shapeGridSize)
    {
        if (roomIcon == null || shapeRectangles == null || shapeRectangles.Count == 0)
            return;

        Vector2 gridCenter = (Vector2)shapeGridSize * 0.5f;
        Vector2 selectedCellCenter = gridCenter;
        float shortestDistance = float.MaxValue;
        for (int rectangleIndex = 0;
             rectangleIndex < shapeRectangles.Count;
             rectangleIndex++)
        {
            RectInt rectangle = shapeRectangles[rectangleIndex];
            for (int y = rectangle.yMin; y < rectangle.yMax; y++)
            {
                for (int x = rectangle.xMin; x < rectangle.xMax; x++)
                {
                    Vector2 cellCenter = new(x + 0.5f, y + 0.5f);
                    float distance = (cellCenter - gridCenter).sqrMagnitude;
                    if (distance >= shortestDistance)
                        continue;

                    selectedCellCenter = cellCenter;
                    shortestDistance = distance;
                }
            }
        }

        RectTransform iconRect = roomIcon.rectTransform;
        Vector2 normalizedAnchor = new(
            selectedCellCenter.x / Mathf.Max(1, shapeGridSize.x),
            selectedCellCenter.y / Mathf.Max(1, shapeGridSize.y));
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

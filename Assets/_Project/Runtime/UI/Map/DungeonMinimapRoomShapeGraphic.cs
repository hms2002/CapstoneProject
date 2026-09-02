using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 : 압축된 방 타일 점유 직사각형을 하나의 UI 메시로 변환해 비정형 미니맵 노드 실루엣을 렌더링한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonMinimapRoomShapeGraphic : MaskableGraphic
{
    private IReadOnlyList<RectInt> shapeRectangles = Array.Empty<RectInt>();
    private Vector2Int shapeGridSize = Vector2Int.one;

    public void ConfigureShape(
        IReadOnlyList<RectInt> rectangles,
        Vector2Int gridSize)
    {
        shapeRectangles = rectangles ?? Array.Empty<RectInt>();
        shapeGridSize = new Vector2Int(
            Mathf.Max(1, gridSize.x),
            Mathf.Max(1, gridSize.y));
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect targetRect = GetPixelAdjustedRect();
        if (shapeRectangles == null || shapeRectangles.Count == 0)
        {
            AddQuad(vertexHelper, targetRect);
            return;
        }

        float cellWidth = targetRect.width / shapeGridSize.x;
        float cellHeight = targetRect.height / shapeGridSize.y;
        for (int rectangleIndex = 0;
             rectangleIndex < shapeRectangles.Count;
             rectangleIndex++)
        {
            RectInt shapeRectangle = shapeRectangles[rectangleIndex];
            if (shapeRectangle.width <= 0 || shapeRectangle.height <= 0)
                continue;

            AddQuad(
                vertexHelper,
                new Rect(
                    targetRect.xMin + shapeRectangle.xMin * cellWidth,
                    targetRect.yMin + shapeRectangle.yMin * cellHeight,
                    shapeRectangle.width * cellWidth,
                    shapeRectangle.height * cellHeight));
        }
    }

    private void AddQuad(VertexHelper vertexHelper, Rect quadRect)
    {
        int startIndex = vertexHelper.currentVertCount;
        Color32 vertexColor = color;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = vertexColor;

        vertex.position = new Vector3(quadRect.xMin, quadRect.yMin);
        vertexHelper.AddVert(vertex);
        vertex.position = new Vector3(quadRect.xMin, quadRect.yMax);
        vertexHelper.AddVert(vertex);
        vertex.position = new Vector3(quadRect.xMax, quadRect.yMax);
        vertexHelper.AddVert(vertex);
        vertex.position = new Vector3(quadRect.xMax, quadRect.yMin);
        vertexHelper.AddVert(vertex);

        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
    }
}

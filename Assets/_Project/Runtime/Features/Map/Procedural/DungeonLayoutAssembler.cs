using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 룸 라이브러리의 템플릿을 Seed 기반으로 선택해 소켓끼리 연결된 방 배치 결과를 만든다.
/// - 연결되는 두 방의 진행축 크기와 난수 편차로 각 직선 복도의 길이를 정한다.
/// - 방과 복도의 예약 bounds 겹침, 소켓 방향/폭, 방 사이 간격만 판단하며 테마와 Tilemap 구현 세부사항은 알지 않는다.
/// </summary>
public sealed class DungeonLayoutAssembler
{
    /// <summary>
    /// 책임:
    /// - 아직 다른 방과 연결되지 않은 배치 소켓의 방/소켓 인덱스, 폭, 월드 시작 셀을 추적한다.
    /// </summary>
    private readonly struct OpenSocket
    {
        public int RoomPlacementId { get; }
        public int SocketIndex { get; }
        public RoomSocketDirection Direction { get; }
        public int Width { get; }
        public Vector2Int WorldCell { get; }

        public OpenSocket(
            int roomPlacementId,
            int socketIndex,
            RoomSocketDirection direction,
            int width,
            Vector2Int worldCell)
        {
            RoomPlacementId = roomPlacementId;
            SocketIndex = socketIndex;
            Direction = direction;
            Width = width;
            WorldCell = worldCell;
        }
    }

    public DungeonLayoutResult Assemble(
        RoomThemeLibrarySO library,
        int seed,
        int requestedRoomCount,
        bool includeBossRoom,
        int maxPlacementAttemptsPerRoom,
        int minimumCorridorLength,
        float corridorLengthPerRoomCell,
        int corridorLengthVariation)
    {
        int targetRoomCount = Mathf.Max(includeBossRoom ? 2 : 1, requestedRoomCount);
        int resolvedMinimumCorridorLength = Mathf.Max(0, minimumCorridorLength);
        float resolvedCorridorLengthPerRoomCell = float.IsFinite(corridorLengthPerRoomCell)
            ? Mathf.Clamp(corridorLengthPerRoomCell, 0f, 1f)
            : 0f;
        int resolvedCorridorLengthVariation = Mathf.Clamp(corridorLengthVariation, 0, 32);
        DungeonLayoutResult result = new(seed, targetRoomCount);

        if (library == null)
        {
            result.MarkFailed("Room theme library is missing.");
            return result;
        }

        System.Random random = new(seed);
        List<RoomTemplateSO> startCandidates = new();
        library.CollectRooms(RoomType.Start, startCandidates);
        RemoveUnusableTemplates(startCandidates);

        RoomTemplateSO startTemplate = SelectWeightedTemplate(startCandidates, random);
        if (startTemplate == null)
        {
            result.MarkFailed("The room library has no usable Start room.");
            return result;
        }

        RectInt startLocalBounds = ResolveLocalBounds(startTemplate.LayoutData);
        Vector2Int startOrigin = -startLocalBounds.position;
        DungeonRoomPlacement startPlacement = new(
            0,
            startTemplate,
            startOrigin,
            TranslateBounds(startLocalBounds, startOrigin));
        result.AddRoom(startPlacement);

        List<OpenSocket> openSockets = new();
        AddOpenSockets(startPlacement, -1, openSockets);

        List<RoomTemplateSO> expansionCandidates = new();
        library.CollectExpansionRooms(expansionCandidates);
        RemoveUnusableTemplates(expansionCandidates);

        List<RoomTemplateSO> bossCandidates = new();
        if (includeBossRoom)
        {
            library.CollectRooms(RoomType.Boss, bossCandidates);
            RemoveUnusableTemplates(bossCandidates);
        }

        int attempts = Mathf.Max(1, maxPlacementAttemptsPerRoom);
        for (int roomIndex = 1; roomIndex < targetRoomCount; roomIndex++)
        {
            bool placingBoss = includeBossRoom && roomIndex == targetRoomCount - 1;
            List<RoomTemplateSO> candidates = placingBoss ? bossCandidates : expansionCandidates;

            if (candidates.Count == 0)
            {
                result.MarkFailed(placingBoss
                    ? "The room library has no usable Boss room."
                    : "The room library has no usable expansion room.");
                return result;
            }

            if (openSockets.Count == 0)
            {
                result.MarkFailed("No open room socket remains for layout expansion.");
                return result;
            }

            if (!TryPlaceNextRoom(
                    result,
                    openSockets,
                    candidates,
                    random,
                    attempts,
                    resolvedMinimumCorridorLength,
                    resolvedCorridorLengthPerRoomCell,
                    resolvedCorridorLengthVariation))
            {
                result.MarkFailed(
                    $"Failed to place room {roomIndex + 1}/{targetRoomCount} without overlap.");
                return result;
            }
        }

        result.MarkComplete();
        return result;
    }

    private static bool TryPlaceNextRoom(
        DungeonLayoutResult result,
        List<OpenSocket> openSockets,
        List<RoomTemplateSO> candidates,
        System.Random random,
        int maxAttempts,
        int minimumCorridorLength,
        float corridorLengthPerRoomCell,
        int corridorLengthVariation)
    {
        List<int> compatibleSocketIndices = new();

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int openSocketListIndex = random.Next(openSockets.Count);
            OpenSocket openSocket = openSockets[openSocketListIndex];
            RoomTemplateSO candidate = SelectWeightedTemplate(candidates, random);
            if (candidate == null)
                return false;

            CollectCompatibleSocketIndices(
                candidate.LayoutData,
                Opposite(openSocket.Direction),
                openSocket.Width,
                compatibleSocketIndices);
            if (compatibleSocketIndices.Count == 0)
                continue;

            int candidateSocketIndex = compatibleSocketIndices[random.Next(compatibleSocketIndices.Count)];
            RoomSocketData candidateSocket = candidate.LayoutData.sockets[candidateSocketIndex];
            DungeonRoomPlacement sourcePlacement = result.GetRoom(openSocket.RoomPlacementId);
            if (sourcePlacement == null)
                continue;

            RectInt candidateLocalBounds = ResolveLocalBounds(candidate.LayoutData);
            int corridorLength = ResolveCorridorLength(
                sourcePlacement.WorldBounds.size,
                candidateLocalBounds.size,
                openSocket.Direction,
                minimumCorridorLength,
                corridorLengthPerRoomCell,
                corridorLengthVariation,
                random);
            Vector2Int direction = DirectionToVector(openSocket.Direction);
            Vector2Int targetWorldCell =
                openSocket.WorldCell + direction * (corridorLength + 1);
            Vector2Int candidateOrigin = targetWorldCell - candidateSocket.localCell;
            RectInt candidateBounds = TranslateBounds(
                candidateLocalBounds,
                candidateOrigin);
            RectInt corridorBounds = CreateCorridorBounds(openSocket, corridorLength);

            if (OverlapsPlacedRoom(candidateBounds, result.Rooms) ||
                OverlapsExistingCorridor(candidateBounds, result.Connections))
            {
                continue;
            }

            if (corridorLength > 0 &&
                (OverlapsPlacedRoom(corridorBounds, result.Rooms) ||
                 OverlapsExistingCorridor(corridorBounds, result.Connections)))
            {
                continue;
            }

            int placementId = result.Rooms.Count;
            DungeonRoomPlacement placement = new(
                placementId,
                candidate,
                candidateOrigin,
                candidateBounds);
            result.AddRoom(placement);
            result.AddConnection(new DungeonSocketConnection(
                openSocket.RoomPlacementId,
                openSocket.SocketIndex,
                placementId,
                candidateSocketIndex,
                corridorLength,
                corridorBounds));

            openSockets.RemoveAt(openSocketListIndex);
            AddOpenSockets(placement, candidateSocketIndex, openSockets);
            return true;
        }

        return false;
    }

    private static int ResolveCorridorLength(
        Vector2Int sourceRoomSize,
        Vector2Int targetRoomSize,
        RoomSocketDirection direction,
        int minimumCorridorLength,
        float corridorLengthPerRoomCell,
        int corridorLengthVariation,
        System.Random random)
    {
        bool horizontal = direction == RoomSocketDirection.Left ||
            direction == RoomSocketDirection.Right;
        int sourceDepth = horizontal ? sourceRoomSize.x : sourceRoomSize.y;
        int targetDepth = horizontal ? targetRoomSize.x : targetRoomSize.y;
        int sizeDrivenLength = Mathf.CeilToInt(
            (Mathf.Max(0, sourceDepth) + Mathf.Max(0, targetDepth)) *
            corridorLengthPerRoomCell);
        int randomVariation = corridorLengthVariation > 0
            ? random.Next(corridorLengthVariation + 1)
            : 0;
        return minimumCorridorLength + sizeDrivenLength + randomVariation;
    }

    private static void AddOpenSockets(
        DungeonRoomPlacement placement,
        int consumedSocketIndex,
        List<OpenSocket> results)
    {
        RoomLayoutData layout = placement.Template.LayoutData;
        if (layout.sockets == null)
            return;

        RectInt localBounds = ResolveLocalBounds(layout);
        for (int i = 0; i < layout.sockets.Count; i++)
        {
            if (i == consumedSocketIndex)
                continue;

            RoomSocketData socket = layout.sockets[i];
            if (!IsSocketValid(socket, localBounds))
                continue;

            results.Add(new OpenSocket(
                placement.PlacementId,
                i,
                socket.direction,
                RoomSocketGeometry.ResolveWidth(socket),
                placement.Origin + socket.localCell));
        }
    }

    private static void CollectCompatibleSocketIndices(
        RoomLayoutData layout,
        RoomSocketDirection requiredDirection,
        int requiredWidth,
        List<int> results)
    {
        results.Clear();
        if (layout.sockets == null)
            return;

        RectInt localBounds = ResolveLocalBounds(layout);
        for (int i = 0; i < layout.sockets.Count; i++)
        {
            RoomSocketData socket = layout.sockets[i];
            if (socket.direction == requiredDirection &&
                RoomSocketGeometry.ResolveWidth(socket) == requiredWidth &&
                IsSocketValid(socket, localBounds))
            {
                results.Add(i);
            }
        }
    }

    private static void RemoveUnusableTemplates(List<RoomTemplateSO> templates)
    {
        for (int i = templates.Count - 1; i >= 0; i--)
        {
            if (!IsTemplateUsable(templates[i]))
                templates.RemoveAt(i);
        }
    }

    private static bool IsTemplateUsable(RoomTemplateSO template)
    {
        if (template == null)
            return false;

        RoomLayoutData layout = template.LayoutData;
        float weight = layout.selectionWeight;
        if (weight <= 0f || float.IsNaN(weight) || float.IsInfinity(weight))
            return false;

        RectInt bounds = ResolveLocalBounds(layout);
        if (bounds.width <= 0 || bounds.height <= 0 || layout.sockets == null)
            return false;

        for (int i = 0; i < layout.sockets.Count; i++)
        {
            if (IsSocketValid(layout.sockets[i], bounds))
                return true;
        }

        return false;
    }

    private static RoomTemplateSO SelectWeightedTemplate(
        List<RoomTemplateSO> candidates,
        System.Random random)
    {
        double totalWeight = 0d;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += candidates[i].LayoutData.selectionWeight;

        if (totalWeight <= 0d)
            return null;

        double selectedWeight = random.NextDouble() * totalWeight;
        for (int i = 0; i < candidates.Count; i++)
        {
            selectedWeight -= candidates[i].LayoutData.selectionWeight;
            if (selectedWeight <= 0d)
                return candidates[i];
        }

        return candidates[candidates.Count - 1];
    }

    private static bool OverlapsPlacedRoom(
        RectInt candidateBounds,
        IReadOnlyList<DungeonRoomPlacement> placedRooms)
    {
        for (int i = 0; i < placedRooms.Count; i++)
        {
            if (candidateBounds.Overlaps(placedRooms[i].WorldBounds))
                return true;
        }

        return false;
    }

    private static bool OverlapsExistingCorridor(
        RectInt candidateBounds,
        IReadOnlyList<DungeonSocketConnection> connections)
    {
        for (int i = 0; i < connections.Count; i++)
        {
            RectInt corridorBounds = connections[i].CorridorBounds;
            if (corridorBounds.width > 0 &&
                corridorBounds.height > 0 &&
                candidateBounds.Overlaps(corridorBounds))
            {
                return true;
            }
        }

        return false;
    }

    private static RectInt CreateCorridorBounds(OpenSocket socket, int corridorLength)
    {
        if (corridorLength <= 0)
            return default;

        Vector2Int direction = DirectionToVector(socket.Direction);
        Vector2Int tangent = RoomSocketGeometry.GetTangent(socket.Direction);
        Vector2Int firstCorner = socket.WorldCell + direction - tangent;
        Vector2Int oppositeCorner =
            socket.WorldCell + direction * corridorLength + tangent * socket.Width;
        int xMin = Mathf.Min(firstCorner.x, oppositeCorner.x);
        int yMin = Mathf.Min(firstCorner.y, oppositeCorner.y);
        int xMax = Mathf.Max(firstCorner.x, oppositeCorner.x);
        int yMax = Mathf.Max(firstCorner.y, oppositeCorner.y);
        return new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
    }

    private static RectInt ResolveLocalBounds(RoomLayoutData layout)
    {
        if (layout.localBounds.width > 0 && layout.localBounds.height > 0)
            return layout.localBounds;

        return new RectInt(Vector2Int.zero, layout.size);
    }

    private static RectInt TranslateBounds(RectInt bounds, Vector2Int origin)
    {
        return new RectInt(bounds.position + origin, bounds.size);
    }

    private static bool IsSocketValid(RoomSocketData socket, RectInt bounds)
    {
        return RoomSocketGeometry.IsValid(socket, bounds);
    }

    private static RoomSocketDirection Opposite(RoomSocketDirection direction)
    {
        return direction switch
        {
            RoomSocketDirection.Up => RoomSocketDirection.Down,
            RoomSocketDirection.Right => RoomSocketDirection.Left,
            RoomSocketDirection.Down => RoomSocketDirection.Up,
            RoomSocketDirection.Left => RoomSocketDirection.Right,
            _ => direction
        };
    }

    private static Vector2Int DirectionToVector(RoomSocketDirection direction)
    {
        return direction switch
        {
            RoomSocketDirection.Up => Vector2Int.up,
            RoomSocketDirection.Right => Vector2Int.right,
            RoomSocketDirection.Down => Vector2Int.down,
            RoomSocketDirection.Left => Vector2Int.left,
            _ => Vector2Int.zero
        };
    }
}

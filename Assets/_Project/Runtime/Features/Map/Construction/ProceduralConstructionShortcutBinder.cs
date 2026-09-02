using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 절차 생성 지름길 방에서 실제로 연결된 소켓 중 하나를 향하도록 공사장 통로만 회전시켜 우회 불가능한 잠금 경로를 구성한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProceduralConstructionShortcutBinder :
    MonoBehaviour,
    IProceduralRoomRuntimeFeature
{
    [SerializeField] private Transform orientationRoot;
    [SerializeField] private RoomSocketDirection authoredGateDirection = RoomSocketDirection.Left;

    private Quaternion authoredLocalRotation;
    private bool hasCapturedAuthoredRotation;

    public Transform OrientationRoot => orientationRoot;
    public bool IsBound { get; private set; }
    public RoomSocketDirection BoundGateDirection { get; private set; }

    private void Awake()
    {
        CaptureAuthoredRotation();
    }

    public bool TryBindProceduralRoom(
        ProceduralRoomRuntimeContext context,
        out string failureReason)
    {
        if (context == null)
        {
            failureReason = "The procedural room context is missing.";
            return false;
        }

        if (orientationRoot == null)
        {
            failureReason = "The construction-site orientation root is missing.";
            return false;
        }

        IReadOnlyList<RoomSocketDirection> connectedDirections =
            context.ConnectedSocketDirections;
        if (connectedDirections == null || connectedDirections.Count != 2)
        {
            failureReason =
                $"Construction shortcut room '{context.RoomId}' must have exactly two connected sockets; " +
                $"found {connectedDirections?.Count ?? 0}.";
            return false;
        }

        RoomSocketDirection firstDirection = connectedDirections[0];
        RoomSocketDirection secondDirection = connectedDirections[1];
        if (!ArePerpendicular(firstDirection, secondDirection))
        {
            failureReason =
                $"Construction shortcut room '{context.RoomId}' requires a corner connection, but received " +
                $"{firstDirection} and {secondDirection}.";
            return false;
        }

        CaptureAuthoredRotation();
        BoundGateDirection = firstDirection;
        orientationRoot.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                ResolveAngle(BoundGateDirection) - ResolveAngle(authoredGateDirection)) *
            authoredLocalRotation;
        IsBound = true;
        failureReason = string.Empty;
        return true;
    }

    public void EditorConfigure(
        Transform targetOrientationRoot,
        RoomSocketDirection gateDirection)
    {
        orientationRoot = targetOrientationRoot;
        authoredGateDirection = gateDirection;
        hasCapturedAuthoredRotation = false;
        CaptureAuthoredRotation();
    }

    private void CaptureAuthoredRotation()
    {
        if (hasCapturedAuthoredRotation || orientationRoot == null)
            return;

        authoredLocalRotation = orientationRoot.localRotation;
        hasCapturedAuthoredRotation = true;
    }

    private static bool ArePerpendicular(
        RoomSocketDirection first,
        RoomSocketDirection second)
    {
        int difference = Mathf.Abs((int)first - (int)second);
        return difference == 1 || difference == 3;
    }

    private static float ResolveAngle(RoomSocketDirection direction)
    {
        return direction switch
        {
            RoomSocketDirection.Up => 90f,
            RoomSocketDirection.Right => 0f,
            RoomSocketDirection.Down => -90f,
            RoomSocketDirection.Left => 180f,
            _ => 0f
        };
    }
}

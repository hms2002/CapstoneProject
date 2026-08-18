using UnityEngine;

/// <summary>
/// 책임 : Gameplay/Core 호출자가 구체 입력 서비스 없이 현재 입력 상태를 조회하게 하는 backend 계약이다.
/// </summary>
public interface IInputActionQueryBackend
{
    Component BackendComponent { get; }
    bool WasPressedThisFrame(InputActionId action);
    bool WasReleasedThisFrame(InputActionId action);
    bool IsPressed(InputActionId action);
    bool IsKeyPressed(KeyCode key);
    bool WasKeyPressedThisFrame(KeyCode key);
    bool WasKeyReleasedThisFrame(KeyCode key);
    Vector2 GetMoveVectorRaw();
    Vector2 GetMoveVectorNormalized();
    Vector3 GetPointerWorldPosition(Camera camera, float z = 0f);
}

/// <summary>
/// 책임 : Gameplay 코드가 Infrastructure 입력 서비스 대신 사용하는 Core 입력 조회 진입점이다.
/// </summary>
public static class InputActionQuery
{
    private static IInputActionQueryBackend backend;

    public static bool IsAvailable => IsBackendAlive(backend);

    public static void RegisterBackend(IInputActionQueryBackend queryBackend)
    {
        backend = queryBackend;
    }

    public static void UnregisterBackend(IInputActionQueryBackend queryBackend)
    {
        if (ReferenceEquals(backend, queryBackend))
            backend = null;
    }

    public static bool WasPressedThisFrame(InputActionId action)
    {
        return IsBackendAlive(backend) && backend.WasPressedThisFrame(action);
    }

    public static bool WasReleasedThisFrame(InputActionId action)
    {
        return IsBackendAlive(backend) && backend.WasReleasedThisFrame(action);
    }

    public static bool IsPressed(InputActionId action)
    {
        return IsBackendAlive(backend) && backend.IsPressed(action);
    }

    public static bool IsKeyPressed(KeyCode key)
    {
        return IsBackendAlive(backend) && backend.IsKeyPressed(key);
    }

    public static bool WasKeyPressedThisFrame(KeyCode key)
    {
        return IsBackendAlive(backend) && backend.WasKeyPressedThisFrame(key);
    }

    public static bool WasKeyReleasedThisFrame(KeyCode key)
    {
        return IsBackendAlive(backend) && backend.WasKeyReleasedThisFrame(key);
    }

    public static Vector2 GetMoveVectorRaw()
    {
        return IsBackendAlive(backend) ? backend.GetMoveVectorRaw() : Vector2.zero;
    }

    public static Vector2 GetMoveVectorNormalized()
    {
        return IsBackendAlive(backend) ? backend.GetMoveVectorNormalized() : Vector2.zero;
    }

    public static Vector3 GetPointerWorldPosition(Camera camera, float z = 0f)
    {
        return IsBackendAlive(backend) ? backend.GetPointerWorldPosition(camera, z) : Vector3.zero;
    }

    private static bool IsBackendAlive(IInputActionQueryBackend candidate)
    {
        return candidate != null && candidate.BackendComponent != null;
    }
}

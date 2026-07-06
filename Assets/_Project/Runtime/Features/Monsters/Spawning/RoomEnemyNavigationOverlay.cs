using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
// 책임: 방 안의 남은 몬스터가 화면 밖에 있을 때 방향 화살표를 생성하고 갱신한다.
public sealed class RoomEnemyNavigationOverlay : MonoBehaviour
{
    [Header("Arrow")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField, Min(1)] private int showThreshold = 4;
    [SerializeField, Range(0f, 0.45f)] private float viewportPadding = 0.08f;
    [SerializeField] private Vector2 arrowVisualForward = Vector2.left;

    [Header("Camera")]
    [SerializeField] private Camera worldCamera;

    private const float DirectionEpsilon = 0.0001f;

    private readonly List<GameObject> aliveMonsters = new();
    private readonly List<GameObject> arrowInstances = new();
    private MonsterSpawnRoomGroup activeRoomGroup;
    private bool missingArrowPrefabLogged;
    private bool missingCameraLogged;

    private void OnEnable()
    {
        MonsterSpawnRoomGroup.ActiveRoomEntered += HandleActiveRoomEntered;
        MonsterSpawnRoomGroup.ActiveRoomExited += HandleActiveRoomExited;

        missingArrowPrefabLogged = false;
        missingCameraLogged = false;
        ResolveActiveRoomGroupFromScene();
        RefreshNavigation();
    }

    private void LateUpdate()
    {
        RefreshNavigation();
    }

    private void OnDisable()
    {
        MonsterSpawnRoomGroup.ActiveRoomEntered -= HandleActiveRoomEntered;
        MonsterSpawnRoomGroup.ActiveRoomExited -= HandleActiveRoomExited;
        HideArrowsFrom(0);
    }

    private void OnDestroy()
    {
        DestroyArrowInstances();
    }

    private void OnValidate()
    {
        showThreshold = Mathf.Max(1, showThreshold);
        viewportPadding = Mathf.Clamp(viewportPadding, 0f, 0.45f);

        if (arrowVisualForward.sqrMagnitude <= DirectionEpsilon)
            arrowVisualForward = Vector2.left;
    }

    private void HandleActiveRoomEntered(MonsterSpawnRoomGroup roomGroup)
    {
        if (roomGroup == null)
            return;

        activeRoomGroup = roomGroup;
        RefreshNavigation();
    }

    private void HandleActiveRoomExited(MonsterSpawnRoomGroup roomGroup)
    {
        if (roomGroup == null || roomGroup != activeRoomGroup)
            return;

        activeRoomGroup = null;
        HideArrowsFrom(0);
    }

    private void RefreshNavigation()
    {
        if (!ResolveActiveRoomGroup())
        {
            HideArrowsFrom(0);
            return;
        }

        int aliveCount = activeRoomGroup.GetAliveRegisteredMonstersNonAlloc(aliveMonsters);
        if (aliveCount <= 0 || aliveCount > showThreshold)
        {
            HideArrowsFrom(0);
            return;
        }

        if (arrowPrefab == null)
        {
            LogMissingArrowPrefab();
            HideArrowsFrom(0);
            return;
        }

        Camera camera = ResolveCamera();
        if (camera == null)
        {
            LogMissingCamera();
            HideArrowsFrom(0);
            return;
        }

        EnsureArrowInstances(aliveCount);

        for (int i = 0; i < aliveCount; i++)
        {
            GameObject monster = aliveMonsters[i];
            GameObject arrow = arrowInstances[i];
            if (monster == null || arrow == null)
                continue;

            Vector3 targetViewport = camera.WorldToViewportPoint(monster.transform.position);
            if (IsTargetVisible(camera, targetViewport))
            {
                HideArrow(arrow);
                continue;
            }

            Vector2 edgeViewport = ResolveEdgeViewportPosition(targetViewport);
            Vector3 arrowPosition = ViewportToOverlayPlane(camera, edgeViewport);
            Vector2 arrowDirection = ResolveCameraRelativeDirection(camera, edgeViewport, targetViewport);

            arrow.transform.SetPositionAndRotation(arrowPosition, ResolveArrowRotation(arrowDirection));

            if (!arrow.activeSelf)
                arrow.SetActive(true);
        }

        HideArrowsFrom(aliveCount);
    }

    private bool ResolveActiveRoomGroup()
    {
        if (activeRoomGroup != null && activeRoomGroup.isActiveAndEnabled && activeRoomGroup.PlayerEncounterEntered)
            return true;

        activeRoomGroup = null;
        return false;
    }

    private bool ResolveActiveRoomGroupFromScene()
    {
        MonsterSpawnRoomGroup[] roomGroups = FindObjectsByType<MonsterSpawnRoomGroup>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < roomGroups.Length; i++)
        {
            MonsterSpawnRoomGroup roomGroup = roomGroups[i];
            if (roomGroup == null || !roomGroup.PlayerEncounterEntered)
                continue;

            activeRoomGroup = roomGroup;
            return true;
        }

        return false;
    }

    private Camera ResolveCamera()
    {
        if (worldCamera != null && worldCamera.isActiveAndEnabled)
            return worldCamera;

        Camera camera = GameplayCameraViewQuery.GetMainCamera();
        return camera != null ? camera : Camera.main;
    }

    private static bool IsTargetVisible(Camera camera, Vector3 viewportPosition)
    {
        return viewportPosition.z >= camera.nearClipPlane
            && viewportPosition.z <= camera.farClipPlane
            && viewportPosition.x >= 0f
            && viewportPosition.x <= 1f
            && viewportPosition.y >= 0f
            && viewportPosition.y <= 1f;
    }

    private Vector2 ResolveEdgeViewportPosition(Vector3 targetViewport)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        Vector2 direction = new Vector2(targetViewport.x - center.x, targetViewport.y - center.y);
        if (direction.sqrMagnitude <= DirectionEpsilon)
            direction = Vector2.up;

        float halfExtent = Mathf.Max(0f, 0.5f - viewportPadding);
        float absX = Mathf.Abs(direction.x);
        float absY = Mathf.Abs(direction.y);
        float scaleX = absX > DirectionEpsilon ? halfExtent / absX : float.PositiveInfinity;
        float scaleY = absY > DirectionEpsilon ? halfExtent / absY : float.PositiveInfinity;
        float scale = Mathf.Min(scaleX, scaleY);

        Vector2 edge = center + direction * scale;
        float min = viewportPadding;
        float max = 1f - viewportPadding;
        return new Vector2(Mathf.Clamp(edge.x, min, max), Mathf.Clamp(edge.y, min, max));
    }

    private Vector3 ViewportToOverlayPlane(Camera camera, Vector2 viewportPosition)
    {
        float depth = Vector3.Dot(transform.position - camera.transform.position, camera.transform.forward);
        if (depth < camera.nearClipPlane)
            depth = camera.nearClipPlane + 0.01f;

        Vector3 worldPosition = camera.ViewportToWorldPoint(new Vector3(viewportPosition.x, viewportPosition.y, depth));
        worldPosition.z = transform.position.z;
        return worldPosition;
    }

    private static Vector2 ResolveCameraRelativeDirection(Camera camera, Vector2 edgeViewport, Vector3 targetViewport)
    {
        Vector2 viewportDirection = new Vector2(targetViewport.x, targetViewport.y) - edgeViewport;
        if (viewportDirection.sqrMagnitude <= DirectionEpsilon)
            viewportDirection = new Vector2(targetViewport.x - 0.5f, targetViewport.y - 0.5f);

        if (viewportDirection.sqrMagnitude <= DirectionEpsilon)
            viewportDirection = Vector2.up;

        Vector3 worldDirection =
            camera.transform.right * viewportDirection.x +
            camera.transform.up * viewportDirection.y;

        worldDirection.z = 0f;
        if (worldDirection.sqrMagnitude <= DirectionEpsilon)
            return viewportDirection.normalized;

        return new Vector2(worldDirection.x, worldDirection.y).normalized;
    }

    private Quaternion ResolveArrowRotation(Vector2 direction)
    {
        Vector2 visualForward = arrowVisualForward.sqrMagnitude > DirectionEpsilon
            ? arrowVisualForward.normalized
            : Vector2.left;

        float visualForwardAngle = Mathf.Atan2(visualForward.y, visualForward.x) * Mathf.Rad2Deg;
        float directionAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0f, 0f, directionAngle - visualForwardAngle);
    }

    private void EnsureArrowInstances(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (i < arrowInstances.Count && arrowInstances[i] != null)
                continue;

            GameObject arrow = Instantiate(arrowPrefab, transform);
            arrow.name = $"{arrowPrefab.name}_{i + 1}";
            arrow.SetActive(false);

            if (i < arrowInstances.Count)
                arrowInstances[i] = arrow;
            else
                arrowInstances.Add(arrow);
        }
    }

    private static void HideArrow(GameObject arrow)
    {
        if (arrow != null && arrow.activeSelf)
            arrow.SetActive(false);
    }

    private void HideArrowsFrom(int startIndex)
    {
        for (int i = Mathf.Max(0, startIndex); i < arrowInstances.Count; i++)
            HideArrow(arrowInstances[i]);
    }

    private void DestroyArrowInstances()
    {
        for (int i = arrowInstances.Count - 1; i >= 0; i--)
        {
            GameObject arrow = arrowInstances[i];
            if (arrow == null)
                continue;

            if (Application.isPlaying)
                Destroy(arrow);
            else
                DestroyImmediate(arrow);
        }

        arrowInstances.Clear();
    }

    private void LogMissingArrowPrefab()
    {
        if (missingArrowPrefabLogged)
            return;

        Debug.LogWarning("[RoomEnemyNavigationOverlay] Missing arrowPrefab.", this);
        missingArrowPrefabLogged = true;
    }

    private void LogMissingCamera()
    {
        if (missingCameraLogged)
            return;

        Debug.LogWarning("[RoomEnemyNavigationOverlay] Could not resolve a gameplay camera.", this);
        missingCameraLogged = true;
    }
}

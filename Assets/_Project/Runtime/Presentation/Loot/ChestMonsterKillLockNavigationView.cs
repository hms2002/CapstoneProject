using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// 책임 : ChestMonsterKillLock의 남은 몬스터 위치를 상자 주변 방향 화살표로 표시한다.
/// </summary>
public sealed class ChestMonsterKillLockNavigationView : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private ChestMonsterKillLock targetLock;
    [SerializeField] private Transform anchor;

    [Header("Arrow")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField, Min(1)] private int showThreshold = 4;
    [SerializeField, Min(0f)] private float arrowDistance = 1.35f;
    [SerializeField] private Vector2 additionalOffset;
    [SerializeField] private Vector2 arrowVisualForward = Vector2.left;

    private const float DirectionEpsilon = 0.0001f;
    private static readonly Color AnchorGizmoColor = new(1f, 0.86f, 0.24f, 0.95f);
    private static readonly Color RadiusGizmoColor = new(1f, 0.86f, 0.24f, 0.32f);
    private static readonly Color OffsetGizmoColor = new(0.3f, 0.95f, 1f, 0.85f);
    private static readonly Color ActiveMonsterGizmoColor = new(0.25f, 1f, 0.45f, 0.9f);
    private static readonly Color SuppressedMonsterGizmoColor = new(1f, 0.45f, 0.25f, 0.55f);

    private readonly List<GameObject> aliveMonsters = new();
    private readonly List<GameObject> arrowInstances = new();
    private bool missingArrowPrefabLogged;

    private void Reset()
    {
        ResolveTargetLock();
    }

    private void Awake()
    {
        ResolveTargetLock();
    }

    private void OnEnable()
    {
        ResolveTargetLock();
        missingArrowPrefabLogged = false;
        RefreshNavigation();
    }

    private void LateUpdate()
    {
        RefreshNavigation();
    }

    private void OnDisable()
    {
        HideArrowsFrom(0);
    }

    private void OnDestroy()
    {
        DestroyArrowInstances();
    }

    private void OnValidate()
    {
        showThreshold = Mathf.Max(1, showThreshold);
        arrowDistance = Mathf.Max(0f, arrowDistance);

        if (arrowVisualForward.sqrMagnitude <= DirectionEpsilon)
            arrowVisualForward = Vector2.left;

        ResolveTargetLock();
    }

    private void OnDrawGizmosSelected()
    {
        DrawNavigationGizmos();
    }

    private void RefreshNavigation()
    {
        if (targetLock == null)
        {
            HideArrowsFrom(0);
            return;
        }

        int aliveCount = targetLock.GetAliveMonstersNonAlloc(aliveMonsters);
        if (aliveCount <= 0 || targetLock.IsUnlocked || aliveCount > showThreshold)
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

        EnsureArrowInstances(aliveCount);

        Vector3 origin = ResolveOrigin();
        for (int i = 0; i < aliveCount; i++)
        {
            GameObject monster = aliveMonsters[i];
            GameObject arrow = arrowInstances[i];
            if (monster == null || arrow == null)
                continue;

            Vector2 direction = ResolveDirection(origin, monster.transform.position);
            arrow.transform.position = origin + (Vector3)(direction * arrowDistance + additionalOffset);
            arrow.transform.rotation = ResolveArrowRotation(direction);

            if (!arrow.activeSelf)
                arrow.SetActive(true);
        }

        HideArrowsFrom(aliveCount);
    }

    private void DrawNavigationGizmos()
    {
        Color previousColor = Gizmos.color;
        ChestMonsterKillLock lockReference = ResolveTargetLockReference();
        Vector3 origin = ResolveOrigin(lockReference);
        Vector3 offsetOrigin = origin + (Vector3)additionalOffset;

        Gizmos.color = AnchorGizmoColor;
        Gizmos.DrawWireSphere(origin, 0.12f);

        if (arrowDistance > 0f)
        {
            Gizmos.color = RadiusGizmoColor;
            Gizmos.DrawWireSphere(origin, arrowDistance);
        }

        if (additionalOffset.sqrMagnitude > DirectionEpsilon)
        {
            Gizmos.color = OffsetGizmoColor;
            Gizmos.DrawLine(origin, offsetOrigin);
            Gizmos.DrawWireSphere(offsetOrigin, 0.08f);
        }

        DrawRuntimeMonsterGizmos(lockReference, origin);
        Gizmos.color = previousColor;
    }

    private void DrawRuntimeMonsterGizmos(ChestMonsterKillLock lockReference, Vector3 origin)
    {
        if (!Application.isPlaying || lockReference == null)
            return;

        int aliveCount = lockReference.GetAliveMonstersNonAlloc(aliveMonsters);
        if (aliveCount <= 0)
            return;

        bool arrowsWouldShow = !lockReference.IsUnlocked && aliveCount <= showThreshold;
        Gizmos.color = arrowsWouldShow ? ActiveMonsterGizmoColor : SuppressedMonsterGizmoColor;

        for (int i = 0; i < aliveCount; i++)
        {
            GameObject monster = aliveMonsters[i];
            if (monster == null)
                continue;

            Vector3 targetPosition = monster.transform.position;
            Vector2 direction = ResolveDirection(origin, targetPosition);
            Vector3 arrowPosition = origin + (Vector3)(direction * arrowDistance + additionalOffset);

            Gizmos.DrawLine(origin, targetPosition);
            Gizmos.DrawLine(origin, arrowPosition);
            Gizmos.DrawWireSphere(arrowPosition, 0.1f);
        }
    }

    private void ResolveTargetLock()
    {
        if (targetLock == null)
            targetLock = GetComponent<ChestMonsterKillLock>();
    }

    private ChestMonsterKillLock ResolveTargetLockReference()
    {
        return targetLock != null ? targetLock : GetComponent<ChestMonsterKillLock>();
    }

    private Vector3 ResolveOrigin()
    {
        return ResolveOrigin(targetLock);
    }

    private Vector3 ResolveOrigin(ChestMonsterKillLock lockReference)
    {
        if (anchor != null)
            return anchor.position;

        if (lockReference != null)
            return lockReference.transform.position;

        return transform.position;
    }

    private static Vector2 ResolveDirection(Vector3 origin, Vector3 target)
    {
        Vector2 delta = target - origin;
        if (delta.sqrMagnitude <= DirectionEpsilon)
            return Vector2.up;

        return delta.normalized;
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

    private void HideArrowsFrom(int startIndex)
    {
        for (int i = Mathf.Max(0, startIndex); i < arrowInstances.Count; i++)
        {
            GameObject arrow = arrowInstances[i];
            if (arrow != null && arrow.activeSelf)
                arrow.SetActive(false);
        }
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

        Debug.LogWarning("[ChestMonsterKillLockNavigationView] Missing arrowPrefab.", this);
        missingArrowPrefabLogged = true;
    }
}

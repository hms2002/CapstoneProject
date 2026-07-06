using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
/// <summary>
/// 책임 : 공사 완료 상태에 맞춰 타일맵/경로 탐색/연결 문 상태를 동기화하는 맵 모듈이다.
/// </summary>
public sealed class ConstructionSiteTilemapModule : MonoBehaviour
{
    [Header("Stable ID")]
    [SerializeField] private string constructionId;

    [Header("State Roots")]
    [SerializeField] private GameObject blockedStateRoot;
    [SerializeField] private GameObject openStateRoot;
    [SerializeField] private bool applySavedStateOnEnable = true;

    [Header("Open Ground")]
    [SerializeField] private Tilemap[] openGroundTilemaps;
    [SerializeField] private bool autoCollectOpenGroundTilemaps = true;
    [SerializeField] private bool syncOpenGroundToPathfinders = true;
    [SerializeField] private TilemapPathfinder2D[] targetPathfinders;

    [Header("Shortcut Target")]
    [SerializeField] private DoorObject targetDoor;
    [SerializeField] private bool openTargetDoorOnCompletion = true;
    [SerializeField] private bool saveShortcutOnCompletion = true;
    [SerializeField] private bool playDoorPresentationOnCompletion;

    private readonly List<Tilemap> resolvedOpenGroundTilemaps = new();
    private readonly List<TilemapPathfinder2D> resolvedPathfinders = new();
    private bool hasAppliedState;
    private bool lastAppliedCompleted;

    public string ConstructionId => constructionId;

    private void OnEnable()
    {
        if (!applySavedStateOnEnable ||
            string.IsNullOrWhiteSpace(constructionId) ||
            !GameDataStore.IsAvailable)
            return;

        ApplyState(RunSpecialNpcConstructionProgress.IsMarkedCompleted(constructionId));
    }

    private void OnDisable()
    {
        SyncPathfinders(completed: false);
    }

    public void ApplyState(bool completed)
    {
        if (completed)
        {
            ApplyCompletedState(null);
            return;
        }

        ApplyIncompleteState();
    }

    public void ApplyIncompleteState()
    {
        ApplyVisualState(completed: false);
    }

    public void ApplyCompletedState(GameObject instigator)
    {
        ApplyVisualState(completed: true);
        OpenTargetDoorIfNeeded(instigator);
    }

    private void ApplyVisualState(bool completed)
    {
        if (blockedStateRoot != null)
            blockedStateRoot.SetActive(!completed);

        if (openStateRoot != null)
            openStateRoot.SetActive(completed);

        SyncPathfinders(completed);

        bool stateChanged = !hasAppliedState || lastAppliedCompleted != completed;
        hasAppliedState = true;
        lastAppliedCompleted = completed;

        if (stateChanged)
            RefreshSafetyTrackers();
    }

    private void OpenTargetDoorIfNeeded(GameObject instigator)
    {
        if (!openTargetDoorOnCompletion || targetDoor == null)
            return;

        if (!targetDoor.IsOpen)
        {
            targetDoor.ForceOpen(
                immediate: true,
                save: saveShortcutOnCompletion,
                instigator: instigator != null ? instigator : gameObject,
                playPresentation: playDoorPresentationOnCompletion);
            return;
        }

        if (saveShortcutOnCompletion)
            ShortcutProgressStore.UnlockShortcut(targetDoor.mapID, targetDoor.doorID, instigator != null ? instigator : gameObject);
    }

    private void SyncPathfinders(bool completed)
    {
        if (!syncOpenGroundToPathfinders)
            return;

        ResolveOpenGroundTilemaps();
        ResolvePathfinders();

        for (int i = 0; i < resolvedPathfinders.Count; i++)
        {
            TilemapPathfinder2D pathfinder = resolvedPathfinders[i];
            if (pathfinder == null)
                continue;

            for (int j = 0; j < resolvedOpenGroundTilemaps.Count; j++)
            {
                Tilemap tilemap = resolvedOpenGroundTilemaps[j];
                if (tilemap == null)
                    continue;

                if (completed)
                    pathfinder.RegisterRuntimeGroundTilemap(tilemap);
                else
                    pathfinder.UnregisterRuntimeGroundTilemap(tilemap);
            }
        }
    }

    private void ResolveOpenGroundTilemaps()
    {
        resolvedOpenGroundTilemaps.Clear();

        if (openGroundTilemaps != null)
        {
            for (int i = 0; i < openGroundTilemaps.Length; i++)
                AddResolvedOpenGroundTilemap(openGroundTilemaps[i]);
        }

        if (!autoCollectOpenGroundTilemaps || resolvedOpenGroundTilemaps.Count > 0 || openStateRoot == null)
            return;

        Tilemap[] childTilemaps = openStateRoot.GetComponentsInChildren<Tilemap>(true);
        int groundLayer = LayerMask.NameToLayer("Ground");
        for (int i = 0; i < childTilemaps.Length; i++)
        {
            Tilemap tilemap = childTilemaps[i];
            if (tilemap == null)
                continue;

            if (groundLayer >= 0 && tilemap.gameObject.layer != groundLayer)
                continue;

            AddResolvedOpenGroundTilemap(tilemap);
        }
    }

    private void AddResolvedOpenGroundTilemap(Tilemap tilemap)
    {
        if (tilemap == null || resolvedOpenGroundTilemaps.Contains(tilemap))
            return;

        resolvedOpenGroundTilemaps.Add(tilemap);
    }

    private void ResolvePathfinders()
    {
        resolvedPathfinders.Clear();

        if (targetPathfinders != null)
        {
            for (int i = 0; i < targetPathfinders.Length; i++)
                AddResolvedPathfinder(targetPathfinders[i]);
        }

        if (resolvedPathfinders.Count > 0)
            return;

        Scene scene = gameObject.scene;
#if UNITY_2023_1_OR_NEWER
        TilemapPathfinder2D[] candidates =
            Object.FindObjectsByType<TilemapPathfinder2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        TilemapPathfinder2D[] candidates = Object.FindObjectsOfType<TilemapPathfinder2D>();
#endif

        for (int i = 0; i < candidates.Length; i++)
        {
            TilemapPathfinder2D candidate = candidates[i];
            if (candidate == null || candidate.gameObject.scene != scene)
                continue;

            AddResolvedPathfinder(candidate);
        }
    }

    private void AddResolvedPathfinder(TilemapPathfinder2D pathfinder)
    {
        if (pathfinder == null || resolvedPathfinders.Contains(pathfinder))
            return;

        resolvedPathfinders.Add(pathfinder);
    }

    private static void RefreshSafetyTrackers()
    {
#if UNITY_2023_1_OR_NEWER
        SafetyTracker[] trackers =
            Object.FindObjectsByType<SafetyTracker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        SafetyTracker[] trackers = Object.FindObjectsOfType<SafetyTracker>();
#endif

        for (int i = 0; i < trackers.Length; i++)
            trackers[i]?.RefreshTilemaps();
    }
}

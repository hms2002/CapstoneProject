using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

/// <summary>
/// 책임 : 시연 치트 실행 결과와 사용자에게 표시할 알림 문구를 함께 전달한다.
/// 치트 적용 여부와 UI 알림 표시를 분리해 알림 UI가 없어도 치트 로직이 안전하게 끝나도록 한다.
/// </summary>
public readonly struct DemoCheatResult
{
    public readonly bool Success;
    public readonly string Message;

    public DemoCheatResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public static DemoCheatResult Succeeded(string message) => new(true, message);
    public static DemoCheatResult Failed(string message) => new(false, message);
}

/// <summary>
/// 책임 : 시연용 치트의 실제 효과를 기존 런타임 시스템 API를 통해 적용한다.
/// AttributeSet, AbilitySystem, WeaponInventory2D, MovementMotor2D를 직접 우회하지 않고 공식 진입점을 사용한다.
/// </summary>
public sealed class DemoCheatService
{
    private static readonly HashSet<AbilityDefinition> AbilityBuffer = new();
    private static readonly List<RunSpecialNpcInteractor> RunSpecialNpcBuffer = new();
    private static readonly List<DemoCheatMapZoomBounds> MapZoomBoundsBuffer = new();
    private readonly UnityEngine.Object logContext;
    private string lastRunSpecialNpcSceneName;
    private int nextRunSpecialNpcIndex;
    private bool hasMapZoomSnapshot;
    private MapZoomCameraSnapshot mapZoomSnapshot;
    private Coroutine mapZoomRoutine;
    private MonoBehaviour mapZoomRoutineOwner;

    public DemoCheatService(UnityEngine.Object logContext)
    {
        this.logContext = logContext;
    }

    public string BuildCheatGuide(DemoCheatSettingsSO settings)
    {
        int magicStoneAmount = Mathf.Max(1, settings.MagicStoneAddAmount);
        List<(KeyCode Key, string Description)> entries = new()
        {
            (settings.MapZoomToggleKey, "맵 전체 줌 토글"),
            (settings.WarpToBossSceneKey, "보스 씬 포탈 설정"),
            (settings.WarpToRunSpecialNpcKey, "Runtime Special NPC 워프"),
            (settings.AddMagicStoneKey, $"마정석 +{magicStoneAmount}"),
            (settings.MaxHealthKey, "체력 MAX"),
            (settings.WarpToPortalKey, "포탈 앞으로 이동"),
            (settings.ResetWeaponCooldownKey, "무기 쿨타임 초기화"),
            (settings.IncreaseAttackKey, $"공격력 +{settings.AttackIncreaseAmount:0.###}")
        };

        entries.RemoveAll(entry => entry.Key == KeyCode.None);
        entries.Sort((left, right) => CompareCheatGuideKeys(left.Key, right.Key));

        List<string> lines = new() { "시연 치트 키" };
        for (int i = 0; i < entries.Count; i++)
            lines.Add($"{FormatKey(entries[i].Key)}: {entries[i].Description}");

        return string.Join("\n", lines);
    }

    public string BuildBossSceneSelectionGuide()
    {
        return string.Join(
            "\n",
            "이동하고 싶은 보스 씬에 맞게 키를 입력하세요",
            "F1: 그림자 보스",
            "F2: 드래곤 보스",
            "F3: 슬라임 보스",
            "F4: 데몬킹 보스",
            "F5: 취소");
    }

    public DemoCheatResult AddMagicStone(DemoCheatSettingsSO settings)
    {
        if (CurrencyManager.Instance == null)
            return Fail("마정석 시스템을 찾을 수 없습니다.");

        int amount = Mathf.Max(1, settings.MagicStoneAddAmount);
        CurrencyManager.Instance.AddMagicStone(amount);

        Log($"마정석 추가. amount={amount}, total={CurrencyManager.Instance.GetMagicStone()}");
        return DemoCheatResult.Succeeded($"마정석을 {amount}개 획득했습니다.");
    }

    public DemoCheatResult WarpPlayerToNextRunSpecialNpc(DemoCheatSettingsSO settings)
    {
        if (!TryResolvePlayer(out Transform player))
            return Fail("플레이어를 찾을 수 없습니다.");

        RunSpecialNpcInteractor npc = FindNextRunSpecialNpc();
        if (npc == null)
            return Fail("이 씬에서 Runtime Special NPC를 찾을 수 없습니다.");

        Transform anchor = npc.GetPromptAnchor();
        Vector3 targetPosition = anchor != null ? anchor.position : npc.transform.position;
        targetPosition.z = player.position.z;

        MovementMotor2D movementMotor = player.GetComponent<MovementMotor2D>();
        if (movementMotor != null)
        {
            movementMotor.WarpTo(targetPosition, clearExternalMovement: true, clearMotion: true);
        }
        else
        {
            SetPlayerPositionImmediate(player, targetPosition);
        }

        Log($"Runtime Special NPC 앞으로 워프. scene={SceneManager.GetActiveScene().name}, npc={npc.name}");
        return DemoCheatResult.Succeeded("Runtime Special NPC 앞으로 이동했습니다.");
    }

    public DemoCheatResult PrepareNearestPortalForBossScene(DemoCheatSettingsSO settings, int bossSceneIndex)
    {
        if (!TryResolvePlayer(out Transform player))
            return Fail("플레이어를 찾을 수 없습니다.");

        if (!TryResolveBossSceneName(settings, bossSceneIndex, out string sceneName))
            return Fail("선택한 보스 씬이 설정되어 있지 않습니다.");

        ScenePortal portal = FindNearestPortal(player.position);
        if (portal == null)
            return Fail("목적지를 설정할 포탈을 찾을 수 없습니다.");

        if (!portal.SetOneShotDestinationOverride(sceneName, this))
            return Fail("포탈 목적지를 설정하지 못했습니다.");

        RestoreMapZoomImmediate();

        Transform anchor = portal.GetPromptAnchor();
        Vector3 targetPosition = anchor != null ? anchor.position : portal.transform.position;
        targetPosition.z = player.position.z;

        MovementMotor2D movementMotor = player.GetComponent<MovementMotor2D>();
        if (movementMotor != null)
        {
            movementMotor.WarpTo(targetPosition, clearExternalMovement: true, clearMotion: true);
        }
        else
        {
            SetPlayerPositionImmediate(player, targetPosition);
        }

        Log($"보스 씬 포탈 override 설정. portal={portal.name}, scene={sceneName}");
        return DemoCheatResult.Succeeded($"다음 포탈 목적지를 {sceneName}(으)로 설정했습니다.");
    }

    public DemoCheatResult RefillPlayerHealth(DemoCheatSettingsSO settings)
    {
        if (!TryResolvePlayer(out Transform player))
            return Fail("플레이어를 찾을 수 없습니다.");

        AttributeSet attributeSet = player.GetComponent<AttributeSet>();
        if (attributeSet == null || settings.HealthAttribute == null || settings.MaxHealthAttribute == null)
        {
            return Fail("체력 치트 설정이 올바르지 않습니다.");
        }

        float maxHealth = attributeSet.GetAttributeValue(settings.MaxHealthAttribute);
        bool applied = attributeSet.TrySetCurrentValue(settings.HealthAttribute, maxHealth, logContext);
        if (!applied)
        {
            return Fail("체력을 회복하지 못했습니다.");
        }

        Log($"체력 MAX 적용. hp={maxHealth:0.###}");
        return DemoCheatResult.Succeeded("체력을 최대치로 회복했습니다.");
    }

    public DemoCheatResult WarpPlayerToNearestPortal(DemoCheatSettingsSO settings)
    {
        if (!TryResolvePlayer(out Transform player))
            return Fail("플레이어를 찾을 수 없습니다.");

        if (!IsPortalWarpAllowedScene())
        {
            Log("포탈 워프 무시: Hub/복도 씬에서만 사용할 수 있습니다.");
            return DemoCheatResult.Failed("이 씬에서는 포탈 워프를 사용할 수 없습니다.");
        }

        ScenePortal portal = FindNearestPortal(player.position);
        if (portal == null)
        {
            return Fail("이동 가능한 포탈을 찾을 수 없습니다.");
        }

        Transform anchor = portal.GetPromptAnchor();
        Vector3 targetPosition = anchor != null ? anchor.position : portal.transform.position;
        targetPosition.z = player.position.z;

        MovementMotor2D movementMotor = player.GetComponent<MovementMotor2D>();
        if (movementMotor != null)
        {
            movementMotor.WarpTo(targetPosition, clearExternalMovement: true, clearMotion: true);
        }
        else
        {
            SetPlayerPositionImmediate(player, targetPosition);
        }

        Log($"포탈 앞으로 워프. scene={SceneManager.GetActiveScene().name}, portal={portal.name}");
        return DemoCheatResult.Succeeded("가장 가까운 포탈 앞으로 이동했습니다.");
    }

    public DemoCheatResult ResetAllOwnedWeaponCooldowns(DemoCheatSettingsSO settings)
    {
        if (!TryResolvePlayer(out Transform player))
            return Fail("플레이어를 찾을 수 없습니다.");

        AbilitySystem abilitySystem = player.GetComponent<AbilitySystem>();
        WeaponInventory2D weaponInventory = player.GetComponent<WeaponInventory2D>();
        if (abilitySystem == null || weaponInventory == null)
        {
            return Fail("초기화할 무기 쿨타임이 없습니다.");
        }

        AbilityBuffer.Clear();
        for (int i = 0; i < weaponInventory.SlotCount; i++)
        {
            WeaponDefinition weapon = weaponInventory.GetWeaponInSlot(i);
            if (weapon == null)
                continue;

            foreach (AbilityDefinition ability in weapon.EnumerateGrantedAbilities())
            {
                if (ability != null)
                    AbilityBuffer.Add(ability);
            }
        }

        if (AbilityBuffer.Count == 0)
            return Fail("초기화할 무기 쿨타임이 없습니다.");

        int resetCount = 0;
        foreach (AbilityDefinition ability in AbilityBuffer)
        {
            if (abilitySystem.TrySetCooldownRemaining(ability, 0f))
                resetCount++;
        }

        Log($"보유 무기 쿨타임 초기화. abilities={resetCount}/{AbilityBuffer.Count}");
        AbilityBuffer.Clear();
        return resetCount > 0
            ? DemoCheatResult.Succeeded("보유 무기 쿨타임을 초기화했습니다.")
            : DemoCheatResult.Failed("초기화할 무기 쿨타임이 없습니다.");
    }

    public DemoCheatResult IncreasePlayerAttack(DemoCheatSettingsSO settings)
    {
        if (!TryResolvePlayer(out Transform player))
            return Fail("플레이어를 찾을 수 없습니다.");

        AttributeSet attributeSet = player.GetComponent<AttributeSet>();
        if (attributeSet == null || settings.AttackAddAttribute == null)
        {
            return Fail("공격력 치트 설정이 올바르지 않습니다.");
        }

        float currentBase = attributeSet.GetBaseValue(settings.AttackAddAttribute);
        float nextBase = currentBase + settings.AttackIncreaseAmount;
        if (!attributeSet.TrySetBaseValue(settings.AttackAddAttribute, nextBase, logContext))
        {
            return Fail("공격력을 증가시키지 못했습니다.");
        }

        AttributeStatSource statSource = player.GetComponent<AttributeStatSource>();
        if (statSource != null)
            statSource.RebuildProvider();

        Log($"공격력 증가. AttackAdd={currentBase:0.###}->{nextBase:0.###}");
        return DemoCheatResult.Succeeded("공격력이 +10 증가했습니다.");
    }

    public DemoCheatResult ToggleMapZoom(DemoCheatSettingsSO settings, MonoBehaviour routineOwner)
    {
        if (settings == null)
            return Fail("맵 줌 치트 설정을 찾을 수 없습니다.");

        if (routineOwner == null)
            return Fail("맵 줌 코루틴 실행 대상을 찾을 수 없습니다.");

        return hasMapZoomSnapshot
            ? RestoreMapZoom(settings, routineOwner)
            : ZoomOutToMap(settings, routineOwner);
    }

    public void RestoreMapZoomImmediate()
    {
        if (!hasMapZoomSnapshot)
            return;

        StopMapZoomRoutine();
        ApplyMapZoomSnapshot(mapZoomSnapshot);
        ClearMapZoomSnapshot();
        Log("맵 줌을 즉시 복구했습니다.");
    }

    private DemoCheatResult ZoomOutToMap(DemoCheatSettingsSO settings, MonoBehaviour routineOwner)
    {
        if (!TryResolveMapZoomTarget(settings, out Vector2 targetCenter, out Vector2 mapSize, out float padding, out string sourceLabel))
            return Fail("맵 줌 크기는 0보다 커야 합니다.");

        CinemachineCamera gameplayCamera = CameraBootstrap.GetPlayerCamera();
        if (gameplayCamera == null || !gameplayCamera.gameObject.activeInHierarchy)
            return Fail("맵 줌에 사용할 게임플레이 카메라를 찾을 수 없습니다.");

        Camera mainCamera = CameraBootstrap.GetMainCamera();
        float aspect = ResolveCameraAspect(mainCamera);
        float targetSize = Mathf.Max(
            mapSize.y * 0.5f,
            mapSize.x / (2f * aspect)) + padding;
        targetSize = Mathf.Max(0.01f, targetSize);

        StopMapZoomRoutine();
        mapZoomSnapshot = CaptureMapZoomSnapshot(gameplayCamera);
        hasMapZoomSnapshot = true;

        gameplayCamera.Follow = null;
        gameplayCamera.LookAt = null;
        gameplayCamera.Priority = Mathf.Max(gameplayCamera.Priority, 10000);

        Vector2 startCenter = ResolveCurrentCameraCenter(mainCamera, gameplayCamera);
        float startSize = GetCameraOrthographicSize(gameplayCamera, targetSize);
        float transitionSeconds = settings.MapZoomTransitionSeconds;

        mapZoomRoutineOwner = routineOwner;
        mapZoomRoutine = routineOwner.StartCoroutine(AnimateMapZoomRoutine(
            gameplayCamera,
            startCenter,
            startSize,
            targetCenter,
            targetSize,
            transitionSeconds,
            () =>
            {
                mapZoomRoutine = null;
                mapZoomRoutineOwner = null;
                Log($"맵 줌 활성화. source={sourceLabel}, center={targetCenter}, mapSize={mapSize}, cameraSize={targetSize:0.###}");
            }));

        return DemoCheatResult.Succeeded("맵 전체 줌을 켰습니다.");
    }

    private DemoCheatResult RestoreMapZoom(DemoCheatSettingsSO settings, MonoBehaviour routineOwner)
    {
        CinemachineCamera gameplayCamera = mapZoomSnapshot.Camera;
        if (gameplayCamera == null)
        {
            StopMapZoomRoutine();
            ClearMapZoomSnapshot();
            return Fail("복구할 맵 줌 카메라를 찾을 수 없습니다.");
        }

        StopMapZoomRoutine();

        Vector2 startCenter = ResolveCurrentCameraCenter(CameraBootstrap.GetMainCamera(), gameplayCamera);
        float startSize = GetCameraOrthographicSize(gameplayCamera, mapZoomSnapshot.OrthographicSize);
        Vector2 targetCenter = ResolveRestoreCameraCenter(mapZoomSnapshot);
        float transitionSeconds = settings != null ? settings.MapZoomTransitionSeconds : 0f;

        mapZoomRoutineOwner = routineOwner;
        mapZoomRoutine = routineOwner.StartCoroutine(AnimateMapZoomRoutine(
            gameplayCamera,
            startCenter,
            startSize,
            targetCenter,
            mapZoomSnapshot.OrthographicSize,
            transitionSeconds,
            () =>
            {
                ApplyMapZoomSnapshot(mapZoomSnapshot);
                ClearMapZoomSnapshot();
                Log("맵 줌을 복구했습니다.");
            }));

        return DemoCheatResult.Succeeded("맵 전체 줌을 원래대로 돌립니다.");
    }

    private bool TryResolveMapZoomTarget(
        DemoCheatSettingsSO settings,
        out Vector2 center,
        out Vector2 mapSize,
        out float padding,
        out string sourceLabel)
    {
        if (TryFindSceneMapZoomBounds(out DemoCheatMapZoomBounds sceneBounds))
        {
            if (sceneBounds.TryGetZoomBounds(out center, out mapSize, out float extraPadding))
            {
                padding = settings.MapZoomPadding + extraPadding;
                sourceLabel = sceneBounds.name;
                return true;
            }

            LogWarning($"맵 줌 범위 오브젝트가 올바르지 않습니다. object={sceneBounds.name}");
        }

        center = settings.MapZoomCenter;
        mapSize = settings.MapZoomSize;
        padding = settings.MapZoomPadding;
        sourceLabel = "DemoCheatSettings";
        return mapSize.x > 0f && mapSize.y > 0f;
    }

    private bool TryFindSceneMapZoomBounds(out DemoCheatMapZoomBounds selected)
    {
        selected = null;
        MapZoomBoundsBuffer.Clear();

        Scene activeScene = SceneManager.GetActiveScene();
        DemoCheatMapZoomBounds[] candidates = UnityEngine.Object.FindObjectsByType<DemoCheatMapZoomBounds>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < candidates.Length; i++)
        {
            DemoCheatMapZoomBounds candidate = candidates[i];
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.gameObject.activeInHierarchy ||
                candidate.gameObject.scene != activeScene)
            {
                continue;
            }

            MapZoomBoundsBuffer.Add(candidate);
        }

        if (MapZoomBoundsBuffer.Count == 0)
            return false;

        MapZoomBoundsBuffer.Sort(CompareMapZoomBoundsStableOrder);
        selected = MapZoomBoundsBuffer[0];
        if (MapZoomBoundsBuffer.Count > 1)
        {
            LogWarning(
                $"활성 씬에 DemoCheatMapZoomBounds가 여러 개 있습니다. scene={activeScene.name}, selected={selected.name}, count={MapZoomBoundsBuffer.Count}");
        }

        MapZoomBoundsBuffer.Clear();
        return selected != null;
    }

    private static int CompareMapZoomBoundsStableOrder(DemoCheatMapZoomBounds a, DemoCheatMapZoomBounds b)
    {
        return string.CompareOrdinal(BuildMapZoomBoundsStableKey(a), BuildMapZoomBoundsStableKey(b));
    }

    private static string BuildMapZoomBoundsStableKey(DemoCheatMapZoomBounds bounds)
    {
        if (bounds == null)
            return string.Empty;

        Transform current = bounds.transform;
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = $"{current.GetSiblingIndex():D4}:{current.name}/{path}";
        }

        return $"{bounds.gameObject.scene.name}/{path}/{bounds.transform.GetSiblingIndex():D4}";
    }

    private static bool TryResolvePlayer(out Transform player)
    {
        player = PlayerRuntimeRegistry.GetPlayerTransform();
        if (player != null)
            return true;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        player = taggedPlayer != null ? taggedPlayer.transform : null;
        return player != null;
    }

    private static bool TryResolveBossSceneName(DemoCheatSettingsSO settings, int bossSceneIndex, out string sceneName)
    {
        sceneName = null;
        IReadOnlyList<string> sceneNames = settings != null ? settings.BossSceneNames : null;
        if (sceneNames == null || bossSceneIndex < 0 || bossSceneIndex >= sceneNames.Count)
            return false;

        string candidate = sceneNames[bossSceneIndex];
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        sceneName = candidate.Trim();
        return true;
    }

    private static bool IsPortalWarpAllowedScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(sceneName))
            return false;

        return sceneName.IndexOf("Hub", StringComparison.OrdinalIgnoreCase) >= 0 ||
               sceneName.IndexOf("Corridor", StringComparison.OrdinalIgnoreCase) >= 0 ||
               sceneName.IndexOf("Hallway", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static ScenePortal FindNearestPortal(Vector3 playerPosition)
    {
        ScenePortal[] portals = UnityEngine.Object.FindObjectsByType<ScenePortal>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        ScenePortal nearest = null;
        float nearestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < portals.Length; i++)
        {
            ScenePortal portal = portals[i];
            if (portal == null)
                continue;

            float distanceSqr = (portal.transform.position - playerPosition).sqrMagnitude;
            if (distanceSqr >= nearestDistanceSqr)
                continue;

            nearest = portal;
            nearestDistanceSqr = distanceSqr;
        }

        return nearest;
    }

    private RunSpecialNpcInteractor FindNextRunSpecialNpc()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!string.Equals(lastRunSpecialNpcSceneName, sceneName, StringComparison.Ordinal))
        {
            lastRunSpecialNpcSceneName = sceneName;
            nextRunSpecialNpcIndex = 0;
        }

        RunSpecialNpcBuffer.Clear();
        RunSpecialNpcInteractor[] interactors = UnityEngine.Object.FindObjectsByType<RunSpecialNpcInteractor>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < interactors.Length; i++)
        {
            RunSpecialNpcInteractor interactor = interactors[i];
            if (interactor != null && interactor.isActiveAndEnabled && interactor.gameObject.activeInHierarchy)
                RunSpecialNpcBuffer.Add(interactor);
        }

        if (RunSpecialNpcBuffer.Count == 0)
            return null;

        RunSpecialNpcBuffer.Sort(CompareRunSpecialNpcStableOrder);
        if (nextRunSpecialNpcIndex < 0 || nextRunSpecialNpcIndex >= RunSpecialNpcBuffer.Count)
            nextRunSpecialNpcIndex = 0;

        RunSpecialNpcInteractor selected = RunSpecialNpcBuffer[nextRunSpecialNpcIndex];
        nextRunSpecialNpcIndex = (nextRunSpecialNpcIndex + 1) % RunSpecialNpcBuffer.Count;
        RunSpecialNpcBuffer.Clear();
        return selected;
    }

    private static int CompareRunSpecialNpcStableOrder(RunSpecialNpcInteractor a, RunSpecialNpcInteractor b)
    {
        return string.CompareOrdinal(BuildRunSpecialNpcStableKey(a), BuildRunSpecialNpcStableKey(b));
    }

    private static string BuildRunSpecialNpcStableKey(RunSpecialNpcInteractor interactor)
    {
        if (interactor == null)
            return string.Empty;

        Transform current = interactor.transform;
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = $"{current.GetSiblingIndex():D4}:{current.name}/{path}";
        }

        return $"{interactor.gameObject.scene.name}/{path}/{interactor.transform.GetSiblingIndex():D4}";
    }

    private static void SetPlayerPositionImmediate(Transform player, Vector3 targetPosition)
    {
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = new Vector2(targetPosition.x, targetPosition.y);
            body.linearVelocity = Vector2.zero;
        }

        player.position = targetPosition;
    }

    private IEnumerator AnimateMapZoomRoutine(
        CinemachineCamera camera,
        Vector2 startCenter,
        float startOrthographicSize,
        Vector2 targetCenter,
        float targetOrthographicSize,
        float transitionSeconds,
        Action onComplete)
    {
        if (camera == null)
            yield break;

        float duration = Mathf.Max(0f, transitionSeconds);
        if (duration <= 0f)
        {
            ApplyMapZoomCameraState(camera, targetCenter, targetOrthographicSize);
            onComplete?.Invoke();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration && camera != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            Vector2 center = Vector2.Lerp(startCenter, targetCenter, easedT);
            float orthographicSize = Mathf.Lerp(startOrthographicSize, targetOrthographicSize, easedT);
            ApplyMapZoomCameraState(camera, center, orthographicSize);
            yield return null;
        }

        if (camera != null)
            ApplyMapZoomCameraState(camera, targetCenter, targetOrthographicSize);

        onComplete?.Invoke();
    }

    private static MapZoomCameraSnapshot CaptureMapZoomSnapshot(CinemachineCamera camera)
    {
        return new MapZoomCameraSnapshot(
            camera,
            camera != null ? camera.Follow : null,
            camera != null ? camera.LookAt : null,
            camera != null ? camera.Priority : 0,
            camera != null ? camera.Lens.OrthographicSize : 6f,
            camera != null ? camera.transform.position : Vector3.zero,
            camera != null ? camera.transform.rotation : Quaternion.identity);
    }

    private static void ApplyMapZoomSnapshot(MapZoomCameraSnapshot snapshot)
    {
        CinemachineCamera camera = snapshot.Camera;
        if (camera == null)
            return;

        camera.Priority = snapshot.Priority;
        camera.Follow = snapshot.Follow;
        camera.LookAt = snapshot.LookAt;
        SetCameraOrthographicSize(camera, snapshot.OrthographicSize);
        Vector2 restoreCenter = ResolveRestoreCameraCenter(snapshot);
        Vector3 restorePosition = snapshot.Position;
        restorePosition.x = restoreCenter.x;
        restorePosition.y = restoreCenter.y;
        camera.ForceCameraPosition(restorePosition, snapshot.Rotation);
    }

    private void ClearMapZoomSnapshot()
    {
        hasMapZoomSnapshot = false;
        mapZoomSnapshot = default;
        mapZoomRoutine = null;
        mapZoomRoutineOwner = null;
    }

    private void StopMapZoomRoutine()
    {
        if (mapZoomRoutine != null && mapZoomRoutineOwner != null)
            mapZoomRoutineOwner.StopCoroutine(mapZoomRoutine);

        mapZoomRoutine = null;
        mapZoomRoutineOwner = null;
    }

    private static void ApplyMapZoomCameraState(
        CinemachineCamera camera,
        Vector2 center,
        float orthographicSize)
    {
        if (camera == null)
            return;

        SetCameraOrthographicSize(camera, orthographicSize);
        Vector3 position = camera.transform.position;
        position.x = center.x;
        position.y = center.y;
        camera.ForceCameraPosition(position, camera.transform.rotation);
    }

    private static float GetCameraOrthographicSize(CinemachineCamera camera, float fallbackOrthographicSize)
    {
        if (camera == null)
            return Mathf.Max(0.01f, fallbackOrthographicSize);

        return Mathf.Max(0.01f, camera.Lens.OrthographicSize);
    }

    private static void SetCameraOrthographicSize(CinemachineCamera camera, float orthographicSize)
    {
        if (camera == null)
            return;

        var lens = camera.Lens;
        lens.OrthographicSize = Mathf.Max(0.01f, orthographicSize);
        camera.Lens = lens;
    }

    private static Vector2 ResolveCurrentCameraCenter(Camera mainCamera, CinemachineCamera gameplayCamera)
    {
        if (mainCamera != null)
            return mainCamera.transform.position;

        return gameplayCamera != null
            ? new Vector2(gameplayCamera.transform.position.x, gameplayCamera.transform.position.y)
            : Vector2.zero;
    }

    private static Vector2 ResolveRestoreCameraCenter(MapZoomCameraSnapshot snapshot)
    {
        if (snapshot.Follow != null)
            return snapshot.Follow.position;

        if (snapshot.LookAt != null)
            return snapshot.LookAt.position;

        return new Vector2(snapshot.Position.x, snapshot.Position.y);
    }

    private static float ResolveCameraAspect(Camera camera)
    {
        if (camera != null && camera.aspect > 0f)
            return camera.aspect;

        if (Screen.height > 0)
            return Mathf.Max(0.01f, (float)Screen.width / Screen.height);

        return 16f / 9f;
    }

    private static int CompareCheatGuideKeys(KeyCode left, KeyCode right)
    {
        int leftRank = GetCheatGuideKeySortRank(left);
        int rightRank = GetCheatGuideKeySortRank(right);
        if (leftRank != rightRank)
            return leftRank.CompareTo(rightRank);

        return string.Compare(FormatKey(left), FormatKey(right), StringComparison.Ordinal);
    }

    private static int GetCheatGuideKeySortRank(KeyCode key)
    {
        string keyName = key.ToString();
        if (keyName.Length > 1 && keyName[0] == 'F' && int.TryParse(keyName.Substring(1), out int functionKeyNumber))
            return functionKeyNumber;

        return 1000 + (int)key;
    }

    private static string FormatKey(KeyCode key)
    {
        if (key == KeyCode.None)
            return "None";

        string keyName = key.ToString();
        if (keyName.StartsWith("Alpha", StringComparison.Ordinal))
            return keyName.Substring("Alpha".Length);

        if (keyName.StartsWith("Keypad", StringComparison.Ordinal))
            return $"Num{keyName.Substring("Keypad".Length)}";

        return keyName;
    }

    private void Log(string message)
    {
        Debug.Log($"[DemoCheat] {message}", logContext);
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[DemoCheat] {message}", logContext);
    }

    private DemoCheatResult Fail(string message)
    {
        LogWarning(message);
        return DemoCheatResult.Failed(message);
    }

    private readonly struct MapZoomCameraSnapshot
    {
        public readonly CinemachineCamera Camera;
        public readonly Transform Follow;
        public readonly Transform LookAt;
        public readonly int Priority;
        public readonly float OrthographicSize;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public MapZoomCameraSnapshot(
            CinemachineCamera camera,
            Transform follow,
            Transform lookAt,
            int priority,
            float orthographicSize,
            Vector3 position,
            Quaternion rotation)
        {
            Camera = camera;
            Follow = follow;
            LookAt = lookAt;
            Priority = priority;
            OrthographicSize = Mathf.Max(0.01f, orthographicSize);
            Position = position;
            Rotation = rotation;
        }
    }
}

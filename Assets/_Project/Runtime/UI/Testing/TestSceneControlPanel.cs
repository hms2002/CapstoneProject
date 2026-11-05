using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using UnityGAS;

/// <summary>
/// Scene-authored test controls. Uses existing gameplay APIs and owns only test
/// instances, undo receipts, UI listeners and a disposable profile copy.
/// </summary>
[DefaultExecutionOrder(-9000)]
[DisallowMultipleComponent]
public sealed class TestSceneControlPanel : MonoBehaviour
{
    [Header("Catalogs")]
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private LevelRewardCatalogSO rewardCatalog;
    [SerializeField] private LevelProgressionConfigSO progressionConfig;
    [SerializeField] private GameObject[] bossPrefabs;
    [SerializeField] private GameObject[] mobPrefabs;
    [SerializeField] private GameObject[] chestPrefabs;
    // Older saved scenes contain snapshot prefabs. Drop their build dependencies when preparing the copy.
    [SerializeField, HideInInspector] private GameObject[] eventRoomPrefabs;
    [SerializeField] private RoomTemplateSO[] eventTemplates;
    [SerializeField] private RoomPieceAuthoring eventRoomShell;

    [Header("Scene")]
    [SerializeField] private GameObject arenaRoot;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private MonsterRoomArea2D roomArea;
    [SerializeField] private MonsterSpawnRoomGroup roomGroup;
    [SerializeField] private Transform spawnOrigin;
    [SerializeField] private RectTransform panelRoot;

    [Header("Choices")]
    [SerializeField] private TestSceneCatalogGrid catalogGrid;
    [SerializeField] private TMP_Dropdown chestDropdown;
    [SerializeField] private TMP_Dropdown eventDropdown;
    [SerializeField] private TMP_Dropdown rewardDropdown;
    [SerializeField] private TMP_Dropdown appliedRewardDropdown;
    [SerializeField] private TMP_Dropdown weaponSlotDropdown;
    [SerializeField] private TMP_InputField spawnCountInput;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text progressionText;

    [Header("Commands")]
    [SerializeField] private Button openWeaponsButton;
    [SerializeField] private Button openRelicsButton;
    [SerializeField] private Button openBossesButton;
    [SerializeField] private Button openMonstersButton;
    [SerializeField] private Button spawnChestButton;
    [SerializeField] private Button spawnEventButton;
    [SerializeField] private Button levelUpButton;
    [SerializeField] private Button openRewardsButton;
    [SerializeField] private Button applyRewardButton;
    [SerializeField] private Button removeRewardButton;
    [SerializeField] private Button clearRewardsButton;
    [SerializeField] private Button clearSpawnedButton;
    [SerializeField] private Button healButton;
    [SerializeField] private Button resetPlayerButton;

    private readonly List<LevelRewardDefinitionSO> rewards = new();
    private readonly List<LevelRewardSelectionState> selectedRewards = new();
    private readonly List<GameObject> spawned = new();
    private readonly Dictionary<LevelRewardSelectionState, TestSceneRewardReceipt> receipts = new();
    private readonly Dictionary<string, TestSceneRewardReceipt> offerReceipts = new(StringComparer.Ordinal);
    private readonly List<RaycastResult> uiRaycasts = new();
    private PointerEventData pointerData;
    private readonly List<LevelRewardSelectionState> knownSelections = new();
    private static readonly InputActionId[] PanelBlockedActions =
    {
        InputActionId.PrimaryAttack, InputActionId.Skill1, InputActionId.Skill2,
        InputActionId.Interact, InputActionId.Dash, InputActionId.SwapWeapon,
        InputActionId.ConsumableSlot1, InputActionId.ConsumableSlot2,
        InputActionId.ConsumableSlot3, InputActionId.ConsumableSlot4,
    };
    private TestSceneProfileStore testProfile;
    private IGameDataStoreBackend originalProfile;
    private TestSceneRunStore testRun;
    private IRunSessionStoreBackend originalRun;
    private string originalRunJson;
    private GameObject activeEvent;
    private RoomTemplateSO activeEventTemplate;
    private bool originalGroundActive;
    private bool originalArenaActive;
    private TilemapPathfinder2D eventPathfinder;
    private Grid originalPathfinderGrid;
    private Tilemap originalPathfinderGround;
    private bool originalRoomGroupEnabled;
    private bool originalAreaColliderEnabled;
    private bool isTestScene;
    private PlayerCombatInput2D blockedCombatInput;
    private LevelRewardSessionController rewardSession;
    private PlayerDeathReturnToHub2D guardedDeathReturn;
    private bool previousDeathReturnEnabled;
    private RunTimeLimitSystem guardedTimer;
    private readonly Dictionary<RunTimeOverReturnToHub, bool> guardedTimeReturns = new();
    private bool resettingPlayer;

    private void Awake()
    {
        isTestScene = gameObject.scene.name == "TestScene";
        if (!isTestScene) { enabled = false; return; }
        originalProfile = GameDataManager.Instance;
        testProfile = new TestSceneProfileStore(GameDataStore.EnsureData(), GameDataStore.ActiveSlotIndex);
        GameDataStore.RegisterBackend(testProfile);
        if (RunSessionStore.Data != null)
            originalRunJson = JsonUtility.ToJson(RunSessionStore.Data);
        originalRun = GamePlayDataManager.Instance;
        if (originalRun != null)
        {
            testRun = new TestSceneRunStore(originalRun);
            RunSessionStore.RegisterBackend(testRun);
        }
        originalGroundActive = groundTilemap != null && groundTilemap.gameObject.activeSelf;
        originalArenaActive = arenaRoot != null && arenaRoot.activeSelf;
        originalRoomGroupEnabled = roomGroup != null && roomGroup.enabled;
        originalAreaColliderEnabled = roomArea != null && roomArea.AreaCollider != null && roomArea.AreaCollider.enabled;
    }

    private void OnEnable()
    {
        if (!isTestScene) return;
        BindButtons(true);
        if (catalogGrid != null) catalogGrid.ExecuteRequested += ExecuteCatalogChoice;
        RunLevelRewards.RewardsChanged += HandleRewardsChanged;
        RunLevelProgression.StateChanged += RefreshProgression;
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        RunTimeLimitSystem.InstanceChanged += GuardTimer;
        GuardTimer(RunTimeLimitSystem.Instance);
        PopulateChoices();
    }

#if UNITY_EDITOR
    // Scene processing can finish after OnEnable in Editor Play. Bind the resized grids
    // and dropdowns again if this instance has already subscribed to its controls.
    public void EditorCatalogPrepared()
    {
        if (eventRoomPrefabs != null && eventRoomPrefabs.Length > 0)
            eventRoomPrefabs = Array.Empty<GameObject>();

        if (!isTestScene || !isActiveAndEnabled) return;
        if (catalogGrid != null && catalogGrid.isActiveAndEnabled) catalogGrid.BindCatalogCells();
        PopulateChoices();
    }
#endif

    private void Start()
    {
        if (!isTestScene) return;
        RunSessionStore.StartRun();
        GuardTimer(RunTimeLimitSystem.Instance);
        RunLevelRewards.RegisterCatalog(rewardCatalog);
        HandlePlayerRegistered(PlayerRuntimeRegistry.CurrentPlayer);
        RefreshProgression();
        SetStatus("TestScene 준비 완료. 테스트 변경은 저장 프로필에 기록하지 않습니다.");
    }

    private void OnDisable()
    {
        if (!isTestScene) return;
        BindButtons(false);
        if (catalogGrid != null)
        {
            catalogGrid.ExecuteRequested -= ExecuteCatalogChoice;
            catalogGrid.Close();
        }
        RunLevelRewards.RewardsChanged -= HandleRewardsChanged;
        RunLevelProgression.StateChanged -= RefreshProgression;
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        RunTimeLimitSystem.InstanceChanged -= GuardTimer;
        if (rewardSession != null)
        {
            rewardSession.SessionOpened -= CaptureOfferReceipts;
            rewardSession.SessionChanged -= CaptureOfferReceipts;
            rewardSession.CloseSession();
        }
        SetPanelInputBlocked(false);
        ClearOwnedInstances();
        if (originalProfile != null) GameDataStore.RegisterBackend(originalProfile);
        else GameDataStore.UnregisterBackend(testProfile);
        if (RunSessionStore.Data != null && !string.IsNullOrEmpty(originalRunJson))
            JsonUtility.FromJsonOverwrite(originalRunJson, RunSessionStore.Data);
        if (originalRun != null) RunSessionStore.RegisterBackend(originalRun);
        else if (testRun != null) RunSessionStore.UnregisterBackend(testRun);
        RunLevelRewards.RebuildActiveEffects();
        guardedTimer?.SetExternalPause(this, false);
        foreach (KeyValuePair<RunTimeOverReturnToHub, bool> entry in guardedTimeReturns)
            if (entry.Key != null) entry.Key.enabled = entry.Value;
        guardedTimeReturns.Clear();
        // Enabling a death return at zero HP would immediately leave the test scene.
        if (guardedDeathReturn != null && PlayerRuntimeRegistry.CurrentPlayer != null &&
            guardedDeathReturn.gameObject == PlayerRuntimeRegistry.CurrentPlayer.gameObject)
        {
            Heal();
            guardedDeathReturn.enabled = previousDeathReturnEnabled;
        }
        receipts.Clear();
        offerReceipts.Clear();
    }

    private void Update()
    {
        if (!isTestScene) return;
        bool overPanel = catalogGrid != null && catalogGrid.IsOpen;
        if (panelRoot != null && EventSystem.current != null)
        {
            Vector2 pointer = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            pointerData ??= new PointerEventData(EventSystem.current);
            pointerData.position = pointer;
            uiRaycasts.Clear();
            EventSystem.current.RaycastAll(pointerData, uiRaycasts);
            foreach (RaycastResult hit in uiRaycasts)
            {
                if (hit.gameObject != null && hit.gameObject.transform.IsChildOf(transform))
                { overPanel = true; break; }
            }
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(transform) &&
                selected.GetComponent<TMP_InputField>() is { isFocused: true })
                overPanel = true;
        }
        SetPanelInputBlocked(overPanel);
    }

    private void SetPanelInputBlocked(bool blocked)
    {
        PlayerCombatInput2D current = PlayerRuntimeRegistry.CurrentPlayer != null
            ? PlayerRuntimeRegistry.CurrentPlayer.GetComponent<PlayerCombatInput2D>() : null;
        if (blockedCombatInput != null && blockedCombatInput != current)
            blockedCombatInput.SetWeaponInputBlocked(this, false);
        blockedCombatInput = current;
        blockedCombatInput?.SetWeaponInputBlocked(this, blocked);
        foreach (InputActionId action in PanelBlockedActions)
            InputActionQuery.SetPressBlocked(action, this, blocked);
    }

    public void GiveWeapon()
    {
        if (!TryGetPlayer(out PlayerInteractor2D player)) return;
        WeaponDefinition weapon = catalogGrid?.SelectedTarget<WeaponDefinition>(TestSceneCatalogGrid.WeaponCategory);
        WeaponInventory2D inventory = player.GetComponent<WeaponInventory2D>();
        int slot = weaponSlotDropdown != null ? weaponSlotDropdown.value : 0;
        if (weapon == null || inventory == null || !inventory.TrySetWeaponSlot(slot, weapon))
        { SetStatus("무기 지급 실패: 중복·슬롯 잠금·실행 중 교체 제한을 확인하세요."); return; }
        inventory.Equip(slot);
        SetStatus($"무기 지급: {weapon.displayName} (슬롯 {slot + 1})");
    }

    public void GiveRelic()
    {
        if (!TryGetPlayer(out PlayerInteractor2D player)) return;
        RelicDefinition relic = catalogGrid?.SelectedTarget<RelicDefinition>(TestSceneCatalogGrid.RelicCategory);
        RelicInventory inventory = player.GetComponent<RelicInventory>();
        if (relic == null || inventory == null) { SetStatus("유물 또는 인벤토리가 없습니다."); return; }
        RelicInventory.AcquireResult result = inventory.TryAcquireOrUpgradeDetailed(relic);
        SetStatus(result == RelicInventory.AcquireResult.Success
            ? $"유물 지급: {relic.displayName}" : $"유물 지급 실패: {result}");
    }

    public void OpenWeapons() => catalogGrid?.Open(TestSceneCatalogGrid.WeaponCategory);
    public void OpenRelics() => catalogGrid?.Open(TestSceneCatalogGrid.RelicCategory);
    public void OpenMonsters() => catalogGrid?.Open(TestSceneCatalogGrid.MonsterCategory);
    public void OpenBosses() => catalogGrid?.Open(TestSceneCatalogGrid.BossCategory);

    public void SpawnBoss() => SpawnActors(TestSceneCatalogGrid.BossCategory, false);
    public void SpawnMonster() => SpawnActors(TestSceneCatalogGrid.MonsterCategory, true);

    private void SpawnActors(int category, bool useMonsterSpawner)
    {
        if (!TryGetPlayer(out PlayerInteractor2D player)) return;
        GameObject prefab = catalogGrid?.SelectedTarget<GameObject>(category);
        if (prefab == null) { SetStatus("생성할 프리팹이 없습니다."); return; }
        int count = 1;
        if (useMonsterSpawner && spawnCountInput != null) int.TryParse(spawnCountInput.text, out count);
        count = Mathf.Clamp(count, 1, 100);
        if (!useMonsterSpawner) ClearTestBosses();
        Vector3 origin = GetSpawnPosition();
        MonsterRoomArea2D spawnArea = activeEvent != null
            ? activeEvent.GetComponentInChildren<MonsterRoomArea2D>() : roomArea;
        MonsterSpawnRoomGroup spawnGroup = activeEvent != null
            ? activeEvent.GetComponentInChildren<MonsterSpawnRoomGroup>() : roomGroup;
        int created = 0;
        for (int i = 0; i < count; i++)
        {
            Vector3 position = origin + new Vector3(i % 10 * 1.5f, i / 10 * 1.5f, 0f);
            if (spawnArea != null && !spawnArea.Contains(position)) break;
            var request = new MonsterSpawnRequest(prefab, position, Quaternion.identity, spawnArea, null, spawnGroup);
            GameObject instance = useMonsterSpawner && MonsterSpawner.Instance != null
                ? MonsterSpawner.Instance.SpawnOne(request)
                : Instantiate(prefab, position, Quaternion.identity);
            if (instance == null) continue;
            SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
            DisableTestTravel(instance);
            if (!useMonsterSpawner) StartTestBossCombat(instance, player.transform);
            if (!useMonsterSpawner || MonsterSpawner.Instance == null)
            {
                var context = new MonsterSpawnContext(position, Quaternion.identity, spawnArea,
                    FindAnyObjectByType<TilemapPathfinder2D>());
                foreach (MonoBehaviour component in instance.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component is IMonsterSpawnContextReceiver receiver) receiver.ApplySpawnContext(context);
                spawnGroup?.NotifyMonsterSpawned(instance);
            }
            spawned.Add(instance);
            created++;
        }
        SetStatus($"생성: {prefab.name} / {created}마리");
        if (created > 0)
        {
            catalogGrid?.Close();
            SetPanelInputBlocked(false);
        }
    }

    private static void StartTestBossCombat(GameObject instance, Transform player)
    {
        // The prefab's OnEnable can start an encounter lock before Instantiate returns.
        // Stop it in this frame, then mark intro finished before the boss's Start/FSM.
        foreach (BossEncounterDirector director in instance.GetComponentsInChildren<BossEncounterDirector>(true)) director.enabled = false;
        foreach (BossTalkManager talk in instance.GetComponentsInChildren<BossTalkManager>(true)) talk.enabled = false;
        foreach (BossDeathPresentation death in instance.GetComponentsInChildren<BossDeathPresentation>(true)) death.enabled = false;
        foreach (BossControllerBase boss in instance.GetComponentsInChildren<BossControllerBase>(true))
        {
            boss.FinishEncounterIntro();
            boss.BeginCombatEncounter(player);
        }
    }

    private void ClearTestBosses()
    {
        foreach (BossControllerBase boss in FindObjectsByType<BossControllerBase>(FindObjectsInactive.Include))
        {
            if (boss == null || boss.gameObject.scene != gameObject.scene) continue;
            boss.SetCombatActive(false);
            boss.gameObject.SetActive(false);
            Destroy(boss.gameObject);
        }
        // These companions can live outside the boss prefab hierarchy.
        foreach (Candlestick candle in FindObjectsByType<Candlestick>(FindObjectsInactive.Include))
            if (candle != null && candle.gameObject.scene == gameObject.scene)
            { candle.gameObject.SetActive(false); Destroy(candle.gameObject); }
        foreach (EgoSwordActor sword in FindObjectsByType<EgoSwordActor>(FindObjectsInactive.Include))
            if (sword != null && sword.gameObject.scene == gameObject.scene)
            { sword.gameObject.SetActive(false); Destroy(sword.gameObject); }
        spawned.RemoveAll(x => x == null || x.GetComponentInChildren<BossControllerBase>(true) != null);
    }

    public void SpawnChest()
    {
        if (!TryGetPlayer(out _)) return;
        GameObject prefab = GetChoice(chestPrefabs, chestDropdown);
        if (prefab == null) { SetStatus("상자 프리팹이 없습니다."); return; }
        GameObject instance = Instantiate(prefab, GetSpawnPosition(), Quaternion.identity);
        SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
        spawned.Add(instance);
        SetStatus($"상자 생성: {prefab.name}");
    }

    public void SpawnEvent()
    {
        if (!TryGetPlayer(out PlayerInteractor2D player)) return;
        RoomTemplateSO template = GetChoice(eventTemplates, eventDropdown);
        if (template == null || eventRoomShell == null) { SetStatus("이벤트 방이 없습니다."); return; }
        ClearOwnedInstances();
        // The authored shell is inactive. Populate and bind the whole room before any placed actor wakes.
        RoomPieceAuthoring authoring = Instantiate(eventRoomShell);
        activeEvent = authoring.gameObject;
        activeEventTemplate = template;
        activeEvent.name = template.name;
        SceneManager.MoveGameObjectToScene(activeEvent, gameObject.scene);
        RectInt bounds = template.LayoutData.localBounds;
        var areaCollider = activeEvent.GetComponent<BoxCollider2D>();
        areaCollider.size = bounds.size;
        areaCollider.offset = bounds.center;
        Vector3 origin = arenaRoot != null ? arenaRoot.transform.position : Vector3.zero;
        activeEvent.transform.position = origin - (Vector3)bounds.center;
        if (!PopulateEventRoom(authoring, template, out string failure))
        {
            ClearOwnedInstances();
            SetStatus($"이벤트 방 구성 실패: {template.name} / {failure}");
            return;
        }
        DisableTestTravel(activeEvent);
        MonsterSpawnRoomGroup eventGroup = activeEvent.GetComponentInChildren<MonsterSpawnRoomGroup>(true);
        MonsterRoomArea2D eventArea = activeEvent.GetComponentInChildren<MonsterRoomArea2D>(true);
        var anchors = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (ProceduralRoomAnchor anchor in activeEvent.GetComponentsInChildren<ProceduralRoomAnchor>(true))
            if (!string.IsNullOrWhiteSpace(anchor.SlotId)) anchors.TryAdd(anchor.SlotId, anchor.Target);
        var context = new ProceduralRoomRuntimeContext(0, template,
            anchors, anchors, roomGroup: eventGroup != null ? eventGroup : roomGroup,
            roomArea: eventArea != null ? eventArea : roomArea);
        foreach (MonoBehaviour component in activeEvent.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component is IProceduralRoomRuntimeFeature feature &&
                !feature.TryBindProceduralRoom(context, out string reason))
            { ClearOwnedInstances(); SetStatus($"이벤트 방 연결 실패: {reason}"); return; }
        }
        if (authoring != null && authoring.FloorTilemap != null)
        {
            eventPathfinder = FindAnyObjectByType<TilemapPathfinder2D>();
            if (eventPathfinder != null)
            {
                originalPathfinderGrid = eventPathfinder.GroundGrid;
                originalPathfinderGround = eventPathfinder.GroundTilemap;
                eventPathfinder.ConfigureGround(authoring.Grid, authoring.FloorTilemap);
            }
            // The event provides a complete room. Arena walls must also be hidden,
            // otherwise a larger event room can intersect the original 30x30 walls.
            if (arenaRoot != null) arenaRoot.SetActive(false);
            else if (groundTilemap != null) groundTilemap.gameObject.SetActive(false);
            if (roomGroup != null) roomGroup.enabled = false;
            if (roomArea != null && roomArea.AreaCollider != null) roomArea.AreaCollider.enabled = false;
        }
        activeEvent.SetActive(true);
        Physics2D.SyncTransforms();
        Vector3 destination = authoring != null && authoring.FloorTilemap != null
            ? FindEventLandingPosition(authoring)
            : GetSpawnPosition();
        if (player.TryGetComponent(out MovementMotor2D motor)) motor.WarpTo(destination, true, true);
        RefreshSafetyTracking();
        SetStatus($"이벤트 방 생성: {template.name}. 생성물 정리로 기본 방에 복귀합니다.");
    }

    private static bool PopulateEventRoom(RoomPieceAuthoring room, RoomTemplateSO template, out string failure)
    {
        RoomBuildData build = template.BuildData;
        foreach (RoomTileLayerKind layer in RoomTileLayerContract.OrderedLayers)
        {
            var tiles = build.GetTiles(layer);
            if (tiles == null) continue;
            Tilemap map = room.GetTilemap(layer);
            foreach (RoomTileData tile in tiles)
                map.SetTile((Vector3Int)tile.localCell, tile.tile);
        }

        failure = null;
        if (build.objectPlacements == null) return true;
        foreach (var placement in build.objectPlacements)
        {
            if (placement.prefab == null) continue;
            var instance = Instantiate(placement.prefab, room.transform, false);
            instance.transform.localPosition = (Vector2)placement.localCell + placement.localOffset;
            instance.transform.localRotation = Quaternion.Euler(0, 0, placement.localRotationDegrees);
            if (placement.localScale != Vector3.zero) instance.transform.localScale = placement.localScale;
            if (placement.childPoseOverrides == null || placement.childPoseOverrides.Count == 0) continue;
            var composite = instance.GetComponent<RoomCompositePoseAuthoring>();
            if (composite == null) { failure = placement.placementId; return false; }
            if (!composite.TryApplyPoseOverrides(placement.childPoseOverrides, out failure)) return false;
        }
        return true;
    }

    public void LevelUp()
    {
        if (!TryGetPlayer(out _)) return;
        LevelProgressionState state = RunLevelProgression.State;
        if (state == null || progressionConfig == null) { SetStatus("레벨 설정이 없습니다."); return; }
        int required = progressionConfig.GetRequiredExperience(state.level);
        if (required <= 0) { SetStatus("최대 레벨입니다."); return; }
        bool granted = RunLevelProgression.TryGrantExperience(progressionConfig,
            Mathf.Max(1, required - state.currentExperience), out _);
        SetStatus(granted ? "레벨업 완료. 보상 선택 또는 지정 효과 적용을 사용하세요." : "레벨업 실패.");
    }

    public void OpenRewards()
    {
        if (!TryGetPlayer(out _)) return;
        if (rewardSession == null) { SetStatus("플레이어의 레벨업 선택 UI를 찾을 수 없습니다."); return; }
        bool opened = rewardSession.TryOpenSession(out string reason);
        SetStatus(opened ? "기존 레벨업 선택 UI를 열었습니다." : reason);
        if (opened)
        {
            catalogGrid?.Close();
            if (panelRoot != null) panelRoot.gameObject.SetActive(false);
        }
    }

    public void ApplyReward()
    {
        if (!TryGetPlayer(out PlayerInteractor2D player)) return;
        LevelRewardDefinitionSO definition = GetChoice(rewards, rewardDropdown);
        if (definition == null) { SetStatus("선택한 레벨업 효과가 없습니다."); return; }
        rewardSession?.CloseSession();
        var receipt = new TestSceneRewardReceipt(player, definition);
        if (!RunLevelRewards.TrySelect(definition, out string reason)) { SetStatus(reason); return; }
        LevelProgressionState state = RunLevelProgression.State;
        LevelRewardSelectionState selection = state.selectedRewards[state.selectedRewards.Count - 1];
        receipt.Complete();
        receipts[selection] = receipt;
        state.activeRewardOffer?.Clear();
        RefreshSelectedRewards();
        SetStatus($"효과 적용: {definition.DisplayName}");
    }

    public void RemoveReward()
    {
        LevelRewardSelectionState selection = GetChoice(selectedRewards, appliedRewardDropdown);
        if (selection == null) { SetStatus("제거할 효과가 없습니다."); return; }
        if (TryRemoveReward(selection, out string reason))
            SetStatus("선택한 효과를 제거했습니다. 레벨과 미수령 보상 수는 유지됩니다.");
        else SetStatus(reason);
    }

    public void ClearRewards()
    {
        var selections = new List<LevelRewardSelectionState>(RunLevelProgression.State?.selectedRewards ?? new());
        int removed = 0;
        string lastFailure = null;
        for (int i = selections.Count - 1; i >= 0; i--)
        {
            if (TryRemoveReward(selections[i], out string reason)) removed++;
            else lastFailure = reason;
        }
        SetStatus(lastFailure == null ? $"효과 {removed}개 제거 완료." : $"효과 {removed}개 제거. {lastFailure}");
    }

    private bool TryRemoveReward(LevelRewardSelectionState selection, out string failureReason)
    {
        failureReason = null;
        if (!isTestScene || !RunSessionStore.IsRunActive ||
            !RunLevelRewards.TryGetDefinition(selection.rewardId, out LevelRewardDefinitionSO definition))
        { failureReason = "현재 테스트 런의 효과가 아닙니다."; return false; }
        bool hasInstant = false;
        foreach (LevelRewardEffectSO effect in definition.Effects)
            hasInstant |= effect != null && effect.Lifetime == LevelRewardEffectLifetime.InstantOnce;
        if (hasInstant && (!receipts.TryGetValue(selection, out TestSceneRewardReceipt receipt) ||
            !receipt.TryRevert(this, out failureReason)))
        {
            failureReason ??= "즉시 효과의 지급 기록이 없습니다. TestScene을 다시 실행하세요.";
            return false;
        }
        rewardSession?.CloseSession();
        if (!RunLevelRewards.TryRemoveSelection(selection, out failureReason, hasInstant)) return false;
        receipts.Remove(selection);
        return true;
    }

    public void ClearSpawned()
    {
        if (!isTestScene || !Application.isPlaying) return;
        ClearOwnedInstances();
        PlayerInteractor2D player = PlayerRuntimeRegistry.CurrentPlayer;
        if (player != null && player.TryGetComponent(out MovementMotor2D motor))
            motor.WarpTo(arenaRoot != null ? arenaRoot.transform.position : Vector3.zero, true, true);
        SetStatus("생성물을 정리하고 기본 테스트 방으로 복귀했습니다.");
    }

    private void ClearOwnedInstances()
    {
        activeEventTemplate = null;
        if (!Application.isPlaying)
        {
            spawned.Clear();
            activeEvent = null;
            return;
        }
        // Event encounters and split bosses can create actors outside the original
        // instance tree. The clear command owns every enemy in this dedicated scene.
        if (isTestScene)
        {
            foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsInactive.Include))
                if (enemy != null && enemy.gameObject.scene == gameObject.scene)
                    Destroy(enemy.gameObject);
            foreach (Candlestick candle in FindObjectsByType<Candlestick>(FindObjectsInactive.Include))
                if (candle != null && candle.gameObject.scene == gameObject.scene)
                    Destroy(candle.gameObject);
            foreach (EgoSwordActor sword in FindObjectsByType<EgoSwordActor>(FindObjectsInactive.Include))
                if (sword != null && sword.gameObject.scene == gameObject.scene)
                    Destroy(sword.gameObject);
        }
        foreach (GameObject instance in spawned)
            if (instance != null) Destroy(instance);
        spawned.Clear();
        if (activeEvent != null) { activeEvent.SetActive(false); Destroy(activeEvent); activeEvent = null; }
        if (eventPathfinder != null)
            eventPathfinder.ConfigureGround(originalPathfinderGrid, originalPathfinderGround);
        eventPathfinder = null;
        originalPathfinderGrid = null;
        originalPathfinderGround = null;
        if (arenaRoot != null) arenaRoot.SetActive(originalArenaActive);
        if (groundTilemap != null) groundTilemap.gameObject.SetActive(originalGroundActive);
        RefreshSafetyTracking();
        if (roomGroup != null) roomGroup.enabled = originalRoomGroupEnabled;
        if (roomArea != null && roomArea.AreaCollider != null) roomArea.AreaCollider.enabled = originalAreaColliderEnabled;
    }

    public void Heal()
    {
        if (!TryGetPlayer(out PlayerInteractor2D player)) return;
        AttributeSet attributes = player.GetComponent<AttributeSet>();
        if (attributes == null) { SetStatus("플레이어 속성이 없습니다."); return; }
        foreach (AttributeSet.MaxLink link in attributes.EnumerateMaxLinks())
            if (link.value != null && link.max != null)
                attributes.TrySetCurrentValue(link.value, attributes.GetCurrentValue(link.max), this);
        SetStatus("플레이어 체력을 회복했습니다.");
    }

    public void TogglePanel()
    {
        if (!isTestScene || panelRoot == null) return;
        bool show = !panelRoot.gameObject.activeSelf;
        panelRoot.gameObject.SetActive(show);
        if (!show)
        {
            catalogGrid?.Close();
            SetPanelInputBlocked(false);
        }
    }

    public void ResetPlayer()
    {
        if (resettingPlayer || !TryGetPlayer(out PlayerInteractor2D player)) return;
        PlayerSpawner spawner = FindAnyObjectByType<PlayerSpawner>();
        if (spawner == null || spawner.gameObject.scene != gameObject.scene)
        { SetStatus("TestScene PlayerSpawner를 찾을 수 없습니다."); return; }
        StartCoroutine(ResetPlayerRoutine(player, spawner));
    }

    private IEnumerator ResetPlayerRoutine(PlayerInteractor2D player, PlayerSpawner spawner)
    {
        resettingPlayer = true;
        rewardSession?.CloseSession();
        ClearOwnedInstances();
        RunSessionStore.ClearPendingPlayerState();
        // Instant rewards belong to resources on the discarded player. Persistent
        // selections stay in run state and rebuild when the replacement registers.
        var selections = new List<LevelRewardSelectionState>(RunLevelProgression.State.selectedRewards);
        player.gameObject.SetActive(false);
        Destroy(player.gameObject);
        yield return null;
        foreach (LevelRewardSelectionState selection in selections)
        {
            if (!RunLevelRewards.TryGetDefinition(selection.rewardId, out LevelRewardDefinitionSO definition)) continue;
            bool hasInstant = false;
            foreach (LevelRewardEffectSO effect in definition.Effects)
                hasInstant |= effect != null && effect.Lifetime == LevelRewardEffectLifetime.InstantOnce;
            if (hasInstant) RunLevelRewards.TryRemoveSelection(selection, out _, true);
        }
        receipts.Clear(); offerReceipts.Clear();
        spawner.SpawnIfNeeded();
        yield return null;
        resettingPlayer = false;
        RefreshSelectedRewards();
        SetStatus("플레이어 초기화 완료. 레벨·지속효과 유지, 무기·유물·즉시 지급효과는 초기화됩니다.");
    }

    private void GuardTimer(RunTimeLimitSystem timer)
    {
        if (guardedTimer != null && guardedTimer != timer) guardedTimer.SetExternalPause(this, false);
        guardedTimer = timer;
        guardedTimer?.SetExternalPause(this, true);
        foreach (RunTimeOverReturnToHub handler in FindObjectsByType<RunTimeOverReturnToHub>(FindObjectsInactive.Include))
        {
            if (!guardedTimeReturns.ContainsKey(handler)) guardedTimeReturns.Add(handler, handler.enabled);
            handler.enabled = false;
        }
    }

    private static void DisableTestTravel(GameObject instance)
    {
        foreach (ScenePortal portal in instance.GetComponentsInChildren<ScenePortal>(true)) portal.enabled = false;
        foreach (SceneTravelTrigger2D trigger in instance.GetComponentsInChildren<SceneTravelTrigger2D>(true)) trigger.enabled = false;
        foreach (SceneTravelInteractable travel in instance.GetComponentsInChildren<SceneTravelInteractable>(true)) travel.enabled = false;
        foreach (BossDefeatEndingSequence ending in instance.GetComponentsInChildren<BossDefeatEndingSequence>(true)) ending.enabled = false;
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        if (player != null && player.TryGetComponent(out PlayerDeathReturnToHub2D deathReturn) && guardedDeathReturn != deathReturn)
        {
            guardedDeathReturn = deathReturn;
            previousDeathReturnEnabled = deathReturn.enabled;
            deathReturn.enabled = false;
        }
        if (rewardSession != null)
        {
            rewardSession.SessionOpened -= CaptureOfferReceipts;
            rewardSession.SessionChanged -= CaptureOfferReceipts;
        }
        // The authored reward session lives on GlobalUIRoot, not on the player prefab.
        rewardSession = player != null ? FindAnyObjectByType<LevelRewardSessionController>(FindObjectsInactive.Include) : null;
        if (rewardSession != null)
        {
            rewardSession.SessionOpened += CaptureOfferReceipts;
            rewardSession.SessionChanged += CaptureOfferReceipts;
        }
        RefreshSelectedRewards();
    }

    private void CaptureOfferReceipts()
    {
        offerReceipts.Clear();
        PlayerInteractor2D player = PlayerRuntimeRegistry.CurrentPlayer;
        if (player == null || rewardSession == null || !rewardSession.IsSessionOpen) return;
        foreach (LevelRewardDefinitionSO definition in rewardSession.Candidates)
            if (definition != null) offerReceipts[definition.RewardId] = new TestSceneRewardReceipt(player, definition);
    }

    private void HandleRewardsChanged()
    {
        List<LevelRewardSelectionState> selections = RunLevelProgression.State?.selectedRewards;
        if (selections != null)
        {
            foreach (LevelRewardSelectionState selection in selections)
            {
                if (selection == null || knownSelections.Contains(selection) || receipts.ContainsKey(selection)) continue;
                if (offerReceipts.TryGetValue(selection.rewardId, out TestSceneRewardReceipt receipt))
                { receipt.Complete(); receipts[selection] = receipt; }
            }
        }
        RefreshSelectedRewards();
    }

    private void PopulateChoices()
    {
        rewards.Clear();
        if (rewardCatalog != null) foreach (LevelRewardDefinitionSO reward in rewardCatalog.Rewards)
            if (reward != null) rewards.Add(reward);
        Populate(rewardDropdown, rewards.ConvertAll(x => x.DisplayName));
        Populate(chestDropdown, Names(chestPrefabs)); Populate(eventDropdown, Names(eventTemplates));
        Populate(weaponSlotDropdown, new List<string> { "슬롯 1", "슬롯 2" });
        RefreshSelectedRewards();
    }

    private void RefreshSelectedRewards()
    {
        int previousIndex = appliedRewardDropdown != null ? appliedRewardDropdown.value : 0;
        selectedRewards.Clear(); knownSelections.Clear();
        var labels = new List<string>();
        List<LevelRewardSelectionState> source = RunLevelProgression.State?.selectedRewards;
        if (source != null) foreach (LevelRewardSelectionState selection in source)
        {
            if (selection == null) continue;
            selectedRewards.Add(selection); knownSelections.Add(selection);
            labels.Add(RunLevelRewards.TryGetDefinition(selection.rewardId, out LevelRewardDefinitionSO definition)
                ? definition.DisplayName : selection.rewardId);
        }
        Populate(appliedRewardDropdown, labels);
        if (appliedRewardDropdown != null) appliedRewardDropdown.SetValueWithoutNotify(Mathf.Clamp(previousIndex, 0, labels.Count - 1));
        RefreshProgression();
    }

    private void RefreshProgression()
    {
        LevelProgressionState state = RunLevelProgression.State;
        if (progressionText != null)
            progressionText.text = state != null
                ? $"Lv.{state.level}  EXP {state.currentExperience}  미수령 {state.pendingRewardCount}  적용 {state.selectedRewards?.Count ?? 0}"
                : "테스트 런 대기 중";
    }

    private bool TryGetPlayer(out PlayerInteractor2D player)
    {
        player = PlayerRuntimeRegistry.CurrentPlayer;
        if (!isTestScene || !Application.isPlaying || player == null || player.gameObject.scene != gameObject.scene)
        { SetStatus("TestScene 플레이어가 준비되지 않았습니다."); return false; }
        return true;
    }

    private Vector3 GetSpawnPosition()
    {
        if (activeEvent != null)
        {
            MonsterRoomArea2D eventArea = activeEvent.GetComponentInChildren<MonsterRoomArea2D>();
            Vector3 candidate = PlayerRuntimeRegistry.CurrentPlayer != null
                ? PlayerRuntimeRegistry.CurrentPlayer.transform.position + Vector3.up * 2f : activeEvent.transform.position;
            if (eventArea != null && eventArea.Contains(candidate)) return candidate;
            RoomPieceAuthoring authoring = activeEvent.GetComponentInChildren<RoomPieceAuthoring>();
            if (authoring != null && authoring.FloorTilemap != null) return FindEventLandingPosition(authoring);
        }
        return spawnOrigin != null ? spawnOrigin.position : PlayerRuntimeRegistry.CurrentPlayer != null
            ? PlayerRuntimeRegistry.CurrentPlayer.transform.position + Vector3.up * 4f : Vector3.zero;
    }

    private Vector3 FindEventLandingPosition(RoomPieceAuthoring authoring)
    {
        Tilemap floor = authoring.FloorTilemap;
        RectInt bounds = activeEventTemplate.LayoutData.localBounds;
        Vector3 preferred = floor.GetCellCenterWorld(new Vector3Int(
            Mathf.FloorToInt(bounds.center.x), Mathf.FloorToInt(bounds.center.y), 0));
        Vector3 best = preferred;
        float nearest = float.PositiveInfinity;
        int blocked = LayerMask.GetMask("Wall", "HoleTrap");
        foreach (Vector3Int cell in floor.cellBounds.allPositionsWithin)
        {
            if (!floor.HasTile(cell)) continue;
            Vector3 position = floor.GetCellCenterWorld(cell);
            float distance = (position - preferred).sqrMagnitude;
            if (distance >= nearest || Physics2D.OverlapCircle(position, .4f, blocked) != null) continue;
            best = position;
            nearest = distance;
        }
        return best;
    }
    private static void RefreshSafetyTracking()
    {
        PlayerInteractor2D player = PlayerRuntimeRegistry.CurrentPlayer;
        if (player != null && player.TryGetComponent(out SafetyTracker tracker))
        {
            tracker.RefreshTilemaps();
        }
    }

    private static T GetChoice<T>(IReadOnlyList<T> entries, TMP_Dropdown dropdown) where T : class
    {
        int index = dropdown != null ? dropdown.value : 0;
        return entries != null && index >= 0 && index < entries.Count ? entries[index] : null;
    }

    private void ExecuteCatalogChoice(int category)
    {
        switch (category)
        {
            case TestSceneCatalogGrid.WeaponCategory: GiveWeapon(); break;
            case TestSceneCatalogGrid.RelicCategory: GiveRelic(); break;
            case TestSceneCatalogGrid.MonsterCategory: SpawnMonster(); break;
            case TestSceneCatalogGrid.BossCategory: SpawnBoss(); break;
        }
    }

    private static List<string> Names(UnityEngine.Object[] assets)
    {
        var names = new List<string>();
        if (assets != null) foreach (var asset in assets) names.Add(asset != null ? asset.name : "(미설정)");
        return names;
    }

    private static void Populate(TMP_Dropdown dropdown, List<string> labels)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions(); dropdown.AddOptions(labels.Count > 0 ? labels : new List<string> { "(없음)" });
        dropdown.interactable = labels.Count > 0;
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        catalogGrid?.ShowFeedback(message);
    }

    private void BindButtons(bool bind)
    {
        Bind(openWeaponsButton, OpenWeapons, bind); Bind(openRelicsButton, OpenRelics, bind);
        Bind(openBossesButton, OpenBosses, bind); Bind(openMonstersButton, OpenMonsters, bind);
        Bind(spawnChestButton, SpawnChest, bind); Bind(spawnEventButton, SpawnEvent, bind);
        Bind(levelUpButton, LevelUp, bind); Bind(openRewardsButton, OpenRewards, bind);
        Bind(applyRewardButton, ApplyReward, bind); Bind(removeRewardButton, RemoveReward, bind);
        Bind(clearRewardsButton, ClearRewards, bind); Bind(clearSpawnedButton, ClearSpawned, bind); Bind(healButton, Heal, bind);
        Bind(resetPlayerButton, ResetPlayer, bind);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction command, bool bind)
    {
        if (button == null) return;
        if (bind) button.onClick.AddListener(command); else button.onClick.RemoveListener(command);
    }
}

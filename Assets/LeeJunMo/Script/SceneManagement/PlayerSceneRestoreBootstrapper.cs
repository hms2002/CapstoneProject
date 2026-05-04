using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

/// <summary>
/// 책임 : 씬 진입 후 생성된 플레이어를 감지해 pending PlayerRuntimeState를 정확히 1회 복원한다.
/// PlayerSpawner와 직접 결합하지 않고, 레지스트리 이벤트와 재시도를 통해 복원 타이밍을 흡수한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSceneRestoreBootstrapper : MonoBehaviour
{
    [Header("Resolver")]
    [SerializeField] private MonoBehaviour resolverSource;

    [Header("Optional Runtime Restorers")]
    [SerializeField] private MonoBehaviour weaponRuntimeRestorerSource;
    [SerializeField] private MonoBehaviour relicRuntimeRestorerSource;

    [Header("Retry Policy")]
    [SerializeField, Min(0.1f)] private float maxWaitSeconds = 5f;
    [SerializeField, Min(0.05f)] private float retryInterval = 0.1f;
    [SerializeField] private bool restoreOnStart = true;

    private IPlayerRuntimeResolver resolver;
    private IWeaponRuntimeStateRestorer weaponRuntimeRestorer;
    private IRelicRuntimeStateRestorer relicRuntimeRestorer;

    private Coroutine restoreRoutine;
    private Coroutine restoreConfirmRoutine;
    private bool hasRestored;
    private bool isRestoreConfirmationPending;

    private void Awake()
    {
        if (resolverSource == null)
            resolverSource = GetComponent<PlayerRuntimeResolverBridge>();

        if (weaponRuntimeRestorerSource == null)
            weaponRuntimeRestorerSource = GetComponent<WeaponAbilityRuntimeStateBridge>();

        if (relicRuntimeRestorerSource == null)
            relicRuntimeRestorerSource = GetComponent<RelicRuntimeStateBridge>();

        resolver = resolverSource as IPlayerRuntimeResolver;
        weaponRuntimeRestorer = weaponRuntimeRestorerSource as IWeaponRuntimeStateRestorer;
        relicRuntimeRestorer = relicRuntimeRestorerSource as IRelicRuntimeStateRestorer;

        if (resolverSource != null && resolver == null)
        {
            Debug.LogError("[PlayerSceneRestoreBootstrapper] resolverSource가 IPlayerRuntimeResolver를 구현하지 않았습니다.", this);
        }

        if (weaponRuntimeRestorerSource != null && weaponRuntimeRestorer == null)
        {
            Debug.LogError("[PlayerSceneRestoreBootstrapper] weaponRuntimeRestorerSource가 IWeaponRuntimeStateRestorer를 구현하지 않았습니다.", this);
        }

        if (relicRuntimeRestorerSource != null && relicRuntimeRestorer == null)
        {
            Debug.LogError("[PlayerSceneRestoreBootstrapper] relicRuntimeRestorerSource가 IRelicRuntimeStateRestorer를 구현하지 않았습니다.", this);
        }
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += OnPlayerRegistered;
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= OnPlayerRegistered;

        if (restoreConfirmRoutine != null)
        {
            StopCoroutine(restoreConfirmRoutine);
            restoreConfirmRoutine = null;
        }
    }

    private void Start()
    {
        if (!restoreOnStart)
            return;

        // 이미 등록된 플레이어가 있으면 즉시 시도
        if (!TryRestorePendingState())
        {
            // 아직 플레이어가 없거나 타이밍이 애매한 경우 재시도 루틴 시작
            restoreRoutine = StartCoroutine(RestoreWhenReadyRoutine());
        }
    }

    /// <summary>
    /// 책임 : PlayerSpawner가 새 플레이어를 등록했을 때 즉시 복원을 시도한다.
    /// 이벤트가 먼저 오더라도 hasRestored로 중복 복원을 막는다.
    /// </summary>
    private void OnPlayerRegistered(PlayerInteractor2D player)
    {
        if (player == null || hasRestored)
            return;

        TryRestorePendingState(player.gameObject);
    }

    /// <summary>
    /// 책임 : 씬 진입 직후 순서가 불안정한 경우를 대비해 일정 시간 동안 복원을 재시도한다.
    /// PlayerSpawner Start 타이밍, 지연 스폰, 초기화 순서 차이를 흡수하는 안전장치다.
    /// </summary>
    private IEnumerator RestoreWhenReadyRoutine()
    {
        float elapsed = 0f;

        while (!hasRestored && elapsed < maxWaitSeconds)
        {
            if (TryRestorePendingState())
                yield break;

            yield return new WaitForSeconds(retryInterval);
            elapsed += retryInterval;
        }

        if (!hasRestored && GamePlayDataManager.Instance != null && GamePlayDataManager.Instance.PeekPendingPlayerState() != null)
        {
            Debug.LogWarning("[PlayerSceneRestoreBootstrapper] 제한 시간 내에 PlayerRuntimeState 복원을 완료하지 못했습니다.", this);
        }

        restoreRoutine = null;
    }

    /// <summary>
    /// 책임 : 현재 씬의 플레이어를 자동 탐색해 pending 상태 복원을 시도한다.
    /// </summary>
    public bool TryRestorePendingState()
    {
        var player = FindPlayer();
        if(player == null) return false;
        var playerWeaponRestorer = player.GetComponent<WeaponAbilityRuntimeStateBridge>();
        if (playerWeaponRestorer != null)
            weaponRuntimeRestorer = playerWeaponRestorer;

        var playerRelicRestorer = player.GetComponent<RelicRuntimeStateBridge>();
        if (playerRelicRestorer != null)
            relicRuntimeRestorer = playerRelicRestorer;
        return TryRestorePendingState(player);
    }

    /// <summary>
    /// 책임 : 플레이어 게임오브젝트에서 복원에 필요한 모든 컴포넌트를 추출하여 Context로 반환한다.
    /// (SRP 적용: 메인 복원 로직과 컴포넌트 탐색 로직의 분리)
    /// </summary>
    private bool TryGatherPlayerComponents(GameObject player, out PlayerSystemContext ctx)
    {
        ctx = new PlayerSystemContext
        {
            weaponInventory = player.GetComponent<WeaponInventory2D>(),
            consumableInventory = player.GetComponent<PlayerConsumableInventory>(),
            relicInventory = player.GetComponent<RelicInventory>(),
            attributeSet = player.GetComponent<AttributeSet>(),
            effectRunner = player.GetComponent<GameplayEffectRunner>(),
            tagSystem = player.GetComponent<TagSystem>(),
            abilitySystem = player.GetComponent<AbilitySystem>()
        };

        if (ctx.weaponInventory == null || ctx.consumableInventory == null || ctx.relicInventory == null)
        {
            Debug.LogWarning(
                "[PlayerSceneRestoreBootstrapper] Player inventory components are missing. Pending PlayerRuntimeState restore will wait.",
                this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 책임 : 지정된 플레이어 GameObject에 pending 상태를 복원한다.
    /// 복원에 필요한 resolver / runtime restorer를 새 플레이어 기준으로 다시 바인딩한 뒤
    /// PlayerRuntimeRestoreCoordinator에 전달한다.
    /// </summary>
    public bool TryRestorePendingState(GameObject player)
    {
        if (hasRestored || isRestoreConfirmationPending)
            return false;

        var gameplay = GamePlayDataManager.Instance;
        if (gameplay == null)
        {
            Debug.LogWarning("[PlayerSceneRestoreBootstrapper] GamePlayDataManager.Instance가 없습니다.", this);
            return false;
        }

        var pendingState = gameplay.PeekPendingPlayerState();
        if (pendingState == null)
            return false;

        if (player == null)
            return false;

        if (!IsRestoreAllowedForCurrentScene(gameplay))
            return false;

        if (!IsItemRestoreReady())
            return false;

        resolver = player.GetComponent<PlayerRuntimeResolverBridge>();
        if (resolver == null)
        {
            Debug.LogError("[PlayerSceneRestoreBootstrapper] Resolver가 없어 PlayerRuntimeState를 복원할 수 없습니다.", this);
            return false;
        }

        if (!CanResolvePendingEquipment(pendingState, resolver))
            return false;

        // 책임 : 플레이어 컴포넌트 일괄 수집
        if (!TryGatherPlayerComponents(player, out var ctx))
            return false;

        // 책임 : 씬마다 새로 생성된 플레이어 기준으로 runtime restorer를 다시 잡는다.
        var playerWeaponRestorer = player.GetComponent<WeaponAbilityRuntimeStateBridge>();
        if (playerWeaponRestorer != null)
            weaponRuntimeRestorer = playerWeaponRestorer;

        // 책임 : 유물도 전용 브리지를 통해 플레이어 기준 restorer를 다시 바인딩한다.
        var playerRelicRestorer = player.GetComponent<RelicRuntimeStateBridge>();
        if (playerRelicRestorer != null)
            relicRuntimeRestorer = playerRelicRestorer;

        PlayerRuntimeRestoreCoordinator.RestoreAll(
            pendingState,
            ctx,
            resolver,
            weaponRuntimeRestorer,
            relicRuntimeRestorer,
            this);

        isRestoreConfirmationPending = true;
        restoreConfirmRoutine = StartCoroutine(ConfirmRestoreNextFrame(gameplay, pendingState, player));
        return true;
    }

    private bool IsRestoreAllowedForCurrentScene(GamePlayDataManager gameplay)
    {
        if (gameplay == null)
            return false;

        SceneTransitionContext transition = gameplay.PeekPendingTransition();
        if (transition == null || string.IsNullOrEmpty(transition.toScene))
            return true;

        string activeSceneName = SceneManager.GetActiveScene().name;
        return string.Equals(activeSceneName, transition.toScene, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 책임 : 복원 직후 한 프레임을 넘긴 뒤 실제 장비 슬롯 상태가 저장본과 일치하는지 검증한다.
    /// Start/OnEnable 초기화가 뒤늦게 복원 결과를 덮는 경우를 탐지하고, 실패 시 pending state를 소비하지 않는다.
    /// </summary>
    private IEnumerator ConfirmRestoreNextFrame(
        GamePlayDataManager gameplay,
        PlayerRuntimeState pendingState,
        GameObject player)
    {
        yield return null;

        restoreConfirmRoutine = null;

        if (hasRestored)
        {
            isRestoreConfirmationPending = false;
            yield break;
        }

        if (gameplay == null || pendingState == null || player == null)
        {
            isRestoreConfirmationPending = false;
            yield break;
        }

        if (!TryGatherPlayerComponents(player, out var ctx))
        {
            isRestoreConfirmationPending = false;
            yield break;
        }

        if (!MatchesPendingEquipmentState(pendingState, ctx))
        {
            isRestoreConfirmationPending = false;
            Debug.LogWarning("[PlayerSceneRestoreBootstrapper] 복원 직후 장비 상태가 저장본과 일치하지 않아 pending state 소비를 보류합니다.", this);
            yield break;
        }

        gameplay.ConsumePendingPlayerState();
        hasRestored = true;
        isRestoreConfirmationPending = false;

        if (restoreRoutine != null)
        {
            StopCoroutine(restoreRoutine);
            restoreRoutine = null;
        }

        Debug.Log("[PlayerSceneRestoreBootstrapper] PlayerRuntimeState 복원 완료.", this);
    }

    /// <summary>
    /// 책임 : 실제 플레이어 인벤토리 상태가 pending PlayerRuntimeState의 장비 배치와 일치하는지 검증한다.
    /// 복원 성공 확정 전에 shell layout이 보존되었는지 확인하는 최종 안전장치다.
    /// </summary>
    private bool MatchesPendingEquipmentState(PlayerRuntimeState pendingState, PlayerSystemContext ctx)
    {
        if (!MatchesPendingWeapons(pendingState.weaponInventory, ctx.weaponInventory))
            return false;

        if (!MatchesPendingRelics(pendingState.relicInventory, ctx.relicInventory))
            return false;

        if (!MatchesPendingConsumables(pendingState.consumableInventory, ctx.consumableInventory))
            return false;

        return true;
    }

    /// <summary>
    /// 책임 : 현재 무기 슬롯/활성 슬롯이 저장본과 같은지 비교한다.
    /// </summary>
    private bool MatchesPendingWeapons(WeaponInventoryState pending, WeaponInventory2D inventory)
    {
        if (pending == null)
            return true;

        if (inventory == null)
            return false;

        var current = inventory.CaptureInventoryState();
        if (current == null)
            return false;

        if (current.activeSlotIndex != pending.activeSlotIndex)
            return false;

        int pendingCount = pending.slotWeaponIds != null ? pending.slotWeaponIds.Length : 0;
        int currentCount = current.slotWeaponIds != null ? current.slotWeaponIds.Length : 0;
        if (pendingCount != currentCount)
            return false;

        for (int i = 0; i < pendingCount; i++)
        {
            if (!string.Equals(current.slotWeaponIds[i], pending.slotWeaponIds[i], System.StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 책임 : 현재 유물 슬롯/레벨이 저장본과 같은지 비교한다.
    /// </summary>
    private bool MatchesPendingRelics(RelicInventoryState pending, RelicInventory inventory)
    {
        if (pending == null)
            return true;

        if (inventory == null)
            return false;

        var current = inventory.CaptureInventoryState();
        if (current == null)
            return false;

        int pendingCount = pending.slots != null ? pending.slots.Length : 0;
        int currentCount = current.slots != null ? current.slots.Length : 0;
        if (pendingCount != currentCount)
            return false;

        for (int i = 0; i < pendingCount; i++)
        {
            var pendingSlot = pending.slots[i];
            var currentSlot = current.slots[i];

            string pendingId = pendingSlot != null ? pendingSlot.relicId : null;
            string currentId = currentSlot != null ? currentSlot.relicId : null;
            int pendingLevel = pendingSlot != null ? pendingSlot.level : 0;
            int currentLevel = currentSlot != null ? currentSlot.level : 0;

            if (!string.Equals(currentId, pendingId, System.StringComparison.Ordinal))
                return false;

            if (currentLevel != pendingLevel)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 책임 : 현재 consumable 슬롯 상태가 저장본과 같은지 비교한다.
    /// </summary>
    private bool MatchesPendingConsumables(ConsumableInventoryState pending, PlayerConsumableInventory inventory)
    {
        if (pending == null)
            return true;

        if (inventory == null)
            return false;

        var current = inventory.CaptureInventoryState();
        if (current == null)
            return false;

        int pendingCount = pending.slots != null ? pending.slots.Length : 0;
        int currentCount = current.slots != null ? current.slots.Length : 0;
        if (pendingCount != currentCount)
            return false;

        for (int i = 0; i < pendingCount; i++)
        {
            var pendingSlot = pending.slots[i];
            var currentSlot = current.slots[i];

            string pendingId = pendingSlot != null ? pendingSlot.consumableId : null;
            string currentId = currentSlot != null ? currentSlot.consumableId : null;

            if (!string.Equals(currentId, pendingId, System.StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 책임 : 씬 복원 직전에 ItemManager가 실제 데이터베이스를 채운 준비 상태인지 확인한다.
    /// ItemManager 인스턴스만 먼저 생성되고 database는 아직 adopt되지 않은 레이스를 막는다.
    /// </summary>
    private bool IsItemRestoreReady()
    {
        if (ItemManager.Instance == null)
            return false;

        return ItemManager.Instance.IsReady;
    }

    /// <summary>
    /// 책임 : pending PlayerRuntimeState에 포함된 장비 ID가 현재 resolver로 모두 해석 가능한지 사전 검증한다.
    /// 하나라도 해석 실패하면 이번 프레임 복원을 보류해 pending state를 다음 재시도까지 유지한다.
    /// </summary>
    private bool CanResolvePendingEquipment(PlayerRuntimeState pendingState, IPlayerRuntimeResolver runtimeResolver)
    {
        if (pendingState == null || runtimeResolver == null)
            return false;

        if (!CanResolvePendingWeapons(pendingState.weaponInventory, runtimeResolver))
            return false;

        if (!CanResolvePendingRelics(pendingState.relicInventory, runtimeResolver))
            return false;

        if (!CanResolvePendingConsumables(pendingState.consumableInventory, runtimeResolver))
            return false;

        return true;
    }

    /// <summary>
    /// 책임 : 저장된 무기 슬롯 ID가 모두 현재 아이템 데이터베이스에 존재하는지 검증한다.
    /// </summary>
    private bool CanResolvePendingWeapons(WeaponInventoryState state, IPlayerRuntimeResolver runtimeResolver)
    {
        if (state?.slotWeaponIds == null)
            return true;

        for (int i = 0; i < state.slotWeaponIds.Length; i++)
        {
            string weaponId = state.slotWeaponIds[i];
            if (string.IsNullOrEmpty(weaponId))
                continue;

            if (runtimeResolver.ResolveWeapon(weaponId) != null)
                continue;

            Debug.LogWarning($"[PlayerSceneRestoreBootstrapper] 무기 복원을 보류합니다. 아직 해석할 수 없는 weaponId={weaponId}, slot={i}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 책임 : 저장된 유물 슬롯 ID가 모두 현재 아이템 데이터베이스에 존재하는지 검증한다.
    /// </summary>
    private bool CanResolvePendingRelics(RelicInventoryState state, IPlayerRuntimeResolver runtimeResolver)
    {
        if (state?.slots == null)
            return true;

        for (int i = 0; i < state.slots.Length; i++)
        {
            var slot = state.slots[i];
            if (slot == null || string.IsNullOrEmpty(slot.relicId))
                continue;

            if (runtimeResolver.ResolveRelic(slot.relicId) != null)
                continue;

            Debug.LogWarning($"[PlayerSceneRestoreBootstrapper] 유물 복원을 보류합니다. 아직 해석할 수 없는 relicId={slot.relicId}, slot={i}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 책임 : 저장된 consumable 슬롯 ID가 모두 현재 아이템 데이터베이스에 존재하는지 검증한다.
    /// </summary>
    private bool CanResolvePendingConsumables(ConsumableInventoryState state, IPlayerRuntimeResolver runtimeResolver)
    {
        if (state?.slots == null)
            return true;

        for (int i = 0; i < state.slots.Length; i++)
        {
            var slot = state.slots[i];
            if (slot == null || string.IsNullOrEmpty(slot.consumableId))
                continue;

            if (runtimeResolver.ResolveConsumable(slot.consumableId) != null)
                continue;

            Debug.LogWarning($"[PlayerSceneRestoreBootstrapper] consumable 복원을 보류합니다. 아직 해석할 수 없는 consumableId={slot.consumableId}, slot={i}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 책임 : 현재 씬에서 복원 대상 플레이어를 찾는다.
    /// 우선순위는 PlayerRuntimeRegistry → Player 태그 순서다.
    /// </summary>
    private GameObject FindPlayer()
    {
        if (PlayerRuntimeRegistry.CurrentPlayer != null)
            return PlayerRuntimeRegistry.CurrentPlayer.gameObject;

        var taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
            return taggedPlayer;

        return null;
    }
}

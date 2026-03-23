using System.Collections;
using UnityEngine;
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
    private bool hasRestored;

    private void Awake()
    {
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
    private void OnPlayerRegistered(SampleTopDownPlayer player)
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
        var playerWeaponRestorer = player.GetComponent<WeaponAbilityRuntimeStateBridge>();
        if (playerWeaponRestorer != null)
            weaponRuntimeRestorer = playerWeaponRestorer;

        var playerRelicRestorer = player.GetComponent<MonoBehaviour>() as IRelicRuntimeStateRestorer;
        if (playerRelicRestorer != null)
            relicRuntimeRestorer = playerRelicRestorer;
        return TryRestorePendingState(player);
    }

    /// <summary>
    /// 책임 : 지정된 플레이어 GameObject에 pending 상태를 복원한다.
    /// 복원에 필요한 resolver / runtime restorer를 새 플레이어 기준으로 다시 바인딩한 뒤
    /// PlayerRuntimeRestoreCoordinator에 전달한다.
    /// </summary>
    public bool TryRestorePendingState(GameObject player)
    {
        if (hasRestored)
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

        resolver = player.GetComponent<PlayerRuntimeResolverBridge>();
        if (resolver == null)
        {
            Debug.LogError("[PlayerSceneRestoreBootstrapper] Resolver가 없어 PlayerRuntimeState를 복원할 수 없습니다.", this);
            return false;
        }

        // 책임 : 씬마다 새로 생성된 플레이어 기준으로 runtime restorer를 다시 잡는다.
        var playerWeaponRestorer = player.GetComponent<WeaponAbilityRuntimeStateBridge>();
        if (playerWeaponRestorer != null)
            weaponRuntimeRestorer = playerWeaponRestorer;

        // 유물도 같은 방식으로 플레이어 기준 restorer를 다시 바인딩한다.
        var playerRelicRestorer = player.GetComponent<MonoBehaviour>() as IRelicRuntimeStateRestorer;
        if (playerRelicRestorer != null)
            relicRuntimeRestorer = playerRelicRestorer;

        var weaponInventory = player.GetComponent<WeaponInventory2D>();
        var relicInventory = player.GetComponent<RelicInventory>();
        var attributeSet = player.GetComponent<AttributeSet>();
        var effectRunner = player.GetComponent<GameplayEffectRunner>();
        var tagSystem = player.GetComponent<TagSystem>();
        var abilitySystem = player.GetComponent<AbilitySystem>();

        PlayerRuntimeRestoreCoordinator.RestoreAll(
            pendingState,
            weaponInventory,
            relicInventory,
            attributeSet,
            effectRunner,
            tagSystem,
            abilitySystem,
            resolver,
            weaponRuntimeRestorer,
            relicRuntimeRestorer,
            this);

        gameplay.ConsumePendingPlayerState();
        hasRestored = true;

        if (restoreRoutine != null)
        {
            StopCoroutine(restoreRoutine);
            restoreRoutine = null;
        }

        Debug.Log("[PlayerSceneRestoreBootstrapper] PlayerRuntimeState 복원 완료.", this);
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
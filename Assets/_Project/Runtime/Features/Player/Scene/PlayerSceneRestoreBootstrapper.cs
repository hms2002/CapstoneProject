using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 梨낆엫 : ??吏꾩엯 ???앹꽦???뚮젅?댁뼱瑜?媛먯???pending PlayerRuntimeState瑜??뺥솗??1??蹂듭썝?쒕떎.
/// PlayerSpawner? 吏곸젒 寃고빀?섏? ?딄퀬, ?덉??ㅽ듃由??대깽?몄? ?ъ떆?꾨? ?듯빐 蹂듭썝 ??대컢???≪닔?쒕떎.
/// </summary>
[DisallowMultipleComponent]
// 책임: 씬 진입 후 pending 플레이어 런타임 상태를 찾아 현재 플레이어에 한 번 복원한다.
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
            Debug.LogError("[PlayerSceneRestoreBootstrapper] resolverSource媛 IPlayerRuntimeResolver瑜?援ы쁽?섏? ?딆븯?듬땲??", this);
        }

        if (weaponRuntimeRestorerSource != null && weaponRuntimeRestorer == null)
        {
            Debug.LogError("[PlayerSceneRestoreBootstrapper] weaponRuntimeRestorerSource媛 IWeaponRuntimeStateRestorer瑜?援ы쁽?섏? ?딆븯?듬땲??", this);
        }

        if (relicRuntimeRestorerSource != null && relicRuntimeRestorer == null)
        {
            Debug.LogError("[PlayerSceneRestoreBootstrapper] relicRuntimeRestorerSource媛 IRelicRuntimeStateRestorer瑜?援ы쁽?섏? ?딆븯?듬땲??", this);
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

        // ?대? ?깅줉???뚮젅?댁뼱媛 ?덉쑝硫?利됱떆 ?쒕룄
        if (!TryRestorePendingState())
        {
            // ?꾩쭅 ?뚮젅?댁뼱媛 ?녾굅????대컢???좊ℓ??寃쎌슦 ?ъ떆??猷⑦떞 ?쒖옉
            restoreRoutine = StartCoroutine(RestoreWhenReadyRoutine());
        }
    }

    /// <summary>
    /// 梨낆엫 : PlayerSpawner媛 ???뚮젅?댁뼱瑜??깅줉?덉쓣 ??利됱떆 蹂듭썝???쒕룄?쒕떎.
    /// ?대깽?멸? 癒쇱? ?ㅻ뜑?쇰룄 hasRestored濡?以묐났 蹂듭썝??留됰뒗??
    /// </summary>
    private void OnPlayerRegistered(PlayerInteractor2D player)
    {
        if (player == null || hasRestored)
            return;

        TryRestorePendingState(player.gameObject);
    }

    /// <summary>
    /// 梨낆엫 : ??吏꾩엯 吏곹썑 ?쒖꽌媛 遺덉븞?뺥븳 寃쎌슦瑜??鍮꾪빐 ?쇱젙 ?쒓컙 ?숈븞 蹂듭썝???ъ떆?꾪븳??
    /// PlayerSpawner Start ??대컢, 吏???ㅽ룿, 珥덇린???쒖꽌 李⑥씠瑜??≪닔?섎뒗 ?덉쟾?μ튂??
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

        if (!hasRestored && RunSessionStore.PeekPendingPlayerState() != null)
        {
            Debug.LogWarning("[PlayerSceneRestoreBootstrapper] ?쒗븳 ?쒓컙 ?댁뿉 PlayerRuntimeState 蹂듭썝???꾨즺?섏? 紐삵뻽?듬땲??", this);
        }

        restoreRoutine = null;
    }

    /// <summary>
    /// 梨낆엫 : ?꾩옱 ?ъ쓽 ?뚮젅?댁뼱瑜??먮룞 ?먯깋??pending ?곹깭 蹂듭썝???쒕룄?쒕떎.
    /// </summary>
    public bool TryRestorePendingState()
    {
        var player = PlayerSceneRestorePlanner.FindPlayer();
        if (player == null)
            return false;

        RebindRuntimeRestorers(player);
        return TryRestorePendingState(player);
    }

    /// <summary>
    /// 梨낆엫 : 吏?뺣맂 ?뚮젅?댁뼱 GameObject??pending ?곹깭瑜?蹂듭썝?쒕떎.
    /// 蹂듭썝???꾩슂??resolver / runtime restorer瑜????뚮젅?댁뼱 湲곗??쇰줈 ?ㅼ떆 諛붿씤?⑺븳 ??
    /// PlayerRuntimeRestoreCoordinator???꾨떖?쒕떎.
    /// </summary>
    public bool TryRestorePendingState(GameObject player)
    {
        if (hasRestored || isRestoreConfirmationPending)
            return false;

        if (!RunSessionStore.IsAvailable)
        {
            Debug.LogWarning("[PlayerSceneRestoreBootstrapper] RunSessionStore backend is missing.", this);
            return false;
        }

        var pendingState = RunSessionStore.PeekPendingPlayerState();
        if (pendingState == null)
            return false;

        if (player == null)
            return false;

        var restoreRequest = new PlayerRuntimeRestoreRequest(player, pendingState);

        if (!PlayerSceneRestorePlanner.IsRestoreAllowedForCurrentScene())
            return false;

        if (!PlayerSceneRestorePlanner.IsItemRestoreReady())
            return false;

        resolver = player.GetComponent<PlayerRuntimeResolverBridge>();

        if (resolver == null)
        {
            Debug.LogError("[PlayerSceneRestoreBootstrapper] Resolver媛 ?놁뼱 PlayerRuntimeState瑜?蹂듭썝?????놁뒿?덈떎.", this);
            return false;
        }

        // 梨낆엫 : ?뚮젅?댁뼱 而댄룷?뚰듃 ?쇨큵 ?섏쭛
        var restoreResult = PlayerSceneRestoreExecutionService.CreateResult(restoreRequest, resolver, this);
        if (!restoreResult.Succeeded)
            return false;

        // 梨낆엫 : ?щ쭏???덈줈 ?앹꽦???뚮젅?댁뼱 湲곗??쇰줈 runtime restorer瑜??ㅼ떆 ?〓뒗??
        RebindRuntimeRestorers(restoreResult.Player);

        // 梨낆엫 : ?좊Ъ???꾩슜 釉뚮━吏瑜??듯빐 ?뚮젅?댁뼱 湲곗? restorer瑜??ㅼ떆 諛붿씤?⑺븳??

        PlayerRuntimeRestoreCoordinator.RestoreAll(
            restoreResult.PendingState,
            restoreResult.Context,
            restoreResult.Resolver,
            weaponRuntimeRestorer,
            relicRuntimeRestorer,
            this);

        isRestoreConfirmationPending = true;
        restoreConfirmRoutine = StartCoroutine(ConfirmRestoreNextFrame(
            restoreResult.PendingState,
            restoreResult.Player));
        return true;
    }

    private void RebindRuntimeRestorers(GameObject player)
    {
        if (player == null)
            return;

        var playerWeaponRestorer = player.GetComponent<WeaponAbilityRuntimeStateBridge>();
        if (playerWeaponRestorer != null)
            weaponRuntimeRestorer = playerWeaponRestorer;

        var playerRelicRestorer = player.GetComponent<RelicRuntimeStateBridge>();
        if (playerRelicRestorer != null)
            relicRuntimeRestorer = playerRelicRestorer;
    }

    /// <summary>
    /// 梨낆엫 : 蹂듭썝 吏곹썑 ???꾨젅?꾩쓣 ?섍릿 ???ㅼ젣 ?λ퉬 ?щ’ ?곹깭媛 ??λ낯怨??쇱튂?섎뒗吏 寃利앺븳??
    /// Start/OnEnable 珥덇린?붽? ?ㅻ뒭寃?蹂듭썝 寃곌낵瑜???뒗 寃쎌슦瑜??먯??섍퀬, ?ㅽ뙣 ??pending state瑜??뚮퉬?섏? ?딅뒗??
    /// </summary>
    private IEnumerator ConfirmRestoreNextFrame(
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

        bool confirmed = PlayerSceneRestoreConfirmationService.TryConfirm(
            pendingState,
            player,
            this);

        if (!confirmed)
        {
            isRestoreConfirmationPending = false;
            yield break;
        }

        hasRestored = true;
        isRestoreConfirmationPending = false;

        ApplyPendingHubLoadFullHealAfterRestore(player);

        if (restoreRoutine != null)
        {
            StopCoroutine(restoreRoutine);
            restoreRoutine = null;
        }

        Debug.Log("[PlayerSceneRestoreBootstrapper] PlayerRuntimeState 蹂듭썝 ?꾨즺.", this);
    }

    /// <summary>
    /// 책임 : 저장/씬 복원이 끝난 뒤 타이틀 프로필 Hub 진입 회복 요청을 소비해 복원값이 회복값을 덮어쓰지 않게 한다.
    /// </summary>
    private static void ApplyPendingHubLoadFullHealAfterRestore(GameObject player)
    {
        if (player == null)
            return;

        if (!SceneDomainNamePolicy.IsHubSceneName(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name))
            return;

        if (!RunSessionStore.ConsumePendingHubLoadFullHeal())
            return;

        PlayerHealthRestoreUtility.FillLinkedHealthToMax(player, player);
    }
}

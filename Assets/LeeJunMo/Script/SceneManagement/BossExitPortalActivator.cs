using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossExitPortalActivator : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private BossBattleEndPrefabCatalogSO prefabCatalog;

    [Header("Portal")]
    [SerializeField] private GameObject portalObj;

    [Header("Behavior")]
    [SerializeField] private bool hidePortalOnStart = true;
    [SerializeField] private bool detachPortalFromBoss = true;

    private BossControllerBase owner;
    private bool hasActivated;

    private void Awake()
    {
        owner = GetComponentInParent<BossControllerBase>();
    }

    private void Start()
    {
        if (hidePortalOnStart)
            HideResolvedPortal();
    }

    private void OnEnable()
    {
        RunProgressCoordinator coordinator = RunProgressCoordinator.EnsureInstance();
        if (coordinator != null)
            coordinator.BossRewardsReady += HandleBossRewardsReady;
    }

    private void OnDisable()
    {
        if (RunProgressCoordinator.Instance != null)
            RunProgressCoordinator.Instance.BossRewardsReady -= HandleBossRewardsReady;
    }

    private void HandleBossRewardsReady(BossRewardContext context)
    {
        if (!CanHandle(context))
            return;

        BossBattleEndAnchors anchors = BossBattleEndAnchors.Resolve(context, this);
        Transform resolvedSpawnPoint = anchors != null ? anchors.PortalSpawnPoint : null;

        bool activated = portalObj != null
            ? ActivatePortal(portalObj, resolvedSpawnPoint, detachPortalFromBoss, ResolveOwnerTransform(context), this)
            : InstantiatePortal(prefabCatalog != null ? prefabCatalog.PortalPrefab : null, resolvedSpawnPoint, this);

        if (!activated)
            return;

        hasActivated = true;
        context.MarkPortalHandled();
    }

    public static void RestorePortalVisibilityAndInteraction(GameObject portalRoot)
    {
        if (portalRoot == null)
            return;

        Renderer[] renderers = portalRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = true;
        }

        Collider2D[] colliders = portalRoot.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = true;
        }

        ScenePortal[] scenePortals = portalRoot.GetComponentsInChildren<ScenePortal>(true);
        for (int i = 0; i < scenePortals.Length; i++)
        {
            if (scenePortals[i] != null)
                scenePortals[i].enabled = true;
        }
    }

    public GameObject ResolvePortalObject()
    {
        if (portalObj != null)
            return portalObj;

        return null;
    }

    private bool CanHandle(BossRewardContext context)
    {
        if (hasActivated || context == null || context.PortalHandled)
            return false;

        if (owner != null && context.Boss != null && !ReferenceEquals(owner, context.Boss))
            return false;

        return true;
    }

    private Transform ResolveOwnerTransform(BossRewardContext context)
    {
        if (context != null && context.Boss != null)
            return context.Boss.transform;

        return transform;
    }

    private void HideResolvedPortal()
    {
        GameObject resolvedPortal = ResolvePortalObject();
        if (resolvedPortal != null)
            resolvedPortal.SetActive(false);
    }

    private static bool ActivatePortal(
        GameObject resolvedPortal,
        Transform spawnPoint,
        bool detachFromOwner,
        Transform ownerTransform,
        Object logContext)
    {
        if (resolvedPortal == null)
        {
            Debug.LogWarning("[BossExitPortalActivator] Portal object is not assigned.", logContext);
            return false;
        }

        Transform portalTransform = resolvedPortal.transform;
        if (portalTransform != null && detachFromOwner && ownerTransform != null && portalTransform.IsChildOf(ownerTransform))
            portalTransform.SetParent(null, true);

        if (portalTransform != null && spawnPoint != null)
            portalTransform.position = spawnPoint.position;

        resolvedPortal.SetActive(true);
        RestorePortalVisibilityAndInteraction(resolvedPortal);
        return true;
    }

    private static bool InstantiatePortal(
        GameObject portalPrefab,
        Transform spawnPoint,
        Object logContext)
    {
        if (portalPrefab == null)
        {
            Debug.LogWarning("[BossExitPortalActivator] Portal object is not assigned.", logContext);
            return false;
        }

        if (spawnPoint == null)
        {
            Debug.LogWarning("[BossExitPortalActivator] Portal spawn anchor is not assigned.", logContext);
            return false;
        }

        GameObject portalInstance = Object.Instantiate(portalPrefab, spawnPoint.position, Quaternion.identity);
        portalInstance.SetActive(true);
        RestorePortalVisibilityAndInteraction(portalInstance);
        return true;
    }
}

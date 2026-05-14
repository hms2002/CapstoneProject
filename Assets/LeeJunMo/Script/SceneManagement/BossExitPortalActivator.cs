using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossExitPortalActivator : MonoBehaviour
{
    [Header("Portal")]
    [SerializeField] private GameObject portalObj;
    [SerializeField] private Transform portalSpawnPoint;
    [SerializeField] private Vector3 portalSpawnOffset;

    [Header("Behavior")]
    [SerializeField] private bool hidePortalOnStart = true;
    [SerializeField] private bool detachPortalFromBoss = true;
    [SerializeField] private bool useBossDropReferencesIfMissing = true;

    private BossControllerBase owner;
    private BossDrop legacyDrop;
    private bool hasActivated;

    private void Awake()
    {
        owner = GetComponentInParent<BossControllerBase>();
        legacyDrop = GetComponentInParent<BossDrop>();
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

        BossDrop referenceDrop = useBossDropReferencesIfMissing ? ResolveLegacyDrop(context) : null;
        GameObject resolvedPortal = portalObj != null ? portalObj : referenceDrop != null ? referenceDrop.portalObj : null;
        Transform resolvedSpawnPoint = portalSpawnPoint != null ? portalSpawnPoint : referenceDrop != null ? referenceDrop.portalSpawnPoint : null;
        Vector3 resolvedOffset = portalSpawnOffset != Vector3.zero
            ? portalSpawnOffset
            : referenceDrop != null ? referenceDrop.portalSpawnOffset : Vector3.zero;
        Vector3 spawnPosition = ResolvePortalSpawnPosition(context, resolvedSpawnPoint, referenceDrop, resolvedOffset);

        if (!ActivatePortal(resolvedPortal, spawnPosition, detachPortalFromBoss, ResolveOwnerTransform(context, referenceDrop), this))
            return;

        hasActivated = true;
        context.MarkPortalHandled();
    }

    public static bool ActivateFromLegacyDrop(BossDrop legacyDrop, BossRewardContext context)
    {
        if (legacyDrop == null)
            return false;

        Vector3 spawnPosition = ResolveLegacyPortalSpawnPosition(legacyDrop);
        Transform ownerTransform = context != null && context.Boss != null ? context.Boss.transform : legacyDrop.transform;
        return ActivatePortal(legacyDrop.portalObj, spawnPosition, true, ownerTransform, legacyDrop);
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

        return useBossDropReferencesIfMissing && legacyDrop != null ? legacyDrop.portalObj : null;
    }

    private bool CanHandle(BossRewardContext context)
    {
        if (hasActivated || context == null || context.PortalHandled)
            return false;

        if (owner != null && context.Boss != null && !ReferenceEquals(owner, context.Boss))
            return false;

        if (owner != null && context.Boss == null && legacyDrop != null && context.LegacyBossDrop != legacyDrop)
            return false;

        return true;
    }

    private BossDrop ResolveLegacyDrop(BossRewardContext context)
    {
        if (legacyDrop != null)
            return legacyDrop;

        if (context != null && context.LegacyBossDrop != null)
            return context.LegacyBossDrop;

        return context != null && context.Boss != null ? context.Boss.GetComponent<BossDrop>() : null;
    }

    private Transform ResolveOwnerTransform(BossRewardContext context, BossDrop referenceDrop)
    {
        if (context != null && context.Boss != null)
            return context.Boss.transform;

        if (referenceDrop != null)
            return referenceDrop.transform;

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
        Vector3 spawnPosition,
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

        if (portalTransform != null)
            portalTransform.position = spawnPosition;

        resolvedPortal.SetActive(true);
        RestorePortalVisibilityAndInteraction(resolvedPortal);
        return true;
    }

    private static Vector3 ResolvePortalSpawnPosition(
        BossRewardContext context,
        Transform resolvedSpawnPoint,
        BossDrop referenceDrop,
        Vector3 resolvedOffset)
    {
        if (resolvedSpawnPoint != null)
            return resolvedSpawnPoint.position + resolvedOffset;

        if (referenceDrop != null && referenceDrop.chestSpawnPoint != null)
            return referenceDrop.chestSpawnPoint.position + resolvedOffset;

        if (context != null && context.Boss != null)
            return context.Boss.transform.position + resolvedOffset;

        if (referenceDrop != null)
            return referenceDrop.transform.position + resolvedOffset;

        return resolvedOffset;
    }

    private static Vector3 ResolveLegacyPortalSpawnPosition(BossDrop legacyDrop)
    {
        if (legacyDrop == null)
            return Vector3.zero;

        if (legacyDrop.portalSpawnPoint != null)
            return legacyDrop.portalSpawnPoint.position + legacyDrop.portalSpawnOffset;

        if (legacyDrop.chestSpawnPoint != null)
            return legacyDrop.chestSpawnPoint.position + legacyDrop.portalSpawnOffset;

        return legacyDrop.transform.position + legacyDrop.portalSpawnOffset;
    }
}

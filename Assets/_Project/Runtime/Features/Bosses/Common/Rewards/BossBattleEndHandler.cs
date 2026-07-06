using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Capstone/Boss/Boss Battle End Handler")]
// 책임: 보스 전투 종료 시 보상, 진행도, 탈출 포탈 처리를 한 번만 실행한다.
public sealed class BossBattleEndHandler : MonoBehaviour
{
    [Header("Boss")]
    [SerializeField] private BossControllerBase boss;

    [Header("Authored Results")]
    [SerializeField] private TreasureChest treasureChest;
    [SerializeField] private GameObject exitPortal;
    [SerializeField] private bool hideAuthoredObjectsOnStart = true;

    private bool hasHandledRewards;
    private bool hasHandledPortal;

    private void Start()
    {
        if (!hideAuthoredObjectsOnStart)
            return;

        if (treasureChest != null)
            treasureChest.gameObject.SetActive(false);

        if (exitPortal != null)
            exitPortal.SetActive(false);
    }

    private void OnEnable()
    {
        RunProgressPlayback.BossRewardsReady += HandleBossRewardsReady;
    }

    private void OnDisable()
    {
        RunProgressPlayback.BossRewardsReady -= HandleBossRewardsReady;
    }

    private void HandleBossRewardsReady(BossRewardContext context)
    {
        if (!CanHandle(context))
            return;

        if (!context.RewardsHandled)
            HandleRewards(context);

        if (!context.PortalHandled)
            HandlePortal(context);
    }

    private bool CanHandle(BossRewardContext context)
    {
        if (context == null)
            return false;

        if (boss == null)
        {
            Debug.LogWarning("[BossBattleEndHandler] Boss is not assigned.", this);
            return false;
        }

        if (context.Boss != null && !ReferenceEquals(boss, context.Boss))
            return false;

        return true;
    }

    private void HandleRewards(BossRewardContext context)
    {
        if (hasHandledRewards)
            return;

        if (context.IsFinalRouteSet)
        {
            if (!BossRewardSpawnService.SpawnPhysicalDrops(context, boss.transform.position, this))
                return;

            hasHandledRewards = true;
            context.MarkRewardsHandled();
            return;
        }

        if (treasureChest == null)
        {
            Debug.LogWarning("[BossBattleEndHandler] TreasureChest is not assigned.", this);
            return;
        }

        bool activated = BossRewardSpawnService.ActivateTreasureChest(new BossRewardActivationRequest(
            context,
            context.SpecialRewardPreset,
            treasureChest,
            boss.transform.position,
            this));

        if (!activated)
            return;

        treasureChest.PlayRewardReveal();
        hasHandledRewards = true;
        context.MarkRewardsHandled();
    }

    private void HandlePortal(BossRewardContext context)
    {
        if (hasHandledPortal)
            return;

        if (exitPortal == null)
        {
            Debug.LogWarning("[BossBattleEndHandler] Exit portal is not assigned.", this);
            return;
        }

        exitPortal.SetActive(true);
        RestorePortalVisibilityAndInteraction(exitPortal);
        PlayPortalRevealPresentation(exitPortal);
        hasHandledPortal = true;
        context.MarkPortalHandled();
    }

    private static void PlayPortalRevealPresentation(GameObject root)
    {
        if (root == null)
            return;

        BossRewardObjectRevealPresentation presentation =
            root.GetComponent<BossRewardObjectRevealPresentation>();
        if (presentation == null)
            presentation = root.GetComponentInChildren<BossRewardObjectRevealPresentation>(true);

        presentation?.PlayReveal();
    }

    private static void RestorePortalVisibilityAndInteraction(GameObject portalRoot)
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
}

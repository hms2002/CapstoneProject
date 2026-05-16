using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossBattleEndAnchors : MonoBehaviour
{
    [Header("Reward")]
    [SerializeField] private Transform rewardSpawnPoint;
    [SerializeField] private Transform scatterOrigin;

    [Header("Portal")]
    [SerializeField] private Transform portalSpawnPoint;

    public Transform RewardSpawnPoint => rewardSpawnPoint;
    public Transform ScatterOrigin => scatterOrigin;
    public Transform PortalSpawnPoint => portalSpawnPoint;

    public static BossBattleEndAnchors Resolve(BossRewardContext context, Component localContext)
    {
        BossBattleEndAnchors anchors = localContext != null
            ? localContext.GetComponentInParent<BossBattleEndAnchors>()
            : null;
        if (anchors != null)
            return anchors;

        if (context != null && context.Boss != null)
        {
            anchors = context.Boss.GetComponentInParent<BossBattleEndAnchors>();
            if (anchors != null)
                return anchors;
        }

        return null;
    }
}

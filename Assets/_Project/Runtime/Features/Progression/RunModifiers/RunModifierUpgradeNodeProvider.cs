using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal readonly struct RunModifierUpgradeNodeLoadRequest
{
    public UpgradeNodeSO[] CachedNodes { get; }
    public UpgradeManager UpgradeManager { get; }
    public string ResourcePath { get; }

    public RunModifierUpgradeNodeLoadRequest(
        UpgradeNodeSO[] cachedNodes,
        UpgradeManager upgradeManager,
        string resourcePath)
    {
        CachedNodes = cachedNodes;
        UpgradeManager = upgradeManager;
        ResourcePath = resourcePath;
    }
}

internal readonly struct RunModifierUpgradeNodeLoadResult
{
    private readonly UpgradeNodeSO[] nodes;

    public UpgradeNodeSO[] Nodes => nodes ?? new UpgradeNodeSO[0];

    public RunModifierUpgradeNodeLoadResult(UpgradeNodeSO[] nodes)
    {
        this.nodes = nodes ?? new UpgradeNodeSO[0];
    }
}

internal static class RunModifierUpgradeNodeProvider
{
    public static RunModifierUpgradeNodeLoadResult Load(RunModifierUpgradeNodeLoadRequest request)
    {
        if (request.CachedNodes != null && request.CachedNodes.Length > 0)
            return new RunModifierUpgradeNodeLoadResult(request.CachedNodes);

        var mergedNodes = new Dictionary<int, UpgradeNodeSO>();
        AddUpgradeManagerNodes(mergedNodes, request.UpgradeManager);
        AddResourceNodes(mergedNodes, request.ResourcePath);

        return new RunModifierUpgradeNodeLoadResult(mergedNodes.Values.ToArray());
    }

    private static void AddUpgradeManagerNodes(Dictionary<int, UpgradeNodeSO> mergedNodes, UpgradeManager upgradeManager)
    {
        if (mergedNodes == null || upgradeManager == null)
            return;

        List<UpgradeNodeSO> upgrades = upgradeManager.GetAllUpgrades();
        if (upgrades == null || upgrades.Count == 0)
            return;

        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeNodeSO node = upgrades[i];
            if (node == null)
                continue;

            mergedNodes[node.nodeID] = node;
        }
    }

    private static void AddResourceNodes(Dictionary<int, UpgradeNodeSO> mergedNodes, string resourcePath)
    {
        if (mergedNodes == null)
            return;

        UpgradeNodeSO[] resourceNodes = Resources.LoadAll<UpgradeNodeSO>(resourcePath);
        if (resourceNodes == null)
            return;

        for (int i = 0; i < resourceNodes.Length; i++)
        {
            UpgradeNodeSO node = resourceNodes[i];
            if (node == null || mergedNodes.ContainsKey(node.nodeID))
                continue;

            mergedNodes.Add(node.nodeID, node);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

public enum LockType { Locked, UnLocked, Purchased }

/// <summary>
/// 책임 : 강화 노드의 비용, 효과, 선행/후행 관계, UI 배치 데이터를 보관한다.
/// </summary>
[CreateAssetMenu(fileName = "NewUpgradeNode", menuName = "Game/Upgrade Node")]
public class UpgradeNodeSO : ScriptableObject
{
    public const float DefaultGridCellWidth = 160f;
    public const float DefaultGridCellHeight = 140f;

    [Header("Basic Info")]
    public int nodeID;
    public string upgradeName;
    [TextArea] public string description;
    public Sprite icon;
    public int price;

    [Header("Effects")]
    public List<UpgradeEffectSO> effects = new List<UpgradeEffectSO>();

    [Header("Graph Links")]
    public List<int> unlockedNodeIDs = new List<int>();
    public List<int> requiredParentIDs = new List<int>();

    [Header("Editor Links")]
    public List<UpgradeNodeSO> nextNodes = new List<UpgradeNodeSO>();
    public List<UpgradeNodeSO> requiredParents = new List<UpgradeNodeSO>();

    [Header("UI Layout")]
    public int gridX = 0;
    public int gridY = 0;

    public Vector2 GetUiPosition()
    {
        return GetUiPosition(new Vector2(DefaultGridCellWidth, DefaultGridCellHeight));
    }

    public Vector2 GetUiPosition(Vector2 gridCellSize)
    {
        float x = gridX * gridCellSize.x;
        float y = gridY * gridCellSize.y;
        return new Vector2(x, y);
    }

    public void ApplyOnPurchase(PlayerInteractor2D player)
    {
        if (effects == null)
            return;

        foreach (UpgradeEffectSO effect in effects)
        {
            if (effect != null)
                effect.ApplyOnPurchase(player);
        }
    }

    public void ReapplyPlayerEffects(PlayerInteractor2D player)
    {
        if (effects == null)
            return;

        foreach (UpgradeEffectSO effect in effects)
        {
            if (effect != null)
                effect.ReapplyForPlayer(player);
        }
    }

    public void ApplyEffect(PlayerInteractor2D player)
    {
        ApplyOnPurchase(player);
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        int currentHash = Animator.StringToHash(name);
        if (nodeID != currentHash)
        {
            nodeID = currentHash;
            EditorAuthoringPlayback.MarkDirty(this);
        }

        SyncList(nextNodes, ref unlockedNodeIDs);
        SyncList(requiredParents, ref requiredParentIDs);
#endif
    }

#if UNITY_EDITOR
    private void SyncList(List<UpgradeNodeSO> source, ref List<int> targetIDs)
    {
        if (source == null)
            return;

        targetIDs ??= new List<int>();
        targetIDs.Clear();

        foreach (UpgradeNodeSO node in source)
        {
            if (node == null)
                continue;

            if (node.nodeID == 0)
                node.nodeID = Animator.StringToHash(node.name);

            if (!targetIDs.Contains(node.nodeID))
                targetIDs.Add(node.nodeID);
        }
    }
#endif
}

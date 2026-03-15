using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum LockType { Locked, UnLocked, Purchased }

[CreateAssetMenu(fileName = "NewUpgradeNode", menuName = "Game/Upgrade Node")]
public class UpgradeNodeSO : ScriptableObject
{
    // ... (기존 필드 동일) ...
    [Header("기본 정보")]
    public int nodeID;
    public string upgradeName;
    [TextArea] public string description;
    public Sprite icon;
    public int price;

    [Header("기능")]
    public List<UpgradeEffectSO> effects = new List<UpgradeEffectSO>();

    [Header("연결 정보")]
    public List<int> unlockedNodeIDs = new List<int>();
    public List<int> requiredParentIDs = new List<int>();

    [Header("에디터용")]
    public List<UpgradeNodeSO> nextNodes = new List<UpgradeNodeSO>();
    public List<UpgradeNodeSO> requiredParents = new List<UpgradeNodeSO>();

    [Header("UI 배치")]
    [Min(0)] public int gridX = 0;
    [Range(-1, 1)] public int gridY = 0;

    // [수정] 시작 위치를 150 -> 50으로 줄여서 화면 왼쪽에 더 붙게 함
    public Vector2 GetUiPosition()
    {
        float startX = 100f; // 시작 여백
        float x = startX + (gridX * 120f);
        float y = gridY * 90f;
        return new Vector2(x, y);
    }

    // ... (ApplyEffect, OnValidate, SyncList 등 기존 로직 동일) ...
    public void ApplyEffect(SampleTopDownPlayer player)
    {
        if (effects == null) return;
        foreach (var effect in effects) if (effect != null) effect.ApplyEffect(player);
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        int currentHash = Animator.StringToHash(this.name);
        if (nodeID != currentHash)
        {
            nodeID = currentHash;
            EditorUtility.SetDirty(this);
        }
        SyncList(nextNodes, ref unlockedNodeIDs);
        SyncList(requiredParents, ref requiredParentIDs);
#endif
    }

#if UNITY_EDITOR
    private void SyncList(List<UpgradeNodeSO> source, ref List<int> targetIDs)
    {
        if (source == null) return;
        if (targetIDs == null) targetIDs = new List<int>();
        targetIDs.Clear();
        foreach (var node in source)
        {
            if (node != null)
            {
                if (node.nodeID == 0) node.nodeID = Animator.StringToHash(node.name);
                if (!targetIDs.Contains(node.nodeID)) targetIDs.Add(node.nodeID);
            }
        }
    }
#endif
}
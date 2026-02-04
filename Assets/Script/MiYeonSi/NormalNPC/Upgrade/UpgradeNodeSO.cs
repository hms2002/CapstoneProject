using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum LockType
{
    Locked,    // 잠김
    UnLocked,  // 해금됨 (구매 가능)
    Purchased  // 이미 구매함
}

[CreateAssetMenu(fileName = "NewUpgradeNode", menuName = "Game/Upgrade Node")]
public class UpgradeNodeSO : ScriptableObject
{
    [Header("기본 정보")]
    public int nodeID;
    public string upgradeName;
    [TextArea] public string description;
    public Sprite icon;
    public int price;

    [Header("기능 (Effects)")]
    // 이 노드가 가진 효과들의 리스트 (컴포지션 패턴)
    public List<UpgradeEffectSO> effects = new List<UpgradeEffectSO>();

    [Header("연결 정보 (시스템)")]
    public List<int> unlockedNodeIDs = new List<int>();
    public List<int> requiredParentIDs = new List<int>();

    [Header("에디터 연결용 (Tool 전용)")]
    public List<UpgradeNodeSO> nextNodes = new List<UpgradeNodeSO>();
    public List<UpgradeNodeSO> requiredParents = new List<UpgradeNodeSO>();

    [Header("UI 배치 정보")]
    [Range(-1, 1)] public int gridX = 0;
    [Min(0)] public int gridY = 0;

    public Vector2 GetUiPosition()
    {
        float startX = 0f;
        float startY = -540f;
        float posX = startX + (gridX * 100f);
        float posY = startY + (gridY * 120f);
        return new Vector2(posX, posY);
    }

    // [수정] TempPlayer -> SampleTopDownPlayer
    public void ApplyEffect(SampleTopDownPlayer player)
    {
        if (effects == null) return;

        foreach (var effect in effects)
        {
            if (effect != null)
            {
                effect.ApplyEffect(player);
            }
        }
    }

    // 데이터 무결성 유지 (ID 생성 및 리스트 동기화)
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
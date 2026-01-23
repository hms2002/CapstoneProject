using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum LockType
{
    Locked,    // 잠김 (구매 불가)
    UnLocked,  // 해금됨 (구매 가능)
    Purchased  // 이미 구매함
}

public abstract class UpgradeNodeSO : ScriptableObject
{
    [Header("기본 정보")]
    // ID가 0이면 OnValidate에서 파일 이름 해시값으로 자동 생성
    public int nodeID;
    public string upgradeName;
    [TextArea] public string description;
    public Sprite icon;
    public int price;

    [Header("연결 정보")]
    // 이 노드를 구매하면 해금되는 자식 노드 ID들
    public List<int> unlockedNodeIDs = new List<int>();

    // 이 노드를 해금하기 위해 필요한 부모 노드 ID들 (AND 조건)
    public List<int> requiredParentIDs = new List<int>();

    [Header("에디터 연결용 (Tool 전용)")]
    public List<UpgradeNodeSO> nextNodes = new List<UpgradeNodeSO>();
    public List<UpgradeNodeSO> requiredParents = new List<UpgradeNodeSO>();

    [Header("UI 배치 정보 (Grid System)")]
    // X축: -1(좌), 0(중), 1(우)
    [Range(-1, 1)] public int gridX = 0;
    // Y축: 0층, 1층, 2층... (위로 쌓임)
    [Min(0)] public int gridY = 0;

    // [핵심] 규격에 따른 실제 UI 좌표 계산
    public Vector2 GetUiPosition()
    {
        // 기준점: (0, -540) -> 화면 중앙 하단
        float startX = 0f;
        float startY = -540f;

        // X축 간격: 100, Y축 간격: 120
        float posX = startX + (gridX * 100f);
        float posY = startY + (gridY * 120f);

        return new Vector2(posX, posY);
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        // 1. ID 자동 생성
        if (nodeID == 0)
        {
            nodeID = Animator.StringToHash(this.name);
            EditorUtility.SetDirty(this);
        }

        // 2. 리스트 동기화 (SO <-> ID)
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

    public virtual void ApplyEffect(TempPlayer player) { }
}
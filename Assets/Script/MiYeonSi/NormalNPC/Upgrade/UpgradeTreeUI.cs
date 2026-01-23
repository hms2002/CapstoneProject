using System.Collections.Generic;
using UnityEngine;

public class UpgradeTreeUI : MonoBehaviour
{
    [Header("프리팹 및 부모 연결")]
    public Transform slotParent;    // 슬롯들이 들어갈 부모
    public GameObject slotPrefab;   // UpgradeSlotUI 프리팹

    public Transform lineParent;    // 선들이 들어갈 부모 (슬롯보다 뒤에 배치)
    public GameObject linePrefab;   // 선 프리팹 (UI Image)

    // 생성된 객체 관리 리스트
    private List<UpgradeSlotUI> allSlots = new List<UpgradeSlotUI>();
    private List<GameObject> allLines = new List<GameObject>();

    private void Start()
    {
        // 게임 시작 시 트리를 자동으로 그립니다.
        BuildUI();
    }

    private void OnEnable()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnDataChanged += RefreshAll;
    }

    private void OnDisable()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnDataChanged -= RefreshAll;
    }

    public void BuildUI()
    {
        // 1. 기존 UI 클리어
        foreach (Transform child in slotParent) Destroy(child.gameObject);
        foreach (Transform child in lineParent) Destroy(child.gameObject);
        allSlots.Clear();
        allLines.Clear();

        Dictionary<int, UpgradeSlotUI> slotDict = new Dictionary<int, UpgradeSlotUI>();
        var allUpgrades = UpgradeManager.Instance.GetAllUpgrades(); // DB에 있는 모든 노드 가져오기

        // 2. 슬롯 생성 (SO에 저장된 좌표 사용)
        foreach (var node in allUpgrades)
        {
            if (node == null) continue;

            GameObject slotObj = Instantiate(slotPrefab, slotParent);
            UpgradeSlotUI slotUI = slotObj.GetComponent<UpgradeSlotUI>();
            RectTransform rect = slotObj.GetComponent<RectTransform>();

            // [핵심] 툴에서 설정한 Grid 좌표를 실제 UI 좌표로 변환하여 배치
            rect.anchoredPosition = node.GetUiPosition();

            // 슬롯 데이터 주입 및 초기화
            slotUI.assignedNode = node;
            slotUI.InitSlot(OnBuyButtonPressed);

            allSlots.Add(slotUI);
            slotDict[node.nodeID] = slotUI;
        }

        // 3. 선 그리기 (Elbow 스타일)
        foreach (var node in allUpgrades)
        {
            if (node == null) continue;
            if (!slotDict.ContainsKey(node.nodeID)) continue;

            var fromSlot = slotDict[node.nodeID];

            // 자식 노드들을 향해 선을 그림
            foreach (var nextId in node.unlockedNodeIDs)
            {
                if (slotDict.TryGetValue(nextId, out var targetSlot))
                {
                    DrawLine(fromSlot.GetComponent<RectTransform>(), targetSlot.GetComponent<RectTransform>());
                }
            }
        }
    }

    // 선 그리기 로직 (직각 꺾기)
    private void DrawLine(RectTransform startRect, RectTransform endRect)
    {
        Vector2 start = startRect.anchoredPosition;
        Vector2 end = endRect.anchoredPosition;

        float elbowOffset = 50f; // 꺾이는 지점의 여유 공간
        float direction = Mathf.Sign(end.y - start.y);

        // Y축 높이가 거의 같으면 직선으로 그림
        if (Mathf.Abs(end.y - start.y) < 10f)
        {
            CreateLineSegment(start, end);
            return;
        }

        // 'ㄷ' 혹은 'ㄹ' 자 형태로 꺾기
        Vector2 elbow1 = start + new Vector2(0, elbowOffset * direction);
        Vector2 elbow2 = new Vector2(end.x, elbow1.y);

        CreateLineSegment(start, new Vector2(start.x, elbow1.y)); // 세로 출발
        CreateLineSegment(elbow1, elbow2); // 가로 이동
        CreateLineSegment(elbow2, end); // 세로 도착
    }

    private void CreateLineSegment(Vector2 start, Vector2 end)
    {
        var line = Instantiate(linePrefab, lineParent);
        var rect = line.GetComponent<RectTransform>();

        Vector2 dir = end - start;
        float dist = dir.magnitude;

        // 선이 너무 짧으면 생성 안 함
        if (dist < 1f)
        {
            Destroy(line);
            return;
        }

        rect.sizeDelta = new Vector2(dist, 4f); // 선 두께: 4
        rect.anchoredPosition = start + (dir / 2);

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rect.rotation = Quaternion.Euler(0, 0, angle);

        allLines.Add(line);
    }

    private void OnBuyButtonPressed(UpgradeNodeSO node)
    {
        UpgradeManager.Instance.TryBuyUpgrade(node.nodeID);
    }

    public void RefreshAll()
    {
        foreach (var slot in allSlots)
        {
            if (slot != null && slot.gameObject.activeInHierarchy)
                slot.RefreshUI();
        }
    }
}
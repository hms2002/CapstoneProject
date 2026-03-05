using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeTreeUI : MonoBehaviour
{
    [Header("UI 연결")]
    // Scroll View의 Content (Panel)
    public RectTransform contentRect;
    public Transform slotParent;    // Panel 아래 Slots (빈 오브젝트)
    public Transform lineParent;    // Panel 아래 Lines (빈 오브젝트)

    [Header("프리팹")]
    public GameObject slotPrefab;
    public GameObject linePrefab;

    private List<UpgradeSlotUI> allSlots = new List<UpgradeSlotUI>();
    private List<GameObject> allLines = new List<GameObject>();

    private void Start()
    {
        // 1. Content (Panel) 설정 강제 초기화
        // 왼쪽부터 시작하도록 Pivot과 Anchor를 (0, 0.5)로 맞춤
        if (contentRect != null)
        {
            contentRect.pivot = new Vector2(0, 0.5f);
            contentRect.anchorMin = new Vector2(0, 0.5f);
            contentRect.anchorMax = new Vector2(0, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
        }

        BuildUI();
    }

    private void OnEnable() { if (UpgradeManager.Instance != null) UpgradeManager.Instance.OnDataChanged += RefreshAll; }
    private void OnDisable() { if (UpgradeManager.Instance != null) UpgradeManager.Instance.OnDataChanged -= RefreshAll; }

    public void BuildUI()
    {
        // 기존 UI 삭제
        foreach (Transform child in slotParent) Destroy(child.gameObject);
        foreach (Transform child in lineParent) Destroy(child.gameObject);
        allSlots.Clear();
        allLines.Clear();

        Dictionary<int, UpgradeSlotUI> slotDict = new Dictionary<int, UpgradeSlotUI>();
        var allUpgrades = UpgradeManager.Instance.GetAllUpgrades();

        float maxX = 0f;

        // 2. 노드 생성
        foreach (var node in allUpgrades)
        {
            if (node == null) continue;

            GameObject slotObj = Instantiate(slotPrefab, slotParent);
            UpgradeSlotUI slotUI = slotObj.GetComponent<UpgradeSlotUI>();
            RectTransform rect = slotObj.GetComponent<RectTransform>();

            // [핵심 수정] 슬롯의 Anchor와 Pivot을 'Middle Left'로 강제 설정
            // 부모(Content)도 Middle Left이므로 좌표계가 완벽히 일치하게 됨
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f); // 슬롯 자체의 중심점은 중앙으로 유지

            // NodeSO 데이터 좌표 사용 (왼쪽 기준 100, 300 등...)
            Vector2 pos = node.GetUiPosition();
            rect.anchoredPosition = pos;

            if (pos.x > maxX) maxX = pos.x;

            slotUI.assignedNode = node;
            slotUI.InitSlot((n) => UpgradeManager.Instance.TryBuyUpgrade(n.nodeID));

            allSlots.Add(slotUI);
            slotDict[node.nodeID] = slotUI;
        }

        // 3. Content 크기 확장
        if (contentRect != null)
        {
            // 마지막 노드 위치 + 여백(300f)만큼 Width를 늘려줌
            float newWidth = Mathf.Max(maxX + 300f, 1000f);
            contentRect.sizeDelta = new Vector2(newWidth, contentRect.sizeDelta.y);
        }

        // 4. 라인 그리기
        foreach (var node in allUpgrades)
        {
            if (node == null || !slotDict.ContainsKey(node.nodeID)) continue;

            foreach (var nextId in node.unlockedNodeIDs)
            {
                if (slotDict.TryGetValue(nextId, out var targetSlot))
                {
                    DrawLine(slotDict[node.nodeID].GetComponent<RectTransform>(),
                             targetSlot.GetComponent<RectTransform>());
                }
            }
        }
    }

    private void DrawLine(RectTransform start, RectTransform end)
    {
        Vector2 s = start.anchoredPosition;
        Vector2 e = end.anchoredPosition;

        if (Vector2.Distance(s, e) < 1f) return;

        float midX = (s.x + e.x) / 2f;
        Vector2 p1 = new Vector2(midX, s.y);
        Vector2 p2 = new Vector2(midX, e.y);

        CreateLineSegment(s, p1);
        CreateLineSegment(p1, p2);
        CreateLineSegment(p2, e);
    }

    private void CreateLineSegment(Vector2 start, Vector2 end)
    {
        if (Vector2.Distance(start, end) < 0.1f) return;

        var line = Instantiate(linePrefab, lineParent);
        var rect = line.GetComponent<RectTransform>();

        // [핵심 수정] 라인도 Anchor를 Middle Left로 맞춤
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0, 0.5f); // 회전 기준점을 왼쪽 끝으로

        Vector2 dir = end - start;
        float dist = dir.magnitude;

        rect.sizeDelta = new Vector2(dist, 4f); // 두께 4
        rect.anchoredPosition = start; // 시작점에 배치하고 회전

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rect.rotation = Quaternion.Euler(0, 0, angle);

        allLines.Add(line);
    }

    public void OnClickClose() => UpgradeManager.Instance.CloseUI();
    public void RefreshAll() { foreach (var s in allSlots) if (s != null) s.RefreshUI(); }
}
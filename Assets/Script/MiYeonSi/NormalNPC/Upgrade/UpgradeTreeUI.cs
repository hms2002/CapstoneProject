using System.Collections.Generic;
using UnityEngine;

public class UpgradeTreeUI : MonoBehaviour
{
    [Header("프리팹 및 부모 연결")]
    public RectTransform slotParent;    // Pivot: (0.5, 0) 패널
    public GameObject slotPrefab;
    public Transform lineParent;
    public GameObject linePrefab;

    private List<UpgradeSlotUI> allSlots = new List<UpgradeSlotUI>();
    private List<GameObject> allLines = new List<GameObject>();

    private void Start() => BuildUI();

    private void OnEnable() { if (UpgradeManager.Instance != null) UpgradeManager.Instance.OnDataChanged += RefreshAll; }
    private void OnDisable() { if (UpgradeManager.Instance != null) UpgradeManager.Instance.OnDataChanged -= RefreshAll; }

    public void BuildUI()
    {
        foreach (Transform child in slotParent) Destroy(child.gameObject);
        foreach (Transform child in lineParent) Destroy(child.gameObject);
        allSlots.Clear(); allLines.Clear();

        Dictionary<int, UpgradeSlotUI> slotDict = new Dictionary<int, UpgradeSlotUI>();
        var allUpgrades = UpgradeManager.Instance.GetAllUpgrades();

        float maxX = 0; float maxY = 0;

        foreach (var node in allUpgrades)
        {
            if (node == null) continue;
            GameObject slotObj = Instantiate(slotPrefab, slotParent);
            UpgradeSlotUI slotUI = slotObj.GetComponent<UpgradeSlotUI>();
            RectTransform rect = slotObj.GetComponent<RectTransform>();

            Vector2 uiPos = node.GetUiPosition();
            rect.anchoredPosition = uiPos;

            // 최대 범위 계산
            if (Mathf.Abs(uiPos.x) > maxX) maxX = Mathf.Abs(uiPos.x);
            if (uiPos.y > maxY) maxY = uiPos.y;

            slotUI.assignedNode = node;
            slotUI.InitSlot((n) => UpgradeManager.Instance.TryBuyUpgrade(n.nodeID));
            allSlots.Add(slotUI);
            slotDict[node.nodeID] = slotUI;
        }

        // [핵심] 패널 크기 업데이트
        UpdateContentSize(maxX, maxY);

        // 선 그리기
        foreach (var node in allUpgrades)
        {
            if (node == null || !slotDict.ContainsKey(node.nodeID)) continue;
            foreach (var nextId in node.unlockedNodeIDs)
            {
                if (slotDict.TryGetValue(nextId, out var targetSlot))
                    DrawLine(slotDict[node.nodeID].GetComponent<RectTransform>(), targetSlot.GetComponent<RectTransform>());
            }
        }
    }

    private void UpdateContentSize(float maxX, float maxY)
    {
        float padding = 200f;
        // Y축 시작 지점(-540)을 고려한 높이 설정
        float newHeight = maxY + padding + 540f;
        slotParent.sizeDelta = new Vector2(slotParent.sizeDelta.x, newHeight);
    }

    // [New] 닫기 버튼용 함수
    public void OnClickClose() => UpgradeManager.Instance.CloseUI();

    private void DrawLine(RectTransform start, RectTransform end)
    {
        Vector2 s = start.anchoredPosition; Vector2 e = end.anchoredPosition;
        float dir = Mathf.Sign(e.y - s.y);
        if (Mathf.Abs(e.y - s.y) < 10f) { CreateLineSegment(s, e); return; }
        Vector2 eb1 = s + new Vector2(0, 50f * dir);
        Vector2 eb2 = new Vector2(e.x, eb1.y);
        CreateLineSegment(s, new Vector2(s.x, eb1.y));
        CreateLineSegment(eb1, eb2);
        CreateLineSegment(eb2, e);
    }

    private void CreateLineSegment(Vector2 start, Vector2 end)
    {
        var line = Instantiate(linePrefab, lineParent);
        var rect = line.GetComponent<RectTransform>();
        Vector2 dir = end - start;
        rect.sizeDelta = new Vector2(dir.magnitude, 4f);
        rect.anchoredPosition = start + (dir / 2);
        rect.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        allLines.Add(line);
    }

    public void RefreshAll() { foreach (var s in allSlots) if (s != null) s.RefreshUI(); }
}
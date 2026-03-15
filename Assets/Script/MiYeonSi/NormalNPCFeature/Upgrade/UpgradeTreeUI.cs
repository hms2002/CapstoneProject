using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// [핵심] UIManager의 통제를 받기 위해 IStackableUI를 상속받습니다!
public class UpgradeTreeUI : MonoBehaviour, IStackableUI
{
    [Header("UI 연결")]
    public RectTransform contentRect;
    public Transform slotParent;
    public Transform lineParent;

    [Header("프리팹")]
    public GameObject slotPrefab;
    public GameObject linePrefab;

    private List<UpgradeSlotUI> allSlots = new List<UpgradeSlotUI>();
    private List<GameObject> allLines = new List<GameObject>();

    // =========================================================
    // IStackableUI 규약 (UIManager가 호출함)
    // =========================================================
    public bool IsActive => gameObject.activeSelf;
    public bool CanCloseOnEscape => true; // ESC 키로 닫기 허용

    public void OpenUI()
    {
        gameObject.SetActive(true);
        RefreshAll(); // 열릴 때 최신 데이터로 슬롯 상태 갱신
    }

    public void CloseUI()
    {
        gameObject.SetActive(false);

        // [핵심] UI가 닫히면 매니저에게 알려서(진동벨) 대화 시스템 등을 재개시킵니다.
        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.OnUIClosed?.Invoke();
        }
    }
    // =========================================================

    private void Start()
    {
        // 1. Content (Panel) 설정 강제 초기화
        if (contentRect != null)
        {
            contentRect.pivot = new Vector2(0, 0.5f);
            contentRect.anchorMin = new Vector2(0, 0.5f);
            contentRect.anchorMax = new Vector2(0, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
        }

        BuildUI();

        // 씬 시작 시에는 기본적으로 꺼져 있도록 설정
        gameObject.SetActive(false);
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

            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            // NodeSO 데이터 좌표 사용
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

        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0, 0.5f);

        Vector2 dir = end - start;
        float dist = dir.magnitude;

        rect.sizeDelta = new Vector2(dist, 4f);
        rect.anchoredPosition = start;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rect.rotation = Quaternion.Euler(0, 0, angle);

        allLines.Add(line);
    }

    // [수정] UI 닫기 버튼(X)을 눌렀을 때의 동작
    public void OnClickClose()
    {
        // 직접 끄지 않고 사령탑(UIManager)에게 닫아달라고(Pop) 요청합니다!
        if (UIManager.Instance != null) UIManager.Instance.PopUI(this);
        else CloseUI();
    }

    public void RefreshAll() { foreach (var s in allSlots) if (s != null) s.RefreshUI(); }
}
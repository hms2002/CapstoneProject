using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeTreeUI : MonoBehaviour, IStackableUI, IMouseCursorDomainSource
{
    public static UpgradeTreeUI EnsureInstance()
    {
        UpgradeTreeUI[] existing = Resources.FindObjectsOfTypeAll<UpgradeTreeUI>();
        for (int i = 0; i < existing.Length; i++)
        {
            UpgradeTreeUI candidate = existing[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;

            return candidate;
        }

        return null;
    }

    [Header("UI References")]
    public RectTransform contentRect;
    public Transform slotParent;
    public Transform lineParent;

    [Header("Prefabs")]
    public GameObject slotPrefab;
    public GameObject linePrefab;

    private readonly List<UpgradeSlotUI> allSlots = new List<UpgradeSlotUI>();
    private readonly List<GameObject> allLines = new List<GameObject>();

    public bool IsActive => gameObject.activeSelf;
    public bool CanCloseOnEscape => true;
    public UIOpenGroup OpenGroup => UIOpenGroup.ExclusiveModal;
    public UIOpenGroup BlockedOpenGroups => UIOpenGroup.ExclusiveModal;
    public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.FreezeAndBlockControl;
    public MouseCursorDomain CursorDomain => MouseCursorDomain.NpcUi;

    public void OpenUI()
    {
        gameObject.SetActive(true);
        RefreshAll();
    }

    public void CloseUI()
    {
        gameObject.SetActive(false);

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUIClosed?.Invoke();
    }

    private void Start()
    {
        if (contentRect != null)
        {
            contentRect.pivot = new Vector2(0, 0.5f);
            contentRect.anchorMin = new Vector2(0, 0.5f);
            contentRect.anchorMax = new Vector2(0, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
        }

        BuildUI();
    }

    private void OnEnable()
    {
        MouseCursorService.EnsureInstance().SetDomain(this, MouseCursorDomain.NpcUi, priority: 100);

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnDataChanged += RefreshAll;

        if (UIManager.Instance != null)
            UIManager.Instance.SetGameplayHudCurrencyHidden(this, true);
    }

    private void OnDisable()
    {
        MouseCursorService.Instance?.ClearDomain(this);

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnDataChanged -= RefreshAll;

        if (UIManager.Instance != null)
            UIManager.Instance.SetGameplayHudCurrencyHidden(this, false);
    }

    public void BuildUI()
    {
        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        foreach (Transform child in lineParent)
            Destroy(child.gameObject);

        allSlots.Clear();
        allLines.Clear();

        Dictionary<int, UpgradeSlotUI> slotDict = new Dictionary<int, UpgradeSlotUI>();
        List<UpgradeNodeSO> allUpgrades = UpgradeManager.Instance.GetAllUpgrades();
        if (allUpgrades == null)
            return;

        float maxX = 0f;

        foreach (var node in allUpgrades)
        {
            if (node == null)
                continue;

            GameObject slotObj = Instantiate(slotPrefab, slotParent);
            UpgradeSlotUI slotUI = slotObj.GetComponent<UpgradeSlotUI>();
            RectTransform rect = slotObj.GetComponent<RectTransform>();

            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Vector2 pos = node.GetUiPosition();
            rect.anchoredPosition = pos;
            if (pos.x > maxX)
                maxX = pos.x;

            slotUI.assignedNode = node;
            slotUI.InitSlot(n => UpgradeManager.Instance.TryBuyUpgrade(n.nodeID));

            allSlots.Add(slotUI);
            slotDict[node.nodeID] = slotUI;
        }

        if (contentRect != null)
        {
            float newWidth = Mathf.Max(maxX + 300f, 1000f);
            contentRect.sizeDelta = new Vector2(newWidth, contentRect.sizeDelta.y);
        }

        foreach (var node in allUpgrades)
        {
            if (node == null || !slotDict.ContainsKey(node.nodeID))
                continue;

            foreach (var nextId in node.unlockedNodeIDs)
            {
                if (!slotDict.TryGetValue(nextId, out var targetSlot))
                    continue;

                DrawLine(
                    slotDict[node.nodeID].GetComponent<RectTransform>(),
                    targetSlot.GetComponent<RectTransform>());
            }
        }
    }

    private void DrawLine(RectTransform start, RectTransform end)
    {
        Vector2 s = start.anchoredPosition;
        Vector2 e = end.anchoredPosition;

        if (Vector2.Distance(s, e) < 1f)
            return;

        float midX = (s.x + e.x) / 2f;
        Vector2 p1 = new Vector2(midX, s.y);
        Vector2 p2 = new Vector2(midX, e.y);

        CreateLineSegment(s, p1);
        CreateLineSegment(p1, p2);
        CreateLineSegment(p2, e);
    }

    private void CreateLineSegment(Vector2 start, Vector2 end)
    {
        if (Vector2.Distance(start, end) < 0.1f)
            return;

        GameObject line = Instantiate(linePrefab, lineParent);
        RectTransform rect = line.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0, 0.5f);

        Vector2 dir = end - start;
        float dist = dir.magnitude;

        rect.sizeDelta = new Vector2(dist, 4f);
        rect.anchoredPosition = start;
        rect.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        allLines.Add(line);
    }

    public void OnClickClose()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.PopUI(this);
        else
            CloseUI();
    }

    public void RefreshAll()
    {
        foreach (var slot in allSlots)
        {
            if (slot != null)
                slot.RefreshUI();
        }
    }
}

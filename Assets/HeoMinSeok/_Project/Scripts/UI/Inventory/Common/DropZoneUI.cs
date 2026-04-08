using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 책임 : 인벤토리 drag 중에만 활성화되는 월드 드롭 전용 UI 타겟을 제공한다.
/// </summary>
public class DropZoneUI : MonoBehaviour, IDropHandler
{
    public static DropZoneUI ActiveInstance { get; private set; }

    [Header("World Drop")]
    [SerializeField] private WorldItemPickup2D worldDropPrefab;
    [SerializeField] private Transform dropOrigin;
    [SerializeField] private float scatterRadius = 0.25f;
    [Header("Presentation")]
    [SerializeField] private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Hide();
    }

    private void OnEnable()
    {
        ActiveInstance = this;
        Hide();
    }

    private void OnDisable()
    {
        if (ActiveInstance == this)
            ActiveInstance = null;
    }

    public void SetDropOrigin(Transform origin) => dropOrigin = origin;

    public void Show()
    {
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!ItemDragContext.Active) return;

        // loot 슬롯을 다시 월드로 드랍 금지
        if (ItemDragContext.Source is WorldLootContainerAdapter)
        {
            DragIcon.Instance?.Hide();
            ItemDragContext.Clear();
            return;
        }

        var item = ItemDragContext.Item;
        var src = ItemDragContext.Source;
        int srcIndex = ItemDragContext.SourceIndex;

        // ✅ (중요) 제거 전에 레벨 확보
        int relicLevel = ItemDragContext.RelicLevel;
        if (relicLevel <= 0 && item is RelicDefinition && src is IRelicLevelProvider p)
            p.TryGetRelicLevel(srcIndex, out relicLevel);

        // Remove from source
        bool removed = src != null && src.TrySet(srcIndex, null);
        if (removed)
            SpawnWorldItem(item, relicLevel);

        DragIcon.Instance?.Hide();
        ItemDragContext.Clear();
        Hide();
    }

    private void SetVisible(bool visible)
    {
        EnsureCanvasGroup();
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void SpawnWorldItem(ScriptableObject item, int relicLevel)
    {
        if (item == null) return;
        if (worldDropPrefab == null) return;

        Vector3 pos = dropOrigin != null ? dropOrigin.position : Vector3.zero;
        if (scatterRadius > 0f)
        {
            var r = Random.insideUnitCircle * scatterRadius;
            pos += new Vector3(r.x, r.y, 0f);
        }

        var drop = Instantiate(worldDropPrefab, pos, Quaternion.identity);
        drop.SetItem(item, relicLevel);
    }
}

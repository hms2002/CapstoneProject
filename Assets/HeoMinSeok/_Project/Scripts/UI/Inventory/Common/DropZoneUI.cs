using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UI drop target: when the user drops a dragged item onto this zone,
/// the item is removed from its source container and spawned into the world.
/// </summary>
public class DropZoneUI : MonoBehaviour, IDropHandler
{
    [Header("World Drop")]
    [SerializeField] private WorldItemPickup2D worldDropPrefab;
    [SerializeField] private Transform dropOrigin;
    [SerializeField] private float scatterRadius = 0.25f;

    public void SetDropOrigin(Transform origin) => dropOrigin = origin;

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

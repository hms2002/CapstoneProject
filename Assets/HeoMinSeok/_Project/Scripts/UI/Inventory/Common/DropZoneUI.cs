using CapstoneAudio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 책임 : 인벤토리 drag 중에만 활성화되는 월드 드롭 전용 UI 타겟을 제공한다.
/// </summary>
public class DropZoneUI : MonoBehaviour, IDropHandler
{
    private static readonly SoundRef DropItemSound = SoundRef.FromKey("sound_ui_DropItem");

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
        if (!ItemDragContext.Active || ItemContainerGroupRegistry.IsInspectionOnly) return;

        TryDropSourceToWorld(
            ItemDragContext.Source,
            ItemDragContext.SourceIndex,
            ItemDragContext.Item,
            ItemDragContext.RelicLevel);

        DragIcon.Instance?.Hide();
        ItemDragContext.Clear();
        Hide();
    }

    /// <summary>
    /// 책임 :
    /// - UI 드래그가 아닌 고정 입력에서도 동일한 월드 드롭 정책을 재사용하게 한다.
    /// - source 슬롯 제거, 유물 레벨 보존, 월드 드롭 스폰을 하나의 안전한 API로 묶는다.
    /// </summary>
    public bool TryDropSourceToWorld(IItemContainer source, int sourceIndex)
    {
        if (ItemContainerGroupRegistry.IsInspectionOnly)
            return false;

        if (source == null || sourceIndex < 0 || sourceIndex >= source.SlotCount)
            return false;

        ScriptableObject item = source.Get(sourceIndex);
        return TryDropSourceToWorld(source, sourceIndex, item, 0);
    }

    private bool TryDropSourceToWorld(
        IItemContainer source,
        int sourceIndex,
        ScriptableObject item,
        int relicLevel)
    {
        if (source == null || item == null)
            return false;

        // loot 슬롯을 다시 월드로 드랍 금지
        if (source is WorldLootContainerAdapter)
            return false;

        if (sourceIndex < 0 || sourceIndex >= source.SlotCount)
            return false;

        // ✅ (중요) 제거 전에 레벨 확보
        if (relicLevel <= 0 && item is RelicDefinition && source is IRelicLevelProvider p)
            p.TryGetRelicLevel(sourceIndex, out relicLevel);

        bool removed = source.TrySet(sourceIndex, null);
        if (!removed)
            return false;

        SpawnWorldItem(item, relicLevel);
        return true;
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

        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        Transform directionSource = playerTransform != null ? playerTransform : dropOrigin;
        Vector3 spawnOrigin = playerTransform != null
            ? playerTransform.position
            : (dropOrigin != null ? dropOrigin.position : Vector3.zero);

        var spawnService = new LootSpawnService(worldDropPrefab.gameObject, null, null);
        var candidatePositions = spawnService.GetForwardGroundPositions(spawnOrigin, directionSource);
        Vector3 landingPosition = candidatePositions.Count > 0
            ? candidatePositions[Random.Range(0, candidatePositions.Count)]
            : spawnService.ResolveForwardGroundPosition(spawnOrigin, directionSource);
        if (landingPosition == spawnOrigin && scatterRadius > 0f)
        {
            var scatter = Random.insideUnitCircle.normalized * scatterRadius;
            landingPosition += new Vector3(scatter.x, scatter.y, 0f);
        }

        spawnService.SpawnAnimatedLootObject(spawnOrigin, landingPosition, item, relicLevel);
        SoundPlaybackUtility.Play(DropItemSound, position: spawnOrigin, sourceObject: this);
    }
}

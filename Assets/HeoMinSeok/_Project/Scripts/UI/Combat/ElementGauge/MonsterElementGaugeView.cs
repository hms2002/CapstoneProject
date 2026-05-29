using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityGAS;

public class MonsterElementGaugeView : MonoBehaviour
{
    // 이 클래스의 책임:
    // 몬스터 속성 게이지 슬롯을 갱신하고 월드 공간에서 타깃 위치를 따라가며 표시 상태를 관리한다.

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private ElementGaugeSystem gaugeSystem;

    [Header("Tracking")]
    [SerializeField] private bool useGaugeAnchor = true;
    [SerializeField] private Vector3 gaugeWorldOffset = new(0f, -1f, 0f);
    [SerializeField] private bool followTarget = true;

    [Header("Sorting")]
    [SerializeField] private bool syncSortingWithTargetRenderer = true;
    [SerializeField] private int sortingOrderOffset;
    [SerializeField] private string fallbackSortingLayerName = "Entity";
    [SerializeField] private int fallbackSortingOrder;

    [Header("UI")]
    [SerializeField] private Transform slotRoot;
    [SerializeField] private ElementGaugeSlotView slotPrefab;
    [SerializeField] private float slotSpacing = 320f;
    [SerializeField] private bool centerSlots = true;

    [Header("Mode")]
    [SerializeField] private bool destroyWhenTargetMissing = true;
    [SerializeField] private bool hideWhenTargetMissing = false;

    private readonly List<ElementGaugeUiModel> models = new();
    private readonly List<ElementGaugeSlotView> slots = new();

    private IMonsterGaugeVisibilityFilter visibilityFilter;
    private Canvas gaugeCanvas;
    private SortingGroup targetSortingGroup;
    private SpriteRenderer targetSortingRenderer;
    private bool dirty = true;

    public void Bind(Transform targetTransform, ElementGaugeSystem targetGaugeSystem)
    {
        Unsubscribe();

        target = targetTransform;
        gaugeSystem = targetGaugeSystem;
        visibilityFilter = ResolveVisibilityFilter(targetTransform);
        CacheSortingReferences();
        dirty = true;

        Subscribe();
        RefreshImmediate();
    }

    private void OnEnable()
    {
        Subscribe();
        dirty = true;
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void LateUpdate()
    {
        if (target == null || gaugeSystem == null)
        {
            HandleMissingTarget();
            return;
        }

        if (followTarget)
            UpdatePosition();

        UpdateSorting();
        UpdateVisibilityState();

        if (!dirty)
            return;

        dirty = false;
        RefreshSlots();
    }

    public void RefreshImmediate()
    {
        if (target == null || gaugeSystem == null)
        {
            HandleMissingTarget();
            return;
        }

        if (followTarget)
            UpdatePosition();

        UpdateSorting();
        UpdateVisibilityState();
        dirty = false;
        RefreshSlots();
    }

    private void RefreshSlots()
    {
        int count = gaugeSystem.GetGaugeUiModels(models, true);
        EnsureSlotCount(count);

        for (int i = 0; i < count; i++)
        {
            slots[i].SetData(models[i]);
        }

        ArrangeVisibleSlots(count);

        for (int i = count; i < slots.Count; i++)
        {
            slots[i].Hide();
        }

        bool anyVisible = count > 0 && IsVisibilityAllowed();
        if (slotRoot != null)
            slotRoot.gameObject.SetActive(anyVisible);
    }

    private void EnsureSlotCount(int required)
    {
        if (slotPrefab == null || slotRoot == null)
            return;

        while (slots.Count < required)
        {
            var slot = Instantiate(slotPrefab, slotRoot);
            slot.Hide();
            slots.Add(slot);
        }
    }

    private void ArrangeVisibleSlots(int count)
    {
        float startX = centerSlots ? -slotSpacing * (count - 1) * 0.5f : 0f;

        for (int i = 0; i < count; i++)
        {
            RectTransform rect = slots[i] != null ? slots[i].transform as RectTransform : null;
            if (rect == null)
                continue;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(startX + slotSpacing * i, 0f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }
    }

    private void UpdatePosition()
    {
        transform.position = ResolveGaugeAnchorPosition();
    }

    private void UpdateSorting()
    {
        if (!syncSortingWithTargetRenderer)
            return;

        if (gaugeCanvas == null)
            gaugeCanvas = GetComponent<Canvas>();

        if (gaugeCanvas == null)
            return;

        gaugeCanvas.overrideSorting = true;

        if (targetSortingGroup != null)
        {
            gaugeCanvas.sortingLayerID = targetSortingGroup.sortingLayerID;
            gaugeCanvas.sortingOrder = targetSortingGroup.sortingOrder + sortingOrderOffset;
            return;
        }

        if (targetSortingRenderer != null)
        {
            gaugeCanvas.sortingLayerID = targetSortingRenderer.sortingLayerID;
            gaugeCanvas.sortingOrder = targetSortingRenderer.sortingOrder + sortingOrderOffset;
            return;
        }

        gaugeCanvas.sortingLayerID = SortingLayer.NameToID(fallbackSortingLayerName);
        gaugeCanvas.sortingOrder = fallbackSortingOrder + sortingOrderOffset;
    }

    private Vector3 ResolveGaugeAnchorPosition()
    {
        if (useGaugeAnchor
            && target != null
            && target.TryGetComponent(out MonsterElementGaugeAnchor2D anchor))
        {
            return anchor.Resolve();
        }

        return target != null ? target.position + gaugeWorldOffset : transform.position;
    }

    private void HandleGaugeChanged(GameplayTag elementTag, float oldValue, float newValue)
    {
        dirty = true;
    }

    private void UpdateVisibilityState()
    {
        if (slotRoot == null)
            return;

        bool shouldShow = IsVisibilityAllowed();
        if (!shouldShow && slotRoot.gameObject.activeSelf)
            slotRoot.gameObject.SetActive(false);
    }

    private void Subscribe()
    {
        if (gaugeSystem != null)
            gaugeSystem.OnGaugeChanged += HandleGaugeChanged;
    }

    private void Unsubscribe()
    {
        if (gaugeSystem != null)
            gaugeSystem.OnGaugeChanged -= HandleGaugeChanged;
    }

    private void HandleMissingTarget()
    {
        if (destroyWhenTargetMissing)
        {
            Destroy(gameObject);
            return;
        }

        if (hideWhenTargetMissing)
            gameObject.SetActive(false);
    }

    private bool IsVisibilityAllowed()
    {
        return visibilityFilter == null || visibilityFilter.ShouldShowGauge();
    }

    private static IMonsterGaugeVisibilityFilter ResolveVisibilityFilter(Transform targetTransform)
    {
        if (targetTransform == null)
            return null;

        MonoBehaviour[] behaviours = targetTransform.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IMonsterGaugeVisibilityFilter filter)
                return filter;
        }

        return null;
    }

    private void CacheSortingReferences()
    {
        gaugeCanvas = GetComponent<Canvas>();
        targetSortingGroup = target != null ? target.GetComponentInChildren<SortingGroup>() : null;
        targetSortingRenderer = ResolveSortingRenderer(target);
    }

    private static SpriteRenderer ResolveSortingRenderer(Transform targetTransform)
    {
        if (targetTransform == null)
            return null;

        Transform visual = targetTransform.Find("Visual");
        if (visual != null && visual.TryGetComponent(out SpriteRenderer visualRenderer))
            return visualRenderer;

        SpriteRenderer[] renderers = targetTransform.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer fallback = null;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (fallback == null)
                fallback = renderer;

            string objectName = renderer.gameObject.name;
            if (objectName.Contains("Shadow") || objectName.Contains("shadow"))
                continue;

            return renderer;
        }

        return fallback;
    }
}

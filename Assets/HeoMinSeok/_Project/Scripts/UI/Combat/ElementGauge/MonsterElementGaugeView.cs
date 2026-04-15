using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public class MonsterElementGaugeView : MonoBehaviour
{
    // 이 클래스의 책임:
    // 몬스터 속성 게이지 슬롯을 갱신하고 월드 공간에서 타깃 위치를 따라가며 표시 상태를 관리한다.

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private ElementGaugeSystem gaugeSystem;

    [Header("Tracking")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, -1.0f, 0f);
    [SerializeField] private bool followTarget = true;

    [Header("UI")]
    [SerializeField] private Transform slotRoot;
    [SerializeField] private ElementGaugeSlotView slotPrefab;

    [Header("Mode")]
    [SerializeField] private bool destroyWhenTargetMissing = false;
    [SerializeField] private bool hideWhenTargetMissing = true;

    private readonly List<ElementGaugeUiModel> models = new();
    private readonly List<ElementGaugeSlotView> slots = new();

    private IMonsterGaugeVisibilityFilter visibilityFilter;
    private bool dirty = true;

    public void Bind(Transform targetTransform, ElementGaugeSystem targetGaugeSystem)
    {
        Unsubscribe();

        target = targetTransform;
        gaugeSystem = targetGaugeSystem;
        visibilityFilter = ResolveVisibilityFilter(targetTransform);
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

    private void UpdatePosition()
    {
        transform.position = target.position + worldOffset;
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
}

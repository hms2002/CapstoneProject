using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 : 도감 엔트리 슬롯 풀을 구성하고 현재 페이지의 항목 목록을 채운다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EncyclopediaEntryGridView : MonoBehaviour
{
    [Header("Slot Pool")]
    [SerializeField] private Transform entryGridRoot;
    [SerializeField] private EncyclopediaEntryButton entrySlotPrefab;
    [SerializeField] private List<EncyclopediaEntryButton> entrySlots = new();
    [SerializeField, Min(1)] private int slotsPerPage = 16;
    [SerializeField] private bool hideTemplateSlot = true;

    private readonly List<EncyclopediaEntryButton> runtimeEntrySlots = new();
    private bool warnedMissingSlotAuthoring;

    public int SlotsPerPage => Mathf.Max(1, slotsPerPage);
    public int NavigationColumnCount => ResolveNavigationColumnCount();
    public bool HasRuntimeSlotAuthoring => entryGridRoot != null && entrySlotPrefab != null;
    public int FallbackSlotCount => entrySlots != null ? entrySlots.Count : 0;
    public bool HasAnySlotAuthoring => HasRuntimeSlotAuthoring || FallbackSlotCount > 0;

    private void Awake()
    {
        ValidateRequiredReferences();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveReferences();
    }

    [ContextMenu("Auto Wire References")]
    private void AutoWireReferences()
    {
        ResolveReferences();
        EditorAuthoringPlayback.MarkDirty(this);
    }
#endif

    public void ResolveReferences()
    {
        if (entryGridRoot == null)
        {
            Transform namedRoot = EncyclopediaReferenceResolver.FindTransform(transform, "EntryGridRoot", "GridRoot", "SlotsRoot");
            entryGridRoot = namedRoot != null ? namedRoot : transform;
        }

        if (entrySlotPrefab == null && entryGridRoot != null)
            entrySlotPrefab = EncyclopediaReferenceResolver.FindComponent<EncyclopediaEntryButton>(entryGridRoot, "EncyclopediaEntrySlot", "EntrySlot", "Slot");

        if ((entrySlots == null || entrySlots.Count == 0) && entrySlotPrefab == null)
            entrySlots = new List<EncyclopediaEntryButton>(GetComponentsInChildren<EncyclopediaEntryButton>(true));
    }

    public int Populate(
        EncyclopediaCatalogSO catalog,
        EncyclopediaCategory category,
        int pageStartIndex,
        int entryCount,
        int selectedIndex,
        Action<EncyclopediaCategory, int> selectedCallback,
        out int visibleCount)
    {
        return Populate(
            category,
            pageStartIndex,
            entryCount,
            selectedIndex,
            (entryCategory, entryIndex) => catalog != null ? catalog.GetDisplayName(entryCategory, entryIndex) : string.Empty,
            (entryCategory, entryIndex) => ResolveEntryIconItem(catalog, entryCategory, entryIndex),
            (entryCategory, entryIndex) => ResolveEntryIconSprite(catalog, entryCategory, entryIndex),
            selectedCallback,
            out visibleCount);
    }

    public int Populate(
        EncyclopediaCategory category,
        int pageStartIndex,
        int entryCount,
        int selectedIndex,
        Func<EncyclopediaCategory, int, string> displayNameResolver,
        Func<EncyclopediaCategory, int, ScriptableObject> iconItemResolver,
        Func<EncyclopediaCategory, int, Sprite> iconSpriteResolver,
        Action<EncyclopediaCategory, int> selectedCallback,
        out int visibleCount)
    {
        bool hasRuntimeSlotAuthoring = HasRuntimeSlotAuthoring;
        if (hasRuntimeSlotAuthoring)
            DeactivateFallbackEntrySlots();
        else
            ValidateRequiredReferences();

        List<EncyclopediaEntryButton> activeSlots = hasRuntimeSlotAuthoring
            ? EnsureRuntimeSlots(SlotsPerPage)
            : ResolveFallbackEntrySlots();
        int availableSlotCount = Mathf.Min(SlotsPerPage, activeSlots.Count);
        visibleCount = Mathf.Clamp(entryCount - pageStartIndex, 0, availableSlotCount);

        for (int i = 0; i < activeSlots.Count; i++)
        {
            EncyclopediaEntryButton slot = activeSlots[i];
            if (slot == null)
                continue;

            bool visible = i < visibleCount;
            slot.gameObject.SetActive(visible);

            if (!visible)
            {
                slot.Clear();
                continue;
            }

            int entryIndex = pageStartIndex + i;
            slot.Configure(
                category,
                entryIndex,
                displayNameResolver != null ? displayNameResolver(category, entryIndex) : string.Empty,
                iconItemResolver != null ? iconItemResolver(category, entryIndex) : null,
                iconSpriteResolver != null ? iconSpriteResolver(category, entryIndex) : null,
                entryIndex == selectedIndex,
                locked: false,
                selectedCallback);
        }

        return availableSlotCount;
    }

    public void RefreshSelection(int selectedIndex)
    {
        List<EncyclopediaEntryButton> activeSlots = GetActiveEntrySlots();
        for (int i = 0; i < activeSlots.Count; i++)
        {
            EncyclopediaEntryButton slot = activeSlots[i];
            if (slot != null)
                slot.SetSelected(slot.EntryIndex == selectedIndex);
        }
    }

    public void Clear()
    {
        List<EncyclopediaEntryButton> activeSlots = GetActiveEntrySlots();
        for (int i = 0; i < activeSlots.Count; i++)
        {
            EncyclopediaEntryButton slot = activeSlots[i];
            if (slot == null)
                continue;

            slot.Clear();
            slot.gameObject.SetActive(false);
        }
    }

    private List<EncyclopediaEntryButton> EnsureRuntimeSlots(int requiredCount)
    {
        if (!HasRuntimeSlotAuthoring)
            return runtimeEntrySlots;

        if (hideTemplateSlot && entrySlotPrefab != null && entryGridRoot != null && entrySlotPrefab.transform.IsChildOf(entryGridRoot))
            entrySlotPrefab.gameObject.SetActive(false);

        for (int i = runtimeEntrySlots.Count; i < requiredCount; i++)
        {
            EncyclopediaEntryButton slot = Instantiate(entrySlotPrefab, entryGridRoot);
            slot.name = $"{entrySlotPrefab.name}_{i:00}";
            runtimeEntrySlots.Add(slot);
        }

        for (int i = 0; i < runtimeEntrySlots.Count; i++)
        {
            EncyclopediaEntryButton slot = runtimeEntrySlots[i];
            if (slot == null)
                continue;

            bool active = i < requiredCount;
            slot.gameObject.SetActive(active);
            if (!active)
                slot.Clear();
        }

        return runtimeEntrySlots;
    }

    private void DeactivateFallbackEntrySlots()
    {
        if (entrySlots == null)
            return;

        for (int i = 0; i < entrySlots.Count; i++)
        {
            EncyclopediaEntryButton slot = entrySlots[i];
            if (slot != null)
                slot.gameObject.SetActive(false);
        }
    }

    private List<EncyclopediaEntryButton> ResolveFallbackEntrySlots()
    {
        if (entrySlots != null && entrySlots.Count > 0)
            return entrySlots;

        return entrySlots ??= new List<EncyclopediaEntryButton>();
    }

    private List<EncyclopediaEntryButton> GetActiveEntrySlots()
    {
        if (runtimeEntrySlots.Count > 0 || HasRuntimeSlotAuthoring)
            return runtimeEntrySlots;

        return ResolveFallbackEntrySlots();
    }

    private int ResolveNavigationColumnCount()
    {
        GridLayoutGroup gridLayout = ResolveGridLayoutGroup();
        if (gridLayout != null)
        {
            int constraintCount = Mathf.Max(1, gridLayout.constraintCount);
            if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                return constraintCount;

            if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount)
                return Mathf.Max(1, Mathf.CeilToInt(SlotsPerPage / (float)constraintCount));
        }

        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(SlotsPerPage)));
    }

    private GridLayoutGroup ResolveGridLayoutGroup()
    {
        if (entryGridRoot != null && entryGridRoot.TryGetComponent(out GridLayoutGroup gridLayout))
            return gridLayout;

        return GetComponent<GridLayoutGroup>();
    }

    private static ScriptableObject ResolveEntryIconItem(EncyclopediaCatalogSO catalog, EncyclopediaCategory category, int index)
    {
        if (category == EncyclopediaCategory.Weapon &&
            catalog != null &&
            catalog.TryGetWeapon(index, out var weaponEntry) &&
            weaponEntry != null)
        {
            return weaponEntry.weapon;
        }

        return null;
    }

    private static Sprite ResolveEntryIconSprite(EncyclopediaCatalogSO catalog, EncyclopediaCategory category, int index)
    {
        return catalog != null ? catalog.GetImage(category, index) : null;
    }

    private void ValidateRequiredReferences()
    {
        if (HasAnySlotAuthoring || warnedMissingSlotAuthoring)
            return;

        warnedMissingSlotAuthoring = true;
        Debug.LogWarning("[EncyclopediaEntryGridView] EntryGridRoot/EntrySlotPrefab are not assigned and no serialized fallback slots exist. The encyclopedia grid cannot display entries.", this);
    }

}

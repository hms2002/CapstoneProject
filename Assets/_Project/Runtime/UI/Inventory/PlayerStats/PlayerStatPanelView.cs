using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 플레이어의 AttributeSet과 최종 Stat 공급원을 읽어 스탯 패널 전체를 구성하고 갱신한다.
/// - 공용 프리팹으로서 인벤토리 UI와 상자 UI 양쪽에서 같은 정의와 렌더링 규칙을 재사용하게 한다.
/// </summary>
public sealed class PlayerStatPanelView : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] private PlayerStatPanelDefinition panelDefinition;

    [Header("Section View")]
    [SerializeField] private Transform sectionRoot;
    [SerializeField] private PlayerStatSectionView sectionPrefab;

    [Header("Optional Binding Override")]
    [SerializeField] private Transform ownerOverride;

    private readonly List<PlayerStatSectionView> spawnedSections = new();

    private Transform boundOwner;
    private AttributeSet attributeSet;
    private AttributeStatSource statSource;
    private bool attributeRefreshPending;
    private IReadOnlyList<InventoryTransferRequest> selectionPreview;
    private Dictionary<AttributeDefinition, AttributeValue> projectedAttributes;
    private RelicInventory relicInventory;
    private WeaponInventory2D weaponInventory;

    private void OnEnable()
    {
        UnbindAttributeEvents();
        BindAttributeEvents();
        if (ownerOverride != null)
            Bind(ownerOverride);
        else
            Refresh();
    }

    private void OnDisable()
    {
        selectionPreview = null;
        projectedAttributes = null;
        attributeRefreshPending = false;
        UnbindAttributeEvents();
    }

    private void LateUpdate()
    {
        if (!attributeRefreshPending)
            return;

        attributeRefreshPending = false;
        Refresh();
    }

    public void Bind(Transform owner)
    {
        if (boundOwner == owner && attributeSet != null)
        {
            UnbindAttributeEvents();
            BindAttributeEvents();
            selectionPreview = null;
            Refresh();
            return;
        }

        UnbindAttributeEvents();

        boundOwner = owner;
        attributeSet = owner != null ? owner.GetComponent<AttributeSet>() : null;
        statSource = owner != null ? owner.GetComponent<AttributeStatSource>() : null;
        relicInventory = owner != null ? owner.GetComponent<RelicInventory>() : null;
        weaponInventory = owner != null ? owner.GetComponent<WeaponInventory2D>() : null;
        selectionPreview = null;
        projectedAttributes = null;

        BindAttributeEvents();
        Rebuild();
    }

    public void Refresh()
    {
        RebuildSelectionPreview();
        if (spawnedSections.Count == 0)
        {
            Rebuild();
            return;
        }

        for (int i = 0; i < spawnedSections.Count; i++)
        {
            if (spawnedSections[i] != null)
                spawnedSections[i].Refresh(ResolveValueText);
        }
    }

    private void Rebuild()
    {
        ClearSections();

        if (panelDefinition == null || sectionRoot == null || sectionPrefab == null || panelDefinition.Sections == null)
            return;

        for (int i = 0; i < panelDefinition.Sections.Length; i++)
        {
            var definition = panelDefinition.Sections[i];
            if (definition == null)
                continue;

            var section = Instantiate(sectionPrefab, sectionRoot);
            section.Build(definition, ResolveValueText);
            spawnedSections.Add(section);
        }
    }

    private void ClearSections()
    {
        for (int i = 0; i < spawnedSections.Count; i++)
        {
            if (spawnedSections[i] != null)
                Destroy(spawnedSections[i].gameObject);
        }

        spawnedSections.Clear();
    }

    private void BindAttributeEvents()
    {
        if (attributeSet != null && isActiveAndEnabled)
            attributeSet.OnAttributeChanged += HandleAttributeChanged;
    }

    private void UnbindAttributeEvents()
    {
        if (attributeSet != null)
            attributeSet.OnAttributeChanged -= HandleAttributeChanged;
    }

    private void HandleAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        // 한 번의 장착에서 여러 Attribute가 연속 변경되어도 스탯 패널 전체는 프레임당 한 번만 갱신한다.
        attributeRefreshPending = true;
    }

    private string ResolveValueText(StatInfoUIDefinition definition)
    {
        if (definition == null)
            return string.Empty;

        switch (definition.ValueMode)
        {
            case PlayerStatValueMode.AttributeCurrent:
            {
                float current = ReadAttribute(definition.ValueAttribute, false);
                return FormatSingleValue(definition, current) + FormatDelta(definition,
                    current, ReadAttribute(definition.ValueAttribute, true));
            }

            case PlayerStatValueMode.AttributeBase:
            {
                float current = attributeSet != null ? attributeSet.GetBaseValue(definition.ValueAttribute) : 0f;
                float projected = projectedAttributes != null && definition.ValueAttribute != null &&
                    projectedAttributes.TryGetValue(definition.ValueAttribute, out var value) ? value.BaseValue : current;
                return FormatSingleValue(definition, current) + FormatDelta(definition, current, projected);
            }

            case PlayerStatValueMode.CurrentAndMaxAttribute:
            {
                float currentValue = attributeSet != null ? attributeSet.GetCurrentValue(definition.ValueAttribute) : 0f;
                float maxValue = attributeSet != null ? attributeSet.GetCurrentValue(definition.MaxAttribute) : 0f;
                return $"{FormatNumber(currentValue, definition.DecimalPlaces)} / " +
                    $"{FormatNumber(maxValue, definition.DecimalPlaces)}{FormatDelta(maxValue, ReadAttribute(definition.MaxAttribute, true), definition.DecimalPlaces, false)}";
            }

            case PlayerStatValueMode.StatId:
                if (definition.StatId == StatId.MoveSpeedFinal)
                {
                    float baseSpeed = statSource != null ? statSource.Get(StatId.MoveSpeedBase) : 0f;
                    float finalSpeed = statSource != null ? statSource.Get(StatId.MoveSpeedFinal) : 0f;
                    // 실제 이동속도를 기본 속도 대비 비율로 투영해 고정 증가량과 배율을 함께 반영한다.
                    float percent = baseSpeed > 0f ? finalSpeed / baseSpeed * 100f : 0f;
                    float projectedBase = ReadStat(StatId.MoveSpeedBase, true);
                    float projectedPercent = projectedBase > 0f ? ReadStat(StatId.MoveSpeedFinal, true) / projectedBase * 100f : 0f;
                    return $"{FormatNumber(percent, definition.DecimalPlaces)}%" + FormatDelta(percent, projectedPercent, definition.DecimalPlaces, true);
                }
                float stat = ReadStat(definition.StatId, false);
                return FormatSingleValue(definition, stat) + FormatDelta(definition, stat, ReadStat(definition.StatId, true));

            default:
                return string.Empty;
        }
    }

    public void SetSelectionPreview(IReadOnlyList<InventoryTransferRequest> plan)
    {
        if (selectionPreview == null && (plan == null || plan.Count == 0)) return;
        selectionPreview = plan != null && plan.Count > 0 ? plan : null;
        Refresh();
    }

    private void RebuildSelectionPreview()
    {
        projectedAttributes = null;
        if (selectionPreview == null || attributeSet == null) return;
        projectedAttributes = attributeSet.CreatePreviewSnapshot();
        var relics = new List<(RelicDefinition relic, int gainedLevel)>();
        var weapons = new List<(int slot, WeaponDefinition weapon)>();
        foreach (var request in selectionPreview)
        {
            var item = request.Source.Get(request.SourceIndex);
            if (item is RelicDefinition relic) relics.Add((relic, request.SourceRelicLevel));
            else if (item is WeaponDefinition weapon) weapons.Add((request.TargetIndex, weapon));
        }
        var previous = weaponInventory != null ? weaponInventory.ActiveWeapon : null;
        var next = weaponInventory != null ? weaponInventory.PreviewEquippedAfterAcquisitions(weapons) : null;
        // Gameplay owns the projection order and conditional relic rules.
        if (relicInventory != null) relicInventory.ProjectAcquisitions(projectedAttributes, relics, previous, next);
        else WeaponStatBinder.ProjectChange(projectedAttributes, previous, next);
    }

    private float ReadAttribute(AttributeDefinition attribute, bool projected)
    {
        if (attribute == null) return 0f;
        if (projected && projectedAttributes != null && projectedAttributes.TryGetValue(attribute, out var value))
            return value.CurrentValue;
        return attributeSet != null ? attributeSet.GetCurrentValue(attribute) : 0f;
    }

    private float ReadProjectedAttribute(AttributeDefinition attribute) => ReadAttribute(attribute, true);
    private float ReadStat(StatId stat, bool projected) => statSource == null ? 0f :
        projected && projectedAttributes != null ? statSource.GetProjected(stat, ReadProjectedAttribute) : statSource.Get(stat);

    private static string FormatDelta(StatInfoUIDefinition definition, float current, float projected) =>
        FormatDelta(current * definition.ValueMultiplier, projected * definition.ValueMultiplier,
            definition.DisplayFormat == PlayerStatDisplayFormat.WholeNumber ? 0 : definition.DecimalPlaces,
            definition.DisplayFormat == PlayerStatDisplayFormat.Percent);

    private static string FormatDelta(float current, float projected, int decimals, bool percent)
    {
        float scale = Mathf.Pow(10f, Mathf.Max(0, decimals));
        float delta = Mathf.Round((projected - current) * scale) / scale;
        if (Mathf.Abs(delta) < 0.00001f) return string.Empty;
        string color = delta > 0f ? "#66DD88" : "#FF6666";
        return $" <color={color}>({(delta > 0f ? "+" : "-")}{FormatNumber(Mathf.Abs(delta), decimals)}{(percent ? "%" : "")})</color>";
    }

    private string FormatSingleValue(StatInfoUIDefinition definition, float rawValue)
    {
        float value = rawValue * definition.ValueMultiplier;

        switch (definition.DisplayFormat)
        {
            case PlayerStatDisplayFormat.Decimal:
                return value.ToString($"F{Mathf.Max(0, definition.DecimalPlaces)}");

            case PlayerStatDisplayFormat.Percent:
                return $"{value.ToString($"F{Mathf.Max(0, definition.DecimalPlaces)}")}%";

            default:
                return FormatNumber(value, 0);
        }
    }

    private static string FormatNumber(float value, int decimalPlaces)
    {
        if (decimalPlaces <= 0)
            return Mathf.RoundToInt(value).ToString();

        return value.ToString($"F{decimalPlaces}");
    }
}

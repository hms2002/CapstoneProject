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

    private void OnEnable()
    {
        if (ownerOverride != null)
            Bind(ownerOverride);
        else
            Refresh();
    }

    private void OnDisable()
    {
        UnbindAttributeEvents();
    }

    public void Bind(Transform owner)
    {
        if (boundOwner == owner && attributeSet != null)
        {
            Refresh();
            return;
        }

        UnbindAttributeEvents();

        boundOwner = owner;
        attributeSet = owner != null ? owner.GetComponent<AttributeSet>() : null;
        statSource = owner != null ? owner.GetComponent<AttributeStatSource>() : null;

        BindAttributeEvents();
        Rebuild();
    }

    public void Refresh()
    {
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
        if (attributeSet != null)
            attributeSet.OnAttributeChanged += HandleAttributeChanged;
    }

    private void UnbindAttributeEvents()
    {
        if (attributeSet != null)
            attributeSet.OnAttributeChanged -= HandleAttributeChanged;
    }

    private void HandleAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        Refresh();
    }

    private string ResolveValueText(StatInfoUIDefinition definition)
    {
        if (definition == null)
            return string.Empty;

        switch (definition.ValueMode)
        {
            case PlayerStatValueMode.AttributeCurrent:
                return FormatSingleValue(definition, attributeSet != null ? attributeSet.GetCurrentValue(definition.ValueAttribute) : 0f);

            case PlayerStatValueMode.AttributeBase:
                return FormatSingleValue(definition, attributeSet != null ? attributeSet.GetBaseValue(definition.ValueAttribute) : 0f);

            case PlayerStatValueMode.CurrentAndMaxAttribute:
            {
                float currentValue = attributeSet != null ? attributeSet.GetCurrentValue(definition.ValueAttribute) : 0f;
                float maxValue = attributeSet != null ? attributeSet.GetCurrentValue(definition.MaxAttribute) : 0f;
                return $"{FormatNumber(currentValue, definition.DecimalPlaces)} / {FormatNumber(maxValue, definition.DecimalPlaces)}";
            }

            case PlayerStatValueMode.StatId:
                return FormatSingleValue(definition, statSource != null ? statSource.Get(definition.StatId) : 0f);

            default:
                return string.Empty;
        }
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

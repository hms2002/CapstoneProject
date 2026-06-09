using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 플레이어 스탯 패널의 한 섹션 제목과 그 아래 행들의 생명주기를 관리한다.
/// - 섹션 정의를 기반으로 행 프리팹을 생성하고, 상위 패널의 값 해석 함수를 통해 갱신한다.
/// </summary>
public sealed class PlayerStatSectionView : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Transform rowRoot;
    [SerializeField] private PlayerStatRowView rowPrefab;

    private readonly List<PlayerStatRowView> spawnedRows = new();
    private readonly List<StatInfoUIDefinition> boundEntries = new();

    public void Build(StatSectionDefinition definition, Func<StatInfoUIDefinition, string> valueResolver)
    {
        Clear();

        if (definition == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (titleText != null)
            titleText.text = definition.Title;

        if (rowRoot == null || rowPrefab == null || definition.Entries == null)
            return;

        for (int i = 0; i < definition.Entries.Length; i++)
        {
            var entry = definition.Entries[i];
            if (entry == null)
                continue;

            var row = Instantiate(rowPrefab, rowRoot);
            row.Set(entry, valueResolver != null ? valueResolver(entry) : string.Empty);
            spawnedRows.Add(row);
            boundEntries.Add(entry);
        }
    }

    public void Refresh(Func<StatInfoUIDefinition, string> valueResolver)
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            if (spawnedRows[i] == null || i >= boundEntries.Count || boundEntries[i] == null)
                continue;

            spawnedRows[i].Set(boundEntries[i], valueResolver != null ? valueResolver(boundEntries[i]) : string.Empty);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            if (spawnedRows[i] != null)
                Destroy(spawnedRows[i].gameObject);
        }

        spawnedRows.Clear();
        boundEntries.Clear();
    }
}

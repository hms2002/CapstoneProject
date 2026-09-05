using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 몬스터 스택 상태 ID와 표시 아이콘을 데이터로 매핑한다.
/// - 런타임 월드 상태 UI가 코드 하드코딩 없이 상태별 스프라이트를 조회하게 만든다.
/// </summary>
[CreateAssetMenu(
    fileName = "MonsterStackStatusIconCatalog",
    menuName = "Project/UI/Monster Stack Status Icon Catalog")]
public sealed class MonsterStackStatusIconCatalog : ScriptableObject
{
    private const string DefaultResourcePath = "UI/MonsterStackStatusIconCatalog";

    private static MonsterStackStatusIconCatalog runtimeInstance;

    [SerializeField] private Sprite fallbackIcon;
    [SerializeField] private List<Entry> entries = new();

    public static Sprite ResolveIcon(string statusId)
    {
        MonsterStackStatusIconCatalog catalog = ResolveRuntimeInstance();
        return catalog != null ? catalog.ResolveIconInternal(statusId) : null;
    }

    private static MonsterStackStatusIconCatalog ResolveRuntimeInstance()
    {
        if (runtimeInstance == null)
            runtimeInstance = Resources.Load<MonsterStackStatusIconCatalog>(DefaultResourcePath);
        return runtimeInstance;
    }

    private Sprite ResolveIconInternal(string statusId)
    {
        if (string.IsNullOrWhiteSpace(statusId))
            return fallbackIcon;

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (string.Equals(entry.statusId, statusId, StringComparison.OrdinalIgnoreCase))
                return entry.icon != null ? entry.icon : fallbackIcon;
        }

        return fallbackIcon;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => runtimeInstance = null;

    /// <summary>
    /// 책임 :
    /// - 하나의 몬스터 스택 상태 ID와 해당 상태 아이콘 스프라이트를 보관한다.
    /// </summary>
    [Serializable]
    private struct Entry
    {
        public string statusId;
        public Sprite icon;
    }
}

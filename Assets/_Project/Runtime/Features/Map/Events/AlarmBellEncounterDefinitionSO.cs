using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 경보 종 웨이브 한 줄에서 어떤 몬스터를 몇 마리 소환할지와 소환 후 보정 정책을 보관한다.
/// </summary>
[Serializable]
public sealed class AlarmBellMonsterEntry
{
    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private StageMonsterSetSO stageMonsterSet;
    [SerializeField, Min(1)] private int count = 1;
    [SerializeField] private bool suppressNonExperienceDrops = true;
    [SerializeField, Min(0f)] private float additionalHpMultiplier = 1f;
    [SerializeField] private bool overrideSpriteTint;
    [SerializeField] private Color spriteTint = new(1f, 0.45f, 0.35f, 1f);

    public int Count => Mathf.Max(1, count);
    public bool SuppressNonExperienceDrops => suppressNonExperienceDrops;
    public float AdditionalHpMultiplier => Mathf.Max(0f, additionalHpMultiplier);
    public bool OverrideSpriteTint => overrideSpriteTint;
    public Color SpriteTint => spriteTint;

    public bool TryResolveMonsterPrefab(int stageIndex, out GameObject prefab)
    {
        prefab = null;
        if (stageMonsterSet != null &&
            stageMonsterSet.TryResolveMonsterPrefab(stageIndex, out prefab))
        {
            return true;
        }

        prefab = monsterPrefab;
        return prefab != null;
    }
}

/// <summary>
/// 책임 : 경보 종 이벤트의 한 웨이브에 포함될 몬스터 엔트리 목록을 보관한다.
/// </summary>
[Serializable]
public sealed class AlarmBellWaveDefinition
{
    [SerializeField] private List<AlarmBellMonsterEntry> monsters = new();

    public IReadOnlyList<AlarmBellMonsterEntry> Monsters =>
        monsters != null
            ? monsters
            : (IReadOnlyList<AlarmBellMonsterEntry>)Array.Empty<AlarmBellMonsterEntry>();
}

/// <summary>
/// 책임 : 경보 종 활성화 시점의 인정 수에 따라 선택될 웨이브 구성과 완료 경험치를 보관한다.
/// </summary>
[Serializable]
public sealed class AlarmBellEncounterTier
{
    [SerializeField, Min(0)] private int minimumRecognitionCount;
    [SerializeField, Min(0)] private int completionExperience;
    [SerializeField] private List<AlarmBellWaveDefinition> waves = new();

    public int MinimumRecognitionCount => Mathf.Max(0, minimumRecognitionCount);
    public int CompletionExperience => Mathf.Max(0, completionExperience);
    public IReadOnlyList<AlarmBellWaveDefinition> Waves =>
        waves != null
            ? waves
            : (IReadOnlyList<AlarmBellWaveDefinition>)Array.Empty<AlarmBellWaveDefinition>();
}

/// <summary>
/// 책임 : 경보 종 이벤트의 런 1회성 Id, 상호작용 문구, 웨이브 타이밍과 보상 데이터를 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "AlarmBellEncounterDefinition", menuName = "Gameplay/Map Events/Alarm Bell Encounter")]
public sealed class AlarmBellEncounterDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string eventId = "alarm_bell";
    [SerializeField] private string interactPromptText = "경보 종 울리기";

    [Header("Timing")]
    [SerializeField, Min(0f)] private float activationDelaySeconds = 1f;
    [SerializeField, Min(0f)] private float nextWaveDelaySeconds = 1.5f;
    [SerializeField, Min(0f)] private float minimumPlayerSpawnDistance = 3f;

    [Header("Reward")]
    [SerializeField] private LevelProgressionConfigSO levelProgressionConfig;

    [Header("Tiers")]
    [SerializeField] private List<AlarmBellEncounterTier> tiers = new();

    public string EventId => eventId;
    public string InteractPromptText => interactPromptText;
    public float ActivationDelaySeconds => Mathf.Max(0f, activationDelaySeconds);
    public float NextWaveDelaySeconds => Mathf.Max(0f, nextWaveDelaySeconds);
    public float MinimumPlayerSpawnDistance => Mathf.Max(0f, minimumPlayerSpawnDistance);
    public LevelProgressionConfigSO LevelProgressionConfig => levelProgressionConfig;
    public IReadOnlyList<AlarmBellEncounterTier> Tiers =>
        tiers != null
            ? tiers
            : (IReadOnlyList<AlarmBellEncounterTier>)Array.Empty<AlarmBellEncounterTier>();

    public bool TryResolveTier(int recognitionCount, out AlarmBellEncounterTier tier)
    {
        tier = null;
        if (tiers == null || tiers.Count == 0)
            return false;

        int safeRecognitionCount = Mathf.Max(0, recognitionCount);
        AlarmBellEncounterTier lowestTier = null;
        int lowestThreshold = int.MaxValue;
        int bestThreshold = int.MinValue;

        for (int tierIndex = 0; tierIndex < tiers.Count; tierIndex++)
        {
            AlarmBellEncounterTier candidate = tiers[tierIndex];
            if (candidate == null)
                continue;

            int threshold = candidate.MinimumRecognitionCount;
            if (threshold < lowestThreshold)
            {
                lowestThreshold = threshold;
                lowestTier = candidate;
            }

            if (threshold > safeRecognitionCount || threshold < bestThreshold)
                continue;

            bestThreshold = threshold;
            tier = candidate;
        }

        tier ??= lowestTier;
        return tier != null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        tiers ??= new List<AlarmBellEncounterTier>();
    }
#endif
}

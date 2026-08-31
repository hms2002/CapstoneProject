using System;
using CapstoneAudio;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CorridorBossRouteSet",
    menuName = "Capstone/Scene Management/Corridor Boss Route Set")]
/// <summary>
/// 복도 씬과 보스 씬을 한 스테이지 진행 단위로 묶고, 포탈 이동 대상 정보를 제공한다.
/// </summary>
public sealed class CorridorBossRouteSetSO : ScriptableObject
{
    [Header("Stable Theme Identity")]
    [SerializeField] private string themeId;

    [SerializeField] private string corridorSceneName;
    [SerializeField] private string corridorEntryPointId = "Default";

    [Space]
    [SerializeField] private string bossSceneName;
    [SerializeField] private string bossEntryPointId = "Default";

    [Header("Display Names")]
    [SerializeField] private string corridorLocationName;
    [SerializeField] private string bossLocationName;

    [Header("BGM")]
    [SerializeField] private SoundRef corridorBgm;
    [SerializeField] private SoundRef bossCombatBgm;

    [Header("Loading")]
    [SerializeField] private RouteSetLoadManifestSO loadManifest;

    [Header("Boss Battle End")]
    [SerializeField] private BossSpecialRewardPresetSO bossSpecialRewardPreset;

    public string ThemeId => themeId;
    public string StableThemeId => !string.IsNullOrWhiteSpace(themeId)
        ? themeId
        : name;
    public string CorridorSceneName => corridorSceneName;
    public string CorridorEntryPointId => corridorEntryPointId;
    public string BossSceneName => bossSceneName;
    public string BossEntryPointId => bossEntryPointId;
    public string CorridorLocationName => corridorLocationName;
    public string BossLocationName => bossLocationName;
    public SoundRef CorridorBgm => corridorBgm;
    public SoundRef BossCombatBgm => bossCombatBgm;
    public RouteSetLoadManifestSO LoadManifest => loadManifest;
    public BossSpecialRewardPresetSO BossSpecialRewardPreset => bossSpecialRewardPreset;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(corridorSceneName) &&
        !string.IsNullOrWhiteSpace(bossSceneName);

    public bool MatchesCorridorScene(string sceneName)
    {
        return SceneNameMatches(sceneName, corridorSceneName);
    }

    public bool MatchesBossScene(string sceneName)
    {
        return SceneNameMatches(sceneName, bossSceneName);
    }

    public bool TryResolveLocationName(string sceneName, out string locationName)
    {
        locationName = null;
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        if (MatchesCorridorScene(sceneName))
        {
            if (string.IsNullOrWhiteSpace(corridorLocationName))
                return false;

            locationName = corridorLocationName;
            return true;
        }

        if (MatchesBossScene(sceneName))
        {
            if (string.IsNullOrWhiteSpace(bossLocationName))
                return false;

            locationName = bossLocationName;
            return true;
        }

        return false;
    }

    private static bool SceneNameMatches(string candidateSceneName, string configuredSceneName)
    {
        if (string.IsNullOrWhiteSpace(candidateSceneName) || string.IsNullOrWhiteSpace(configuredSceneName))
            return false;

        if (string.Equals(candidateSceneName, configuredSceneName, StringComparison.OrdinalIgnoreCase))
            return true;

#if UNITY_EDITOR
        return IsEditorDuplicateSceneName(candidateSceneName, configuredSceneName);
#else
        return false;
#endif
    }

#if UNITY_EDITOR
    private static bool IsEditorDuplicateSceneName(string candidateSceneName, string configuredSceneName)
    {
        if (!candidateSceneName.StartsWith(configuredSceneName + " ", StringComparison.OrdinalIgnoreCase))
            return false;

        int suffixStart = configuredSceneName.Length + 1;
        if (suffixStart >= candidateSceneName.Length)
            return false;

        for (int i = suffixStart; i < candidateSceneName.Length; i++)
        {
            if (!char.IsDigit(candidateSceneName[i]))
                return false;
        }

        return true;
    }
#endif

    public bool TryCreateCorridorRoute(TransitionType transitionType, out PortalRouteDecision route)
    {
        route = default;
        if (!IsValid)
            return false;

        route = new PortalRouteDecision(
            corridorSceneName,
            string.IsNullOrWhiteSpace(corridorEntryPointId) ? "Default" : corridorEntryPointId,
            transitionType);
        return true;
    }

    public bool TryCreateBossRoute(TransitionType transitionType, out PortalRouteDecision route)
    {
        route = default;
        if (!IsValid)
            return false;

        route = new PortalRouteDecision(
            bossSceneName,
            string.IsNullOrWhiteSpace(bossEntryPointId) ? "Default" : bossEntryPointId,
            transitionType);
        return true;
    }
}

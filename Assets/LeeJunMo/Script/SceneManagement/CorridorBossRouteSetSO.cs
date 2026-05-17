using System;
using CapstoneAudio;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CorridorBossRouteSet",
    menuName = "Capstone/Scene Management/Corridor Boss Route Set")]
public sealed class CorridorBossRouteSetSO : ScriptableObject
{
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

    public bool TryResolveLocationName(string sceneName, out string locationName)
    {
        locationName = null;
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        if (string.Equals(sceneName, corridorSceneName, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(corridorLocationName))
                return false;

            locationName = corridorLocationName;
            return true;
        }

        if (string.Equals(sceneName, bossSceneName, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(bossLocationName))
                return false;

            locationName = bossLocationName;
            return true;
        }

        return false;
    }

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

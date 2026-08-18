using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 보스전 클리어 조건, 최종 사망 연출, 보상/포털 활성화를 씬 단위로 조율한다.
/// - 개별 보스 사망과 보스전 종료를 분리해 단일보스와 분열/다중 보스가 같은 종료 파이프라인을 쓰게 한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Capstone/Boss/Boss Encounter End Director")]
public sealed class BossEncounterEndDirector : MonoBehaviour
{
    private static readonly List<BossEncounterEndDirector> ActiveDirectors = new();

    [Header("Clear")]
    [SerializeField] private BossEncounterClearCondition clearCondition;
    [SerializeField] private bool suppressManagedBossAutomaticRewards = true;

    [Header("Final Presentation")]
    [SerializeField] private BossDeathPresentation finalDeathPresentation;
    [SerializeField] private bool useFinalDeathPresentation = true;
    [SerializeField, Min(0f)] private float rewardDelayAfterClearSeconds = 0f;

    [Header("Rewards")]
    [SerializeField] private TreasureChest treasureChest;
    [SerializeField] private GameObject exitPortal;
    [SerializeField] private bool hideAuthoredObjectsOnStart = true;

    [Header("Experience Reward")]
    [SerializeField] private ExperiencePickup2D experiencePickupPrefab;
    [SerializeField, Min(0)] private int stageOneBossExperience = 120;
    [SerializeField, Min(0)] private int stageTwoBossExperience = 150;
    [SerializeField, Min(0)] private int stageThreeBossExperience = 180;
    [SerializeField, Min(1)] private int experiencePerPickup = 5;
    [SerializeField, Min(1)] private int maximumExperiencePickupCount = 30;
    [SerializeField, Min(0f)] private float experiencePickupScatterRadius = 1.2f;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private bool hasCompleted;
    private Coroutine completionRoutine;

    public static bool SuppressesAutomaticRewardReady(BossControllerBase boss)
    {
        if (boss == null)
            return false;

        for (int i = 0; i < ActiveDirectors.Count; i++)
        {
            BossEncounterEndDirector director = ActiveDirectors[i];
            if (director != null && director.SuppressesRewardsFor(boss))
                return true;
        }

        return false;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        if (!hideAuthoredObjectsOnStart)
            return;

        if (treasureChest != null)
        {
            LogDebug($"Hiding authored chest on start: {treasureChest.name}.");
            treasureChest.gameObject.SetActive(false);
        }

        if (exitPortal != null)
        {
            LogDebug($"Hiding authored portal on start: {exitPortal.name}.");
            exitPortal.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (!ActiveDirectors.Contains(this))
            ActiveDirectors.Add(this);
    }

    private void OnDisable()
    {
        ActiveDirectors.Remove(this);

        if (completionRoutine != null)
        {
            StopCoroutine(completionRoutine);
            completionRoutine = null;
        }
    }

    private void Update()
    {
        if (hasCompleted)
            return;

        ResolveReferences();
        if (clearCondition == null || !clearCondition.IsCleared)
            return;

        hasCompleted = true;
        completionRoutine = StartCoroutine(CompleteEncounterRoutine());
    }

    private void ResolveReferences()
    {
        if (clearCondition == null)
            clearCondition = GetComponent<BossEncounterClearCondition>();
    }

    private bool SuppressesRewardsFor(BossControllerBase boss)
    {
        return suppressManagedBossAutomaticRewards &&
               clearCondition != null &&
               clearCondition.ControlsBoss(boss);
    }

    private IEnumerator CompleteEncounterRoutine()
    {
        if (rewardDelayAfterClearSeconds > 0f)
            yield return new WaitForSeconds(rewardDelayAfterClearSeconds);

        BossControllerBase rewardBoss = clearCondition != null ? clearCondition.RewardBoss : null;
        LogDebug($"Clear condition completed. RewardBoss={(rewardBoss != null ? rewardBoss.name : "None")}.");
        BossRewardContext context = BuildRewardContext(rewardBoss);
        Vector3 rewardOrigin = clearCondition != null ? clearCondition.RewardOrigin : transform.position;
        bool usedCustomFinalePresentation = false;

        if (clearCondition is IBossEncounterFinalePresentationProvider finaleProvider &&
            finaleProvider.TryCreateFinalePresentationRoutine(this, out IEnumerator finaleRoutine) &&
            finaleRoutine != null)
        {
            usedCustomFinalePresentation = true;
            yield return finaleRoutine;
        }

        BossDeathPresentation presentation = ResolveFinalDeathPresentation(rewardBoss);
        if (!usedCustomFinalePresentation && useFinalDeathPresentation && presentation != null)
        {
            presentation.Bind(rewardBoss);
            if (presentation.IsRunning || presentation.TryBeginDeathSequence(false))
            {
                while (presentation != null && presentation.IsRunning)
                    yield return null;
            }

            if (presentation != null && presentation.CompletedViaTerminalEnding)
            {
                LogDebug("Encounter completed through terminal ending sequence.");
                completionRoutine = null;
                yield break;
            }
        }

        LogRewardContext(context);

        HandleExperienceReward(context, rewardOrigin);
        HandleRewards(context, rewardOrigin);
        HandlePortal(context);
        LogDebug("Encounter completed.");
        completionRoutine = null;
    }

    private BossDeathPresentation ResolveFinalDeathPresentation(BossControllerBase rewardBoss)
    {
        if (finalDeathPresentation != null)
            return finalDeathPresentation;

        return rewardBoss != null ? rewardBoss.GetComponent<BossDeathPresentation>() : null;
    }

    private static BossRewardContext BuildRewardContext(BossControllerBase rewardBoss)
    {
        BossRewardModifierAggregate modifiers = RunModifierService.CurrentRewardSnapshot.BossRewardModifiers;
        return BossRunProgressPolicy.Evaluate(
            new BossRunProgressRequest(
                rewardBoss,
                RunRoutePlayback.Backend,
                modifiers)).ToRewardContext();
    }

    private void HandleRewards(BossRewardContext context, Vector3 rewardOrigin)
    {
        if (context == null || context.RewardsHandled)
            return;

        if (context.IsFinalRouteSet)
        {
            LogDebug(
                $"Skipping treasure chest because the current route is the final route set. " +
                $"RouteSet={ResolveRouteSetName(context)}.");
            if (BossRewardSpawnService.SpawnPhysicalDrops(context, rewardOrigin, this))
                context.MarkRewardsHandled();

            return;
        }

        if (treasureChest == null)
        {
            Debug.LogWarning("[BossEncounterEndDirector] TreasureChest is not assigned.", this);
            return;
        }

        LogDebug(
            $"Activating treasure chest '{treasureChest.name}' at '{treasureChest.gameObject.name}'. " +
            $"BeforeActiveSelf={treasureChest.gameObject.activeSelf}.");

        bool activated = BossRewardSpawnService.ActivateTreasureChest(new BossRewardActivationRequest(
            context,
            context.SpecialRewardPreset,
            treasureChest,
            rewardOrigin,
            this));

        if (!activated)
        {
            LogDebug($"Treasure chest activation returned false for '{treasureChest.name}'.");
            return;
        }

        treasureChest.PlayRewardReveal();
        LogDebug(
            $"Treasure chest '{treasureChest.name}' activated. " +
            $"AfterActiveSelf={treasureChest.gameObject.activeSelf}.");
        context.MarkRewardsHandled();
    }

    private void HandleExperienceReward(BossRewardContext context, Vector3 rewardOrigin)
    {
        if (context == null || context.IsFinalRouteSet || experiencePickupPrefab == null || !RunSessionStore.IsRunActive)
            return;

        int stageExperience = ResolveNormalStageBossExperience(RunRoutePlayback.CurrentStageIndexOrInvalid);
        if (stageExperience <= 0)
            return;

        int spawnedCount = ExperiencePickupDropSpawner.SpawnDistributed(
            experiencePickupPrefab,
            rewardOrigin,
            stageExperience,
            experiencePerPickup,
            maximumExperiencePickupCount,
            experiencePickupScatterRadius);
        LogDebug(
            $"Spawned boss experience. Stage={RunRoutePlayback.CurrentStageIndexOrInvalid + 1}, " +
            $"TotalExperience={stageExperience}, PickupCount={spawnedCount}.");
    }

    private int ResolveNormalStageBossExperience(int stageIndex)
    {
        return stageIndex switch
        {
            0 => stageOneBossExperience,
            1 => stageTwoBossExperience,
            2 => stageThreeBossExperience,
            _ => 0
        };
    }

    private void HandlePortal(BossRewardContext context)
    {
        if (context == null || context.PortalHandled)
            return;

        if (exitPortal == null)
        {
            Debug.LogWarning("[BossEncounterEndDirector] Exit portal is not assigned.", this);
            return;
        }

        exitPortal.SetActive(true);
        RestorePortalVisibilityAndInteraction(exitPortal);
        PlayPortalRevealPresentation(exitPortal);
        LogDebug($"Portal '{exitPortal.name}' activated.");
        context.MarkPortalHandled();
    }

    private static void PlayPortalRevealPresentation(GameObject root)
    {
        if (root == null)
            return;

        BossRewardObjectRevealPresentation presentation =
            root.GetComponent<BossRewardObjectRevealPresentation>();
        if (presentation == null)
            presentation = root.GetComponentInChildren<BossRewardObjectRevealPresentation>(true);

        presentation?.PlayReveal();
    }

    private static void RestorePortalVisibilityAndInteraction(GameObject portalRoot)
    {
        if (portalRoot == null)
            return;

        Renderer[] renderers = portalRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = true;
        }

        Collider2D[] colliders = portalRoot.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = true;
        }

        ScenePortal[] scenePortals = portalRoot.GetComponentsInChildren<ScenePortal>(true);
        for (int i = 0; i < scenePortals.Length; i++)
        {
            if (scenePortals[i] != null)
                scenePortals[i].enabled = true;
        }
    }

    private void LogDebug(string message)
    {
        if (logDebug)
            Debug.Log($"[BossEncounterEndDirector] {message}", this);
    }

    private void LogRewardContext(BossRewardContext context)
    {
        if (!logDebug)
            return;

        RunRouteCatalogSO activeCatalog = RunRoutePlayback.ActiveRouteCatalog;
        string catalogName = activeCatalog != null
            ? activeCatalog.name
            : "None";
        bool hasActivePlan = RunRoutePlayback.HasActivePlan;
        int currentStageIndex = RunRoutePlayback.CurrentStageIndexOrInvalid;
        int totalStageCount = RunRoutePlayback.TotalStageCount;

        LogDebug(
            $"Reward context. RouteSet={ResolveRouteSetName(context)}, " +
            $"IsFinalRouteSet={(context != null && context.IsFinalRouteSet)}, " +
            $"HasActivePlan={hasActivePlan}, Stage={currentStageIndex + 1}/{totalStageCount}, " +
            $"Catalog={catalogName}, RouteSetKey={(context != null ? context.RouteSetKey : 0)}.");
    }

    private static string ResolveRouteSetName(BossRewardContext context)
    {
        return context != null && context.RouteSet != null ? context.RouteSet.name : "None";
    }
}

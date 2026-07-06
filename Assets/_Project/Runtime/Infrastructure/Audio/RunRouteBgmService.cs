using System;
using CapstoneAudio;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임:
/// - 씬 전환과 보스 전투 상태에 맞춰 현재 재생해야 할 BGM을 결정하고 SoundManager에 재생을 요청한다.
/// - 타이틀/허브/복도/보스 전투 BGM 전환 규칙을 한 곳에서 관리한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RunRouteBgmService : MonoBehaviour, IRunRouteBgmBackend
{
    public static RunRouteBgmService Instance { get; private set; }

    private static bool s_isQuitting;
    private static readonly IRunRouteBgmBackend NullBackend = new NullRunRouteBgmBackend();

#pragma warning disable 0414
    [HideInInspector, SerializeField, Min(0f)] private float sceneBgmFadeDuration = 0.5f;
    [HideInInspector, SerializeField, Min(0f)] private float bossCombatBgmFadeDuration = 0.75f;
#pragma warning restore 0414
    [SerializeField] private string titleSceneName = "TitleScene";
    [SerializeField] private SoundRef titleSceneBgm = SoundRef.FromKey("TitleSceneBGM");
    [SerializeField] private bool verboseLogging;

    private SoundRef currentMusicRef;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        RunRouteBgmPlayback.RegisterBackend(NullBackend);

        if (s_isQuitting)
            return;

        EnsureInstance();
    }

    public static RunRouteBgmService EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        RunRouteBgmService existing = FindAnyObjectByType<RunRouteBgmService>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject root = new GameObject(nameof(RunRouteBgmService));
        return root.AddComponent<RunRouteBgmService>();
    }

    public void NotifyBossCombatStarted()
    {
        CorridorBossRouteSetSO currentStage = PortalRouteManager.Instance != null
            ? PortalRouteManager.Instance.CurrentStageSet
            : null;

        if (currentStage == null || !currentStage.BossCombatBgm.IsSet)
            return;

        PlayMusicIfChanged(currentStage.BossCombatBgm, "Boss combat started");
    }

    /// <summary>현재 활성 씬 기준 BGM을 다시 판정해 타이틀 정리/씬 직접 시작 뒤에도 음악 상태를 복구합니다.</summary>
    public void RefreshActiveSceneBgm()
    {
        RefreshSceneBgm(SceneManager.GetActiveScene(), forceRestart: false);
    }

    /// <summary>현재 BGM 캐시와 무관하게 활성 씬 BGM 재생 요청을 다시 보냅니다.</summary>
    public void ForceRefreshActiveSceneBgm()
    {
        RefreshSceneBgm(SceneManager.GetActiveScene(), forceRestart: true);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RunRouteBgmPlayback.RegisterBackend(this);
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        RefreshActiveSceneBgm();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            RunRouteBgmPlayback.RegisterBackend(NullBackend);
        }
    }

    /// <summary>
    /// 책임 : BGM 서비스가 아직 생성되지 않은 초기 호출을 안전하게 무시하는 no-op backend다.
    /// </summary>
    private sealed class NullRunRouteBgmBackend : IRunRouteBgmBackend
    {
        public void NotifyBossCombatStarted()
        {
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshSceneBgm(scene, forceRestart: false);
    }

    private void RefreshSceneBgm(Scene scene, bool forceRestart)
    {
        if (!scene.IsValid())
            return;

        if (TryResolveTitleBgm(scene, out SoundRef titleBgm))
        {
            PlayMusicIfChanged(titleBgm, $"Title scene loaded ({scene.name})", forceRestart);
            return;
        }

        if (TryResolveHubBgm(scene, out SoundRef hubBgm))
        {
            PlayMusicIfChanged(hubBgm, $"Hub scene loaded ({scene.name})", forceRestart);
            return;
        }

        if (TryResolveCurrentStageCorridorBgm(scene, out SoundRef corridorBgm))
        {
            PlayMusicIfChanged(corridorBgm, $"Corridor scene loaded ({scene.name})", forceRestart);
            return;
        }

        if (TryResolveBossScenePreCombatBgm(scene, out SoundRef carryOverBgm))
        {
            if (forceRestart || !AreEquivalent(currentMusicRef, carryOverBgm))
                PlayMusicIfChanged(carryOverBgm, $"Boss scene pre-combat fallback ({scene.name})", forceRestart);

            return;
        }

        StopTitleBgmIfCarriedIntoNonTitleScene(scene);
    }

    private bool TryResolveTitleBgm(Scene scene, out SoundRef bgm)
    {
        bgm = default;

        if (!scene.IsValid())
            return false;

        if (!SceneNameEquals(scene.name, titleSceneName))
            return false;

        if (!titleSceneBgm.IsSet)
            return false;

        bgm = titleSceneBgm;
        return true;
    }

    private static bool TryResolveCurrentStageCorridorBgm(Scene scene, out SoundRef bgm)
    {
        bgm = default;

        CorridorBossRouteSetSO currentStage = PortalRouteManager.Instance != null
            ? PortalRouteManager.Instance.CurrentStageSet
            : null;

        if (currentStage == null)
            return false;

        if (!SceneNameEquals(scene.name, currentStage.CorridorSceneName))
            return false;

        if (!currentStage.CorridorBgm.IsSet)
            return false;

        bgm = currentStage.CorridorBgm;
        return true;
    }

    private static bool TryResolveBossScenePreCombatBgm(Scene scene, out SoundRef bgm)
    {
        bgm = default;

        CorridorBossRouteSetSO currentStage = PortalRouteManager.Instance != null
            ? PortalRouteManager.Instance.CurrentStageSet
            : null;

        if (currentStage == null)
            return false;

        if (!SceneNameEquals(scene.name, currentStage.BossSceneName))
            return false;

        if (!currentStage.CorridorBgm.IsSet)
            return false;

        bgm = currentStage.CorridorBgm;
        return true;
    }

    private static bool TryResolveHubBgm(Scene scene, out SoundRef bgm)
    {
        bgm = default;

        if (!scene.IsValid())
            return false;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            GameObject root = roots[rootIndex];
            if (root == null)
                continue;

            ScenePortal[] portals = root.GetComponentsInChildren<ScenePortal>(true);
            for (int portalIndex = 0; portalIndex < portals.Length; portalIndex++)
            {
                ScenePortal portal = portals[portalIndex];
                if (portal == null || portal.PortalTransitionType != TransitionType.HubToRunStart)
                    continue;

                RunRouteCatalogSO catalog = portal.StartRunRouteCatalog;
                if (catalog == null ||
                    !catalog.HubBgm.IsSet ||
                    !SceneNameEquals(scene.name, catalog.HubSceneName))
                {
                    continue;
                }

                bgm = catalog.HubBgm;
                return true;
            }
        }

        return false;
    }

    private void PlayMusicIfChanged(SoundRef soundRef, string reason, bool forceRestart = false)
    {
        if (!soundRef.IsSet)
            return;

        if (!forceRestart && AreEquivalent(currentMusicRef, soundRef))
            return;

        SoundManager.EnsureInstance().PlayMusic(soundRef);
        currentMusicRef = soundRef;

        if (verboseLogging)
        {
            Debug.Log(
                $"[RunRouteBgmService] Switched BGM. key={soundRef.key}, reason={reason}",
                this);
        }
    }

    private void StopTitleBgmIfCarriedIntoNonTitleScene(Scene scene)
    {
        if (!scene.IsValid() || SceneNameEquals(scene.name, titleSceneName))
            return;

        if (!titleSceneBgm.IsSet || !AreEquivalent(currentMusicRef, titleSceneBgm))
            return;

        StopCurrentMusic($"No BGM resolved for non-title scene ({scene.name}) after title BGM");
    }

    private void StopCurrentMusic(string reason)
    {
        currentMusicRef = default;
        SoundManager.Instance?.StopMusic();

        if (verboseLogging)
            Debug.Log($"[RunRouteBgmService] Stopped BGM. reason={reason}", this);
    }

    private static bool AreEquivalent(SoundRef left, SoundRef right)
    {
        return string.Equals(NormalizeKey(left.key), NormalizeKey(right.key), StringComparison.OrdinalIgnoreCase)
               && Mathf.Approximately(left.EffectiveVolumeMultiplier, right.EffectiveVolumeMultiplier);
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }

    private static bool SceneNameEquals(string left, string right)
    {
        return string.Equals(left, right, StringComparison.Ordinal);
    }
}

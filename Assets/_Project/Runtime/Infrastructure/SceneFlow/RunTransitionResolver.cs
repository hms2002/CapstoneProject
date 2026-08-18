using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunTransitionResolver : MonoBehaviour
{
    public static RunTransitionResolver Instance { get; private set; }

    private static bool s_isQuitting;

    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool verboseLogging;
    [SerializeField] private RunTransitionDirective defaultDirective;
    [SerializeField] private List<RunTransitionRule> rules = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        var go = new GameObject(nameof(RunTransitionResolver));
        go.AddComponent<RunTransitionResolver>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            if (persistAcrossScenes)
                DontDestroyOnLoad(gameObject);

            return;
        }

        Instance.TryAdoptConfiguration(
            persistAcrossScenes,
            verboseLogging,
            defaultDirective,
            rules);

        Destroy(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    public RunTransitionDirective Resolve(PortalRouteDecision route)
    {
        if (!route.IsValid)
            return default;

        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (rule == null || !rule.Matches(route))
                continue;

            var resolved = rule.Directive;

            if (verboseLogging)
            {
                Debug.Log(
                    $"[RunTransitionResolver] Directive resolved for target={route.TargetSceneName}, type={route.TransitionType}, directive={resolved}",
                    this);
            }

            return resolved;
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"[RunTransitionResolver] Using default directive for target={route.TargetSceneName}, type={route.TransitionType}, directive={defaultDirective}",
                this);
        }

        return defaultDirective;
    }

    private void TryAdoptConfiguration(
        bool incomingPersistAcrossScenes,
        bool incomingVerboseLogging,
        RunTransitionDirective incomingDefaultDirective,
        List<RunTransitionRule> incomingRules)
    {
        bool hasIncomingConfig = HasMeaningfulConfiguration(incomingDefaultDirective, incomingRules);
        if (!hasIncomingConfig)
            return;

        bool hasCurrentConfig = HasMeaningfulConfiguration(defaultDirective, rules);
        if (hasCurrentConfig)
        {
            if (verboseLogging)
            {
                Debug.LogWarning(
                    "[RunTransitionResolver] A scene instance tried to supply another configuration, but the global resolver already has one. Keeping the existing configuration.",
                    this);
            }

            return;
        }

        persistAcrossScenes = incomingPersistAcrossScenes;
        verboseLogging = incomingVerboseLogging;
        defaultDirective = incomingDefaultDirective;
        rules = incomingRules != null ? new List<RunTransitionRule>(incomingRules) : new List<RunTransitionRule>();

        if (verboseLogging)
        {
            Debug.Log(
                $"[RunTransitionResolver] Adopted configuration from a scene instance. ruleCount={rules.Count}, defaultDirective={defaultDirective}",
                this);
        }
    }

    private static bool HasMeaningfulConfiguration(RunTransitionDirective directive, List<RunTransitionRule> configuredRules)
    {
        if (configuredRules != null && configuredRules.Count > 0)
            return true;

        return directive.action != RunTransitionAction.None || directive.endReason != RunEndReason.None;
    }
}

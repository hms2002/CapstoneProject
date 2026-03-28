using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SceneTransitionPolicyResolver : MonoBehaviour
{
    public static SceneTransitionPolicyResolver Instance { get; private set; }

    private static bool s_isQuitting;

    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool verboseLogging;
    [SerializeField] private SceneTransitionPolicy defaultPolicy;
    [SerializeField] private List<SceneTransitionPolicyRule> rules = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        var go = new GameObject(nameof(SceneTransitionPolicyResolver));
        go.AddComponent<SceneTransitionPolicyResolver>();
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
            defaultPolicy,
            rules);

        Destroy(gameObject);
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

    public SceneTransitionPolicy Resolve(PortalRouteDecision route)
    {
        if (!route.IsValid)
            return default;

        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (rule == null || !rule.Matches(route))
                continue;

            var resolved = rule.Policy;

            if (verboseLogging)
            {
                Debug.Log(
                    $"[SceneTransitionPolicyResolver] Policy resolved for target={route.TargetSceneName}, type={route.TransitionType}, policy={resolved}",
                    this);
            }

            return resolved;
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"[SceneTransitionPolicyResolver] Using default policy for target={route.TargetSceneName}, type={route.TransitionType}, policy={defaultPolicy}",
                this);
        }

        return defaultPolicy;
    }

    private void TryAdoptConfiguration(
        bool incomingPersistAcrossScenes,
        bool incomingVerboseLogging,
        SceneTransitionPolicy incomingDefaultPolicy,
        List<SceneTransitionPolicyRule> incomingRules)
    {
        bool hasIncomingConfig = HasMeaningfulConfiguration(incomingDefaultPolicy, incomingRules);
        if (!hasIncomingConfig)
            return;

        bool hasCurrentConfig = HasMeaningfulConfiguration(defaultPolicy, rules);
        if (hasCurrentConfig)
        {
            if (verboseLogging)
            {
                Debug.LogWarning(
                    "[SceneTransitionPolicyResolver] A scene instance tried to supply another configuration, but the global resolver already has one. Keeping the existing configuration.",
                    this);
            }

            return;
        }

        persistAcrossScenes = incomingPersistAcrossScenes;
        verboseLogging = incomingVerboseLogging;
        defaultPolicy = incomingDefaultPolicy;
        rules = incomingRules != null ? new List<SceneTransitionPolicyRule>(incomingRules) : new List<SceneTransitionPolicyRule>();

        if (verboseLogging)
        {
            Debug.Log(
                $"[SceneTransitionPolicyResolver] Adopted configuration from a scene instance. ruleCount={rules.Count}, defaultPolicy={defaultPolicy}",
                this);
        }
    }

    private static bool HasMeaningfulConfiguration(SceneTransitionPolicy policy, List<SceneTransitionPolicyRule> configuredRules)
    {
        if (configuredRules != null && configuredRules.Count > 0)
            return true;

        return policy.fullyHealPlayer ||
               policy.resetCooldowns ||
               policy.clearAllEffects ||
               policy.clearCombatOnlyEffects;
    }
}

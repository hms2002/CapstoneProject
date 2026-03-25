using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunTransitionResolver : MonoBehaviour
{
    public static RunTransitionResolver Instance { get; private set; }

    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool verboseLogging;
    [SerializeField] private RunTransitionDirective defaultDirective;
    [SerializeField] private List<RunTransitionRule> rules = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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
}

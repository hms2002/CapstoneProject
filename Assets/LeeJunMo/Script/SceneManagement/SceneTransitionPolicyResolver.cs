using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SceneTransitionPolicyResolver : MonoBehaviour
{
    public static SceneTransitionPolicyResolver Instance { get; private set; }

    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool verboseLogging;
    [SerializeField] private SceneTransitionPolicy defaultPolicy;
    [SerializeField] private List<SceneTransitionPolicyRule> rules = new();

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
}

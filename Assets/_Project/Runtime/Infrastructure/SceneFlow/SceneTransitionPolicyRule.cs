using System;
using UnityEngine;

[Serializable]
public sealed class SceneTransitionPolicyRule
{
    [SerializeField] private bool enabled = true;
    [SerializeField] private TransitionType transitionType = TransitionType.None;

    [Header("Policy Result")]
    [SerializeField] private SceneTransitionPolicy policy;

    public bool Matches(PortalRouteDecision route)
    {
        if (!enabled || !route.IsValid)
            return false;

        return route.TransitionType == transitionType;
    }

    public SceneTransitionPolicy Policy => policy;
}

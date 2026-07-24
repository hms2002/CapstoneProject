using System;
using UnityEngine;

[Serializable]
public sealed class RunTransitionRule
{
    [SerializeField] private bool enabled = true;
    [SerializeField] private TransitionType transitionType = TransitionType.None;

    [Header("Run Result")]
    [SerializeField] private RunTransitionDirective directive;

    public bool Matches(PortalRouteDecision route)
    {
        if (!enabled || !route.IsValid)
            return false;

        return route.TransitionType == transitionType;
    }

    public RunTransitionDirective Directive => directive;
}

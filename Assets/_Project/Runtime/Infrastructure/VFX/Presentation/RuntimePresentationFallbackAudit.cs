using System.Collections.Generic;
using UnityEngine;

internal static class RuntimePresentationFallbackAudit
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static readonly HashSet<string> LoggedFallbacks = new HashSet<string>();
#endif

    public static void Record(Object context, string fallbackName, string migrationTarget)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string key = $"{fallbackName}:{(context != null ? context.GetHashCode() : 0)}";
        if (!LoggedFallbacks.Add(key))
            return;

        Debug.LogWarning(
            $"[RuntimePresentationFallback] {fallbackName} created runtime UI/presentation hierarchy. " +
            $"Author {migrationTarget} before treating this path as build-facing UI.",
            context);
#endif
    }
}

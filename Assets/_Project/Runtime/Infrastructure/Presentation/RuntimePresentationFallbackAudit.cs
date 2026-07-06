using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임: 런타임 fallback UI/Presentation 생성 경로를 개발 빌드에서 한 번씩 경고해 authored prefab/scene 이관 대상을 드러낸다.
/// </summary>
public static class RuntimePresentationFallbackAudit
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

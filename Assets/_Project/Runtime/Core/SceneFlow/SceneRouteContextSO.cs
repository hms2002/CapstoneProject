using System;
using UnityEngine;

/// <summary>
/// 책임 : Core의 씬 연결 데이터가 Gameplay의 구체 경로 에셋 타입을 참조하지 않고 목적지 씬의 런 경로 문맥을 보관하게 하는 추상 계약이다.
/// </summary>
public abstract class SceneRouteContextSO : ScriptableObject
{
    public abstract string StableContextId { get; }

    public abstract bool MatchesScene(string sceneName);

    protected static bool SceneNameMatches(string candidateSceneName, string configuredSceneName)
    {
        if (string.IsNullOrWhiteSpace(candidateSceneName) || string.IsNullOrWhiteSpace(configuredSceneName))
            return false;

        if (string.Equals(candidateSceneName, configuredSceneName, StringComparison.OrdinalIgnoreCase))
            return true;

#if UNITY_EDITOR
        return IsEditorDuplicateSceneName(candidateSceneName, configuredSceneName);
#else
        return false;
#endif
    }

#if UNITY_EDITOR
    private static bool IsEditorDuplicateSceneName(string candidateSceneName, string configuredSceneName)
    {
        if (!candidateSceneName.StartsWith(configuredSceneName + " ", StringComparison.OrdinalIgnoreCase))
            return false;

        int suffixStart = configuredSceneName.Length + 1;
        if (suffixStart >= candidateSceneName.Length)
            return false;

        for (int i = suffixStart; i < candidateSceneName.Length; i++)
        {
            if (!char.IsDigit(candidateSceneName[i]))
                return false;
        }

        return true;
    }
#endif
}

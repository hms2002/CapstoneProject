using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임 :
/// - 현재 씬 이름을 기준으로 런 타이머를 감소시켜야 하는지 판정한다.
/// - 준비 구간과 전투 구간을 데이터 목록으로 분리해, 타이머 시스템이 게임 문맥을 직접 해석하지 않게 만든다.
/// </summary>
public sealed class DefaultStageTimerPolicy : MonoBehaviour, IStageTimerPolicy
{
    [Header("Tick In These Scene Name Keywords")]
    [SerializeField] private List<string> tickingSceneNameKeywords = new()
    {
        "Boss",
        "Corridor",
        "Hallway"
    };

    [Header("Do Not Tick In These Scene Name Keywords")]
    [SerializeField] private List<string> pausedSceneNameKeywords = new()
    {
        "Hub",
        "SampleScene",
        "Tests"
    };

    [Header("Fallback")]
    [SerializeField] private bool defaultShouldTick;

    public bool ShouldTick()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrWhiteSpace(sceneName))
            return defaultShouldTick;

        if (ContainsKeyword(sceneName, pausedSceneNameKeywords))
            return false;

        if (ContainsKeyword(sceneName, tickingSceneNameKeywords))
            return true;

        return defaultShouldTick;
    }

    private static bool ContainsKeyword(string sceneName, List<string> keywords)
    {
        if (keywords == null || keywords.Count == 0)
            return false;

        for (int i = 0; i < keywords.Count; i++)
        {
            string keyword = keywords[i];
            if (string.IsNullOrWhiteSpace(keyword))
                continue;

            if (sceneName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }
}

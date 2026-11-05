using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 대화 시작 전 현재 런타임/저장 상태를 기준으로 Ink 시작 knot을 선택하는 공용 계약을 제공한다.
/// - BossDialogueRunner가 특정 보스의 조건 분기 구현을 직접 알지 않도록 분리한다.
/// </summary>
public interface IDialogueStartKnotSelector
{
    string SelectStartKnot(NPCData npcData, TextAsset inkJSON);
}

// 재조우 본대사는 다른 후보가 있을 때만 직전 경로를 제외하고 균등 추첨한다.
internal static class BossDialogueRepeatSelection
{
    public static string Select(IReadOnlyList<string> candidates, string previousStartPath)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        int alternativeCount = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (!string.Equals(candidates[i], previousStartPath, System.StringComparison.Ordinal))
                alternativeCount++;
        }

        bool excludePrevious = alternativeCount > 0;
        int selectedIndex = Random.Range(0, excludePrevious ? alternativeCount : candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            if (excludePrevious && string.Equals(candidates[i], previousStartPath, System.StringComparison.Ordinal))
                continue;

            if (selectedIndex-- == 0)
                return candidates[i];
        }

        return null;
    }
}

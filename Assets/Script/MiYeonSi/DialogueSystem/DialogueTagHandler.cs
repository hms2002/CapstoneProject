using System;
using System.Collections.Generic;
using UnityEngine;

public class DialogueTagHandler : MonoBehaviour
{
    // 다중 NPC 지원을 위해 targetId와 value를 넘깁니다.
    public Action<string, string> OnPortraitEnterRequested;
    public Action<string, string> OnPortraitFaceRequested;
    public Action<string, string> OnPortraitEmoteRequested;
    public Action<string, string> OnPortraitMoveRequested;
    public Action<string, string> OnPortraitActionRequested;

    // exit과 feature는 인자가 1개면 충분합니다.
    public Action<string> OnPortraitExitRequested;
    public Action<string> OnFeatureRequested;

    // [핵심 추가] 호감도 요청 전용 진동벨 (NPC데이터, 올릴 수치, 끝났을 때 누를 버튼)
    public Action<NPCData, int, Action> OnAffectionRequested;

    // [수정] 대화 재개(onComplete) 콜백을 파라미터로 받습니다.
    public bool ProcessTags(List<string> tags, NPCData currentNPC, Action onComplete)
    {
        bool isBlocking = false;
        if (tags == null || tags.Count == 0) return false;

        string defaultNpcId = currentNPC != null ? currentNPC.id.ToString() : "Unknown";

        foreach (string tag in tags)
        {
            string[] split = tag.Split(':');
            if (split.Length < 2) continue;

            string command = split[0].Trim().ToLower();
            string targetId = defaultNpcId;
            string value = "";

            if (split.Length == 2)
            {
                value = split[1].Trim();
            }
            else if (split.Length >= 3)
            {
                targetId = split[1].Trim();
                value = split[2].Trim();
            }

            switch (command)
            {
                case "enter":
                    OnPortraitEnterRequested?.Invoke(targetId, value);
                    break;
                case "face":
                    OnPortraitFaceRequested?.Invoke(targetId, value);
                    break;
                case "emote":
                    OnPortraitEmoteRequested?.Invoke(targetId, value);
                    break;
                case "pos":
                case "move":
                    OnPortraitMoveRequested?.Invoke(targetId, value);
                    break;
                case "action":
                    OnPortraitActionRequested?.Invoke(targetId, value);
                    break;
                case "exit":
                    OnPortraitExitRequested?.Invoke(targetId);
                    break;
                case "feature":
                    OnFeatureRequested?.Invoke(value);
                    isBlocking = true;
                    break;
                case "add_aff":
                    if (int.TryParse(value, out int amount))
                    {
                        // [핵심] 이제 매니저를 찾지 않습니다! 허공에 방송만 합니다.
                        OnAffectionRequested?.Invoke(currentNPC, amount, onComplete);
                        isBlocking = true;
                    }
                    break;
                default:
                    Debug.LogWarning($"[TagHandler] 처리되지 않은 태그: {tag}");
                    break;
            }
        }
        return isBlocking;
    }
}
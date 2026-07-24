using System;
using System.Collections.Generic;
using UnityEngine;

public class DialogueTagHandler : MonoBehaviour
{
    public Action<string, string> OnPortraitEnterRequested;
    public Action<string, string> OnPortraitFaceRequested;
    public Action<string, string> OnPortraitEmoteRequested;
    public Action<string, string> OnPortraitMoveRequested;
    public Action<string, string> OnPortraitActionRequested;
    public Action<string> OnPortraitExitRequested;

    public Action<string, Action> OnFeatureRequested;
    public Action<NPCData, int, Action> OnAffectionRequested;
    public Action<Action> OnChoiceFailureRequested;

    public bool ProcessTags(List<string> tags, NPCData currentNPC, Action onComplete)
    {
        bool isBlocking = false;
        if (tags == null || tags.Count == 0) return false;

        string defaultNpcId = currentNPC != null ? currentNPC.id.ToString() : "Unknown";

        foreach (string tag in tags)
        {
            string[] split = tag.Split(':');
            if (split.Length < 1) continue;

            string command = split[0].Trim().ToLower();
            string targetId = defaultNpcId;
            string value = "";
            bool hasValue = split.Length >= 2;

            if (!hasValue && command != "speaker" && command != "choice_fail" && command != "aff_fail" && command != "fail_aff")
                continue;

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
                // [핵심 추가] Controller가 이미 처리한 태그는 경고를 띄우지 않고 쿨하게 무시합니다!
                case "speaker":
                case "anim":
                case "dialogue_anim":
                case "effect":
                case "camerashake":
                    break;

                case "enter": OnPortraitEnterRequested?.Invoke(targetId, value); break;
                case "face": OnPortraitFaceRequested?.Invoke(targetId, value); break;
                case "emote": OnPortraitEmoteRequested?.Invoke(targetId, value); break;
                case "pos":
                case "move": OnPortraitMoveRequested?.Invoke(targetId, value); break;
                case "action": OnPortraitActionRequested?.Invoke(targetId, value); break;
                case "exit": OnPortraitExitRequested?.Invoke(targetId); break;

                case "feature":
                    if (isBlocking)
                    {
                        Debug.LogWarning($"[TagHandler] 이미 blocking 태그를 처리 중이어서 추가 feature 태그를 무시합니다: {tag}");
                        break;
                    }

                    OnFeatureRequested?.Invoke(value, onComplete);
                    isBlocking = true;
                    break;

                case "add_aff":
                    if (isBlocking)
                    {
                        Debug.LogWarning($"[TagHandler] 이미 blocking 태그를 처리 중이어서 추가 add_aff 태그를 무시합니다: {tag}");
                        break;
                    }

                    if (int.TryParse(value, out int amount))
                    {
                        OnAffectionRequested?.Invoke(currentNPC, amount, onComplete);
                        isBlocking = true;
                    }
                    break;

                case "choice_fail":
                case "aff_fail":
                case "fail_aff":
                    if (isBlocking)
                    {
                        Debug.LogWarning($"[TagHandler] 이미 blocking 태그를 처리 중이어서 추가 choice_fail 태그를 무시합니다: {tag}");
                        break;
                    }

                    OnChoiceFailureRequested?.Invoke(onComplete);
                    isBlocking = true;
                    break;

                default:
                    Debug.LogWarning($"[TagHandler] 처리되지 않은 태그: {tag}");
                    break;
            }
        }
        return isBlocking;
    }
}

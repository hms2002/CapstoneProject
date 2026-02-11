using System.Collections.Generic;
using UnityEngine;

public class DialogueTagHandler : MonoBehaviour
{
    [SerializeField] private DialogueUIManager uiManager;
    [SerializeField] private NPCDatabase npcDatabase;

    public bool ProcessTags(List<string> tags, NPCData currentNPC)
    {
        bool isBlocking = false;
        if (tags == null) return false;

        foreach (string tag in tags)
        {
            string[] split = tag.Split(':');
            if (split.Length == 0) continue;

            string key = split[0].Trim().ToLower();
            string val = split.Length > 1 ? split[1].Trim() : "";

            // [디버그] 태그가 들어오는지 확인
            // Debug.Log($"[TagHandler] 태그 감지됨 - Key: {key}, Value: {val}");

            switch (key)
            {
                case "feature":
                    uiManager.ExecuteFeature(val);
                    isBlocking = true;
                    break;

                case "add_aff":
                    // [디버그] 호감도 태그 인식 확인
                    Debug.Log($"<color=yellow>[TagHandler] 호감도 태그 인식! 추가할 값: {val}</color>");

                    // 호감도 증가 시도
                    if (AffectionManager.Instance.AddAffection(currentNPC, int.Parse(val)))
                        isBlocking = true;
                    break;

                case "enter":
                    if (int.TryParse(val, out int enterId))
                    {
                        string pos = split.Length > 2 ? split[2].Trim() : "center";
                        uiManager.SpawnPortrait(npcDatabase.GetNPC(enterId), pos);
                    }
                    break;

                case "exit":
                    if (int.TryParse(val, out int exitId)) uiManager.DespawnPortrait(exitId);
                    break;

                case "emote":
                    if (currentNPC != null)
                    {
                        var ctrl = uiManager.GetPortrait(currentNPC.id);
                        if (ctrl != null) ctrl.ShowEmote(val);
                    }
                    break;

                default:
                    HandlePortraitCommand(key, split, currentNPC);
                    break;
            }
        }
        return isBlocking;
    }

    private void HandlePortraitCommand(string key, string[] split, NPCData currentNPC)
    {
        if (int.TryParse(key, out int id))
        {
            var ctrl = uiManager.GetPortrait(id);
            if (ctrl != null && split.Length >= 3)
            {
                string cmd = split[1].Trim().ToLower();
                string val = split[2].Trim();
                if (cmd == "face") ctrl.SetExpression(val);
                else if (cmd == "emote") ctrl.ShowEmote(val);
                else if (cmd == "move") ctrl.MovePosition(val);
            }
        }
        else if (currentNPC != null && split.Length >= 2)
        {
            var ctrl = uiManager.GetPortrait(currentNPC.id);
            if (ctrl != null)
            {
                string val = split[1].Trim();
                if (key == "face") ctrl.SetExpression(val);
                else if (key == "emote") ctrl.ShowEmote(val);
            }
        }
    }
}
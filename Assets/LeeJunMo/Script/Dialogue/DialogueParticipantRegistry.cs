using System.Collections.Generic;

public sealed class DialogueParticipantRegistry
{
    private readonly Dictionary<int, NPCData> activeNPCs = new Dictionary<int, NPCData>();

    public NPCData CurrentNPCData { get; private set; }
    public int CurrentSpeakerId { get; private set; } = -1;
    public string CurrentSpeakerName { get; private set; } = string.Empty;

    public void Initialize(List<NPCData> participants)
    {
        activeNPCs.Clear();

        foreach (NPCData npc in participants)
        {
            if (npc == null || activeNPCs.ContainsKey(npc.id))
                continue;

            activeNPCs.Add(npc.id, npc);
        }

        SetCurrentNPCData(participants[0]);
    }

    public void Clear()
    {
        activeNPCs.Clear();
        CurrentNPCData = null;
        CurrentSpeakerId = -1;
        CurrentSpeakerName = string.Empty;
    }

    public void HandleSpeakerTag(List<string> currentTags)
    {
        foreach (string tag in currentTags)
        {
            string[] splitTag = tag.Split(':');
            if (splitTag.Length != 2)
                continue;

            if (splitTag[0].Trim().ToLower() != "speaker")
                continue;

            string speakerValue = splitTag[1].Trim();
            if (int.TryParse(speakerValue, out int id))
            {
                CurrentSpeakerId = id;
                NPCData data = GetOrLoadNPC(speakerValue);
                CurrentSpeakerName = data != null ? data.npcName : "???";
            }
            else
            {
                CurrentSpeakerId = -1;
                CurrentSpeakerName = speakerValue;
            }

            break;
        }
    }

    public NPCData GetOrLoadNPC(string idStr)
    {
        if (!int.TryParse(idStr, out int npcId))
            return null;

        if (activeNPCs.TryGetValue(npcId, out NPCData data))
            return data;

        NPCData loadedData = NPCManager.Instance?.GetNPCData(npcId);
        if (loadedData != null)
            activeNPCs.Add(npcId, loadedData);

        return loadedData;
    }

    private void SetCurrentNPCData(NPCData npcData)
    {
        CurrentNPCData = npcData;
        CurrentSpeakerId = npcData != null ? npcData.id : -1;
        CurrentSpeakerName = npcData != null ? npcData.npcName : string.Empty;
    }
}

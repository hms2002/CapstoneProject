using System.Collections.Generic;

public readonly struct AffectionChangeResult
{
    public readonly int NpcId;
    public readonly int PreviousAmount;
    public readonly int NewAmount;
    public readonly int Delta;

    public AffectionChangeResult(int npcId, int previousAmount, int newAmount, int delta)
    {
        NpcId = npcId;
        PreviousAmount = previousAmount;
        NewAmount = newAmount;
        Delta = delta;
    }
}

public sealed class AffectionProgressStore
{
    private readonly Dictionary<int, int> affectionByNpcId = new Dictionary<int, int>();

    public void Load(GameData data)
    {
        affectionByNpcId.Clear();

        if (data == null)
            return;

        data.affectionData ??= new AffectionSaveData();
        data.affectionData.affectionRecords ??= new List<AffectionRecord>();

        foreach (AffectionRecord record in data.affectionData.affectionRecords)
        {
            affectionByNpcId[record.npcId] = record.amount;
        }
    }

    public int GetAffection(int npcId)
    {
        return affectionByNpcId.TryGetValue(npcId, out int amount) ? amount : 0;
    }

    public AffectionChangeResult AddAffection(GameData data, int npcId, int amount, bool syncToGameData = true)
    {
        int previousAmount = GetAffection(npcId);
        int newAmount = previousAmount + amount;

        affectionByNpcId[npcId] = newAmount;
        if (syncToGameData)
            SyncToGameData(data, npcId, newAmount);

        return new AffectionChangeResult(npcId, previousAmount, newAmount, amount);
    }

    public void SetAffection(GameData data, int npcId, int amount, bool syncToGameData = true)
    {
        affectionByNpcId[npcId] = amount;
        if (syncToGameData)
            SyncToGameData(data, npcId, amount);
    }

    private static void SyncToGameData(GameData data, int npcId, int amount)
    {
        if (data == null)
            return;

        data.affectionData ??= new AffectionSaveData();
        data.affectionData.affectionRecords ??= new List<AffectionRecord>();

        List<AffectionRecord> records = data.affectionData.affectionRecords;
        AffectionRecord record = records.Find(x => x.npcId == npcId);
        if (record != null)
            record.amount = amount;
        else
            records.Add(new AffectionRecord(npcId, amount));
    }
}

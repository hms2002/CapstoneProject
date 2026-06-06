using System.Collections.Generic;
using UnityEngine;

public sealed class LootTableResolver
{
    private readonly List<StageLootTable> stageTables;
    private readonly GraveLootTable graveLootTable;

    public LootTableResolver(List<StageLootTable> stageTables, GraveLootTable graveLootTable)
    {
        this.stageTables = stageTables;
        this.graveLootTable = graveLootTable;
    }

    public StageLootTable GetCurrentTable(int currentStageIndex)
    {
        if (stageTables == null || stageTables.Count == 0)
            return null;

        int idx = Mathf.Clamp(currentStageIndex, 0, stageTables.Count - 1);
        return stageTables[idx];
    }

    public GraveLootTable GetGraveLootTable()
    {
        return graveLootTable;
    }
}

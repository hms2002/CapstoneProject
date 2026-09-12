using System;

/// <summary>Compares actual placed-room repetition before corridor aesthetics, without owning generation or serialized content.</summary>
public readonly struct DungeonTemplateRepetitionMetrics : IComparable<DungeonTemplateRepetitionMetrics>
{
    public int AdjacentTemplates { get; }
    public int AdjacentShapes { get; }
    public int NearbyTemplates { get; }
    public int NearbyShapes { get; }
    public bool IsRepeatFree => AdjacentTemplates + AdjacentShapes + NearbyTemplates + NearbyShapes == 0;

    public DungeonTemplateRepetitionMetrics(int adjacentTemplates, int adjacentShapes, int nearbyTemplates, int nearbyShapes)
    {
        AdjacentTemplates = adjacentTemplates; AdjacentShapes = adjacentShapes;
        NearbyTemplates = nearbyTemplates; NearbyShapes = nearbyShapes;
    }

    public int CompareTo(DungeonTemplateRepetitionMetrics other)
    {
        int result = AdjacentTemplates.CompareTo(other.AdjacentTemplates);
        if (result == 0) result = AdjacentShapes.CompareTo(other.AdjacentShapes);
        if (result == 0) result = NearbyTemplates.CompareTo(other.NearbyTemplates);
        if (result == 0) result = NearbyShapes.CompareTo(other.NearbyShapes);
        return result;
    }

    public override string ToString() =>
        $"Adjacent same room={AdjacentTemplates}, shape={AdjacentShapes}; nearby same room={NearbyTemplates}, shape={NearbyShapes}";
}

/// <summary>Exposes bounded-search diagnostics for one physically valid template assignment; not saved into room assets or run snapshots.</summary>
public sealed class DungeonTemplateSelectionReport
{
    public DungeonTemplateRepetitionMetrics Metrics { get; }
    public int RelaxationPhase { get; }
    public int SearchSteps { get; }
    public string Description { get; }

    internal DungeonTemplateSelectionReport(DungeonTemplateRepetitionMetrics metrics, int phase, int steps, string description)
    {
        Metrics = metrics; RelaxationPhase = phase; SearchSteps = steps; Description = description;
    }
}

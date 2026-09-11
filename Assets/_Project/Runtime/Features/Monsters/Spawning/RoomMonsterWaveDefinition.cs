using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns the stable identity, label and pre-spawn delay of one authored room wave.</summary>
[Serializable]
public struct RoomMonsterWaveDefinition
{
    public const string DefaultId = "wave_default";
    public string id;
    public string displayName;
    [Min(0f)] public float startDelaySeconds;

    public static string ResolveId(string value) => string.IsNullOrWhiteSpace(value) ? DefaultId : value;

    public static List<RoomMonsterWaveDefinition> CopyOrDefault(IReadOnlyList<RoomMonsterWaveDefinition> source)
    {
        if (source != null && source.Count > 0)
            return new List<RoomMonsterWaveDefinition>(source);

        return new List<RoomMonsterWaveDefinition>
        {
            new() { id = DefaultId, displayName = "Wave 1" }
        };
    }
}

using System;
using UnityEngine;

namespace UnityGAS
{
    public enum MonsterStatusValueKind { Stacks = 0, Seconds = 1 }

    /// <summary>Target-owned status contract. Views read values; only the source changes gameplay.</summary>
    public interface IMonsterStatusSource
    {
        string StatusId { get; }
        MonsterStatusValueKind ValueKind { get; }
        float DisplayValue { get; }
        Color DisplayColor { get; }
        bool IsActive { get; }
        event Action PulseRequested;
        void Clear();
    }
}

using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Read-only view of an AttributeValue.
    /// Use this in formula/AI/UI code to prevent accidental mutations (SetBaseValue/AddModifier).
    /// </summary>
    public interface IReadOnlyAttributeValue
    {
        AttributeDefinition Definition { get; }
        float BaseValue { get; }
        float CurrentValue { get; }
    }
}

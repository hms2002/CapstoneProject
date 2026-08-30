using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 유물·장비 변경 경로에서 전체 Resources 탐색과 불필요한 Attribute 재계산이 다시 도입되지 않도록 검증한다.
/// </summary>
public sealed class CorePerformanceRegressionEditModeTests
{
    // 책임 : 테스트 modifier source를 서로 구분하기 위한 수명 독립 ScriptableObject 토큰이다.
    private sealed class ModifierSource : ScriptableObject
    {
    }

    [Test]
    public void LinkedValueCompensator_DoesNotUseLegacyResourcesPolicyLookup()
    {
        string sourcePath = Path.Combine(
            Application.dataPath,
            "_Project/Runtime/Core/Attributes/AttributeLinkedValueCompensator.cs");
        string source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Not.Contain("Resources.LoadAll"));
        Assert.That(source, Does.Not.Contain("AttributeLinkedValueCompensationPolicySO"));
        Assert.That(source, Does.Not.Contain("cachedPolicies"));
    }

    [Test]
    public void RemoveModifiersFromSource_UnknownSource_DoesNotReportAChange()
    {
        AttributeDefinition definition = CreateDefinition();
        ModifierSource appliedSource = ScriptableObject.CreateInstance<ModifierSource>();
        ModifierSource unknownSource = ScriptableObject.CreateInstance<ModifierSource>();

        try
        {
            var value = new AttributeValue(definition);
            value.AddModifier(new AttributeModifier(ModifierType.Flat, 5f, appliedSource));
            value.ForceRecalculate();

            bool changed = value.RemoveModifiersFromSource(unknownSource);

            Assert.That(changed, Is.False);
            Assert.That(value.CurrentValue, Is.EqualTo(15f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(unknownSource);
            Object.DestroyImmediate(appliedSource);
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void RemoveModifiersFromSource_MatchingSource_RemovesOnlyThatSourcesModifiers()
    {
        AttributeDefinition definition = CreateDefinition();
        ModifierSource removedSource = ScriptableObject.CreateInstance<ModifierSource>();
        ModifierSource retainedSource = ScriptableObject.CreateInstance<ModifierSource>();

        try
        {
            var value = new AttributeValue(definition);
            value.AddModifier(new AttributeModifier(ModifierType.Flat, 5f, removedSource));
            value.AddModifier(new AttributeModifier(ModifierType.Percent, 0.5f, retainedSource));
            value.ForceRecalculate();

            bool changed = value.RemoveModifiersFromSource(removedSource);
            value.ForceRecalculate();

            Assert.That(changed, Is.True);
            Assert.That(value.CurrentValue, Is.EqualTo(15f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(retainedSource);
            Object.DestroyImmediate(removedSource);
            Object.DestroyImmediate(definition);
        }
    }

    private static AttributeDefinition CreateDefinition()
    {
        AttributeDefinition definition = ScriptableObject.CreateInstance<AttributeDefinition>();
        definition.defaultBaseValue = 10f;
        definition.minValue = 0f;
        definition.maxValue = 1000f;
        return definition;
    }
}

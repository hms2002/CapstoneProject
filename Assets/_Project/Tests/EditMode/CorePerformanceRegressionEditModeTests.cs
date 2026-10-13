using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>
/// 책임 : 유물·장비 변경 경로에서 전체 Resources 탐색과 불필요한 Attribute 재계산이 다시 도입되지 않도록 검증한다.
/// </summary>
public sealed class CorePerformanceRegressionEditModeTests
{
    // 책임 : 테스트 modifier source를 서로 구분하기 위한 수명 독립 ScriptableObject 토큰이다.
    private sealed class ModifierSource : ScriptableObject
    {
    }

    private sealed class QueryEffect : GameplayEffect
    {
        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1) { }
        public override void Remove(GameObject target, GameObject instigator) { }
    }

    [Test]
    public void EffectQueries_PreserveFirstMatchExpiryAndUnityNullSourceSemantics()
    {
        var effect = ScriptableObject.CreateInstance<QueryEffect>();
        var target = new GameObject("EffectQueryTarget");
        var source = ScriptableObject.CreateInstance<ModifierSource>();
        try
        {
            var repository = new ActiveGameplayEffectRepository();
            var expired = new ActiveGameplayEffect(effect, target) { TimeRemaining = 0f, SourceObject = source };
            var active = new ActiveGameplayEffect(effect, target) { TimeRemaining = 5f, SourceObject = source };
            repository.Add(expired);
            repository.Add(active);
            Assert.AreSame(expired, repository.FindFirst(effect, target));
            Assert.AreSame(expired, repository.FindFirst(effect, target, source));
            Assert.IsTrue(repository.HasActive(effect, target));
            Assert.IsTrue(repository.HasActive(effect, target, source));
            active.TimeRemaining = 0f;
            Assert.IsFalse(repository.HasActive(effect, target, source));
            UnityEngine.Object.DestroyImmediate(source);
            Assert.AreSame(expired, repository.FindFirst(effect, target, null));
            Assert.IsNull(repository.FindFirst(null, target));
            Assert.IsFalse(repository.HasActive(effect, null));
        }
        finally
        {
            if (source != null) UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    [Test]
    public void EffectQueries_MeasureCapturingSearchAllocationAgainstOriginalLookup()
    {
        var effect = ScriptableObject.CreateInstance<QueryEffect>();
        var target = new GameObject("EffectAllocationTarget");
        try
        {
            var repository = new ActiveGameplayEffectRepository();
            var entry = new ActiveGameplayEffect(effect, target) { TimeRemaining = 5f };
            repository.Add(entry);
            var originalEntries = new List<ActiveGameplayEffect> { entry };
            for (int i = 0; i < 32; i++)
            {
                LegacyFindFirst(originalEntries, effect, target);
                repository.FindFirst(effect, target);
            }
            long calibration = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(new byte[4096]);
            bool counterAvailable = GC.GetAllocatedBytesForCurrentThread() - calibration >= 4096;
            const int repetitions = 1024;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < repetitions; i++) LegacyFindFirst(originalEntries, effect, target);
            long originalBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < repetitions; i++) repository.FindFirst(effect, target);
            long optimizedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreSame(entry, repository.FindFirst(effect, target));
            TestContext.WriteLine($"EFFECTALLOC,queries={repetitions},counter={counterAvailable},original_bytes={originalBytes},optimized_bytes={optimizedBytes}");
            if (counterAvailable)
                Assert.Less(optimizedBytes, originalBytes);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(effect);
        }
    }

    private static ActiveGameplayEffect LegacyFindFirst(List<ActiveGameplayEffect> entries, GameplayEffect effect, GameObject target)
        => entries.FirstOrDefault(candidate => candidate.Effect == effect && candidate.Target == target);

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

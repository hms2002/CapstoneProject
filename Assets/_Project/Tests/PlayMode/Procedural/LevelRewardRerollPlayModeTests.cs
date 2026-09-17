#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class LevelRewardRerollPlayModeTests
{
    private readonly List<LevelRewardDefinitionSO> definitions = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < definitions.Count; i++)
            Object.DestroyImmediate(definitions[i]);
        definitions.Clear();
    }

    [TestCase(6, 0)]
    [TestCase(5, 1)]
    [TestCase(4, 2)]
    [TestCase(3, 3)]
    public void FillCandidateIds_MaximizesNovelCardsBeforeBackfillingPreviousCards(
        int eligibleCount,
        int expectedPreviousCount)
    {
        for (int i = 0; i < eligibleCount; i++)
            definitions.Add(CreateDefinition(((char)('A' + i)).ToString()));

        var previous = new HashSet<string>(new[] { "A", "B", "C" });
        var result = new List<string>();
        MethodInfo fill = typeof(RunLevelRewardOffers).GetMethod(
            "FillCandidateIds",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(fill, Is.Not.Null);
        fill.Invoke(null, new object[] { definitions, previous, result });

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(new HashSet<string>(result), Has.Count.EqualTo(3));
        Assert.That(result.FindAll(previous.Contains), Has.Count.EqualTo(expectedPreviousCount));
    }

    private LevelRewardDefinitionSO CreateDefinition(string rewardId)
    {
        LevelRewardDefinitionSO definition = ScriptableObject.CreateInstance<LevelRewardDefinitionSO>();
        typeof(LevelRewardDefinitionSO)
            .GetField("rewardId", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(definition, rewardId);
        return definition;
    }
}
#endif

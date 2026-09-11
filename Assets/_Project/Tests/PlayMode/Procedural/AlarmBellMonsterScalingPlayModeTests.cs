using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityGAS;

/// <summary>
/// Responsibility: verify bell-only attack-speed scaling preserves normal monsters,
/// shared authoring data and unrelated stats, with safe defaults for missing options.
/// </summary>
public sealed class AlarmBellMonsterScalingPlayModeTests
{
    private readonly List<Object> ownedObjects = new();
    private AlarmBellInteractable bell;
    private AttributeDefinition attackSpeed;
    private AttributeDefinition health;
    private AttributeDefinition moveSpeed;
    private AttributeDefinition attack;
    private AttributeInitProfileSO initProfile;

    [SetUp]
    public void SetUp()
    {
        var bellObject = Own(new GameObject("AlarmBellScalingTest", typeof(CircleCollider2D)));
        bell = bellObject.AddComponent<AlarmBellInteractable>();
        attackSpeed = CreateAttribute("AttackSpeedBase", 1f);
        health = CreateAttribute("Health", 60f);
        moveSpeed = CreateAttribute("MoveSpeed", 5f);
        attack = CreateAttribute("AttackBase", 10f);
        initProfile = Own(ScriptableObject.CreateInstance<AttributeInitProfileSO>());
        SetField(initProfile, "entries", new[]
        {
            new AttributeInitProfileSO.Entry { attribute = attackSpeed, baseValue = 1f },
            new AttributeInitProfileSO.Entry { attribute = health, baseValue = 60f },
            new AttributeInitProfileSO.Entry { attribute = moveSpeed, baseValue = 5f },
            new AttributeInitProfileSO.Entry { attribute = attack, baseValue = 10f }
        });
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = ownedObjects.Count - 1; i >= 0; i--)
        {
            if (ownedObjects[i] != null)
                Object.DestroyImmediate(ownedObjects[i]);
        }
        ownedObjects.Clear();
    }

    [TestCase(1.5f)]
    [TestCase(3f)]
    [TestCase(4.5f)]
    public void SpawnOptions_MultiplyAlreadyScaledSpeedForBellMonsterOnly(float previousSpeed)
    {
        AttributeSet elite = CreateMonster("Elite");
        AttributeSet normal = CreateMonster("Normal");
        Assert.That(elite.TrySetBaseValue(attackSpeed, previousSpeed, bell), Is.True);
        Assert.That(normal.TrySetBaseValue(attackSpeed, previousSpeed, bell), Is.True);
        var entry = new AlarmBellMonsterEntry();
        SetField(entry, "additionalAttackSpeedMultiplier", 1.5f);

        ApplyOptions(elite.gameObject, entry);

        Assert.That(elite.GetBaseValue(attackSpeed), Is.EqualTo(previousSpeed * 1.5f).Within(0.0001f));
        Assert.That(normal.GetBaseValue(attackSpeed), Is.EqualTo(previousSpeed));
        Assert.That(elite.GetBaseValue(health), Is.EqualTo(60f));
        Assert.That(elite.GetBaseValue(moveSpeed), Is.EqualTo(5f));
        Assert.That(elite.GetBaseValue(attack), Is.EqualTo(10f));
        Assert.That(initProfile.Entries[0].baseValue, Is.EqualTo(1f));
        Assert.That(attackSpeed.defaultBaseValue, Is.EqualTo(1f));
    }

    [Test]
    public void DefaultEntry_LeavesAttackSpeedUnchanged()
    {
        AttributeSet monster = CreateMonster("DefaultOptions");
        monster.TrySetBaseValue(attackSpeed, 3f, bell);
        var entry = new AlarmBellMonsterEntry();

        Assert.That(entry.AdditionalAttackSpeedMultiplier, Is.EqualTo(1f));
        ApplyOptions(monster.gameObject, entry);
        Assert.That(monster.GetBaseValue(attackSpeed), Is.EqualTo(3f));
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidMultiplier_FallsBackToNoChange(float multiplier)
    {
        var entry = new AlarmBellMonsterEntry();
        SetField(entry, "additionalAttackSpeedMultiplier", multiplier);
        Assert.That(entry.AdditionalAttackSpeedMultiplier, Is.EqualTo(1f));
    }

    [Test]
    public void SpawnOptions_TolerateMissingMonsterAttributesAndEntry()
    {
        var entry = new AlarmBellMonsterEntry();
        SetField(entry, "additionalAttackSpeedMultiplier", 1.5f);
        var empty = Own(new GameObject("NoAttributes"));

        Assert.DoesNotThrow(() => ApplyOptions(null, entry));
        Assert.DoesNotThrow(() => ApplyOptions(empty, null));
        Assert.DoesNotThrow(() => ApplyOptions(empty, entry));
        empty.AddComponent<AttributeSet>();
        Assert.DoesNotThrow(() => ApplyOptions(empty, entry));
    }

    private AttributeSet CreateMonster(string name)
    {
        var monster = Own(new GameObject(name));
        monster.SetActive(false);
        AttributeSet attributes = monster.AddComponent<AttributeSet>();
        SetField(attributes, "baseInitProfile", initProfile);
        monster.SetActive(true);
        return attributes;
    }

    private AttributeDefinition CreateAttribute(string name, float defaultValue)
    {
        var definition = Own(ScriptableObject.CreateInstance<AttributeDefinition>());
        definition.name = name;
        definition.attributeName = name;
        definition.defaultBaseValue = defaultValue;
        return definition;
    }

    private void ApplyOptions(GameObject monster, AlarmBellMonsterEntry entry)
    {
        MethodInfo method = typeof(AlarmBellInteractable).GetMethod(
            "ApplySpawnedMonsterOptions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(bell, new object[] { monster, entry });
    }

    private T Own<T>(T value) where T : Object
    {
        ownedObjects.Add(value);
        return value;
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }
}

#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Checks authored player binding and effect-driven arrow lifetime across source refresh, expiration and disable.</summary>
public sealed class AlcoholBuffPlayerVisualPlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type ViewType => Type.GetType("UnityGAS.AlcoholBuffPlayerVisual, Presentation", true);
    private GameObject target;
    private GameObject sourceA;
    private GameObject sourceB;
    private GameplayEffect effect;
    private GameplayEffectRunner runner;
    private MonoBehaviour view;

    [SetUp]
    public void Setup()
    {
        target = new GameObject("AlcoholBuffRecipient");
        target.AddComponent<AttributeSet>();
        runner = target.AddComponent<GameplayEffectRunner>();
        sourceA = new GameObject("PuddleA");
        sourceB = new GameObject("PuddleB");
        effect = AssetDatabase.LoadAssetAtPath<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_Puddle_AlcoholBuff.asset");
        view = (MonoBehaviour)target.AddComponent(ViewType);
        ViewType.GetField("alcoholBuff", Private).SetValue(view, effect);
        ViewType.GetField("arrowSprite", Private).SetValue(view,
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/UI/DownArrow.png"));
    }

    [TearDown]
    public void Cleanup()
    {
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(sourceA);
        Object.DestroyImmediate(sourceB);
    }

    private void Tick() => ViewType.GetMethod("LateUpdate", Private).Invoke(view, null);
    private void Apply(GameObject source) => runner.ApplyEffectSpec(
        new GameplayEffectSpec(effect, new GameplayEffectContext(target, source) { SourceObject = source }), target);

    [Test]
    public void ActualPlayerPrefabReferencesAlcoholAndArrow()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PF Player.prefab");
        Component authored = prefab.GetComponent(ViewType);
        Assert.That(authored, Is.Not.Null);
        Assert.That(ViewType.GetField("alcoholBuff", Private).GetValue(authored), Is.EqualTo(effect));
        Assert.That(ViewType.GetField("arrowSprite", Private).GetValue(authored), Is.Not.Null);
    }

    [Test]
    public void OverlappingSourcesReuseOneSetAndExpirationHidesIt()
    {
        Tick();
        Assert.That(target.GetComponentsInChildren<SpriteRenderer>(), Is.Empty);
        Apply(sourceA);
        Tick();
        var arrows = target.GetComponentsInChildren<SpriteRenderer>();
        Assert.That(arrows.Length, Is.EqualTo(3));
        foreach (var arrow in arrows) Assert.That(arrow.enabled, Is.True);
        Apply(sourceB);
        Apply(sourceA);
        Tick();
        Assert.That(target.GetComponentsInChildren<SpriteRenderer>(), Is.EqualTo(arrows));
        Assert.That(runner.ActiveEffects.Count, Is.EqualTo(1));
        runner.FindActiveEffect(effect, target).TimeRemaining = 0;
        Tick();
        foreach (var arrow in arrows) Assert.That(arrow.enabled, Is.False);
        runner.ClearAllActiveEffects();
        Apply(sourceB);
        Tick();
        Assert.That(target.GetComponentsInChildren<SpriteRenderer>(), Is.EqualTo(arrows));
        foreach (var arrow in arrows) Assert.That(arrow.enabled, Is.True);
    }

    [Test]
    public void SourceRemovalDoesNotHideRemainingBuffButCleanseAndDisableDo()
    {
        Apply(sourceA);
        Tick();
        var arrows = target.GetComponentsInChildren<SpriteRenderer>();
        Object.DestroyImmediate(sourceA);
        Tick();
        foreach (var arrow in arrows) Assert.That(arrow.enabled, Is.True);
        view.enabled = false;
        foreach (var arrow in arrows) Assert.That(arrow.enabled, Is.False);
        view.enabled = true;
        Tick();
        foreach (var arrow in arrows) Assert.That(arrow.enabled, Is.True);
        runner.ClearAllActiveEffects();
        Tick();
        foreach (var arrow in arrows) Assert.That(arrow.enabled, Is.False);
    }
}
#endif

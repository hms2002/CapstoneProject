#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

public sealed class ElectricReapplicationGaugePlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(false)]
    [TestCase(true)]
    public void DirectShock_ActiveRenewalsDoubleBeyondAuthoredClamp_AndStayTargetLocal(bool useSpec)
    {
        using var fixture = new Fixture();
        float[] expected = { 50f, 100f, 200f, 400f, 800f };
        foreach (float threshold in expected)
        {
            fixture.Direct(0, useSpec);
            Assert.That(fixture.Threshold(0), Is.EqualTo(threshold));
            Assert.That(fixture.Remaining(0), Is.EqualTo(4f));
            Assert.That(fixture.Threshold(1), Is.EqualTo(50f));
            Assert.That(fixture.Actors[0].Attributes.GetCurrentValue(fixture.ThresholdAttributes[0]), Is.EqualTo(50f),
                "Runtime escalation must not write/clamp the authored attribute.");
        }
        Assert.That(fixture.ThresholdAttributes[0].maxValue, Is.EqualTo(500f));
        fixture.Direct(1, useSpec);
        Assert.That(fixture.Threshold(1), Is.EqualTo(50f));
        fixture.Direct(1, useSpec);
        Assert.That(fixture.Threshold(1), Is.EqualTo(100f));
        Assert.That(fixture.Threshold(0), Is.EqualTo(800f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PopupMetadataUnsetOrCustom_DoesNotSelectTheGameplayGauge(bool customPopup)
    {
        using var fixture = new Fixture();
        GameplayTag popup = customPopup ? fixture.Elements[1] : null;
        Set(fixture.Trigger, "electricPopupElementTag", popup);
        fixture.Direct(0, useSpec: true);
        Assert.That(fixture.Threshold(0), Is.EqualTo(50f));
        fixture.Direct(0, useSpec: false);
        Assert.That(fixture.Threshold(0), Is.EqualTo(100f));
        Assert.That(fixture.Damage.Hits.Count, Is.EqualTo(2));
        foreach (var hit in fixture.Damage.Hits)
            Assert.That(hit.PopupTag, Is.SameAs(popup), "Popup metadata stays independent of the bound gameplay channel.");
        for (int i = 1; i < fixture.Elements.Length; i++)
        {
            Assert.That(fixture.Actors[0].Gauge.GetThreshold(fixture.Elements[i]), Is.EqualTo(50f));
            Assert.That(fixture.Actors[0].Gauge.GetValue(fixture.Elements[i]), Is.Zero);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ShockEnd_ResetsToLiveBase_AndNextFreshApplicationDoesNotDouble(bool naturalExpiry)
    {
        using var fixture = new Fixture();
        fixture.Direct(0);
        fixture.Direct(0);
        fixture.Direct(0);
        Assert.That(fixture.Threshold(0), Is.EqualTo(200f));
        fixture.Actors[0].Attributes.TrySetBaseValue(fixture.ThresholdAttributes[0], 75f, fixture.Owner);
        if (naturalExpiry) fixture.TickEffects(0, 4f);
        else fixture.Actors[0].Runner.RemoveEffect(fixture.Status, fixture.Actors[0].Object);
        Assert.That(fixture.Actors[0].Runner.HasActiveEffect(fixture.Status, fixture.Actors[0].Object), Is.False);
        Assert.That(fixture.Threshold(0), Is.EqualTo(75f));
        fixture.Direct(0, useSpec: !naturalExpiry);
        Assert.That(fixture.Threshold(0), Is.EqualTo(75f));
        fixture.Direct(0);
        Assert.That(fixture.Threshold(0), Is.EqualTo(150f));
    }

    [Test]
    public void LiveBaseAndCurrentModifiers_AffectCurrentRequirementAndItsNextDoubling()
    {
        using var fixture = new Fixture();
        var actor = fixture.Actors[0];
        var attribute = fixture.ThresholdAttributes[0];
        fixture.Direct(0);
        fixture.Direct(0);
        Assert.That(fixture.Threshold(0), Is.EqualTo(100f));
        Assert.That(actor.Attributes.TrySetBaseValue(attribute, 75f, fixture.Owner), Is.True);
        Assert.That(fixture.Threshold(0), Is.EqualTo(150f));
        var bonus = new AttributeModifier(ModifierType.Flat, 25f, fixture.Owner);
        Assert.That(actor.Attributes.TryAddModifier(attribute, bonus), Is.True);
        Assert.That(actor.Attributes.GetBaseValue(attribute), Is.EqualTo(75f));
        Assert.That(actor.Attributes.GetCurrentValue(attribute), Is.EqualTo(100f));
        Assert.That(fixture.Threshold(0), Is.EqualTo(200f));
        fixture.Direct(0);
        Assert.That(fixture.Threshold(0), Is.EqualTo(400f));
        Assert.That(actor.Attributes.TryRemoveModifier(attribute, bonus), Is.True);
        Assert.That(fixture.Threshold(0), Is.EqualTo(300f));
    }

    [TestCase(200f, 3, 200f, 0f)]
    [TestCase(500f, 4, 400f, 100f)]
    [TestCase(800f, 5, 800f, 0f)]
    public void ElectricOverflow_PaysEachChangedThresholdBeforeAnotherTrigger(float buildUp, int triggers, float threshold, float remainder)
    {
        using var fixture = new Fixture();
        var actor = fixture.Actors[0];
        actor.Gauge.AddBuildUp(fixture.Elements[0], buildUp, fixture.Owner, fixture.Owner);
        Assert.That(actor.TriggerCounts[fixture.Elements[0]], Is.EqualTo(triggers));
        Assert.That(fixture.Damage.Hits.Count, Is.EqualTo(triggers));
        Assert.That(fixture.Threshold(0), Is.EqualTo(threshold));
        Assert.That(actor.Gauge.GetValue(fixture.Elements[0]), Is.EqualTo(remainder));
        var models = new List<ElementGaugeUiModel>();
        Assert.That(actor.Gauge.GetGaugeUiModels(models), Is.EqualTo(4));
        var electric = models.Find(model => model.ElementTag == fixture.Elements[0]);
        Assert.That(electric.Current, Is.EqualTo(remainder));
        Assert.That(electric.Threshold, Is.EqualTo(threshold));
        Assert.That(electric.Ratio, Is.EqualTo(remainder / threshold));
        Assert.That(electric.Visible, Is.EqualTo(remainder > 0f));
        for (int i = 1; i < 4; i++)
            Assert.That(actor.Gauge.GetThreshold(fixture.Elements[i]), Is.EqualTo(50f));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void OtherElementOverflow_KeepsFixedThresholdAndExistingTriggerCount(int elementIndex)
    {
        using var fixture = new Fixture();
        var actor = fixture.Actors[0];
        var element = fixture.Elements[elementIndex];
        actor.Gauge.AddBuildUp(element, 150f, fixture.Owner, fixture.Owner);
        Assert.That(actor.TriggerCounts[element], Is.EqualTo(3));
        Assert.That(actor.Gauge.GetValue(element), Is.Zero);
        Assert.That(actor.Gauge.GetThreshold(element), Is.EqualTo(50f));
        Assert.That(fixture.Threshold(0), Is.EqualTo(50f));
        Assert.That(fixture.Damage.Hits, Is.Empty);
        actor.Gauge.AddBuildUp(element, 20f, fixture.Owner, fixture.Owner);
        Assert.That(actor.Gauge.GetValue(element), Is.EqualTo(20f));
    }

    [Test]
    public void OverflowCarryDisabled_StillTriggersOnlyOncePerAddedHit()
    {
        using var fixture = new Fixture();
        var actor = fixture.Actors[0];
        Set(actor.Gauge, "allowOverflowCarry", false);
        actor.Gauge.AddBuildUp(fixture.Elements[0], 999f, fixture.Owner, fixture.Owner);
        Assert.That(actor.TriggerCounts[fixture.Elements[0]], Is.EqualTo(1));
        Assert.That(fixture.Threshold(0), Is.EqualTo(50f));
        actor.Gauge.AddBuildUp(fixture.Elements[0], 999f, fixture.Owner, fixture.Owner);
        Assert.That(actor.TriggerCounts[fixture.Elements[0]], Is.EqualTo(2));
        Assert.That(fixture.Threshold(0), Is.EqualTo(100f));
        Assert.That(actor.Gauge.GetValue(fixture.Elements[0]), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GaugeClearOrRebuild_RemovesEscalationAndAccumulatedValue(bool rebuild)
    {
        using var fixture = new Fixture();
        var actor = fixture.Actors[0];
        fixture.Direct(0);
        fixture.Direct(0);
        fixture.Direct(0);
        actor.Gauge.AddBuildUp(fixture.Elements[0], 25f, fixture.Owner, fixture.Owner);
        if (rebuild) actor.Gauge.RebuildRuntimeStates();
        else actor.Gauge.ClearAll();
        Assert.That(fixture.Threshold(0), Is.EqualTo(50f));
        Assert.That(actor.Gauge.GetValue(fixture.Elements[0]), Is.Zero);
        fixture.Direct(0);
        Assert.That(fixture.Threshold(0), Is.EqualTo(100f), "The still-active shock is a genuine renewal, using the reset base.");
        actor.Runner.ClearAllActiveEffects();
        fixture.Direct(0);
        Assert.That(fixture.Threshold(0), Is.EqualTo(50f));
    }

    [TestCase("missing")]
    [TestCase("instant")]
    [TestCase("rejected")]
    public void UnsuccessfulStatusApplication_DoesNotDoubleAnExistingRequirement(string kind)
    {
        using var fixture = new Fixture();
        fixture.Direct(0);
        fixture.Direct(0);
        GameplayEffect unavailable = null;
        if (kind == "instant")
        {
            unavailable = fixture.Own(Object.Instantiate(fixture.Status));
            unavailable.duration = 0f;
        }
        else if (kind == "rejected")
        {
            unavailable = fixture.Own(ScriptableObject.CreateInstance<RejectingStatus>());
            unavailable.duration = 4f;
            unavailable.grantedTags.Add(fixture.Status.grantedTags[0]);
        }
        Set(fixture.Trigger, "electrocutedStatusEffect", unavailable);
        fixture.Direct(0);
        Assert.That(fixture.Threshold(0), Is.EqualTo(100f));
        Set(fixture.Trigger, "electrocutedStatusEffect", fixture.Status);
        fixture.Direct(0);
        Assert.That(fixture.Threshold(0), Is.EqualTo(200f), "The rejected attempt must not be counted as a renewal.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DeadOrInactiveTarget_CannotIncreaseTheRequirement(bool dead)
    {
        using var fixture = new Fixture();
        fixture.Direct(0);
        fixture.Direct(0);
        var actor = fixture.Actors[0];
        if (dead) actor.State.IsDead = true;
        else actor.Object.SetActive(false);
        int hits = fixture.Damage.Hits.Count;
        fixture.Direct(0);
        Assert.That(fixture.Threshold(0), Is.EqualTo(50f));
        Assert.That(fixture.Damage.Hits.Count, Is.EqualTo(hits));
    }

    [Test]
    public void ExternalOrRestoredActiveShock_IsRenewedFromBase_WithoutInheritingOldEscalation()
    {
        using var fixture = new Fixture();
        var actor = fixture.Actors[0];
        actor.Runner.ApplyEffectSpec(new GameplayEffectSpec(fixture.Status, fixture.Owner), actor.Object);
        Assert.That(fixture.Threshold(0), Is.EqualTo(50f));
        fixture.Direct(0);
        Assert.That(fixture.Threshold(0), Is.EqualTo(100f));
        fixture.Direct(0);
        Assert.That(fixture.Threshold(0), Is.EqualTo(200f));
        var snapshot = actor.Runner.CaptureActiveEffectSnapshots();
        actor.Runner.ClearAllActiveEffects();
        actor.Runner.RestoreActiveEffectSnapshots(snapshot, id => fixture.Status, actor.Object, fixture.Owner);
        Assert.That(fixture.Threshold(0), Is.EqualTo(50f));
        fixture.Direct(0);
        Assert.That(fixture.Threshold(0), Is.EqualTo(100f));
    }

    [UnityTest]
    public IEnumerator DecayAndUi_UseElevatedThreshold_ThenReturnToBaseWhenShockEnds()
    {
        using var fixture = new Fixture();
        var actor = fixture.Actors[0];
        fixture.Direct(0);
        fixture.Direct(0);
        actor.Gauge.AddBuildUp(fixture.Elements[0], 25f, fixture.Owner, fixture.Owner);
        yield return null;
        float dt = Time.deltaTime;
        Assert.That(dt, Is.GreaterThan(0f));
        Set(actor.Gauge, "decayDelaySeconds", 0f);
        Set(actor.Gauge, "decayPercentPerSecond", .1f);
        Set(actor.Gauge, "useDecay", true);
        typeof(ElementGaugeSystem).GetMethod("TickDecay", Private).Invoke(actor.Gauge, null);
        float remaining = Mathf.Max(0f, 25f - 100f * .1f * dt);
        Assert.That(actor.Gauge.GetValue(fixture.Elements[0]), Is.EqualTo(remaining).Within(.0001f));
        var models = new List<ElementGaugeUiModel>();
        actor.Gauge.GetGaugeUiModels(models);
        Assert.That(models.Find(model => model.ElementTag == fixture.Elements[0]).Ratio,
            Is.EqualTo(remaining / 100f).Within(.0001f));
        actor.Runner.ClearAllActiveEffects();
        typeof(ElementGaugeSystem).GetMethod("TickDecay", Private).Invoke(actor.Gauge, null);
        Assert.That(actor.Gauge.GetValue(fixture.Elements[0]),
            Is.EqualTo(Mathf.Max(0f, remaining - 50f * .1f * dt)).Within(.0001f));
        actor.Gauge.GetGaugeUiModels(models);
        Assert.That(models.Find(model => model.ElementTag == fixture.Elements[0]).Threshold, Is.EqualTo(50f));
    }

    [Test]
    public void ChainExtensionEchoAndNaturalExpiryJar_DoNotCountAsDirectReapplications()
    {
        using var fixture = new Fixture();
        fixture.Direct(1);
        fixture.Direct(1);
        fixture.TickEffects(1, 3f);
        fixture.Actors[2].Runner.ApplyEffectSpec(new GameplayEffectSpec(fixture.Status, fixture.Owner), fixture.Actors[2].Object);
        var extension = ScriptableObject.CreateInstance<RelicLogic_ChainShockExtension_Managed>();
        extension.electrocutedStatusEffect = fixture.Status;
        fixture.Equip(extension);
        fixture.Equip(ScriptableObject.CreateInstance<RelicLogic_ElectricEcho_Managed>());
        var jar = ScriptableObject.CreateInstance<RelicLogic_ElectricExpiryDamage_Managed>();
        jar.electrocutedStatusEffect = fixture.Status;
        jar.electricTriggerEffect = fixture.Trigger;
        fixture.Equip(jar);
        Set(fixture.Trigger, "chainRadius", 1.1f);
        Physics2D.SyncTransforms();
        fixture.Damage.Hits.Clear();
        fixture.Direct(0);
        Assert.That(fixture.Damage.Hits.Count, Is.EqualTo(5), "Three original hits and two follower-only Echo hits.");
        Assert.That(fixture.Threshold(0), Is.EqualTo(50f));
        Assert.That(fixture.Threshold(1), Is.EqualTo(100f));
        Assert.That(fixture.Threshold(2), Is.EqualTo(50f));
        Assert.That(fixture.Remaining(1), Is.EqualTo(1.5f));
        foreach (var actor in fixture.Actors)
            Assert.That(actor.Gauge.GetValue(fixture.Elements[0]), Is.Zero, "Damage-only chain/Echo never adds gauge build-up.");
        fixture.TickEffects(1, 2f);
        Assert.That(fixture.Damage.Hits.Count, Is.EqualTo(6));
        Assert.That(fixture.Damage.Hits[^1].Damage, Is.EqualTo(4f));
        Assert.That(fixture.Threshold(1), Is.EqualTo(50f));
        Assert.That(fixture.Actors[1].Runner.HasActiveEffect(fixture.Status, fixture.Actors[1].Object), Is.False);
        Assert.That(fixture.Actors[1].Gauge.GetValue(fixture.Elements[0]), Is.Zero);
    }

    public sealed class RejectingStatus : GameplayEffect
    {
        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1) =>
            target.GetComponent<GameplayEffectRunner>().RemoveEffect(this, target);
        public override void Remove(GameObject target, GameObject instigator) { }
    }
    public sealed class RecordingDamage : GameplayEffect, ISpecGameplayEffect
    {
        public GameplayTag Key;
        public readonly List<(GameObject Target, float Damage, GameplayTag PopupTag)> Hits = new();
        public void Apply(GameplayEffectSpec spec, GameObject target)
        {
            Assert.That(spec.TryGetSetByCallerMagnitude(Key, out float damage), Is.True);
            Hits.Add((target, damage, spec.Context?.DamagePopupElementTag));
        }
        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1) => Assert.Fail("Electric damage must retain its spec path.");
        public override void Remove(GameObject target, GameObject instigator) { }
    }
    private sealed class Actor
    {
        public GameObject Object;
        public AttributeSet Attributes;
        public GameplayEffectRunner Runner;
        public ElementGaugeSystem Gauge;
        public ElectricGaugeActorState State;
        public readonly Dictionary<GameplayTag, int> TriggerCounts = new();
    }
    private sealed class Fixture : IDisposable
    {
        private readonly List<Object> owned = new();
        private readonly List<(RelicLogic Logic, RelicContext Context)> equipped = new();
        public readonly GameObject Owner;
        public readonly AttributeSet SourceAttributes;
        public readonly AbilitySystem SourceSystem;
        public readonly GameplayEffect Status;
        public readonly GE_ElectricShockTrigger Trigger;
        public readonly RecordingDamage Damage;
        public readonly AttributeDefinition[] ThresholdAttributes = new AttributeDefinition[4];
        public readonly GameplayTag[] Elements = new GameplayTag[4];
        public readonly List<Actor> Actors = new();

        public Fixture()
        {
            Owner = Own(new GameObject("Electric gauge source"));
            Owner.SetActive(false);
            var electricBase = Load<AttributeDefinition>("Assets/_Project/Data/Attributes/Definitions/ElementAttribute/ElectricElementBaseAttribute.asset");
            var electricAdd = Load<AttributeDefinition>("Assets/_Project/Data/Attributes/Definitions/ElementAttribute/ElectricElementAddAttribute.asset");
            var electricMul = Load<AttributeDefinition>("Assets/_Project/Data/Attributes/Definitions/ElementAttribute/ElectricElementMulAttribute.asset");
            SourceAttributes = Owner.AddComponent<AttributeSet>();
            Set(SourceAttributes, "attributeCatalog", AttributeCatalog(electricBase, electricAdd, electricMul));
            Owner.AddComponent<TagSystem>();
            Owner.AddComponent<GameplayEffectRunner>();
            SourceSystem = Owner.AddComponent<AbilitySystem>();
            var stats = Owner.AddComponent<AttributeStatSource>();
            Set(stats, "statBindingsOverride", Load<StatTypeBindings>("Assets/_Project/Data/Attributes/Formulas/StatTypeBindings.asset"));
            Owner.SetActive(true);
            SourceAttributes.TrySetBaseValue(electricBase, 0f, Owner);
            SourceAttributes.TrySetBaseValue(electricMul, 1f, Owner);
            SourceAttributes.TrySetBaseValue(electricAdd, 10f, Owner);
            Assert.That(stats.Get(StatId.ElectricFinal), Is.EqualTo(10f));
            Status = Own(Object.Instantiate(Load<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_ElectrocutedStatus.asset")));
            Trigger = Own(Object.Instantiate(Load<GE_ElectricShockTrigger>("Assets/_Project/Data/Abilities/Effects/GE_ElectricShockTrigger.asset")));
            Damage = Own(ScriptableObject.CreateInstance<RecordingDamage>());
            Damage.Key = Resources.Load<GameplayTag>("Tags/Data.Damage");
            Set(Trigger, "electrocutedStatusEffect", Status);
            Set(Trigger, "electrocutedTag", Status.grantedTags[0]);
            Set(Trigger, "electricDamageEffect", Damage);
            Set(Trigger, "electricDamageKey", Damage.Key);
            Set(Trigger, "chainRadius", 0f);
            Set(Trigger, "chainVfxPrefab", null);
            var catalog = Own(ScriptableObject.CreateInstance<ElementGaugeCatalog>());
            catalog.definitions = new ElementGaugeDefinition[4];
            string[] names = { "Electric", "Fire", "Poison", "Blood" };
            for (int i = 0; i < names.Length; i++)
            {
                var authored = Load<AttributeDefinition>("Assets/_Project/Data/Attributes/Definitions/ElementAttribute/Max" + names[i] + "ElementGaugeAttribute.asset");
                Assert.That(authored.defaultBaseValue, Is.EqualTo(50f));
                ThresholdAttributes[i] = Own(Object.Instantiate(authored));
                var definition = Own(Object.Instantiate(Load<ElementGaugeDefinition>("Assets/_Project/Data/Attributes/ElementGauges/" + names[i] + " Element Gauge Definition.asset")));
                Elements[i] = definition.elementTag;
                definition.maxGaugeAttribute = ThresholdAttributes[i];
                definition.triggerEffect = i == 0 ? Trigger : null;
                definition.triggerVfxPrefab = definition.sustainVfxPrefab = null;
                catalog.definitions[i] = definition;
            }
            Set(Trigger, "electricPopupElementTag", Elements[0]);
            var attributes = AttributeCatalog(ThresholdAttributes);
            for (int i = 0; i < 3; i++)
            {
                var target = Own(new GameObject("Electric gauge target " + i));
                target.SetActive(false);
                target.transform.position = new Vector3(4000f + i, 4000f, 0f);
                var actor = new Actor { Object = target, Attributes = target.AddComponent<AttributeSet>(),
                    Runner = target.AddComponent<GameplayEffectRunner>() };
                Set(actor.Attributes, "attributeCatalog", attributes);
                target.AddComponent<TagSystem>();
                actor.State = target.AddComponent<ElectricGaugeActorState>();
                actor.Gauge = target.AddComponent<ElementGaugeSystem>();
                Set(actor.Gauge, "catalog", catalog);
                Set(actor.Gauge, "useDecay", false);
                var collider = target.AddComponent<CircleCollider2D>();
                collider.radius = .05f;
                collider.isTrigger = true;
                target.AddComponent<CombatHurtbox2D>();
                target.SetActive(true);
                actor.Gauge.RebuildRuntimeStates();
                actor.Gauge.OnGaugeTriggered += element =>
                {
                    actor.TriggerCounts.TryGetValue(element, out int count);
                    actor.TriggerCounts[element] = count + 1;
                };
                Actors.Add(actor);
            }
        }
        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }
        private AttributeCatalogSO AttributeCatalog(params AttributeDefinition[] definitions)
        {
            var catalog = Own(ScriptableObject.CreateInstance<AttributeCatalogSO>());
            Set(catalog, "attributes", definitions);
            return catalog;
        }
        public T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        public void Direct(int index, bool useSpec = true)
        {
            if (useSpec) Trigger.Apply(new GameplayEffectSpec(Trigger, Owner), Actors[index].Object);
            else Trigger.Apply(Actors[index].Object, Owner);
        }
        public float Threshold(int index) => Actors[index].Gauge.GetThreshold(Elements[0]);
        public float Remaining(int index) => Actors[index].Runner.GetRemainingTime(Status, Actors[index].Object);
        public void TickEffects(int index, float seconds) => typeof(GameplayEffectRunner).GetMethod("TickEffects", Private)
            .Invoke(Actors[index].Runner, new object[] { seconds });
        public void Equip(RelicLogic logic)
        {
            Own(logic);
            var context = new RelicContext { owner = Owner, abilitySystem = SourceSystem, attributeSet = SourceAttributes,
                level = 1, token = Own(ScriptableObject.CreateInstance<RelicRuntimeToken>()) };
            logic.OnEquipped(context);
            equipped.Add((logic, context));
        }
        public void Dispose()
        {
            foreach (var entry in equipped) entry.Logic.OnUnequipped(entry.Context);
            foreach (var actor in Actors)
                if (actor.Object != null) actor.Runner.ClearAllActiveEffects();
            foreach (var value in owned)
                if (value is GameObject && value != null) Object.DestroyImmediate(value);
            foreach (var value in owned)
                if (value != null) Object.DestroyImmediate(value);
        }
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
}

public sealed class ElectricGaugeActorState : MonoBehaviour, ICombatActionState
{
    public bool IsDead { get; set; }
}
#endif

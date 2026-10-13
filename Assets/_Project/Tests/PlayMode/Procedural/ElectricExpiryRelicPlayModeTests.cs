#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

public sealed class ElectricExpiryRelicPlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void CurrentJar_IsOneLevelAuthoredRelic_WithFortyPercentElectricTooltip()
    {
        var definition = Load<RelicDefinition>("Assets/_Project/Data/Items/Relics/Definitions/RD_CurrentJar.asset");
        var database = Load<ItemDatabase>("Assets/_Project/Data/Items/ItemDatabase.asset");
        Assert.That(definition.maxLevel, Is.EqualTo(1));
        Assert.That(definition.dropLevel, Is.EqualTo(1));
        CollectionAssert.Contains(database.allRelics, definition);
        var logic = definition.logic as RelicLogic_ElectricExpiryDamage_Managed;
        Assert.That(logic, Is.Not.Null);
        Assert.That(logic.electricCoefficient, Is.EqualTo(0.4f));
        Assert.That(logic.electrocutedStatusEffect, Is.EqualTo(Load<GameplayEffect>(
            "Assets/_Project/Data/Abilities/Effects/GE_ElectrocutedStatus.asset")));
        Assert.That(logic.electricTriggerEffect, Is.EqualTo(Load<GE_ElectricShockTrigger>(
            "Assets/_Project/Data/Abilities/Effects/GE_ElectricShockTrigger.asset")));
        Assert.That(logic.BuildTooltip(definition, 1, default).effectText, Does.Contain("40%"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NaturalExpiry_UsesLiveElectricOnly_AfterStatusCleanup_AndDoesNotRepeat(bool useSpec)
    {
        using var fixture = new Fixture();
        fixture.SetElectric(4f);
        fixture.ApplyStatus(useSpec);
        fixture.SetElectric(25f);
        fixture.Damage.OnDamage = (spec, target) =>
        {
            Assert.That(fixture.Runner.HasActiveEffect(fixture.Status, target), Is.False);
            Assert.That(fixture.TargetTags.HasTag(fixture.ShockTag), Is.False);
            Assert.That(fixture.Status.Removes, Is.EqualTo(1));
        };
        fixture.Tick(4.1f);
        Assert.That(fixture.Damage.Hits, Has.Count.EqualTo(1));
        var hit = fixture.Damage.Hits[0];
        Assert.That(hit.Damage, Is.EqualTo(10f), "The live Electric value, without the initial discharge's flat five, supplies all damage.");
        Assert.That(hit.Target, Is.EqualTo(fixture.Target));
        Assert.That(hit.Spec.Context.Instigator, Is.EqualTo(fixture.Owner));
        Assert.That(hit.Spec.Context.Causer, Is.EqualTo(useSpec ? fixture.Causer : fixture.Owner));
        Assert.That(hit.Spec.Context.SourceObject, Is.EqualTo(fixture.Trigger));
        Assert.That(hit.Spec.Context.DamagePopupElementTag, Is.EqualTo(fixture.PopupTag));
        Assert.That(fixture.Status.Applies, Is.EqualTo(1));
        fixture.Tick(100f);
        Assert.That(fixture.Damage.Hits, Has.Count.EqualTo(1));
    }

    [Test]
    public void ZeroElectric_NaturalExpiry_HasNoFlatFallbackDamage()
    {
        using var fixture = new Fixture();
        fixture.ApplyStatus();
        fixture.SetElectric(0f);
        fixture.Tick(4.1f);
        Assert.That(fixture.Damage.Hits, Is.Empty);
        Assert.That(fixture.Runner.HasActiveEffect(fixture.Status, fixture.Target), Is.False);
    }

    [TestCase("remove")]
    [TestCase("clear")]
    [TestCase("reduce")]
    [TestCase("replacement")]
    public void ExplicitEndingOrReplacement_DoesNotEmitAnExpiryHit(string ending)
    {
        using var fixture = new Fixture();
        fixture.SetElectric(25f);
        fixture.ApplyStatus();
        switch (ending)
        {
            case "remove": fixture.Runner.RemoveEffect(fixture.Status, fixture.Target); break;
            case "clear": fixture.Runner.ClearAllActiveEffects(); break;
            case "reduce": fixture.Runner.ReduceRemainingTimeByGrantedTag(fixture.Target, fixture.ShockTag, 5f); break;
            case "replacement":
                fixture.Runner.RemoveEffect(fixture.Status, fixture.Target);
                fixture.ApplyStatus();
                break;
        }
        Assert.That(fixture.Damage.Hits, Is.Empty);
        fixture.Tick(ending == "replacement" ? 0.1f : 5f);
        Assert.That(fixture.Damage.Hits, Is.Empty);
    }

    [Test]
    public void Renewal_ResetsTheOldDeadline_AndKeepsTheLastApplyingOwnerAndCauser()
    {
        using var fixture = new Fixture();
        fixture.SetElectric(10f);
        fixture.ApplyStatus();
        fixture.Tick(3f);
        fixture.Runner.ApplyEffectSpec(new GameplayEffectSpec(fixture.Status, fixture.Owner, fixture.OtherOwner), fixture.Target);
        Assert.That(fixture.Damage.Hits, Is.Empty);
        fixture.Tick(1.1f);
        Assert.That(fixture.Damage.Hits, Is.Empty, "The replaced deadline cannot masquerade as natural expiry.");
        fixture.Tick(3f);
        Assert.That(fixture.Damage.Hits, Has.Count.EqualTo(1));
        Assert.That(fixture.Damage.Hits[0].Damage, Is.EqualTo(4f));
        Assert.That(fixture.Damage.Hits[0].Spec.Context.Causer, Is.EqualTo(fixture.OtherOwner));

        fixture.ApplyStatus();
        fixture.Runner.ApplyEffectSpec(new GameplayEffectSpec(fixture.Status, fixture.OtherOwner), fixture.Target);
        fixture.Tick(4.1f);
        Assert.That(fixture.Damage.Hits, Has.Count.EqualTo(1), "The owner's jar cannot claim another instigator's renewed shock.");
    }

    [Test]
    public void OtherStatusOrOtherInstigatorNaturalExpiry_IsIgnored()
    {
        using var fixture = new Fixture();
        fixture.SetElectric(25f);
        var other = fixture.NewStatus(4f);
        fixture.Runner.ApplyEffectSpec(new GameplayEffectSpec(other, fixture.Owner), fixture.Target);
        fixture.Tick(4.1f);
        fixture.Runner.ApplyEffectSpec(new GameplayEffectSpec(fixture.Status, fixture.OtherOwner), fixture.Target);
        fixture.Tick(4.1f);
        Assert.That(fixture.Damage.Hits, Is.Empty);
    }

    [TestCase("owner-dead")]
    [TestCase("target-dead")]
    [TestCase("owner-disabled")]
    [TestCase("target-disabled")]
    [TestCase("player-owner-death")]
    [TestCase("player-target-death")]
    public void DeadOrDisabledActors_DoNotReceiveAnExpiryDamageSideEffect(string actorState)
    {
        using var fixture = new Fixture();
        fixture.SetElectric(25f);
        fixture.ApplyStatus();
        switch (actorState)
        {
            case "owner-dead": fixture.OwnerState.Dead = true; break;
            case "target-dead": fixture.TargetState.Dead = true; break;
            case "owner-disabled": fixture.Owner.SetActive(false); break;
            case "target-disabled": fixture.Target.SetActive(false); break;
            case "player-owner-death": SetPlayerDeathFlag(fixture.Owner); break;
            case "player-target-death": SetPlayerDeathFlag(fixture.Target); break;
        }
        fixture.Tick(4.1f);
        Assert.That(fixture.Damage.Hits, Is.Empty);
    }

    [Test]
    public void UnequipReapplyRestoreAndManagerDestruction_OwnExactlyOneFutureExpirySubscription()
    {
        using var fixture = new Fixture();
        fixture.SetElectric(10f);
        fixture.ApplyStatus();
        fixture.Logic.OnUnequipped(fixture.Context);
        fixture.Tick(4.1f);
        Assert.That(fixture.Damage.Hits, Is.Empty);
        fixture.Logic.OnEquipped(fixture.Context);
        fixture.Logic.OnEquipped(fixture.Context);
        fixture.Logic.OnRestoreAttached(fixture.Context);
        fixture.Logic.OnRestoreAttached(fixture.Context);
        Assert.That(fixture.Manager.RegisteredProcs, Has.Count.EqualTo(1));
        Assert.That(fixture.Damage.Hits, Is.Empty, "Restoring hooks must not replay a completed expiry.");
        fixture.ApplyStatus();
        fixture.Tick(4.1f);
        Assert.That(fixture.Damage.Hits, Has.Count.EqualTo(1));
        fixture.ApplyStatus();
        Object.DestroyImmediate(fixture.Manager);
        fixture.Tick(4.1f);
        Assert.That(fixture.Damage.Hits, Has.Count.EqualTo(1));
    }

    [Test]
    public void RestoringRemainingShock_IsSilent_AndZeroRemainingRestoreNeverTriggersLater()
    {
        using var fixture = new Fixture();
        fixture.SetElectric(10f);
        fixture.ApplyStatus();
        fixture.Tick(1f);
        var snapshots = fixture.Runner.CaptureActiveEffectSnapshots();
        Assert.That(fixture.Status.name, Is.Not.Empty);
        Assert.That(snapshots, Has.Count.EqualTo(1));
        Assert.That(snapshots[0].effectId, Is.EqualTo(fixture.Status.name));
        Assert.That(snapshots[0].remainingTime, Is.EqualTo(3f).Within(0.0001f));
        fixture.Logic.OnRestoreDetached(fixture.Context);
        fixture.Runner.ClearAllActiveEffects();
        fixture.Logic.OnRestoreAttached(fixture.Context);
        fixture.Runner.RestoreActiveEffectSnapshots(snapshots,
            id => id == fixture.Status.name ? fixture.Status : null, fixture.Target, fixture.Owner);
        Assert.That(fixture.Runner.HasActiveEffect(fixture.Status, fixture.Target), Is.True);
        Assert.That(fixture.Runner.GetRemainingTime(fixture.Status, fixture.Target), Is.EqualTo(3f).Within(0.0001f));
        Assert.That(fixture.Damage.Hits, Is.Empty);
        fixture.Tick(3.1f);
        Assert.That(fixture.Damage.Hits, Has.Count.EqualTo(1), "A restored positive timer may later reach its real natural deadline.");
        fixture.Runner.RestoreActiveEffectSnapshots(new[]
        {
            new ActiveGameplayEffectSnapshot { effectId = fixture.Status.name, remainingTime = 0f, stackCount = 1 }
        }, _ => fixture.Status, fixture.Target, fixture.Owner);
        Assert.That(fixture.Runner.HasActiveEffect(fixture.Status, fixture.Target), Is.False);
        Assert.That(fixture.Damage.Hits, Has.Count.EqualTo(1));
        fixture.Tick(0.1f);
        Assert.That(fixture.Damage.Hits, Has.Count.EqualTo(1), "Zero-time restore is explicit cleanup, not a new natural expiry.");
    }

    [Test]
    public void ExpiryDamageMayClearOtherEffects_AfterRepositoryIterationHasFinished()
    {
        using var fixture = new Fixture();
        fixture.SetElectric(10f);
        fixture.ApplyStatus();
        var other = fixture.NewStatus(10f);
        fixture.Runner.ApplyEffectSpec(new GameplayEffectSpec(other, fixture.Owner), fixture.Target);
        fixture.Damage.OnDamage = (_, _) =>
        {
            fixture.TargetState.Dead = true;
            fixture.Runner.ClearAllActiveEffects();
        };
        Assert.DoesNotThrow(() => fixture.Tick(4.1f));
        Assert.That(fixture.Damage.Hits, Has.Count.EqualTo(1));
        Assert.That(fixture.Runner.ActiveEffects, Is.Empty);
    }

    private static void SetPlayerDeathFlag(GameObject actor)
    {
        var death = actor.AddComponent<PlayerDeathReturnToHub2D>();
        typeof(PlayerDeathReturnToHub2D).GetField("isDeathSequenceRunning", Private).SetValue(death, true);
        Assert.That(death.IsDeathSequenceRunning, Is.True);
    }

    private static T Load<T>(string path) where T : Object
    {
        var value = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(value, Is.Not.Null, path);
        return value;
    }

    public sealed class ActorState : MonoBehaviour, ICombatActionState
    {
        public bool Dead;
        public bool IsDead => Dead;
    }

    public sealed class TrackedStatus : GameplayEffect
    {
        public int Applies, Removes;
        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1) => Applies++;
        public override void Remove(GameObject target, GameObject instigator) => Removes++;
    }

    public sealed class RecordingDamage : GameplayEffect, ISpecGameplayEffect
    {
        public GameplayTag MagnitudeKey;
        public readonly List<(GameObject Target, float Damage, GameplayEffectSpec Spec)> Hits = new();
        public Action<GameplayEffectSpec, GameObject> OnDamage;
        public void Apply(GameplayEffectSpec spec, GameObject target)
        {
            Assert.That(spec.TryGetSetByCallerMagnitude(MagnitudeKey, out float damage), Is.True);
            Hits.Add((target, damage, spec));
            OnDamage?.Invoke(spec, target);
        }
        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1) => Assert.Fail("Electric secondary damage must preserve its spec payload.");
        public override void Remove(GameObject target, GameObject instigator) { }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<Object> owned = new();
        public readonly GameObject Owner, Target, OtherOwner, Causer;
        public readonly ActorState OwnerState, TargetState;
        public readonly AttributeSet Attributes;
        public readonly AttributeDefinition ElectricAdd;
        public readonly GameplayEffectRunner Runner;
        public readonly TagSystem TargetTags;
        public readonly GameplayTag ShockTag, PopupTag;
        public readonly TrackedStatus Status;
        public readonly RecordingDamage Damage;
        public readonly GE_ElectricShockTrigger Trigger;
        public readonly RelicLogic_ElectricExpiryDamage_Managed Logic;
        public readonly RelicContext Context;
        public RelicProcManager Manager => Owner.GetComponent<RelicProcManager>();

        public Fixture()
        {
            var baseAttribute = Load<AttributeDefinition>("Assets/_Project/Data/Attributes/Definitions/ElementAttribute/ElectricElementBaseAttribute.asset");
            ElectricAdd = Load<AttributeDefinition>("Assets/_Project/Data/Attributes/Definitions/ElementAttribute/ElectricElementAddAttribute.asset");
            var multiplier = Load<AttributeDefinition>("Assets/_Project/Data/Attributes/Definitions/ElementAttribute/ElectricElementMulAttribute.asset");
            var catalog = Own(ScriptableObject.CreateInstance<AttributeCatalogSO>());
            typeof(AttributeCatalogSO).GetField("attributes", Private).SetValue(catalog, new[] { baseAttribute, ElectricAdd, multiplier });
            var profile = Own(ScriptableObject.CreateInstance<DamageProfileDefinition>());
            profile.statBindings = Load<StatTypeBindings>("Assets/_Project/Data/Attributes/Formulas/StatTypeBindings.asset");
            Owner = Own(new GameObject("Electric expiry owner"));
            Owner.SetActive(false);
            Attributes = Owner.AddComponent<AttributeSet>();
            typeof(AttributeSet).GetField("attributeCatalog", Private).SetValue(Attributes, catalog);
            Owner.AddComponent<TagSystem>();
            Owner.AddComponent<GameplayEffectRunner>();
            var system = Owner.AddComponent<AbilitySystem>();
            typeof(AbilitySystem).GetField("damageProfile", Private).SetValue(system, profile);
            OwnerState = Owner.AddComponent<ActorState>();
            Owner.SetActive(true);
            Attributes.TrySetBaseValue(baseAttribute, 0f, Owner);
            Attributes.TrySetBaseValue(multiplier, 1f, Owner);
            SetElectric(0f);
            Target = Own(new GameObject("Electric expiry target"));
            TargetState = Target.AddComponent<ActorState>();
            TargetTags = Target.AddComponent<TagSystem>();
            Runner = Target.AddComponent<GameplayEffectRunner>();
            OtherOwner = Own(new GameObject("Unrelated electric expiry source"));
            Causer = Own(new GameObject("Original electric causer"));
            var authoredStatus = Load<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_ElectrocutedStatus.asset");
            ShockTag = authoredStatus.grantedTags[0];
            Status = NewStatus(4f);
            Trigger = Own(Object.Instantiate(Load<GE_ElectricShockTrigger>("Assets/_Project/Data/Abilities/Effects/GE_ElectricShockTrigger.asset")));
            Damage = Own(ScriptableObject.CreateInstance<RecordingDamage>());
            Damage.MagnitudeKey = ShockTag;
            void Set(string field, object value) => typeof(GE_ElectricShockTrigger).GetField(field, Private).SetValue(Trigger, value);
            Set("electricDamageEffect", Damage);
            Set("electricDamageKey", Damage.MagnitudeKey);
            Set("chainVfxPrefab", null);
            PopupTag = (GameplayTag)typeof(GE_ElectricShockTrigger).GetField("electricPopupElementTag", Private).GetValue(Trigger);
            Assert.That(PopupTag, Is.Not.Null);
            Logic = Own(ScriptableObject.CreateInstance<RelicLogic_ElectricExpiryDamage_Managed>());
            Logic.electrocutedStatusEffect = Status;
            Logic.electricTriggerEffect = Trigger;
            Context = new RelicContext
            {
                owner = Owner, abilitySystem = system, attributeSet = Attributes, level = 1,
                token = Own(ScriptableObject.CreateInstance<RelicRuntimeToken>())
            };
            Logic.OnEquipped(Context);
        }

        public T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        public TrackedStatus NewStatus(float seconds)
        {
            var status = Own(ScriptableObject.CreateInstance<TrackedStatus>());
            status.name = $"ElectricExpiryFixtureStatus_{status.GetInstanceID()}";
            status.duration = seconds;
            status.grantedTags.Add(ShockTag);
            return status;
        }
        public void SetElectric(float value) => Assert.That(Attributes.TrySetBaseValue(ElectricAdd, value, Owner), Is.True);
        public void ApplyStatus(bool useSpec = true)
        {
            if (useSpec) Runner.ApplyEffectSpec(new GameplayEffectSpec(Status, Owner, Causer), Target);
            else Runner.ApplyEffect(Status, Target, Owner);
        }
        public void Tick(float seconds) => typeof(GameplayEffectRunner).GetMethod("TickEffects", Private).Invoke(Runner, new object[] { seconds });
        public void Dispose()
        {
            Logic.OnUnequipped(Context);
            Runner.ClearAllActiveEffects();
            foreach (var value in owned)
                if (value is GameObject && value != null) Object.DestroyImmediate(value);
            foreach (var value in owned)
                if (value != null && value is not GameObject) Object.DestroyImmediate(value);
        }
    }
}
#endif

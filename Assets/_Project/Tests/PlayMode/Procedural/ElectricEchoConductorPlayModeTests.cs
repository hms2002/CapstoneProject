#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

public sealed class ElectricEchoConductorPlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void Burst_UsesAdditiveStepsAfterModifiers_ThenOneOrderedFollowerReplay(bool useSpec, bool reversedRegistration)
    {
        using var fixture = new Fixture();
        fixture.UseCapturedPresentation();
        IDisposable modifier = null;
        if (reversedRegistration) modifier = ElectricDischargeDamageModifiers.Register(fixture.AddTwo);
        fixture.EquipBonuses();
        if (!reversedRegistration) modifier = ElectricDischargeDamageModifiers.Register(fixture.AddTwo);
        var extension = fixture.Equip(ScriptableObject.CreateInstance<RelicLogic_ChainShockExtension_Managed>());
        extension.electrocutedStatusEffect = fixture.Status;
        int applications = 0;
        using var listener = ElectricShockApplications.Register(context =>
        {
            if (context.Instigator == fixture.Owner) applications++;
        });
        try
        {
            for (int burst = 1; burst <= 2; burst++)
            {
                fixture.Damage.Hits.Clear();
                fixture.Presentations.Clear();
                fixture.Apply(useSpec);
                AssertOrdered(fixture, new[] { 17f, 14.4f, 16.8f, 19.2f, 19.2f, 19.2f }, echo: true);
                Assert.That(fixture.Presentations.Count, Is.EqualTo(2));
                Assert.That(fixture.Presentations[0].Replay, Is.False);
                Assert.That(fixture.Presentations[0].HitCount, Is.EqualTo(6));
                Assert.That(fixture.Presentations[1].Replay, Is.True);
                Assert.That(fixture.Presentations[1].HitCount, Is.EqualTo(11));
                for (int i = 0; i < 6; i++)
                    Assert.That(fixture.Presentations[1].Points[i], Is.EqualTo(fixture.Targets[i].transform.position),
                        "Replay retains the saved origin and follower order; the replay interface omits the origin snap.");
                Assert.That(applications, Is.EqualTo(burst), "Echo must not apply or renew shock/paralysis.");
                Assert.That(fixture.Remaining(0), Is.EqualTo(4f));
                for (int i = 1; i < 6; i++)
                    Assert.That(fixture.Remaining(i), Is.EqualTo(1.25f + i * .01f + .5f * burst).Within(.0001f),
                        "Only original follower hits may extend the existing shock duration.");
                Assert.That(fixture.Remaining(6), Is.EqualTo(1.31f).Within(.0001f));
            }
            Assert.That(fixture.Untagged.GetComponent<GameplayEffectRunner>().HasActiveEffect(fixture.Status, fixture.Untagged), Is.False);
            Assert.That(fixture.BaselineCap, Is.EqualTo(3), "Relics must not mutate the shared trigger cap.");
        }
        finally { modifier.Dispose(); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Snapshot_InitialStatusCanUnequipBonuses_WithoutChangingCurrentBurst(bool useSpec)
    {
        using var fixture = new Fixture();
        fixture.EquipBonuses();
        fixture.Targets[0].GetComponent<TagSystem>().OnTagAdded += tag =>
        {
            if (tag != fixture.Status.grantedTags[0]) return;
            fixture.Conductor.OnUnequipped(fixture.ConductorContext);
            fixture.Echo.OnUnequipped(fixture.EchoContext);
            Assert.That(fixture.Attributes.TrySetBaseValue(fixture.ElectricAttribute, 100f, fixture.Owner), Is.True);
        };
        fixture.Apply(useSpec);
        AssertOrdered(fixture, new[] { 15f, 12f, 14f, 16f, 16f, 16f }, echo: true);
        Assert.That(GE_ElectricShockTrigger.ReadElectricAttribute(fixture.Owner), Is.EqualTo(100f));

        fixture.Attributes.TrySetBaseValue(fixture.ElectricAttribute, 10f, fixture.Owner);
        fixture.Damage.Hits.Clear();
        fixture.Apply(useSpec);
        AssertOrdered(fixture, new[] { 15f, 10f, 10f, 10f, 10f, 10f }, echo: false);
    }

    [Test]
    public void Replay_UsesOnlyPositiveFollowers_AndSavedOriginWhenInitialTargetIsDestroyed()
    {
        using var fixture = new Fixture();
        fixture.UseCapturedPresentation();
        fixture.EquipBonuses();
        Vector3 originalPoint = fixture.Targets[0].transform.position;
        using var modifier = ElectricDischargeDamageModifiers.Register(context =>
            context.Target == fixture.Targets[1] ? 0f : context.BaseDamage);
        fixture.Damage.OnHit = hit =>
        {
            if (hit.Target != fixture.Targets[5]) return;
            fixture.Damage.OnHit = null;
            Object.DestroyImmediate(fixture.Targets[0]);
            fixture.Targets[2].AddComponent<ElectricEchoDeathProbe>().IsDead = true;
            fixture.Targets[2].GetComponent<GameplayEffectRunner>().ClearAllActiveEffects();
            fixture.Targets[3].SetActive(false);
            // Clearing shock on a surviving original recipient does not authorize a new search or status application.
            fixture.Targets[4].GetComponent<GameplayEffectRunner>().ClearAllActiveEffects();
        };
        fixture.Apply(useSpec: true);
        var hits = fixture.Damage.Hits;
        Assert.That(hits.Count, Is.EqualTo(7));
        int[] expected = { 0, 2, 3, 4, 5, 4, 5 };
        for (int i = 0; i < expected.Length; i++)
            Assert.That(hits[i].Target, Is.SameAs(fixture.Targets[expected[i]]));
        Assert.That(hits[5].Damage, Is.EqualTo(8f));
        Assert.That(hits[6].Damage, Is.EqualTo(8f));
        Assert.That(fixture.Remaining(2), Is.Zero, "Echo must not resurrect a death-cleared status.");
        Assert.That(fixture.Remaining(4), Is.Zero, "Echo is damage-only even after a cleanse.");
        Assert.That(fixture.Presentations.Count, Is.EqualTo(2));
        Assert.That(fixture.Presentations[1].Replay, Is.True);
        Assert.That(fixture.Presentations[1].Points, Is.EqualTo(new[] { originalPoint,
            fixture.Targets[4].transform.position, fixture.Targets[5].transform.position }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EachRelic_WorksIndependentlyWithTheBaselineThreeTargetCap(bool conductor)
    {
        using var fixture = new Fixture();
        if (conductor) fixture.Equip(ScriptableObject.CreateInstance<RelicLogic_ElectricChainDamage_Managed>());
        else fixture.Equip(ScriptableObject.CreateInstance<RelicLogic_ElectricEcho_Managed>());
        fixture.Apply(useSpec: true);
        AssertOrdered(fixture, conductor ? new[] { 15f, 12f, 14f } : new[] { 15f, 10f, 10f }, echo: !conductor);
        Assert.That(fixture.BaselineCap, Is.EqualTo(3));
    }

    [Test]
    public void Echo_NoChainSearch_DoesNotRepeatInitialDamageOrPresentation()
    {
        using var fixture = new Fixture();
        fixture.UseCapturedPresentation();
        fixture.Equip(ScriptableObject.CreateInstance<RelicLogic_ElectricEcho_Managed>());
        typeof(GE_ElectricShockTrigger).GetField("chainRadius", Private).SetValue(fixture.Discharge, 0f);
        fixture.Apply(useSpec: true);
        AssertOrdered(fixture, new[] { 15f }, echo: false);
        Assert.That(fixture.Remaining(1), Is.EqualTo(1.26f).Within(.0001f));
        Assert.That(fixture.Presentations.Count, Is.EqualTo(1));
        Assert.That(fixture.Presentations[0].Replay, Is.False);
        Assert.That(fixture.Presentations[0].Points.Count, Is.EqualTo(1));
    }

    [Test]
    public void AuthoredPresenter_ReplayRetainsSegmentsAndScale_WithoutInitialSnap()
    {
        var authored = AssetDatabase.LoadAssetAtPath<GE_ElectricShockTrigger>("Assets/_Project/Data/Abilities/Effects/GE_ElectricShockTrigger.asset");
        var prefab = (MonoBehaviour)typeof(GE_ElectricShockTrigger).GetField("chainVfxPrefab", Private).GetValue(authored);
        Assert.That(prefab, Is.InstanceOf<IChainReplayPresentation>());
        MonoBehaviour original = null, replay = null;
        try
        {
            original = Object.Instantiate(prefab);
            replay = Object.Instantiate(prefab);
            Vector3[] points = { new(2000f, 2000f, 0f), new(2001f, 2000f, 0f), new(2002f, 2000f, 0f) };
            ((IChainPointPresentation)original).Play(points);
            ((IChainReplayPresentation)replay).PlayReplay(points);
            List<SpriteRenderer> Read(MonoBehaviour instance, string field) =>
                (List<SpriteRenderer>)instance.GetType().GetField(field, Private).GetValue(instance);
            var originalSegments = Read(original, "activeSegments");
            var replaySegments = Read(replay, "activeSegments");
            var originalSnaps = Read(original, "activeSnaps");
            var replaySnaps = Read(replay, "activeSnaps");
            Assert.That(originalSegments.Count, Is.EqualTo(2));
            Assert.That(replaySegments.Count, Is.EqualTo(2), "Origin-to-first-follower segment is retained.");
            Assert.That(originalSnaps.Count, Is.EqualTo(3));
            Assert.That(replaySnaps.Count, Is.EqualTo(2));
            for (int i = 0; i < 2; i++)
            {
                Assert.That(replaySnaps[i].transform.position, Is.EqualTo(points[i + 1]));
                Assert.That(replaySnaps[i].transform.localScale, Is.EqualTo(originalSnaps[i + 1].transform.localScale));
                Assert.That(replaySnaps[i].color, Is.EqualTo(originalSnaps[i + 1].color));
                Assert.That(replaySegments[i].size, Is.EqualTo(originalSegments[i].size));
                Assert.That(replaySegments[i].transform.position, Is.EqualTo(originalSegments[i].transform.position));
            }
        }
        finally
        {
            if (original != null) Object.DestroyImmediate(original.gameObject);
            if (replay != null) Object.DestroyImmediate(replay.gameObject);
        }
    }

    [Test]
    public void AttachRestoreDetachAndManagerDestruction_PreserveOwnerIsolationAndRemoveHooks()
    {
        using var fixture = new Fixture();
        fixture.EquipBonuses();
        fixture.Conductor.OnRestoreAttached(fixture.ConductorContext);
        fixture.Conductor.OnRestoreAttached(fixture.ConductorContext);
        fixture.Echo.OnRestoreAttached(fixture.EchoContext);
        fixture.Echo.OnRestoreAttached(fixture.EchoContext);
        fixture.Apply(useSpec: true);
        AssertOrdered(fixture, new[] { 15f, 12f, 14f, 16f, 16f, 16f }, echo: true);
        fixture.Damage.Hits.Clear();
        fixture.Apply(useSpec: true, source: fixture.OtherSource);
        AssertOrdered(fixture, new[] { 5f, 5f, 5f }, echo: false);

        fixture.Conductor.OnRestoreDetached(fixture.ConductorContext);
        fixture.Echo.OnRestoreDetached(fixture.EchoContext);
        var conductorContext = fixture.ConductorContext;
        var echoContext = fixture.EchoContext;
        conductorContext.owner = fixture.OtherSource;
        echoContext.owner = fixture.OtherSource;
        fixture.Conductor.OnRestoreAttached(conductorContext);
        fixture.Echo.OnRestoreAttached(echoContext);
        fixture.Damage.Hits.Clear();
        fixture.Apply(useSpec: true, source: fixture.OtherSource);
        AssertOrdered(fixture, new[] { 5f, 6f, 7f }, echo: true);

        Object.DestroyImmediate(fixture.OtherSource.GetComponent<RelicProcManager>());
        fixture.Damage.Hits.Clear();
        fixture.Apply(useSpec: true, source: fixture.OtherSource);
        AssertOrdered(fixture, new[] { 5f, 5f, 5f }, echo: false);
    }

    [Test]
    public void SecondaryDamage_PreservesSpecActorsAndPopup_WithoutApplyingAnyBurstSideEffects()
    {
        using var fixture = new Fixture();
        fixture.EquipBonuses();
        int modifiers = 0, applications = 0;
        using var modifier = ElectricDischargeDamageModifiers.Register(context => { modifiers++; return context.BaseDamage + 100f; });
        using var listener = ElectricShockApplications.Register(context => applications++);
        var popup = AssetDatabase.LoadAssetAtPath<GameplayTag>("Assets/_Project/Resources/Tags/Element.Electric.asset");
        Assert.That(popup, Is.Not.Null);
        Assert.That(fixture.Discharge.ApplySecondaryElectricDamage(fixture.Targets[0], fixture.Owner, fixture.OtherSource, 7f, popup), Is.True);
        Assert.That(fixture.Damage.Hits.Count, Is.EqualTo(1));
        var hit = fixture.Damage.Hits[0];
        Assert.That(hit.Damage, Is.EqualTo(7f));
        Assert.That(hit.Context.Instigator, Is.SameAs(fixture.Owner));
        Assert.That(hit.Context.Causer, Is.SameAs(fixture.OtherSource));
        Assert.That(hit.Context.SourceObject, Is.SameAs(fixture.Discharge));
        Assert.That(hit.Context.DamagePopupElementTag, Is.SameAs(popup));
        Assert.That(modifiers, Is.Zero);
        Assert.That(applications, Is.Zero);
        Assert.That(fixture.Remaining(0), Is.Zero);
        Assert.That(fixture.Remaining(1), Is.EqualTo(1.26f).Within(.0001f));
        Assert.That(fixture.Discharge.ApplySecondaryElectricDamage(fixture.Targets[0], fixture.Owner, null, 0f), Is.False);
        Assert.That(fixture.Discharge.ApplySecondaryElectricDamage(fixture.Targets[0], fixture.Owner, null, float.NaN), Is.False);
        Assert.That(fixture.Damage.Hits.Count, Is.EqualTo(1));
    }

    private static void AssertOrdered(Fixture fixture, float[] damage, bool echo)
    {
        Assert.That(fixture.Damage.Hits.Count, Is.EqualTo(damage.Length + (echo ? damage.Length - 1 : 0)));
        for (int i = 0; i < fixture.Damage.Hits.Count; i++)
        {
            int index = i < damage.Length ? i : i - damage.Length + 1;
            var hit = fixture.Damage.Hits[i];
            Assert.That(hit.Target, Is.SameAs(fixture.Targets[index]));
            Assert.That(hit.Damage, Is.EqualTo(damage[index] * (i >= damage.Length ? .5f : 1f)).Within(.0001f));
        }
    }

    public sealed class RecordingDamage : GameplayEffect, ISpecGameplayEffect
    {
        public GameplayTag Key;
        public readonly List<Hit> Hits = new();
        public Action<Hit> OnHit;
        public readonly struct Hit
        {
            public Hit(GameObject target, float damage, GameplayEffectContext context) { Target = target; Damage = damage; Context = context; }
            public GameObject Target { get; }
            public float Damage { get; }
            public GameplayEffectContext Context { get; }
        }
        public void Apply(GameplayEffectSpec spec, GameObject target)
        {
            Assert.That(spec.TryGetSetByCallerMagnitude(Key, out float magnitude), Is.True);
            var hit = new Hit(target, magnitude, spec.Context);
            Hits.Add(hit);
            OnHit?.Invoke(hit);
        }
        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1) => Assert.Fail("Electric damage requires its spec path.");
        public override void Remove(GameObject target, GameObject instigator) { }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<Object> owned = new();
        private readonly List<(RelicLogic Logic, RelicContext Context)> equipped = new();
        public readonly GameObject Owner, OtherSource, Untagged;
        public readonly AttributeSet Attributes;
        public readonly AttributeDefinition ElectricAttribute;
        public readonly GE_ElectricShockTrigger Discharge;
        public readonly GameplayEffect Status;
        public readonly RecordingDamage Damage;
        public readonly List<GameObject> Targets = new();
        public readonly List<PresentationCall> Presentations = new();
        private Action<bool, IReadOnlyList<Vector3>, GameObject> capture;
        public RelicLogic_ElectricChainDamage_Managed Conductor;
        public RelicLogic_ElectricEcho_Managed Echo;
        public RelicContext ConductorContext, EchoContext;
        public int BaselineCap => (int)typeof(GE_ElectricShockTrigger).GetField("maxTargetsPerDischarge", Private).GetValue(Discharge);

        public Fixture()
        {
            Owner = Own(new GameObject("Echo conductor owner"));
            Owner.SetActive(false);
            ElectricAttribute = Own(ScriptableObject.CreateInstance<AttributeDefinition>());
            ElectricAttribute.defaultBaseValue = 10f;
            ElectricAttribute.maxValue = 1000f;
            var catalog = Own(ScriptableObject.CreateInstance<AttributeCatalogSO>());
            typeof(AttributeCatalogSO).GetField("attributes", Private).SetValue(catalog, new[] { ElectricAttribute });
            Attributes = Owner.AddComponent<AttributeSet>();
            typeof(AttributeSet).GetField("attributeCatalog", Private).SetValue(Attributes, catalog);
            var bindings = Own(ScriptableObject.CreateInstance<StatTypeBindings>());
            typeof(StatTypeBindings).GetField("bindings", Private).SetValue(bindings,
                new List<StatTypeBindings.Binding> { new() { id = StatId.ElectricFinal, attribute = ElectricAttribute } });
            typeof(AttributeStatSource).GetField("statBindingsOverride", Private).SetValue(Owner.AddComponent<AttributeStatSource>(), bindings);
            Owner.SetActive(true);
            OtherSource = Own(new GameObject("Other echo conductor source"));
            Status = Own(ScriptableObject.CreateInstance<GE_StatusOnlyDuration>());
            Status.duration = 4f;
            Status.grantedTags.Add(AssetDatabase.LoadAssetAtPath<GameplayEffect>("Assets/_Project/Data/Abilities/Effects/GE_ElectrocutedStatus.asset").grantedTags[0]);
            Discharge = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GE_ElectricShockTrigger>("Assets/_Project/Data/Abilities/Effects/GE_ElectricShockTrigger.asset")));
            Damage = Own(ScriptableObject.CreateInstance<RecordingDamage>());
            Damage.Key = Status.grantedTags[0];
            Set("electrocutedStatusEffect", Status);
            Set("electrocutedTag", Status.grantedTags[0]);
            Set("electricDamageEffect", Damage);
            Set("electricDamageKey", Damage.Key);
            Set("chainRadius", 1.1f);
            Set("chainVfxPrefab", null);
            for (int i = 0; i < 7; i++)
            {
                var target = CreateTarget(i);
                Targets.Add(target);
                if (i == 0) continue;
                target.GetComponent<GameplayEffectRunner>().ApplyEffectSpec(new GameplayEffectSpec(Status, Owner), target);
                target.GetComponent<GameplayEffectRunner>().FindActiveEffect(Status, target).TimeRemaining = 1.25f + .01f * i;
            }
            Untagged = CreateTarget(.25f);
            var child = Own(new GameObject("Duplicate chain hurtbox"));
            child.transform.SetParent(Targets[1].transform, false);
            AddHurtbox(child);
            Physics2D.SyncTransforms();
        }
        private void Set(string field, object value) => typeof(GE_ElectricShockTrigger).GetField(field, Private).SetValue(Discharge, value);
        private GameObject CreateTarget(float offset)
        {
            var target = Own(new GameObject("Echo conductor target"));
            target.transform.position = new Vector3(2000f + offset, 2000f, 0f);
            target.AddComponent<TagSystem>();
            target.AddComponent<GameplayEffectRunner>();
            AddHurtbox(target);
            return target;
        }
        private static void AddHurtbox(GameObject target)
        {
            var collider = target.AddComponent<CircleCollider2D>();
            collider.radius = .05f;
            collider.isTrigger = true;
            target.AddComponent<CombatHurtbox2D>();
        }
        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        public void UseCapturedPresentation()
        {
            var prefab = Own(new GameObject("Captured electric chain presentation"));
            prefab.SetActive(false);
            Set("chainVfxPrefab", prefab.AddComponent<ElectricEchoChainPresentationProbe>());
            capture = (replay, points, instance) =>
            {
                Own(instance);
                Presentations.Add(new PresentationCall(replay, new List<Vector3>(points), Damage.Hits.Count));
            };
            ElectricEchoChainPresentationProbe.OnPlay = capture;
        }
        public T Equip<T>(T logic) where T : RelicLogic
        {
            Own(logic);
            var context = new RelicContext { owner = Owner, attributeSet = Attributes, level = 1,
                token = Own(ScriptableObject.CreateInstance<RelicRuntimeToken>()) };
            logic.OnEquipped(context);
            equipped.Add((logic, context));
            return logic;
        }
        public void EquipBonuses()
        {
            var bell = ScriptableObject.CreateInstance<RelicLogic_ElectricChainTargets_Managed>();
            bell.additionalTargetsByLevel = new() { 3 };
            Equip(bell);
            Conductor = Equip(ScriptableObject.CreateInstance<RelicLogic_ElectricChainDamage_Managed>());
            ConductorContext = equipped[^1].Context;
            Echo = Equip(ScriptableObject.CreateInstance<RelicLogic_ElectricEcho_Managed>());
            EchoContext = equipped[^1].Context;
        }
        public float AddTwo(ElectricDischargeDamageModifierContext context) => context.BaseDamage + (context.Instigator == Owner ? 2f : 0f);
        public float Remaining(int index) => Targets[index].GetComponent<GameplayEffectRunner>().GetRemainingTime(Status, Targets[index]);
        public void Apply(bool useSpec, GameObject source = null)
        {
            source ??= Owner;
            if (useSpec) Discharge.Apply(new GameplayEffectSpec(Discharge, source), Targets[0]);
            else Discharge.Apply(Targets[0], source);
        }
        public void Dispose()
        {
            if (ElectricEchoChainPresentationProbe.OnPlay == capture) ElectricEchoChainPresentationProbe.OnPlay = null;
            foreach (var entry in equipped) entry.Logic.OnUnequipped(entry.Context);
            foreach (var target in Targets)
                if (target != null) target.GetComponent<GameplayEffectRunner>().ClearAllActiveEffects();
            foreach (var value in owned)
                if (value is GameObject && value != null) Object.DestroyImmediate(value);
            foreach (var value in owned)
                if (value != null) Object.DestroyImmediate(value);
        }
    }

    private readonly struct PresentationCall
    {
        public PresentationCall(bool replay, List<Vector3> points, int hitCount) { Replay = replay; Points = points; HitCount = hitCount; }
        public bool Replay { get; }
        public List<Vector3> Points { get; }
        public int HitCount { get; }
    }
}

public sealed class ElectricEchoDeathProbe : MonoBehaviour, ICombatActionState
{
    public bool IsDead { get; set; }
}

public sealed class ElectricEchoChainPresentationProbe : MonoBehaviour, IChainPointPresentation, IChainReplayPresentation
{
    public static Action<bool, IReadOnlyList<Vector3>, GameObject> OnPlay;
    public void Play(IReadOnlyList<Vector3> points) => OnPlay?.Invoke(false, points, gameObject);
    public void PlayReplay(IReadOnlyList<Vector3> points) => OnPlay?.Invoke(true, points, gameObject);
}
#endif

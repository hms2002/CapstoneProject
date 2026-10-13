#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

public sealed class ElectricBoundAttackPlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Type TimedEffectType = Type.GetType("UnityGAS.TimedAnimatedHitEffect2D, Presentation", true);

    [UnityTest]
    public IEnumerator NonparentedMelee_PreservesSharedHitIdsVisualSpeedAndRemainingLifetime()
    {
        using var fixture = new Fixture();
        var target = fixture.Target(fixture.Owner.transform.position);
        var actorObject = fixture.ObjectAt("Bound melee", target.transform.position);
        actorObject.AddComponent<BoxCollider2D>().isTrigger = true;
        var actor = actorObject.AddComponent<MeleeHitboxActor>();
        var visual = fixture.ObjectAt("Detached melee visual", target.transform.position);
        visual.transform.SetParent(actor.transform, true);
        var animator = visual.AddComponent<Animator>();
        animator.speed = 1.7f;
        var sharedIds = new HashSet<int>();
        CombatHitPause2D.ApplyParalysis(fixture.Owner, 0.35f);
        actor.Setup(new MeleeHitboxSpawnContext
        {
            ownerSystem = fixture.System, hitPayload = fixture.Payload, lifetime = 0.16f,
            worldPosition = target.transform.position, hitboxSize = Vector2.one,
            sharedHitTargetIds = sharedIds, ignoreTarget = fixture.Owner, damageLayers = 1 << Fixture.TargetLayer,
            overrideAttachToOwnerOnSetup = true, attachToOwnerOnSetup = false
        });
        Assert.That(actor.transform.parent, Is.Null, "The cast must remain unparented.");
        Assert.That(sharedIds, Is.Empty, "Setup's immediate scan must not consume IDs while paralyzed.");
        yield return null;
        Assert.That(animator.speed, Is.Zero);
        yield return new WaitForSeconds(0.19f);
        Assert.That(CombatHitPause2D.IsParalyzedOn(fixture.Owner), Is.True);
        Assert.That(actor != null, Is.True, "The melee lifetime must retain its remaining action time.");
        Assert.That(actor.GetComponent<Collider2D>().enabled, Is.True);
        Assert.That(fixture.Damage.Count(target), Is.Zero);
        Assert.That(sharedIds, Is.Empty);
        yield return WaitUntilResumed(fixture.Owner);
        typeof(AttackBase).GetMethod("OnTriggerStay2D", Private).Invoke(actor, new object[] { target.GetComponent<Collider2D>() });
        Assert.That(fixture.Damage.Count(target), Is.EqualTo(1));
        CollectionAssert.Contains(sharedIds, target.GetInstanceID());
        typeof(AttackBase).GetMethod("OnTriggerStay2D", Private).Invoke(actor, new object[] { target.GetComponent<Collider2D>() });
        Assert.That(fixture.Damage.Count(target), Is.EqualTo(1), "The existing shared hit identity must survive resume.");
        yield return null;
        Assert.That(animator.speed, Is.EqualTo(1.7f));
        Assert.That(actor != null, Is.True);
        yield return new WaitForSeconds(0.2f);
        Assert.That(actor == null, Is.True, "The same cast must expire after its remaining lifetime.");
    }

    [UnityTest]
    public IEnumerator NonparentedMeleeVisual_RestoresOriginalSpeedAfterOverlappingNormalHitPause()
    {
        using var fixture = new Fixture();
        var actorObject = fixture.ObjectAt("Bound visual overlap melee", fixture.Owner.transform.position);
        actorObject.AddComponent<BoxCollider2D>().isTrigger = true;
        var actor = actorObject.AddComponent<MeleeHitboxActor>();
        var animator = actorObject.AddComponent<Animator>();
        animator.speed = 1.4f;
        actor.Setup(new MeleeHitboxSpawnContext
        {
            ownerSystem = fixture.System, hitPayload = fixture.Payload, lifetime = 2f,
            worldPosition = fixture.Owner.transform.position, ignoreTarget = fixture.Owner
        });
        CombatHitPause2D.ApplyParalysis(fixture.Owner, 0.12f);
        yield return null;
        Assert.That(animator.speed, Is.Zero);
        CombatHitPause2D.FreezeVictim(fixture.Owner, 0.28f);
        yield return WaitUntilResumed(fixture.Owner);
        Assert.That(CombatHitPause2D.IsPausedOn(fixture.Owner), Is.True);
        yield return null;
        Assert.That(animator.speed, Is.Zero, "Finishing paralysis must not release overlapping normal hit pause.");
        float deadline = Time.realtimeSinceStartup + 2f;
        while (CombatHitPause2D.IsPausedOn(fixture.Owner) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(CombatHitPause2D.IsPausedOn(fixture.Owner), Is.False);
        yield return null;
        Assert.That(animator.speed, Is.EqualTo(1.4f));
    }

    [UnityTest]
    public IEnumerator NonparentedMeleeVisual_DestroyDeadlineExcludesParalysis()
    {
        using var fixture = new Fixture();
        var actorObject = fixture.ObjectAt("Bound visual lifetime melee", fixture.Owner.transform.position);
        actorObject.AddComponent<BoxCollider2D>().isTrigger = true;
        var actor = actorObject.AddComponent<MeleeHitboxActor>();
        actor.Setup(new MeleeHitboxSpawnContext
        {
            ownerSystem = fixture.System, hitPayload = fixture.Payload, lifetime = 2f,
            worldPosition = fixture.Owner.transform.position, ignoreTarget = fixture.Owner
        });
        var visual = fixture.ObjectAt("Bound self-collecting visual", actor.transform.position);
        visual.transform.SetParent(actor.transform, true);
        visual.SetActive(false);
        visual.AddComponent<Animator>();
        var player = visual.AddComponent<HitboxVisualAnimatorPlayer>();
        var clip = fixture.Own(new AnimationClip());
        clip.SetCurve(string.Empty, typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0f, 0f, 0.1f, 0f));
        typeof(HitboxVisualAnimatorPlayer).GetField("clip", Private).SetValue(player, clip);
        typeof(HitboxVisualAnimatorPlayer).GetField("playOnEnable", Private).SetValue(player, false);
        typeof(HitboxVisualAnimatorPlayer).GetField("restartOnEnable", Private).SetValue(player, false);
        typeof(HitboxVisualAnimatorPlayer).GetField("destroyOnComplete", Private).SetValue(player, true);
        visual.SetActive(true);
        CombatHitPause2D.ApplyParalysis(fixture.Owner, 0.3f);
        player.Play();
        yield return new WaitForSeconds(0.17f);
        Assert.That(visual != null, Is.True, "Self-collecting melee visuals must retain their clip lifetime during paralysis.");
        yield return WaitUntilResumed(fixture.Owner);
        yield return new WaitForSeconds(0.15f);
        Assert.That(visual == null, Is.True);
        Assert.That(actor != null, Is.True);
    }

    [UnityTest]
    public IEnumerator AttachedTimedEffect_PreservesRegistryAndLifetime_WhileDetachedEffectContinues()
    {
        using var fixture = new Fixture();
        var target = fixture.Target(fixture.Owner.transform.position);
        var registry = new SharedHitRegistry2D();
        var attached = fixture.TimedEffect(true, 0.14f, registry);
        var detached = fixture.TimedEffect(false, 0.08f, null);
        CombatHitPause2D.ApplyParalysis(fixture.Owner, 0.35f);
        TimedEffectType.GetMethod("EnableHitCollision").Invoke(attached, null);
        Assert.That(fixture.Damage.Count(target), Is.Zero);
        Assert.That(registry.Contains(target), Is.False);
        TimedEffectType.GetMethod("EnableHitCollision").Invoke(detached, null);
        Assert.That(fixture.Damage.Count(target), Is.EqualTo(1), "An already-emitted detached effect remains independent.");
        yield return new WaitForSeconds(0.19f);
        Assert.That(CombatHitPause2D.IsParalyzedOn(fixture.Owner), Is.True);
        Assert.That(attached != null, Is.True);
        Assert.That(detached == null, Is.True, "Independent world effect lifetime must continue.");
        Assert.That(registry.Contains(target), Is.False);
        Assert.That(fixture.Damage.Count(target), Is.EqualTo(1));
        yield return WaitUntilResumed(fixture.Owner);
        yield return null;
        yield return null;
        Assert.That(fixture.Damage.Count(target), Is.EqualTo(2), "The deferred attached overlap must apply after resume.");
        Assert.That(registry.Contains(target), Is.True);
        TimedEffectType.GetMethod("EnableHitCollision").Invoke(attached, null);
        Assert.That(fixture.Damage.Count(target), Is.EqualTo(2), "Resume must not clear the local/shared once-per-effect registry.");
        yield return new WaitForSeconds(0.18f);
        Assert.That(attached == null, Is.True);
    }

    [UnityTest]
    public IEnumerator AttachedSustainedArea_PreservesCooldown_WhileDetachedAreaKeepsTicking()
    {
        using var fixture = new Fixture();
        var target = fixture.Target(fixture.Owner.transform.position);
        var attached = fixture.Area(true, interval: 0.25f);
        attached.Play(fixture.Payload);
        Assert.That(fixture.Damage.Count(target), Is.EqualTo(1));
        yield return new WaitForSeconds(0.03f);
        CombatHitPause2D.ApplyParalysis(fixture.Owner, 0.35f);
        var detachedTarget = fixture.Target(fixture.Owner.transform.position + Vector3.right * 10f);
        var detached = fixture.Area(false, detachedTarget.transform.position);
        detached.Play(fixture.Payload);
        Assert.That(fixture.Damage.Count(detachedTarget), Is.EqualTo(1));
        yield return new WaitForSeconds(0.19f);
        Assert.That(CombatHitPause2D.IsParalyzedOn(fixture.Owner), Is.True);
        Assert.That(fixture.Damage.Count(target), Is.EqualTo(1));
        Assert.That(fixture.Damage.Count(detachedTarget), Is.GreaterThanOrEqualTo(2));
        yield return WaitUntilResumed(fixture.Owner);
        yield return new WaitForSeconds(0.03f);
        Assert.That(fixture.Damage.Count(target), Is.EqualTo(1), "Paralysis must preserve the periodic cooldown's remaining action time.");
        yield return new WaitForSeconds(0.3f);
        Assert.That(fixture.Damage.Count(target), Is.GreaterThanOrEqualTo(2));
    }

    [UnityTest]
    public IEnumerator IndependentProjectile_ContinuesMovementHitsAndLifetimeDuringOwnerParalysis()
    {
        using var fixture = new Fixture();
        var projectileObject = fixture.ObjectAt("Independent projectile", fixture.Owner.transform.position);
        projectileObject.AddComponent<CircleCollider2D>().isTrigger = true;
        var projectile = projectileObject.AddComponent<OddIronProjectile2D>();
        projectile.Setup(new ProjectileAttackSpawnContext
        {
            ownerSystem = fixture.System, hitPayload = fixture.Payload, direction = Vector2.right,
            speed = 4f, lifetime = 1f, damageLayers = 1 << Fixture.TargetLayer, ignoreTarget = fixture.Owner
        });
        var target = fixture.Target(fixture.Owner.transform.position + Vector3.right * 20f);
        CombatHitPause2D.ApplyParalysis(fixture.Owner, 0.4f);
        Vector3 before = projectile.transform.position;
        yield return new WaitForSeconds(0.06f);
        Assert.That(projectile.transform.position.x, Is.GreaterThan(before.x + 0.1f));
        Assert.That(projectile.ActionOwner, Is.Null);
        typeof(AttackBase).GetMethod("OnTriggerStay2D", Private).Invoke(projectile, new object[] { target.GetComponent<Collider2D>() });
        Assert.That(fixture.Damage.Count(target), Is.EqualTo(1));

        var expiryObject = fixture.ObjectAt("Independent projectile expiry", fixture.Owner.transform.position + Vector3.up * 3f);
        expiryObject.AddComponent<CircleCollider2D>().isTrigger = true;
        var expiry = expiryObject.AddComponent<OddIronProjectile2D>();
        expiry.Setup(new ProjectileAttackSpawnContext
        {
            ownerSystem = fixture.System, hitPayload = fixture.Payload, direction = Vector2.up,
            speed = 1f, lifetime = 0.09f
        });
        yield return new WaitForSeconds(0.15f);
        Assert.That(CombatHitPause2D.IsParalyzedOn(fixture.Owner), Is.True);
        Assert.That(expiry == null, Is.True, "Independent projectile lifetime must remain on normal scaled time.");
    }

    [UnityTest]
    public IEnumerator StoppedBoundOwner_CancelsHitsAndRegistries_WhileIndependentProjectileContinues()
    {
        foreach (string stop in new[] { "dead", "disabled", "destroyed" })
        {
            using var fixture = new Fixture();
            var state = fixture.Owner.AddComponent<ActionStateProbe>();
            Vector3 position = fixture.Owner.transform.position;
            var target = fixture.Target(position);
            CombatHitPause2D.ApplyParalysis(fixture.Owner, 0.4f);
            var sharedIds = new HashSet<int>();
            var meleeObject = fixture.ObjectAt("Stopped owner melee", position);
            meleeObject.AddComponent<BoxCollider2D>().isTrigger = true;
            var melee = meleeObject.AddComponent<MeleeHitboxActor>();
            melee.Setup(new MeleeHitboxSpawnContext
            {
                ownerSystem = fixture.System, hitPayload = fixture.Payload, lifetime = 2f,
                worldPosition = position, sharedHitTargetIds = sharedIds, damageLayers = 1 << Fixture.TargetLayer
            });
            var sharedRegistry = new SharedHitRegistry2D();
            var timed = fixture.TimedEffect(true, 2f, sharedRegistry);
            TimedEffectType.GetMethod("EnableHitCollision").Invoke(timed, null);
            var area = fixture.Area(true);
            area.Play(fixture.Payload, sharedRegistry);
            var projectileObject = fixture.ObjectAt("Stopped owner independent projectile", position + Vector3.up * 5f);
            projectileObject.AddComponent<CircleCollider2D>().isTrigger = true;
            var projectile = projectileObject.AddComponent<OddIronProjectile2D>();
            projectile.Setup(new ProjectileAttackSpawnContext
            {
                ownerSystem = fixture.System, hitPayload = fixture.Payload, lifetime = 2f,
                direction = Vector2.right, speed = 4f, damageLayers = 1 << Fixture.TargetLayer
            });
            if (stop == "dead") { state.IsDead = true; CombatHitPause2D.ClearParalysis(fixture.Owner); }
            else if (stop == "disabled") fixture.Owner.SetActive(false);
            else Object.DestroyImmediate(fixture.Owner);

            typeof(AttackBase).GetMethod("OnTriggerStay2D", Private).Invoke(melee, new object[] { target.GetComponent<Collider2D>() });
            if (timed != null) TimedEffectType.GetMethod("EnableHitCollision").Invoke(timed, null);
            if (area != null) typeof(SustainedHitArea2D).GetMethod("ScanAndApply", Private).Invoke(area, null);
            Assert.That(fixture.Damage.Count(target), Is.Zero, stop + "/bound damage must be canceled before resume.");
            Assert.That(sharedIds, Is.Empty, stop + "/cancellation must not consume shared melee IDs.");
            Assert.That(sharedRegistry.Contains(target), Is.False, stop + "/cancellation must not consume shared effect IDs.");
            float projectileX = projectile.transform.position.x;
            yield return null;
            yield return null;
            Assert.That(melee == null, Is.True, stop + "/the bound melee must expire instead of resuming.");
            Assert.That(projectile != null, Is.True);
            Assert.That(projectile.transform.position.x, Is.GreaterThan(projectileX), stop + "/independent movement must continue.");
            if (stop == "disabled")
            {
                fixture.Owner.SetActive(true);
                if (timed != null) TimedEffectType.GetMethod("EnableHitCollision").Invoke(timed, null);
                yield return null;
                Assert.That(fixture.Damage.Count(target), Is.Zero, "Re-enabling the owner must not reactivate the canceled attached effect.");
            }
            if (stop != "destroyed")
            {
                typeof(AttackBase).GetMethod("OnTriggerStay2D", Private).Invoke(projectile, new object[] { target.GetComponent<Collider2D>() });
                Assert.That(fixture.Damage.Count(target), Is.EqualTo(1), stop + "/already-fired projectile damage retains its original behavior.");
            }
        }
    }

    private static IEnumerator WaitUntilResumed(GameObject owner)
    {
        float deadline = Time.realtimeSinceStartup + 2f;
        while (CombatHitPause2D.IsParalyzedOn(owner) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(CombatHitPause2D.IsParalyzedOn(owner), Is.False, "Local paralysis did not expire.");
    }

    public sealed class RecordingDamage : GameplayEffect, ISpecGameplayEffect
    {
        private readonly Dictionary<GameObject, int> hits = new();
        public int Count(GameObject target) => hits.TryGetValue(target, out int count) ? count : 0;
        public void Apply(GameplayEffectSpec spec, GameObject target) => hits[target] = Count(target) + 1;
        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1) => Assert.Fail("Use the snapshot payload.");
        public override void Remove(GameObject target, GameObject instigator) { }
    }

    public sealed class ActionStateProbe : MonoBehaviour, ICombatActionState
    {
        public bool IsDead { get; set; }
    }

    private sealed class Fixture : IDisposable
    {
        public const int TargetLayer = 8;
        private readonly List<Object> owned = new();
        public readonly GameObject Owner;
        public readonly AbilitySystem System;
        public readonly RecordingDamage Damage;
        public readonly CombatHitPayload Payload;

        public Fixture()
        {
            Owner = ObjectAt("Bound attack owner", new Vector3(15000f, 15000f));
            Owner.AddComponent<AttributeSet>();
            Owner.AddComponent<TagSystem>();
            Owner.AddComponent<GameplayEffectRunner>();
            System = Owner.AddComponent<AbilitySystem>();
            Damage = Own(ScriptableObject.CreateInstance<RecordingDamage>());
            Payload = new CombatHitPayload { sourceSystem = System, damageEffect = Damage, finalHpDamage = 1f, causer = Owner };
        }

        public T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        public GameObject ObjectAt(string name, Vector3 position)
        {
            var result = Own(new GameObject(name)); result.transform.position = position; return result;
        }

        public GameObject Target(Vector3 position)
        {
            var target = ObjectAt("Bound attack target", position);
            target.layer = TargetLayer;
            target.AddComponent<AttributeSet>();
            target.AddComponent<TagSystem>();
            target.AddComponent<GameplayEffectRunner>();
            var collider = target.AddComponent<BoxCollider2D>(); collider.isTrigger = true; collider.size = Vector2.one * 0.3f;
            target.AddComponent<CombatHurtbox2D>();
            Physics2D.SyncTransforms();
            return target;
        }

        public Component TimedEffect(bool attached, float lifetime, SharedHitRegistry2D registry)
        {
            var root = ObjectAt(attached ? "Attached timed effect" : "Detached timed effect", Owner.transform.position);
            if (attached) root.transform.SetParent(Owner.transform, true);
            var collider = root.AddComponent<BoxCollider2D>(); collider.isTrigger = true;
            var effect = root.AddComponent(TimedEffectType);
            TimedEffectType.GetMethod("ConfigureHitCollision").Invoke(effect, new object[] { new Collider2D[] { collider }, (LayerMask)(1 << TargetLayer), true });
            TimedEffectType.GetMethod("Play", new[] { typeof(float), typeof(CombatHitPayload), typeof(SharedHitRegistry2D) })
                .Invoke(effect, new object[] { lifetime, Payload, registry });
            Physics2D.SyncTransforms();
            return effect;
        }

        public SustainedHitArea2D Area(bool attached, Vector3? position = null, float interval = 0.12f)
        {
            var root = ObjectAt(attached ? "Attached area" : "Detached area", position ?? Owner.transform.position);
            if (attached) root.transform.SetParent(Owner.transform, true);
            var collider = root.AddComponent<BoxCollider2D>(); collider.isTrigger = true;
            var area = root.AddComponent<SustainedHitArea2D>();
            area.Configure(new Collider2D[] { collider }, 1 << TargetLayer, interval, true, false);
            Physics2D.SyncTransforms();
            return area;
        }

        public void Dispose()
        {
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        }
    }
}
#endif

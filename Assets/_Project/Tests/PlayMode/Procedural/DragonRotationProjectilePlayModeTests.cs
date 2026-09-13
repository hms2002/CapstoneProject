#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CapstoneAudio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Verifies spin projectile counts, per-launch aiming, scheduling and cancellation with isolated actors.</summary>
public sealed class DragonRotationProjectilePlayModeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string StrategyPath = "Assets/_Project/Data/Abilities/Strategies/AL_DragonRotation.asset";
    private readonly List<Object> owned = new();
    private Random.State randomState;

    [SetUp]
    public void SetUp()
    {
        randomState = Random.state;
        Random.InitState(428);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var projectile in Object.FindObjectsByType<DragonSpinProjectile2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (projectile.name.StartsWith("SpinProjectileTestSource"))
                Object.DestroyImmediate(projectile.gameObject);
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        Random.state = randomState;
    }

    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private static void Set(AbilityLogic_DragonRotation logic, string name, object value) =>
        typeof(AbilityLogic_DragonRotation).GetField(name, PrivateInstance).SetValue(logic, value);
    private static object Invoke(AbilityLogic_DragonRotation logic, string name, params object[] args) =>
        typeof(AbilityLogic_DragonRotation).GetMethod(name, PrivateInstance).Invoke(logic, args);

    private AbilityLogic_DragonRotation CreateLogic(out DragonController dragon, out Transform target)
    {
        var logic = Own(ScriptableObject.CreateInstance<AbilityLogic_DragonRotation>());
        Set(logic, "projectileCountMultiplier", 4);
        Set(logic, "spinLoopSound", default(SoundRef));
        Set(logic, "projectileFireSound", default(SoundRef));
        Set(logic, "damageAmount", 0f);
        Set(logic, "visualSwayAmplitude", 0f);
        var actor = Own(new GameObject("SpinDragonTest"));
        actor.SetActive(false);
        dragon = actor.AddComponent<DragonController>();
        // Keep unrelated boss AI/bootstrap inactive, but provide the real projectile owner contract.
        typeof(Enemy).GetField("abilitySystem", PrivateInstance).SetValue(dragon, actor.GetComponent<AbilitySystem>());
        target = Own(new GameObject("SpinTargetTest")).transform;
        target.position = Vector3.up * 5f;
        dragon.SetCombatTarget(target);
        var source = Own(new GameObject("SpinProjectileTestSource"));
        source.SetActive(false);
        source.AddComponent<CircleCollider2D>().isTrigger = true;
        source.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        source.AddComponent<DragonSpinProjectile2D>();
        Set(logic, "projectilePrefab", source);
        Set(logic, "projectileDamageEffect", Own(ScriptableObject.CreateInstance<GE_Damage_Spec>()));
        return logic;
    }

    [Test]
    public void AuthoredStrategy_Emits28InsteadOf7_WithoutExtendingSpin()
    {
        var authored = AssetDatabase.LoadAssetAtPath<AbilityLogic_DragonRotation>(StrategyPath);
        Assert.That(authored, Is.Not.Null);
        Assert.That(typeof(AbilityLogic_DragonRotation).GetField("spinSeconds", PrivateInstance).GetValue(authored), Is.EqualTo(2.5f));
        Assert.That(Invoke(authored, "ResolveProjectileCount"), Is.EqualTo(28));
        var baseline = Own(Object.Instantiate(authored));
        Set(baseline, "projectileCountMultiplier", 1);
        Assert.That(Invoke(baseline, "ResolveProjectileCount"), Is.EqualTo(7));
    }

    [TestCase(1f / 60f)]
    [TestCase(0.37f)]
    [TestCase(3f)]
    public void Scheduler_KeepsExactCountAcrossFrameRates_AndAimsEveryThirdShot(float frameStep)
    {
        var logic = CreateLogic(out var dragon, out var target);
        var shots = new List<GameObject>();
        int count = (int)Invoke(logic, "ResolveProjectileCount");
        object[] args = { dragon, shots, 0f, count, 0 };
        for (float elapsed = 0f; elapsed < 2.5f; elapsed += frameStep)
        {
            args[2] = elapsed;
            Invoke(logic, "SpawnDueProjectiles", args);
        }
        args[2] = 2.5f;
        Invoke(logic, "SpawnDueProjectiles", args);
        Assert.That(shots.Count, Is.EqualTo(28));
        Assert.That(args[4], Is.EqualTo(28));
        Invoke(logic, "SpawnDueProjectiles", args);
        Assert.That(shots.Count, Is.EqualTo(28), "Completion must not spawn twice.");
        int aimedCount = 0;
        for (int i = 0; i < shots.Count; i++)
        {
            var projectile = shots[i].GetComponent<DragonSpinProjectile2D>();
            Vector2 direction = Direction(projectile);
            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(0.0001f));
            if ((i + 1) % 3 == 0)
            {
                aimedCount++;
                Vector2 expected = (target.position - shots[i].transform.position).normalized;
                Assert.That(Vector2.Dot(direction, expected), Is.GreaterThan(0.9999f));
            }
            else
            {
                Assert.That(Vector2.Distance(direction, Vector2.up), Is.GreaterThan(0.001f), "Fixed-seed random shots must remain scattered.");
            }
        }
        Assert.That(aimedCount, Is.EqualTo(9));
    }

    [Test]
    public void AimedShot_UsesLiveTargetCenterAtLaunch_AndDoesNotHomeOrOvershootCloseTarget()
    {
        var logic = CreateLogic(out var dragon, out var target);
        var shots = new List<GameObject>();
        var hurtbox = target.gameObject.AddComponent<BoxCollider2D>();
        hurtbox.offset = Vector2.up;
        Invoke(logic, "SpawnProjectile", dragon, shots, 2);
        var first = shots[0].GetComponent<DragonSpinProjectile2D>();
        Vector2 firstDirection = Direction(first);
        target.position = Vector3.right * 5f;
        Physics2D.SyncTransforms();
        Invoke(logic, "SpawnProjectile", dragon, shots, 5);
        var second = shots[1].GetComponent<DragonSpinProjectile2D>();
        Vector2 expected = ((Vector2)hurtbox.bounds.center - (Vector2)second.transform.position).normalized;
        Assert.That(Vector2.Dot(Direction(second), expected), Is.GreaterThan(0.9999f));
        typeof(DragonSpinProjectile2D).GetMethod("TickAttack", PrivateInstance).Invoke(first, new object[] { 0.1f });
        Assert.That(Direction(first), Is.EqualTo(firstDirection), "Moving the target must not change a launched direction.");

        hurtbox.offset = Vector2.zero;
        target.position = Vector3.right * 0.1f;
        Physics2D.SyncTransforms();
        Invoke(logic, "SpawnProjectile", dragon, shots, 8);
        Assert.That(shots[2].transform.position.x, Is.InRange(0f, 0.099f));
        Assert.That(Vector2.Dot(Direction(shots[2].GetComponent<DragonSpinProjectile2D>()), Vector2.right), Is.GreaterThan(0.9999f));
    }

    [Test]
    public void MissingOrCoincidentTarget_StillProducesNormalizedShots()
    {
        var logic = CreateLogic(out var dragon, out var target);
        var shots = new List<GameObject>();
        target.position = dragon.transform.position;
        Invoke(logic, "SpawnProjectile", dragon, shots, 2);
        dragon.SetCombatTarget(null);
        Invoke(logic, "SpawnProjectile", dragon, shots, 5);
        Assert.That(shots.Count, Is.EqualTo(2));
        foreach (var shot in shots)
            Assert.That(Direction(shot.GetComponent<DragonSpinProjectile2D>()).magnitude, Is.EqualTo(1f).Within(0.0001f));
    }

    [UnityTest]
    public IEnumerator CancelledSpin_DoesNotFlushPendingShots_AndDestroysAlreadySpawnedShots()
    {
        var logic = CreateLogic(out var dragon, out _);
        var spec = new AbilitySpec(null);
        var token = new AbilityCancellationToken();
        typeof(AbilitySpec).GetProperty("Token").SetValue(spec, token);
        var routine = (IEnumerator)Invoke(logic, "RunSpin", dragon, spec);
        Assert.That(routine.MoveNext(), Is.True);
        var live = new List<GameObject>();
        foreach (var projectile in Object.FindObjectsByType<DragonSpinProjectile2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (projectile.name == "SpinProjectileTestSource(Clone)") live.Add(projectile.gameObject);
        Assert.That(live.Count, Is.EqualTo(1));
        token.Cancel();
        Assert.That(routine.MoveNext(), Is.False);
        yield return null;
        foreach (var shot in live) Assert.That(shot == null, Is.True);
        foreach (var projectile in Object.FindObjectsByType<DragonSpinProjectile2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Assert.That(projectile.name, Is.Not.EqualTo("SpinProjectileTestSource(Clone)"));
    }

    private static Vector2 Direction(DragonSpinProjectile2D projectile) =>
        (Vector2)typeof(DragonSpinProjectile2D).GetField("direction", PrivateInstance).GetValue(projectile);
}
#endif

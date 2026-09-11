#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>
/// Verifies variable shot budgets, fixed rest deadlines and real ranged-prefab firing boundaries.
/// Owns and cleans up all test-created scene roots and temporary authoring copies.
/// </summary>
public sealed class MonsterProjectileBurstCadencePlayModeTests
{
    private readonly List<Object> temporaryAssets = new();
    private HashSet<GameObject> existingRoots;
    private Random.State randomState;

    [SetUp]
    public void SetUp()
    {
        randomState = Random.state;
        Random.InitState(1831);
        existingRoots = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
    }

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!existingRoots.Contains(root))
                Object.DestroyImmediate(root);
        }
        foreach (Object asset in temporaryAssets)
            Object.DestroyImmediate(asset);
        temporaryAssets.Clear();
        Random.state = randomState;
    }

    [Test]
    public void EachCycle_RerollsThreeToFiveShots_ThenRestsExactlyTwoSeconds()
    {
        var cadence = new MobProjectileBurstCadence();
        var observedCounts = new HashSet<int>();
        float now = 10f;
        for (int cycle = 0; cycle < 64; cycle++)
        {
            int shots = FillBurst(cadence, now);
            Assert.That(shots, Is.InRange(3, 5));
            observedCounts.Add(shots);
            Assert.That(cadence.GetRemainingRestSeconds(now), Is.EqualTo(2f));
            Assert.That(cadence.IsResting(now + 1.99f), Is.True);
            Assert.That(cadence.IsResting(now + 2f), Is.False);
            now += 2f;
        }
        CollectionAssert.AreEquivalent(new[] { 3, 4, 5 }, observedCounts);
    }

    [Test]
    public void RejectedShotsDuringRest_DoNotExtendDeadlineOrConsumeNextBudget()
    {
        var cadence = new MobProjectileBurstCadence();
        FillBurst(cadence, 10f);
        for (int i = 0; i < 50; i++)
            cadence.RecordShot(11f);
        Assert.That(cadence.GetRemainingRestSeconds(11f), Is.EqualTo(1f));
        Assert.That(FillBurst(cadence, 12f), Is.InRange(3, 5));
    }

    [Test]
    public void QueriesAndOtherInstances_DoNotConsumeShotsOrStartRest()
    {
        var first = new MobProjectileBurstCadence();
        var second = new MobProjectileBurstCadence();
        FillBurst(first, 10f);
        for (int i = 0; i < 100; i++)
        {
            Assert.That(second.IsResting(10f), Is.False);
            Assert.That(second.GetRemainingRestSeconds(10f), Is.Zero);
        }
        Assert.That(FillBurst(second, 10f), Is.InRange(3, 5));
    }

    [TestCase("CommonCorridor/GoblinGunner.prefab", 1)]
    [TestCase("CommonCorridor/LizardMage.prefab", 1)]
    [TestCase("BeerMonster.prefab", 1)]
    [TestCase("ShadowCorridor/StrangeCandlestick/StrangeCandlestick.prefab", 1)]
    [TestCase("SlimeCorridor/Wizard.prefab", 4)]
    public void PrefabFire_CountsSuccessfulEmissions_AndBlocksRequestsDuringRest(string path, int projectilesPerShot)
    {
        Mob owner = CreateMonster(path);
        var target = new GameObject("BurstTarget");
        target.transform.position = owner.transform.position + Vector3.right;
        MobProjectileBurstCadence cadence = GetCadence(owner);
        var effect = ScriptableObject.CreateInstance<GE_Damage_Spec>();
        temporaryAssets.Add(effect);
        var payload = new CombatHitPayload
        {
            sourceSystem = owner.GetComponent<AbilitySystem>(),
            damageEffect = effect,
            causer = owner.gameObject,
            finalHpDamage = 1f
        };

        int before = CountProjectiles();
        int shots = 0;
        while (!cadence.IsResting(Time.time) && shots < 6)
        {
            Fire(owner, target, payload);
            shots++;
            Assert.That(CountProjectiles(), Is.EqualTo(before + shots * projectilesPerShot));
        }

        Assert.That(shots, Is.InRange(3, 5));
        Assert.That(cadence.IsResting(Time.time), Is.True);
        Assert.That(((IMobAttackDecisionSource)owner).TryBuildAttackRequest(out _), Is.False);
        // The argument is already attack-speed scaled, including elite speed bonuses.
        Assert.That(owner.ResolvePostAttackRecoverSeconds(0.01f), Is.EqualTo(2f).Within(0.001f));
        Assert.That(owner.ResolvePostAttackRecoverSeconds(3f), Is.GreaterThanOrEqualTo(3f));
        Fire(owner, target, payload);
        Assert.That(CountProjectiles(), Is.EqualTo(before + shots * projectilesPerShot));

        SetField(cadence, "restUntil", Time.time);
        Fire(owner, target, payload);
        Assert.That(CountProjectiles(), Is.EqualTo(before + (shots + 1) * projectilesPerShot));
    }

    [Test]
    public void LizardRunner_StopsWithinAnExistingSequence_WhenBudgetIsExhausted()
    {
        var owner = (LizardMage)CreateMonster("CommonCorridor/LizardMage.prefab");
        var ability = Object.Instantiate((AbilityDefinition)GetField(owner, "burstAbility"));
        var logic = Object.Instantiate(owner.BurstLogic);
        temporaryAssets.Add(ability);
        temporaryAssets.Add(logic);
        ability.logic = logic;
        SetField(logic, "shotCount", 10);
        SetField(logic, "warningSeconds", 0f);
        SetField(logic, "shotInterval", 0f);
        SetField(owner, "burstAbility", ability);
        var target = new GameObject("LizardBurstTarget");
        target.transform.position = owner.transform.position + Vector3.right;

        int before = CountProjectiles();
        var run = owner.GetComponent<LizardMageBurstRunner>().Run(owner.GetComponent<AbilitySystem>(), null, target);
        Assert.That(run.MoveNext(), Is.False, "Zero-delay sequence must finish without waiting inside the attack.");
        Assert.That(CountProjectiles() - before, Is.InRange(3, 5));
        Assert.That(owner.IsRestingBetweenBursts, Is.True);
        Assert.That(owner.GetComponent<LizardMageBurstRunner>().IsRunning, Is.False);
        Assert.That(owner.TryBuildBurstContext(owner.GetComponent<AbilitySystem>(), null, target, out _), Is.False);
    }

    [Test]
    public void WizardInvalidPayload_DoesNotCountAsFiring()
    {
        var owner = (Wizard)CreateMonster("SlimeCorridor/Wizard.prefab");
        for (int i = 0; i < 10; i++)
            owner.FireScatterShot(default);
        Assert.That(GetCadence(owner).IsResting(Time.time), Is.False);
        Assert.That(GetField(GetCadence(owner), "shotsRemaining"), Is.EqualTo(0));
    }

    private static int FillBurst(MobProjectileBurstCadence cadence, float now)
    {
        int shots = 0;
        while (!cadence.IsResting(now) && shots < 6)
        {
            cadence.RecordShot(now);
            shots++;
        }
        return shots;
    }

    private static Mob CreateMonster(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monsters/" + path);
        Assert.That(prefab, Is.Not.Null);
        return Object.Instantiate(prefab, new Vector3(10000f, 10000f), Quaternion.identity).GetComponent<Mob>();
    }

    private static int CountProjectiles() => Object.FindObjectsByType<LightBeadProjectile2D>(FindObjectsSortMode.None).Length;

    private static MobProjectileBurstCadence GetCadence(Mob owner) => (MobProjectileBurstCadence)GetField(owner, "burstCadence");

    private static object GetField(object owner, string name) => owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    private static void SetField(object owner, string name, object value) => owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);

    private static void Fire(Mob owner, GameObject target, CombatHitPayload payload)
    {
        Vector2 origin = owner.transform.position;
        switch (owner)
        {
            case GoblinGunner gunner:
                gunner.FireProjectile(new GoblinGunner.ShotContext(target, origin, Vector2.right, 0f, 0.1f, 5f, 1f, 10f, 0, 0, payload));
                break;
            case LizardMage mage:
                mage.FireProjectile(new LizardMage.BurstContext(target, origin, Vector2.right, 0f, 0.1f, 5f, 1f, 10f, 0, 0, payload));
                break;
            case BeerMonster beer:
                beer.FireProjectile(new BeerMonster.ShotContext(target, origin, Vector2.right, 0f, 0.1f, 5f, 1f, 10f, 0, 0, payload));
                break;
            case StrangeCandlestick candlestick:
                candlestick.FireProjectile(target);
                break;
            case Wizard wizard:
                wizard.FireScatterShot(new Wizard.ScatterShotContext(target, origin, Vector2.right, 0, 0f, 5f, 24f, payload));
                break;
            default:
                Assert.Fail("Unsupported ranged monster in fixture.");
                break;
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityGAS;
using UnityGAS.Sample;
using Object = UnityEngine.Object;

/// <summary>Verifies candle availability gates and keeps pattern cancellation separate from summon teardown.</summary>
public sealed class WitchCandleSelectionPlayModeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> createdObjects = new();
    private SelectionTestWitch witch;
    private CleanupTestSkeleton skeleton;
    private AbilityLogic_WitchExtinguishCandle extinguishLogic;
    private BossPatternEntry extinguishPattern;
    private BossPatternEntry otherPattern;
    private Random.State originalRandomState;

    [SetUp]
    public void SetUp()
    {
        originalRandomState = Random.state;
        Assert.AreEqual(0, Candlestick.Instances.Count, "Fixture requires an isolated test scene.");
        GameObject bossObject = CreateObject("CandleSelectionWitch");
        bossObject.AddComponent<AttributeSet>();
        witch = bossObject.AddComponent<SelectionTestWitch>();
        SetField(typeof(BossControllerBase), witch, "blackboard", new BossBlackboard(witch.transform));
        SetField(typeof(BossControllerBase), witch, "patternRuntime", new BossPatternRuntimeState());
        SetField(typeof(BossControllerBase), witch, "stateMachine", new BossStateMachine(witch.Blackboard));
        SetField(typeof(BossControllerBase), witch, "combatIdleState", new BossCombatIdleState(witch));
        SetField(typeof(BossControllerBase), witch, "hasInitializedBossRuntime", true);
        SetField(typeof(BossControllerBase), witch, "combatActive", true);
        SetField(typeof(Witch), witch, "candleService", bossObject.AddComponent<WitchCandleService>());
        SetField(typeof(Witch), witch, "extinguishPatternExecutor", bossObject.AddComponent<WitchExtinguishPatternExecutor>());

        GameObject target = CreateObject("CandleSelectionTarget");
        witch.SetCombatTarget(target.transform);
        witch.Blackboard.Tick(0f, target.transform, 1f);
        extinguishLogic = CreateAsset<AbilityLogic_WitchExtinguishCandle>();
        AbilityDefinition ability = CreateAsset<AbilityDefinition>();
        ability.logic = extinguishLogic;
        extinguishPattern = MakePattern(ability);
        otherPattern = MakePattern(CreateAsset<AbilityDefinition>());
        witch.ConfigurePatterns(extinguishPattern, otherPattern);

        GameObject skeletonObject = CreateObject("CandleSelectionSummon");
        skeletonObject.AddComponent<BoxCollider2D>();
        skeleton = skeletonObject.AddComponent<CleanupTestSkeleton>();
        witch.RegisterRetreatSummon(skeleton);
    }

    [TearDown]
    public void TearDown()
    {
        if (witch != null) Object.DestroyImmediate(witch.gameObject);
        for (int i = createdObjects.Count - 1; i >= 0; i--)
            if (createdObjects[i] != null) Object.DestroyImmediate(createdObjects[i]);
        createdObjects.Clear();
        Random.state = originalRandomState;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NoCandles_ExcludesExtinguishIncludingForcedFollowUp(bool forced)
    {
        BossPatternEvalResult result = Evaluate(forced);
        Assert.AreEqual(BossPatternEvalState.HardFail, result.State);
        Assert.AreEqual(0, result.GetWeight(extinguishPattern.SelectionWeight));
        Assert.IsTrue(witch.EvaluatePattern(otherPattern).CanUse, "Other patterns must remain available.");
        Assert.AreSame(otherPattern, witch.SelectNextPattern());
        Assert.AreEqual(0, skeleton.DeathCount);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AllSealed_ExcludesExtinguish_AndUnsealingRestoresIt(bool forced)
    {
        Candlestick candle = CreateCandle();
        CreateCandle().Seal();
        Assert.IsTrue(Evaluate(forced).CanUse);
        Assert.IsTrue(candle.Seal());
        Assert.IsFalse(Evaluate(forced).CanUse);
        CandlestickSeal seal = candle.GetComponent<CandlestickSeal>();
        for (int i = 0; i < seal.MaxSealHits; i++) Assert.IsTrue(seal.UseHit());
        Assert.IsTrue(Evaluate(forced).CanUse);
    }

    [Test]
    public void DisabledLastCandle_IsNotASelectionTarget()
    {
        Candlestick candle = CreateCandle();
        Assert.IsTrue(witch.EvaluatePattern(extinguishPattern).CanUse);
        candle.gameObject.SetActive(false);
        Assert.IsFalse(witch.EvaluatePattern(extinguishPattern).CanUse);
        candle.gameObject.SetActive(true);
        Assert.IsTrue(witch.EvaluatePattern(extinguishPattern).CanUse);
    }

    [Test]
    public void UnavailableForcedFollowUp_FallsBackToOtherPatterns()
    {
        witch.PatternRuntime.QueueFollowUpAbility(extinguishPattern.Ability);
        Assert.AreSame(otherPattern, witch.SelectNextPattern());
        Assert.IsFalse(witch.PatternRuntime.HasQueuedFollowUpAbility);
        Assert.AreEqual(0, skeleton.DeathCount);
    }

    [Test]
    public void LastCandleSealedAfterSelection_CancelsOnlyThePattern()
    {
        Candlestick candle = CreateCandle();
        Assert.IsTrue(witch.EvaluatePattern(extinguishPattern).CanUse);
        witch.PatternRuntime.BeginPattern(extinguishPattern);
        candle.Seal();

        Assert.IsFalse(witch.TryBeginExtinguishPattern(extinguishLogic, 1.2f, out float duration));
        Assert.AreEqual(1.2f, duration);
        Assert.IsFalse(witch.StartExtinguish(extinguishLogic, 1.2f), "Legacy entry must use the same gate.");
        witch.AbortCurrentPattern();

        Assert.IsNull(witch.PatternRuntime.CurrentPattern);
        Assert.IsNull(witch.PatternRuntime.ReservedPattern);
        Assert.IsFalse(witch.RuntimeData.HasActiveExtinguishSelection);
        Assert.AreEqual(0, skeleton.DeathCount);
        witch.SetCombatActive(false);
        Assert.AreEqual(1, skeleton.DeathCount, "A cancelled pattern must not discard summon ownership.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PatternEndDuringCombat_PreservesSummons(bool forced)
    {
        witch.PatternRuntime.BeginPattern(otherPattern);
        if (forced) witch.AbortCurrentPattern();
        else witch.FinishCurrentPattern();
        Assert.IsNull(witch.PatternRuntime.CurrentPattern);
        Assert.AreEqual(0, skeleton.DeathCount);
    }

    [Test]
    public void GroggyEntry_PreservesSummons()
    {
        witch.PatternRuntime.BeginPattern(otherPattern);
        new BossGroggyState(witch).OnEnter();
        Assert.IsNull(witch.PatternRuntime.CurrentPattern);
        Assert.AreEqual(0, skeleton.DeathCount);
    }

    [Test]
    public void PhaseChange_PreservesSummonsAndReturnsToIdle()
    {
        witch.PatternRuntime.BeginPattern(otherPattern);
        witch.ChangePhase();
        Assert.AreSame(witch.GetCombatIdleState(), witch.StateMachine.CurrentState);
        Assert.IsNull(witch.PatternRuntime.CurrentPattern);
        Assert.AreEqual(0, skeleton.DeathCount);
    }

    [Test]
    public void CombatEnd_CleansSummonsEvenWithoutActivePattern_ExactlyOnce()
    {
        witch.SetCombatActive(false);
        witch.SetCombatActive(false);
        Assert.AreEqual(1, skeleton.DeathCount);
        Assert.AreEqual(0, RegisteredSummonCount());
    }

    [Test]
    public void BossDeath_CleansSummonsWhileCombatFlagIsStillActive()
    {
        witch.MarkDead();
        witch.AbortCurrentPattern();
        Assert.AreEqual(1, skeleton.DeathCount);
        Assert.AreEqual(0, RegisteredSummonCount());
    }

    [Test]
    public void BossDestroyed_CleansSurvivingSummons()
    {
        Object.DestroyImmediate(witch.gameObject);
        Assert.AreEqual(1, skeleton.DeathCount);
    }

    private BossPatternEvalResult Evaluate(bool forced)
    {
        return forced ? witch.EvaluateFollowUp(extinguishPattern) : witch.EvaluatePattern(extinguishPattern);
    }

    private Candlestick CreateCandle()
    {
        GameObject candleObject = CreateObject("SelectionTestCandle");
        candleObject.AddComponent<BoxCollider2D>();
        return candleObject.AddComponent<Candlestick>();
    }

    private GameObject CreateObject(string name)
    {
        GameObject instance = new GameObject(name);
        instance.transform.position = new Vector3(10000f, 10000f, 0f);
        createdObjects.Add(instance);
        return instance;
    }

    private T CreateAsset<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        createdObjects.Add(asset);
        return asset;
    }

    private static BossPatternEntry MakePattern(AbilityDefinition ability)
    {
        return BossPatternEntry.CreateRuntime(ability, 100, 0, 0, 0f, 0f, 0f, 999f, 0f, 1f);
    }

    private int RegisteredSummonCount()
    {
        return ((List<DeadsSkeleton>)typeof(Witch).GetField("activeRetreatSummons", PrivateInstance).GetValue(witch)).Count;
    }

    private static void SetField(System.Type declaringType, object instance, string name, object value)
    {
        declaringType.GetField(name, PrivateInstance).SetValue(instance, value);
    }

    /// <summary>Skips scene bootstrapping while exposing real pattern evaluation and lifecycle hooks.</summary>
    public sealed class SelectionTestWitch : Witch
    {
        protected override void Awake() { }
        protected override void Start() { }
        protected override void Update() { }
        public BossPatternEvalResult EvaluateFollowUp(BossPatternEntry pattern) => EvaluateForcedFollowUpPattern(pattern);
        public void MarkDead() => isDead = true;
        public void ChangePhase() => OnPhaseChanged(0, 1);
        public void ConfigurePatterns(params BossPatternEntry[] patterns)
        {
            SetRuntimePhases(new[] { BossPhaseConfig.CreateRuntime("CandleSelectionTest", 1f, 0f, 0f, patterns) });
        }
    }

    /// <summary>Records cleanup requests without spawning loot, starting AI, or scheduling death presentation.</summary>
    public sealed class CleanupTestSkeleton : DeadsSkeleton
    {
        public int DeathCount { get; private set; }
        protected override void Awake() { }
        protected override void Start() { }
        protected override void Die()
        {
            DeathCount++;
            isDead = true;
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Verifies intro-only light immunity through the skeleton's real sequence and trigger entry points.</summary>
public sealed class DeadsSkeletonLightIntroPlayModeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject skeletonObject;
    private GameObject lightObject;
    private LightTestSkeleton skeleton;
    private Collider2D lightCollider;

    [SetUp]
    public void SetUp()
    {
        skeletonObject = new GameObject("SkeletonLightIntroTest");
        skeletonObject.SetActive(false);
        skeletonObject.transform.position = new Vector3(10000f, 10000f, 0f);
        skeletonObject.AddComponent<BoxCollider2D>();
        skeleton = skeletonObject.AddComponent<LightTestSkeleton>();
        skeletonObject.SetActive(true);

        lightObject = new GameObject("SkeletonTestLight");
        lightObject.transform.position = skeletonObject.transform.position;
        lightObject.AddComponent<CandlestickLightZone>();
        lightCollider = lightObject.GetComponent<CircleCollider2D>();
        Physics2D.SyncTransforms();
        Assert.IsTrue((bool)Invoke("IsInsideCandlestickLight"), "Fixture must overlap a real light trigger.");
    }

    [TearDown]
    public void TearDown()
    {
        if (skeletonObject != null) Object.DestroyImmediate(skeletonObject);
        if (lightObject != null) Object.DestroyImmediate(lightObject);
    }

    [Test]
    public void StartingInsideLight_KeepsTransforming()
    {
        skeleton.BeginSelfDestructSequence(null, 1000f);
        Assert.IsTrue(skeleton.IsSelfDestructIntroActive());
        Assert.AreEqual(0, skeleton.DeathCount);
    }

    [Test]
    public void AdvancingInsideLight_DoesNotCompleteTheIntro()
    {
        skeleton.BeginSelfDestructSequence(null, 1000f);
        Assert.AreEqual(SelfDestructSequenceStatus.Running, skeleton.AdvanceSelfDestructSequence());
        Assert.AreEqual(0, skeleton.DeathCount);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LightEntry_KillsOnlyAfterIntro(bool introFinished)
    {
        MoveLightAway();
        skeleton.BeginSelfDestructSequence(null, 1000f);
        if (introFinished) FinishIntro();
        Invoke("OnTriggerEnter2D", lightCollider);
        Assert.AreEqual(introFinished ? 1 : 0, skeleton.DeathCount);
    }

    [Test]
    public void IntroFinishesInsideLight_DiesWithoutAnotherTriggerEntry()
    {
        skeleton.BeginSelfDestructSequence(null, 1000f);
        FinishIntro();
        Assert.AreEqual(SelfDestructSequenceStatus.Completed, skeleton.AdvanceSelfDestructSequence());
        Assert.AreEqual(1, skeleton.DeathCount);
    }

    [Test]
    public void LeavingLightBeforeIntroFinishes_DoesNotDeferALethalHit()
    {
        skeleton.BeginSelfDestructSequence(null, 1000f);
        Invoke("OnTriggerEnter2D", lightCollider);
        MoveLightAway();
        FinishIntro();
        Assert.AreEqual(SelfDestructSequenceStatus.Running, skeleton.AdvanceSelfDestructSequence());
        Assert.AreEqual(0, skeleton.DeathCount);
    }

    [Test]
    public void ZeroDurationIntro_StillDiesImmediatelyInsideLight()
    {
        skeleton.BeginSelfDestructSequence(null, 0f);
        Assert.AreEqual(1, skeleton.DeathCount);
    }

    [Test]
    public void NormalMode_IsStillImmuneToLight()
    {
        Invoke("OnTriggerEnter2D", lightCollider);
        Assert.AreEqual(SelfDestructSequenceStatus.Cancelled, skeleton.AdvanceSelfDestructSequence());
        Assert.AreEqual(0, skeleton.DeathCount);
    }

    [Test]
    public void CancelledIntro_RestoresNormalModeLightImmunity()
    {
        skeleton.BeginSelfDestructSequence(null, 1000f);
        skeleton.CancelSelfDestructSequence();
        Invoke("OnTriggerEnter2D", lightCollider);
        Assert.AreEqual(SelfDestructSequenceStatus.Cancelled, skeleton.AdvanceSelfDestructSequence());
        Assert.AreEqual(0, skeleton.DeathCount);
    }

    private void FinishIntro()
    {
        // Test the exact transition boundary without changing global time or waiting for animation.
        typeof(DeadsSkeleton).GetField("selfDestructIntroEndTime", PrivateInstance).SetValue(skeleton, Time.time);
    }

    private void MoveLightAway()
    {
        lightObject.transform.position += Vector3.right * 20f;
        Physics2D.SyncTransforms();
    }

    private object Invoke(string method, params object[] arguments)
    {
        return typeof(DeadsSkeleton).GetMethod(method, PrivateInstance).Invoke(skeleton, arguments);
    }

    /// <summary>Records death decisions without starting AI, spawning loot, or invoking death presentation.</summary>
    public sealed class LightTestSkeleton : DeadsSkeleton
    {
        public int DeathCount { get; private set; }
        protected override void Awake() { }
        protected override void Start() { }
        protected override void Die()
        {
            if (isDead) return;
            isDead = true;
            DeathCount++;
        }
    }
}
#endif

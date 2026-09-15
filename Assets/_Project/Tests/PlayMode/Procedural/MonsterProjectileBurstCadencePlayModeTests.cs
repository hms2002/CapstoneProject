#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>
/// Verifies shot budgets, wall-aware spawning, navigation retry throttling and real ranged-prefab firing boundaries.
/// Owns and cleans up all test-created scene roots and temporary authoring copies.
/// </summary>
public sealed class MonsterProjectileBurstCadencePlayModeTests
{
    /// <summary>Responsibility: observe cancellation through the production coordinator without starting a real attack.</summary>
    private sealed class PitTestRunner : IMobPatternRunner
    {
        public bool IsRunning { get; private set; } = true;
        public void Cancel() => IsRunning = false;
    }

    [TestCase("CommonCorridor/ArcaneMeleeGolem.prefab")]
    [TestCase("CommonCorridor/ArcaneTankGolem.prefab")]
    [TestCase("CommonCorridor/GoblinGunner.prefab")]
    [TestCase("CommonCorridor/GoblinTank.prefab")]
    [TestCase("CommonCorridor/GoblinWarrior.prefab")]
    [TestCase("CommonCorridor/LizardMage.prefab")]
    [TestCase("CommonCorridor/LizardWarrior.prefab")]
    [TestCase("BeerMonster.prefab")]
    [TestCase("Frog.prefab")]
    [TestCase("ShadowCorridor/ShadowMonster.prefab")]
    [TestCase("ShadowCorridor/ShadowServant/ShadowServant.prefab")]
    [TestCase("ShadowCorridor/Dead'sSkeleton.prefab")]
    public void GroundMonsterPitFall_CancelsMotionDiesAndClearsRoomCount(string prefabPath)
    {
        Mob owner = CreateMonster(prefabPath);
        Assert.That(owner, Is.Not.Null);
        var reaction = owner.GetComponent<PitFallReaction2D>();
        Assert.That(reaction, Is.Not.Null, "The authored prefab must opt into pit falling.");
        Collider2D body = null;
        foreach (var collider in owner.GetComponentsInChildren<Collider2D>())
            if (collider.enabled && !collider.isTrigger && collider.attachedRigidbody == owner.GetComponent<Rigidbody2D>())
            { body = collider; break; }
        Assert.That(body, Is.Not.Null);
        Assert.That(PitFallTarget.TryCreate(body, out var target), Is.True);
        Assert.That(target.Reaction, Is.SameAs(reaction));
        var trap = new GameObject("MonsterPit").AddComponent<HoleTrap>();
        Assert.That(reaction.CanReactToPitFall(trap), Is.True);
        var group = new GameObject("PitRoom").AddComponent<MonsterSpawnRoomGroup>();
        group.NotifyMonsterSpawned(owner.gameObject);
        Assert.That(group.RemainingRegisteredOrPendingCount, Is.EqualTo(1));
        var coordinator = owner.GetComponent<MobAbilityCoordinator>();
        Assert.That(coordinator, Is.Not.Null);
        var runner = new PitTestRunner();
        Assert.That(coordinator.TryBeginRunner(runner), Is.True);
        var motion = owner.GetComponent<AbilityMotionController2D>();
        Assert.That(motion, Is.Not.Null);
        motion.StartDash(Vector2.right, 8f, 3f);
        int deaths = 0;
        owner.DeathStarted += _ => deaths++;
        var context = new PitFallContext(target.AbilitySystem, null, target.Transform, trap.gameObject,
            null, null, 10f, 1f, target.Transform.position, target.Transform.position, trap, reaction);
        var routine = PitFallExecutor.Execute(context);
        try
        {
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(reaction.IsPitFallActive, Is.True);
            Assert.That(reaction.CanReactToPitFall(trap), Is.False);
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(motion.HasActiveMotion, Is.False);
            Assert.That((bool)typeof(Mob).GetMethod("IsPitFallSuppressed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null), Is.True);
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(owner.IsDead, Is.True);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(group.RemainingRegisteredOrPendingCount, Is.EqualTo(0));
            owner.RequestDeath();
            Assert.That(deaths, Is.EqualTo(1), "Repeated contact must not duplicate death.");
        }
        finally { (routine as System.IDisposable)?.Dispose(); }
    }

    [Test]
    public void PlayerPitWalking_ClampsOwnedBodyButPreservesRawDashDirection()
    {
        bool oldQueries = Physics2D.queriesHitTriggers;
        try
        {
            Physics2D.queriesHitTriggers = true;
            var player = new GameObject("PitWalkingPlayer");
            player.transform.position = new Vector3(10000f, 10000f);
            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            var child = new GameObject("BodyCollision");
            child.transform.SetParent(player.transform, false);
            child.transform.localPosition = new Vector3(0f, 0.2f);
            var body = child.AddComponent<BoxCollider2D>();
            body.size = new Vector2(0.4f, 0.3f);
            var input = player.AddComponent<PlayerIntentInput2D>();
            var pit = new GameObject("WalkingPit");
            pit.layer = LayerMask.NameToLayer("HoleTrap");
            pit.transform.position = player.transform.position + new Vector3(0.8f, 0.2f);
            var trigger = pit.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(0.4f, 2f);
            Physics2D.SyncTransforms();

            Vector2 clamped = input.FilterIntentVelocity(Vector2.right * 10f, 0.1f);
            Assert.That(clamped.x, Is.GreaterThan(3f).And.LessThan(4f));
            Assert.That(input.FilterIntentVelocity(Vector2.left * 10f, 0.1f), Is.EqualTo(Vector2.left * 10f));
            Assert.That(input.FilterIntentVelocity(Vector2.up * 10f, 0.1f), Is.EqualTo(Vector2.up * 10f));
            typeof(PlayerIntentInput2D).GetProperty("RawMoveInput").SetValue(input, Vector2.right);
            Assert.That(input.RawMoveInput, Is.EqualTo(Vector2.right));

            // The entire physical body must remain outside, not just its foot point.
            for (int i = 0; i < 20; i++)
            {
                rb.position += input.FilterIntentVelocity(Vector2.right * 10f, 0.1f) * 0.1f;
                Physics2D.SyncTransforms();
            }
            Assert.That(PlayerPitFootprint2D.IsInside(trigger, body.bounds.center, PlayerPitFootprint2D.FallInset), Is.False);
            Assert.That(trigger.OverlapPoint(body.bounds.center), Is.False);
            Assert.That(body.Distance(trigger).isOverlapped, Is.False);
            Assert.That(input.FilterIntentVelocity(Vector2.right * 10f, 0.1f).magnitude, Is.LessThan(0.02f));
            Vector2 slide = input.FilterIntentVelocity(new Vector2(5f, 5f), 0.02f);
            Assert.That(slide.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(slide.y, Is.EqualTo(5f).Within(0.001f));
            Assert.That(input.FilterIntentVelocity(Vector2.left * 5f, 0.02f), Is.EqualTo(Vector2.left * 5f));
            rb.position = new Vector2(10000.69f, 10000f);
            Physics2D.SyncTransforms();
            Assert.That(input.FilterIntentVelocity(Vector2.left * 0.1f, 0.02f).x, Is.LessThan(0f));
            Assert.That(input.FilterIntentVelocity(Vector2.up * 2f, 0.02f).y, Is.EqualTo(2f).Within(0.001f));
            Assert.That(input.FilterIntentVelocity(Vector2.right, 0.02f), Is.EqualTo(Vector2.zero));
            // A shallow dash entry is also recoverable, even before the old 0.07 inset.
            rb.position = new Vector2(10000.62f, 10000f);
            Physics2D.SyncTransforms();
            Assert.That(input.FilterIntentVelocity(Vector2.left * 0.1f, 0.02f).x, Is.LessThan(0f));
            Assert.That(input.FilterIntentVelocity(Vector2.up, 0.02f).y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(input.FilterIntentVelocity(Vector2.right, 0.02f), Is.EqualTo(Vector2.zero));
        }
        finally { Physics2D.queriesHitTriggers = oldQueries; }
    }

    [TestCase(1f, 0f)]
    [TestCase(-1f, 0f)]
    [TestCase(0f, 1f)]
    [TestCase(0f, -1f)]
    public void PlayerPitAuthoredBody_StopsAndSlidesInAllDirections(float dx, float dy)
    {
        bool oldQueries = Physics2D.queriesHitTriggers;
        try
        {
            Physics2D.queriesHitTriggers = true;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PF Player.prefab");
            var player = new GameObject("AuthoredPitPlayer");
            player.transform.position = new Vector3(100f, 100f);
            player.transform.localScale = prefab.transform.localScale;
            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            var bodyObject = Object.Instantiate(prefab.transform.Find("BodyCollision").gameObject, player.transform, false);
            var body = bodyObject.GetComponent<Collider2D>();
            var tracker = player.AddComponent<SafetyTracker>();
            var settings = new SerializedObject(prefab.GetComponent<SafetyTracker>());
            SetField(tracker, "footOffset", settings.FindProperty("footOffset").vector3Value);
            var input = player.AddComponent<PlayerIntentInput2D>();
            Physics2D.SyncTransforms();
            Vector2 direction = new Vector2(dx, dy);
            Vector2 tangent = new Vector2(-dy, dx);
            var pitObject = new GameObject("AuthoredBodyPit");
            pitObject.layer = LayerMask.NameToLayer("HoleTrap");
            float extent = dx != 0f ? body.bounds.extents.x : body.bounds.extents.y;
            pitObject.transform.position = (Vector2)body.bounds.center + direction * (extent + 0.9f);
            var pit = pitObject.AddComponent<BoxCollider2D>();
            pit.size = dx != 0f ? new Vector2(1f, 4f) : new Vector2(4f, 1f);
            pit.isTrigger = true;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 60; i++)
            {
                rb.position += input.FilterIntentVelocity(direction * 5f, 0.02f) * 0.02f;
                Physics2D.SyncTransforms();
                Assert.That(body.Distance(pit).isOverlapped, Is.False, "Authored capsule must never enter the pit while walking.");
            }
            Assert.That(input.FilterIntentVelocity(direction * 5f, 0.02f).magnitude, Is.LessThan(0.01f));
            Vector2 slide = input.FilterIntentVelocity((direction + tangent) * 5f, 0.02f);
            Assert.That(Vector2.Dot(slide, direction), Is.EqualTo(0f).Within(0.001f));
            Assert.That(Vector2.Dot(slide, tangent), Is.EqualTo(5f).Within(0.001f));
            Assert.That(input.FilterIntentVelocity(-direction * 5f, 0.02f), Is.EqualTo(-direction * 5f));
        }
        finally { Physics2D.queriesHitTriggers = oldQueries; }
    }

    [Test]
    public void PlayerPitDash_IgnoreTagDoesNotPreventFallAndMotionIsCancelled()
    {
        var player = new GameObject("PitDashPlayer");
        player.tag = "Player";
        player.transform.position = new Vector3(10000f, 10000f);
        var rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        var body = player.AddComponent<BoxCollider2D>();
        var tags = player.AddComponent<TagSystem>();
        player.AddComponent<AbilitySystem>();
        player.AddComponent<SafetyTracker>();
        var motion = player.AddComponent<AbilityMotionController2D>();
        var dashTag = Resources.Load<GameplayTag>("Tags/State.Move.Dash");
        Assert.That(dashTag, Is.Not.Null);
        tags.AddTag(dashTag, 1);
        var pitObject = new GameObject("DashPit");
        pitObject.transform.position = player.GetComponent<SafetyTracker>().FootPosition;
        var pitCollider = pitObject.AddComponent<BoxCollider2D>();
        pitCollider.size = Vector2.one * 2f;
        pitCollider.isTrigger = true;
        var trap = pitObject.AddComponent<HoleTrap>();
        Physics2D.SyncTransforms();
        SetField(trap, "ignoreTag", dashTag);
        SetField(trap, "logDebug", false);
        var args = new object[] { body, default(PitFallContext) };
        bool accepted = (bool)typeof(HoleTrap).GetMethod("TryBuildFallContext", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trap, args);
        Assert.That(accepted, Is.True, "Dash must no longer grant player pit immunity.");
        motion.StartDash(Vector2.right, 10f, 1f);
        Assert.That(motion.HasActiveMotion, Is.True);
        var routine = PitFallExecutor.Execute((PitFallContext)args[1]);
        try
        {
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(motion.HasActiveMotion, Is.False, "Pit entry must interrupt dash immediately.");
            Assert.That(rb.linearVelocity, Is.EqualTo(Vector2.zero));
        }
        finally { (routine as System.IDisposable)?.Dispose(); }
    }

    [Test]
    public void PlayerPitFootprint_EdgeAndCornerAreSafeButInteriorFalls()
    {
        var go = new GameObject("InsetPit");
        go.transform.position = new Vector3(10000f, 10000f);
        var pit = go.AddComponent<BoxCollider2D>();
        pit.size = Vector2.one * 2f;
        pit.isTrigger = true;
        Physics2D.SyncTransforms();
        Vector2 center = go.transform.position;
        Assert.That(PlayerPitFootprint2D.IsInside(pit, center + new Vector2(0.95f, 0f), 0.1f), Is.False);
        Assert.That(PlayerPitFootprint2D.IsInside(pit, center + new Vector2(0.95f, 0.95f), 0.1f), Is.False);
        Assert.That(PlayerPitFootprint2D.IsInside(pit, center + new Vector2(0.85f, 0.85f), 0.1f), Is.True);
        Assert.That(PlayerPitFootprint2D.IsInside(pit, center, 0.1f), Is.True);
    }

    [Test]
    public void PlayerPitDash_HardStopCleansIndependentDashTags()
    {
        var player = new GameObject("IndependentDashPlayer");
        var tags = player.AddComponent<TagSystem>();
        var system = player.AddComponent<AbilitySystem>();
        var motion = player.AddComponent<AbilityMotionController2D>();
        var input = player.AddComponent<PlayerIntentInput2D>();
        typeof(PlayerIntentInput2D).GetProperty("RawMoveInput").SetValue(input, Vector2.right);
        var data = ScriptableObject.CreateInstance<UnityGAS.Sample.Dash2DData>();
        temporaryAssets.Add(data);
        data.duration = 10f;
        data.invulnerableTag = Resources.Load<GameplayTag>("Tags/State.Invulnerable");
        Assert.That(data.invulnerableTag, Is.Not.Null);
        var definition = ScriptableObject.CreateInstance<AbilityDefinition>();
        temporaryAssets.Add(definition);
        definition.sourceObject = data;
        var logic = ScriptableObject.CreateInstance<UnityGAS.Sample.AbilityLogic_Dash2D>();
        temporaryAssets.Add(logic);
        var routine = logic.Activate(system, new AbilitySpec(definition), null);
        try
        {
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(motion.HasActiveMotion, Is.True);
            Assert.That(tags.HasExplicitTag(data.invulnerableTag), Is.True);
            var hardStop = Resources.Load<GameplayTag>("Tags/State.Move.Blocked");
            Assert.That(hardStop, Is.Not.Null);
            tags.AddTag(hardStop, 1);
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(motion.HasActiveMotion, Is.False);
            Assert.That(tags.HasExplicitTag(data.invulnerableTag), Is.False);
        }
        finally { (routine as System.IDisposable)?.Dispose(); }
    }

    [Test]
    public void RecoveryRetreat_AvoidsPitChoosesSideAndStopsWhenSurrounded()
    {
        bool oldQueries = Physics2D.queriesHitTriggers;
        try
        {
            Physics2D.queriesHitTriggers = true;
            Mob owner = CreateMonster("CommonCorridor/GoblinGunner.prefab");
            var resolve = typeof(Mob).GetMethod("ResolveWallSafeRecoveryRetreatDirection", BindingFlags.Instance | BindingFlags.NonPublic);
            var blocked = typeof(Mob).GetMethod("IsRecoveryRetreatDirectionBlocked", BindingFlags.Instance | BindingFlags.NonPublic);
            Vector2 center = owner.transform.position;
            foreach (Collider2D body in owner.GetComponentsInChildren<Collider2D>())
            {
                if (body.enabled && !body.isTrigger)
                {
                    center = body.bounds.center;
                    break;
                }
            }

            BoxCollider2D AddPit(Vector2 offset, Vector2 size)
            {
                var go = new GameObject("RetreatPit");
                go.layer = LayerMask.NameToLayer("HoleTrap");
                go.transform.position = center + offset;
                var pit = go.AddComponent<BoxCollider2D>();
                pit.size = size;
                pit.isTrigger = true;
                return pit;
            }

            BoxCollider2D back = AddPit(Vector2.right * 1.5f, new Vector2(0.2f, 1f));
            Physics2D.SyncTransforms();
            Assert.That((bool)blocked.Invoke(owner, new object[] { Vector2.right, 0.25f }), Is.False);
            Assert.That((bool)blocked.Invoke(owner, new object[] { Vector2.right, 2f }), Is.True,
                "A pit beyond the old fixed probe must block a longer retreat.");
            Vector2 side = (Vector2)resolve.Invoke(owner, new object[] { Vector2.right, 2f });
            Assert.That(Mathf.Abs(side.y), Is.EqualTo(1f));
            AddPit(Vector2.up * 1.5f, new Vector2(1f, 0.2f));
            AddPit(Vector2.down * 1.5f, new Vector2(1f, 0.2f));
            Physics2D.SyncTransforms();
            Assert.That((Vector2)resolve.Invoke(owner, new object[] { Vector2.right, 2f }), Is.EqualTo(Vector2.zero));
            back.enabled = false;
            Physics2D.SyncTransforms();
            Assert.That((Vector2)resolve.Invoke(owner, new object[] { Vector2.right, 2f }), Is.EqualTo(Vector2.right));
        }
        finally
        {
            Physics2D.queriesHitTriggers = oldQueries;
        }
    }

    private readonly List<Object> temporaryAssets = new();
    private HashSet<GameObject> existingRoots;
    private Random.State randomState;

    [Test]
    public void OverlappingAlcoholSources_KeepOneHudHandleUntilEffectExpires()
    {
        var target = CreateMonster("CommonCorridor/GoblinGunner.prefab").gameObject;
        var runtime = PlayerStatusRuntime.GetOrAdd(target);
        var definition = AssetDatabase.LoadAssetAtPath<CombatBuffDebuffApplicationDefinition>(
            "Assets/_Project/Data/Abilities/Effects/CBD_Puddle_AlcoholBuff.asset");
        Assert.That(definition, Is.Not.Null);
        var a = new GameObject("AlcoholSourceA");
        var b = new GameObject("AlcoholSourceB");
        var first = CombatBuffDebuffApplier.GetOrAdd(a);
        var second = CombatBuffDebuffApplier.GetOrAdd(b);
        for (int i = 0; i < 6; i++)
        {
            Assert.That(first.ApplyFromSource(a, target, definition, "Puddle.Alcohol", 0.35f), Is.True);
            Assert.That(second.ApplyFromSource(b, target, definition, "Puddle.Alcohol", 0.35f), Is.True);
            Assert.That(runtime.ActiveStatusCount, Is.EqualTo(1));
        }
        var recipient = target.GetComponent<CombatBuffDebuffApplier>();
        Assert.That(recipient, Is.Not.Null);
        Object.DestroyImmediate(a);
        Object.DestroyImmediate(b);
        typeof(CombatBuffDebuffApplier).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(recipient, null);
        Assert.That(runtime.ActiveStatusCount, Is.EqualTo(1), "Independent duration survives both sources.");
        var runner = target.GetComponent<AbilitySystem>().EffectRunner;
        var active = runner.FindActiveEffect(definition.GameplayEffect, target);
        Assert.That(active.StackCount, Is.EqualTo(1));
        active.TimeRemaining = 0f;
        typeof(CombatBuffDebuffApplier).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(recipient, null);
        Assert.That(runtime.ActiveStatusCount, Is.Zero);
    }

    [TestCase("CommonCorridor/GoblinGunner.prefab")]
    [TestCase("CommonCorridor/LizardMage.prefab")]
    [TestCase("BeerMonster.prefab")]
    [TestCase("ShadowCorridor/StrangeCandlestick/StrangeCandlestick.prefab")]
    [TestCase("SlimeCorridor/Wizard.prefab")]
    public void WallAtSpawn_BlocksFireWithoutConsumingBurst_AndClearingWallAllowsFire(string path)
    {
        Mob owner = CreateMonster(path);
        var source = (IMobProjectileLaneSource)owner;
        var target = new GameObject("ShotLaneTarget");
        target.transform.position = owner.transform.position + Vector3.right * 3f;
        var wall = new GameObject("ShotLaneWall");
        wall.layer = 30;
        wall.transform.position = owner.transform.position;
        wall.AddComponent<BoxCollider2D>().size = Vector2.one * 0.2f;
        Physics2D.SyncTransforms();
        var effect = ScriptableObject.CreateInstance<GE_Damage_Spec>();
        temporaryAssets.Add(effect);
        var payload = new CombatHitPayload { sourceSystem = owner.GetComponent<AbilitySystem>(), damageEffect = effect, causer = owner.gameObject, finalHpDamage = 1f };
        int before = CountProjectiles();
        Assert.That(MobProjectileLaneUtility.IsClearToTarget(source, owner.transform.position, target), Is.False);
        Fire(owner, target, payload);
        Assert.That(CountProjectiles(), Is.EqualTo(before));
        Assert.That(GetField(GetCadence(owner), "shotsRemaining"), Is.EqualTo(0));
        wall.transform.position += Vector3.up * 10f;
        Physics2D.SyncTransforms();
        Assert.That(MobProjectileLaneUtility.IsClearToTarget(source, owner.transform.position, target), Is.True);
        Fire(owner, target, payload);
        Assert.That(CountProjectiles(), Is.GreaterThan(before));
        if (owner is StrangeCandlestick candle) Assert.That(candle.CanUseChaseMovement(), Is.False);
    }

    [Test]
    public void LaneChecksProjectileWidth_NotOnlyCenterRay_AndIgnoresChildSightRadius()
    {
        Mob owner = CreateMonster("CommonCorridor/GoblinGunner.prefab");
        var source = (IMobProjectileLaneSource)owner;
        var target = new GameObject("WidthTarget");
        target.transform.position = owner.transform.position + Vector3.right * 4f;
        var wall = new GameObject("WallOffCenterRay");
        wall.layer = 30;
        wall.AddComponent<BoxCollider2D>().size = Vector2.one * 0.1f;
        wall.transform.position = owner.transform.position + new Vector3(2f, 0.25f);
        Physics2D.SyncTransforms();
        Assert.That(Physics2D.Linecast(owner.transform.position, target.transform.position, 1 << 30).collider, Is.Null);
        Assert.That(MobProjectileLaneUtility.IsClearToTarget(source, owner.transform.position, target), Is.False);
        wall.transform.position += Vector3.up;
        Physics2D.SyncTransforms();
        Assert.That(MobProjectileLaneUtility.IsClearToTarget(source, owner.transform.position, target), Is.True);
    }

    [Test]
    public void EmptyPath_DoesNotRetryBeforeDeadline_AndExhaustedPathRetriesAfterward()
    {
        var chase = new GameObject("ChaseRetry").AddComponent<EnemyChaseIntent2D>();
        SetField(chase, "nextPathRebuildTime", Time.time + 0.35f);
        var query = typeof(EnemyChaseIntent2D).GetMethod("ShouldRebuildChasePath", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(query.Invoke(chase, new object[] { Vector2.zero }), Is.False);
        SetField(chase, "nextPathRebuildTime", Time.time);
        Assert.That(query.Invoke(chase, new object[] { Vector2.zero }), Is.True);
    }

    [Test]
    public void MissingPathfinder_IsRetriedAfterDeadline_AndDestroyedCacheIsReplaced()
    {
        var chase = new GameObject("LateNavigationChase").AddComponent<EnemyChaseIntent2D>();
        var resolve = typeof(EnemyChaseIntent2D).GetMethod("ResolvePathfinder", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(resolve.Invoke(chase, null), Is.Null);
        var first = new GameObject("LateNavigation").AddComponent<TilemapPathfinder2D>();
        Assert.That(resolve.Invoke(chase, null), Is.Null, "Misses respect the retry interval.");
        SetField(chase, "nextFallbackPathfinderSearchTime", 0f);
        Assert.That(resolve.Invoke(chase, null), Is.SameAs(first));
        Object.DestroyImmediate(first.gameObject);
        var second = new GameObject("ReplacementNavigation").AddComponent<TilemapPathfinder2D>();
        SetField(chase, "nextFallbackPathfinderSearchTime", 0f);
        Assert.That(resolve.Invoke(chase, null), Is.SameAs(second));
        second.enabled = false;
        Assert.That(resolve.Invoke(chase, null), Is.Null);
    }

    [TestCase("CommonCorridor/GoblinGunner.prefab")]
    [TestCase("CommonCorridor/LizardMage.prefab")]
    [TestCase("BeerMonster.prefab")]
    [TestCase("SlimeCorridor/Wizard.prefab")]
    public void BlockedLane_OverridesStopRange_AndUsesPathAroundWall(string path)
    {
        Mob owner = CreateMonster(path);
        var chase = owner.GetComponent<EnemyChaseIntent2D>();
        Assert.That(chase, Is.Not.Null);
        var target = new GameObject("NavigationTarget");
        target.transform.position = owner.transform.position + Vector3.right * 2f;
        typeof(Enemy).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, target.transform);
        var pathfinder = new GameObject("ShotPathfinder").AddComponent<TilemapPathfinder2D>();
        SetField(chase, "fallbackPathfinder", pathfinder);
        SetField(chase, "triedResolveFallbackPathfinder", true);
        SetField(chase, "stopRange", 3f);
        SetField(chase, "nextShotLaneCheckTime", 0f);
        chase.StartChase();
        var wall = new GameObject("NavigationWall");
        wall.layer = 30;
        wall.transform.position = owner.transform.position + Vector3.right;
        wall.AddComponent<BoxCollider2D>().size = Vector2.one * 0.8f;
        Physics2D.SyncTransforms();
        Assert.That(owner.CanUseChaseMovement(), Is.True);
        var blockedIntent = chase.GetIntent();
        Assert.That(Mathf.Abs(blockedIntent.Direction.y), Is.GreaterThan(0.5f), "Move around the wall rather than straight into it or stopping inside stopRange.");
        wall.transform.position += Vector3.up * 10f;
        SetField(chase, "nextShotLaneCheckTime", 0f);
        Physics2D.SyncTransforms();
        Assert.That(chase.GetIntent().Direction, Is.EqualTo(Vector2.zero), "Clear lane restores the authored stopping distance.");
    }

    [Test]
    public void RoomContext_IgnoresRangeOnlyWhileBothActorsAreInside()
    {
        Mob owner = CreateMonster("CommonCorridor/GoblinGunner.prefab");
        var chase = owner.GetComponent<EnemyChaseIntent2D>();
        var target = new GameObject("FarRoomTarget");
        target.transform.position = owner.transform.position + Vector3.right * 12f;
        typeof(Enemy).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, target.transform);
        var room = new GameObject("RoomContext");
        room.transform.position = owner.transform.position;
        var bounds = room.AddComponent<BoxCollider2D>();
        bounds.isTrigger = true;
        bounds.size = Vector2.one * 30f;
        var area = room.AddComponent<MonsterRoomArea2D>();
        area.Configure(bounds);
        Physics2D.SyncTransforms();
        Assert.That(chase.IsTargetWithinDetectionRange(), Is.False, "Unscoped boss summons retain authored range.");
        chase.ApplySpawnContext(new MonsterSpawnContext(owner.transform.position, Quaternion.identity, area, null));
        Assert.That(chase.IsTargetWithinDetectionRange(), Is.True);
        chase.StartChase();
        Assert.That(chase.GetIntent().Direction.x, Is.GreaterThan(0f));
        target.transform.position += Vector3.right * 10f;
        Physics2D.SyncTransforms();
        Assert.That(chase.IsTargetWithinDetectionRange(), Is.False);
        chase.SetIgnoreDetectionRange(true);
        Assert.That(chase.IsTargetWithinDetectionRange(), Is.True, "Explicit special-monster overrides remain independent.");
        chase.SetIgnoreDetectionRange(false);
        chase.ApplySpawnContext(default);
        Assert.That(chase.IsTargetWithinDetectionRange(), Is.False);
    }

    [Test]
    public void PawnRoomContext_IgnoresRangeOnlyWithinOwningRoom()
    {
        Mob owner = CreateMonster("SlimeCorridor/Pawn.prefab");
        var chase = owner.GetComponent<PawnOrbitContactIntent2D>();
        var target = new GameObject("FarPawnTarget");
        target.transform.position = owner.transform.position + Vector3.right * 12f;
        typeof(Enemy).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, target.transform);
        var room = new GameObject("PawnRoomContext");
        room.transform.position = owner.transform.position;
        var bounds = room.AddComponent<BoxCollider2D>();
        bounds.isTrigger = true;
        bounds.size = Vector2.one * 30f;
        var area = room.AddComponent<MonsterRoomArea2D>();
        area.Configure(bounds);
        Physics2D.SyncTransforms();
        Assert.That(chase.IsTargetWithinDetectionRange(), Is.False);
        chase.ApplySpawnContext(new MonsterSpawnContext(owner.transform.position, Quaternion.identity, area, null));
        Assert.That(chase.IsTargetWithinDetectionRange(), Is.True);
        target.transform.position += Vector3.right * 10f;
        Physics2D.SyncTransforms();
        Assert.That(chase.IsTargetWithinDetectionRange(), Is.False);
        target.transform.position = room.transform.position;
        owner.transform.position += Vector3.right * 20f;
        Physics2D.SyncTransforms();
        Assert.That(chase.IsTargetWithinDetectionRange(), Is.False);
        owner.transform.position = room.transform.position;
        target.transform.position += Vector3.right * 12f;
        chase.ApplySpawnContext(default);
        Physics2D.SyncTransforms();
        Assert.That(chase.IsTargetWithinDetectionRange(), Is.False);
    }

    [Test]
    public void BlockedShotLane_DoesNotDiscardAnUnfinishedPath()
    {
        var chase = new GameObject("StablePath").AddComponent<EnemyChaseIntent2D>();
        ((List<Vector2>)GetField(chase, "chasePath")).Add(Vector2.up);
        SetField(chase, "shotLaneBlocked", true);
        SetField(chase, "nextPathRebuildTime", 0f);
        var query = typeof(EnemyChaseIntent2D).GetMethod("ShouldRebuildChasePath", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(query.Invoke(chase, new object[] { Vector2.zero }), Is.False);
        Assert.That(query.Invoke(chase, new object[] { Vector2.right }), Is.True);
    }

    [Test]
    public void TilemapWall_PursuitMakesProgressAcrossRepeatedRebuilds_AndStopsWhenUnreachable()
    {
        Mob owner = CreateMonster("CommonCorridor/GoblinGunner.prefab");
        Vector3 origin = owner.transform.position;
        var grid = new GameObject("TranslatedNavigationGrid").AddComponent<Grid>();
        grid.transform.position = origin;
        var floor = new GameObject("Floor").AddComponent<Tilemap>();
        floor.transform.SetParent(grid.transform, false);
        var tile = ScriptableObject.CreateInstance<Tile>();
        temporaryAssets.Add(tile);
        for (int y = -3; y <= 3; y++)
            for (int x = -2; x <= 7; x++) floor.SetTile(new Vector3Int(x, y), tile);
        var wallMap = new GameObject("Wall").AddComponent<Tilemap>();
        wallMap.transform.SetParent(grid.transform, false);
        wallMap.gameObject.layer = 30;
        var texture = new Texture2D(1, 1);
        temporaryAssets.Add(texture);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        temporaryAssets.Add(sprite);
        var wallTile = ScriptableObject.CreateInstance<Tile>();
        temporaryAssets.Add(wallTile);
        wallTile.sprite = sprite;
        wallTile.colliderType = Tile.ColliderType.Grid;
        var collider = wallMap.gameObject.AddComponent<TilemapCollider2D>();
        for (int y = -1; y <= 1; y++) wallMap.SetTile(new Vector3Int(2, y), wallTile);
        collider.ProcessTilemapChanges();
        var finder = new GameObject("GridPathfinder").AddComponent<TilemapPathfinder2D>();
        SetField(finder, "grid", grid);
        SetField(finder, "groundTilemap", floor);
        var chase = owner.GetComponent<EnemyChaseIntent2D>();
        var target = new GameObject("GridTarget");
        target.transform.position = origin + new Vector3(5.5f, 0.5f);
        owner.transform.position = origin + new Vector3(0.5f, 0.5f);
        typeof(Enemy).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, target.transform);
        chase.ApplySpawnContext(new MonsterSpawnContext(owner.transform.position, Quaternion.identity, null, finder));
        chase.StartChase();
        Physics2D.SyncTransforms();
        Assert.That(finder.HasDirectWalkableSegment(owner.transform.position, target.transform.position), Is.False);
        var rebuild = typeof(EnemyChaseIntent2D).GetMethod("RebuildChasePath", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int tick = 0; tick < 500 && Vector2.Distance(owner.transform.position, target.transform.position) > 0.8f; tick++)
        {
            if (tick % 7 == 0) rebuild.Invoke(chase, new object[] { finder, (Vector2)target.transform.position });
            SetField(chase, "nextShotLaneCheckTime", 0f);
            Vector2 direction = chase.GetIntent().Direction;
            Vector2 next = (Vector2)owner.transform.position + direction * 0.05f;
            Assert.That(finder.HasDirectWalkableSegment(owner.transform.position, next, chase.GetNavigationFootprint()), Is.True);
            owner.transform.position = next;
            Physics2D.SyncTransforms();
        }
        Assert.That(Vector2.Distance(owner.transform.position, target.transform.position), Is.LessThan(0.85f));
        owner.transform.position = origin + new Vector3(0.5f, 0.5f);
        for (int y = -3; y <= 3; y++) wallMap.SetTile(new Vector3Int(2, y), wallTile);
        collider.ProcessTilemapChanges();
        Physics2D.SyncTransforms();
        chase.ApplySpawnContext(new MonsterSpawnContext(owner.transform.position, Quaternion.identity, null, finder));
        SetField(chase, "nextShotLaneCheckTime", 0f);
        Assert.That(chase.GetIntent().Direction, Is.EqualTo(Vector2.zero), "No wall-directed fallback when the floor has no route.");
    }

    [Test]
    public void LoggedWallStall_UsesBodyEnvelopeInsteadOfOversizedRootProbe()
    {
        Mob owner = CreateMonster("CommonCorridor/GoblinWarrior.prefab");
        var chase = owner.GetComponent<EnemyChaseIntent2D>();
        foreach (var existing in owner.GetComponentsInChildren<Collider2D>()) existing.enabled = false;
        var bodyObject = new GameObject("BodyCollision");
        bodyObject.transform.SetParent(owner.transform, false);
        bodyObject.transform.localPosition = Vector3.up * 0.15f;
        var body = bodyObject.AddComponent<CapsuleCollider2D>();
        body.direction = CapsuleDirection2D.Horizontal;
        body.size = new Vector2(0.4f, 0.3f);
        var hurtbox = new GameObject("IgnoredHurtbox");
        hurtbox.transform.SetParent(owner.transform, false);
        hurtbox.AddComponent<BoxCollider2D>().isTrigger = true;
        hurtbox.GetComponent<BoxCollider2D>().size = Vector2.one * 8f;
        var independent = new GameObject("IndependentChildBody");
        independent.transform.SetParent(owner.transform, false);
        independent.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        independent.AddComponent<BoxCollider2D>().size = Vector2.one * 5f;
        var wall = new GameObject("LoggedWall");
        wall.layer = 30;
        wall.transform.position = owner.transform.position + new Vector3(-0.5037f, 0f);
        var wallCollider = wall.AddComponent<BoxCollider2D>();
        wallCollider.size = new Vector2(0.4f, 8f);
        var finder = new GameObject("BodyAwareFinder").AddComponent<TilemapPathfinder2D>();
        var grid = new GameObject("LoggedGrid").AddComponent<Grid>();
        grid.transform.position = owner.transform.position - new Vector3(0.31f, 0.34f);
        SetField(finder, "grid", grid);
        var target = new GameObject("LoggedTarget");
        target.transform.position = owner.transform.position + new Vector3(3.09f, -3.03f);
        typeof(Enemy).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, target.transform);
        chase.ApplySpawnContext(new MonsterSpawnContext(owner.transform.position, Quaternion.identity, null, finder));
        chase.StartChase();
        Physics2D.SyncTransforms();
        var footprint = chase.GetNavigationFootprint();
        Assert.That(footprint.Size.x, Is.EqualTo(0.4f).Within(0.002f));
        Assert.That(footprint.Size.y, Is.EqualTo(0.3f).Within(0.002f));
        Assert.That(footprint.CenterOffset.y, Is.EqualTo(0.15f).Within(0.002f));
        Assert.That(Physics2D.Distance(body, wallCollider).isOverlapped, Is.False);
        Vector2 center = (Vector2)owner.transform.position + new Vector2(0.19f, 0.16f);
        Assert.That(finder.HasDirectWalkableSegment(owner.transform.position, center), Is.False, "Reproduces the old root-probe false positive.");
        Assert.That(finder.HasDirectWalkableSegment(owner.transform.position, center, footprint), Is.True);
        Assert.That(finder.TryBuildPath(owner.transform.position, target.transform.position, out var path, footprint), Is.True);
        Assert.That(path.Count, Is.GreaterThan(1));
        for (int i = 1; i < path.Count; i++)
            Assert.That(finder.HasDirectWalkableSegment(path[i - 1], path[i], footprint), Is.True);
        Assert.That(chase.GetIntent().Direction.sqrMagnitude, Is.GreaterThan(0.5f));
        Assert.That(finder.HasDirectWalkableSegment(owner.transform.position, center), Is.False, "Small-body query must not mutate shared defaults.");
        Assert.That(finder.HasDirectWalkableSegment(owner.transform.position, center,
            new MonsterNavigationFootprint2D(Vector2.one, Vector2.zero)), Is.False, "Larger bodies retain their own clearance.");
        bodyObject.transform.localScale = new Vector3(-2f, 2f, 1f);
        body.offset = new Vector2(0.05f, 0.02f);
        Physics2D.SyncTransforms();
        var scaled = chase.GetNavigationFootprint();
        Assert.That(scaled.Size.x, Is.EqualTo(body.bounds.size.x).Within(0.002f));
        Assert.That(scaled.CenterOffset.x, Is.EqualTo(body.bounds.center.x - owner.transform.position.x).Within(0.002f));
        body.enabled = false;
        Assert.That(chase.GetNavigationFootprint().IsValid, Is.False, "Disabled bodies and triggers do not provide a footprint.");
    }

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

    [TestCase("CommonCorridor/GoblinGunner.prefab")]
    [TestCase("CommonCorridor/LizardMage.prefab")]
    [TestCase("BeerMonster.prefab")]
    [TestCase("ShadowCorridor/StrangeCandlestick/StrangeCandlestick.prefab")]
    public void LockedShot_IgnoresTargetsNewPosition(string path)
    {
        Mob owner = CreateMonster(path);
        var target = new GameObject("MovedAfterLockOn");
        target.transform.position = owner.transform.position + Vector3.up * 3f;
        var effect = ScriptableObject.CreateInstance<GE_Damage_Spec>();
        temporaryAssets.Add(effect);
        var payload = new CombatHitPayload { sourceSystem = owner.GetComponent<AbilitySystem>(), damageEffect = effect, causer = owner.gameObject, finalHpDamage = 1f };
        var before = new HashSet<LightBeadProjectile2D>(Object.FindObjectsByType<LightBeadProjectile2D>(FindObjectsSortMode.None));
        for (int i = 0; i < 2; i++)
        {
            if (owner is StrangeCandlestick candle)
                candle.FireProjectile(target, owner.transform.position, Vector2.right);
            else
                Fire(owner, target, payload);
            target.transform.position += Vector3.left * 2f;
        }
        int added = 0;
        foreach (var projectile in Object.FindObjectsByType<LightBeadProjectile2D>(FindObjectsSortMode.None))
        {
            if (before.Contains(projectile)) continue;
            added++;
            Assert.That((Vector2)GetField(projectile, "direction"), Is.EqualTo(Vector2.right));
        }
        Assert.That(added, Is.EqualTo(2));
    }

    [TestCase("Weapon.WindWeapon", false, false)]
    [TestCase("Weapon.WindWeapon", true, false)]
    [TestCase("Weapon.Flowering", false, false)]
    [TestCase("Weapon.Flowering", true, true)]
    [TestCase("Weapon.OddIron", false, false)]
    [TestCase("Weapon.OddIron", true, true)]
    [TestCase("Weapon.ApprenticeHeroSword", false, true)]
    public void WeaponDrops_RespectSourceRestrictions(string id, bool chest, bool expected)
    {
        var type = typeof(LootPoolService).Assembly.GetType("LootPoolItemSelectionService", true);
        var method = type.GetMethod("CanDropWeapon", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method.Invoke(null, new object[] { id, chest }), Is.EqualTo(expected));
        if (!chest)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            temporaryAssets.Add(weapon);
            weapon.weaponId = id;
            Assert.That(new LootPoolService().GetRandomWeaponFromCandidates(new[] { weapon }, new HashSet<string>()) != null, Is.EqualTo(expected));
        }
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

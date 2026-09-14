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
    private readonly List<Object> temporaryAssets = new();
    private HashSet<GameObject> existingRoots;
    private Random.State randomState;

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
